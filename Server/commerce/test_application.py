"""HTTP boundary tests with isolated wallets and a fake Google transport; no live purchases."""
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock
from application import CommerceApplication


class ApplicationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.google = Mock()
        self.google.get.return_value.json.return_value = {
            'purchaseState': 0, 'obfuscatedExternalAccountId': 'account-a',
            'quantity': 1, 'consumptionState': 0,
        }
        self.app = CommerceApplication(
            Path(self.tmp.name) / 'wallet.db',
            lambda header: 'account-a' if header == 'Bearer test-session' else None,
            self.google, 'com.example.dimensionsfront')

    def tearDown(self):
        self.tmp.cleanup()

    def request(self, path, data=None, authorization='Bearer test-session', raw=None):
        body = json.dumps(data or {}).encode() if raw is None else raw
        env = {'REQUEST_METHOD': 'POST', 'PATH_INFO': path,
               'CONTENT_LENGTH': str(len(body)), 'wsgi.input': io.BytesIO(body),
               'HTTP_AUTHORIZATION': authorization}
        result = {}
        def start(status, headers):
            result.update(status=status, headers=dict(headers))
        result['body'] = json.loads(b''.join(self.app(env, start)))
        return result

    def test_authentication_required_before_google_or_wallet_access(self):
        result = self.request('/google-play/fulfill', {'productId': 'dimensions_shards_500', 'purchaseToken': 'token'}, authorization='')
        self.assertEqual(result['status'], '401 Unauthorized')
        self.google.get.assert_not_called()
        self.assertEqual(self.app.ledger.wallet('account-a')['balance'], 0)

    def test_fulfillment_retry_returns_one_durable_credit(self):
        data = {'productId': 'dimensions_shards_1200', 'purchaseToken': 'token'}
        first = self.request('/google-play/fulfill', data)
        second = self.request('/google-play/fulfill', data)
        self.assertEqual(first['status'], '200 OK')
        self.assertEqual(first['body'], second['body'])
        self.assertEqual(second['body']['wallet']['balance'], 1200)
        self.assertEqual(second['headers']['Cache-Control'], 'no-store')
        self.google.get.assert_called_with(
            'https://androidpublisher.googleapis.com/androidpublisher/v3/applications/com.example.dimensionsfront/purchases/products/dimensions_shards_1200/tokens/token', timeout=15)

    def test_client_amount_account_and_verified_payload_cannot_override_google(self):
        result = self.request('/google-play/fulfill', {
            'productId': 'dimensions_shards_500', 'purchaseToken': 'token', 'amount': 999999,
            'account': 'attacker', 'verified': {'purchaseState': 0}})
        self.assertEqual(result['body']['wallet']['balance'], 500)
        self.assertEqual(self.app.ledger.wallet('attacker')['balance'], 0)

    def test_google_outage_is_retryable_and_never_grants(self):
        self.google.get.side_effect = RuntimeError('sensitive upstream details')
        result = self.request('/google-play/fulfill', {'productId': 'dimensions_shards_500', 'purchaseToken': 'secret-token'})
        self.assertEqual(result['status'], '503 Service Unavailable')
        self.assertEqual(result['body'], {'fulfilled': False})
        self.assertEqual(self.app.ledger.wallet('account-a')['balance'], 0)

    def test_invalid_payloads_are_rejected_before_google(self):
        for raw in (b'[]', b'null', b'"string"', b'{', b'{"productId":[],"purchaseToken":"x"}',
                    b'{"productId":"unknown","purchaseToken":"x"}',
                    b'{"productId":"dimensions_shards_500","purchaseToken":""}',
                    b'{"productId":"dimensions_shards_500","purchaseToken":42}'):
            with self.subTest(raw=raw):
                self.assertEqual(self.request('/google-play/fulfill', raw=raw)['status'], '400 Bad Request')
        self.google.get.assert_not_called()

    def test_removed_2500_pack_is_rejected_before_google(self):
        result = self.request('/google-play/fulfill', {
            'productId': 'dimensions_shards_2500', 'purchaseToken': 'old-pack-token'})
        self.assertEqual(result['status'], '400 Bad Request')
        self.google.get.assert_not_called()
        self.assertEqual(self.app.ledger.wallet('account-a')['balance'], 0)

    def test_verified_shards_claim_into_one_retry_safe_balance(self):
        self.request('/google-play/fulfill', {'productId': 'dimensions_shards_500', 'purchaseToken': 'claim-token'})
        first = self.request('/wallet/claim')
        second = self.request('/wallet/claim')
        self.assertEqual(first['body']['wallet']['claimAmount'], 500)
        self.assertEqual(first['body']['wallet']['claimId'], second['body']['wallet']['claimId'])
        self.assertEqual(first['body']['wallet']['balance'], 0)
        claim_id = first['body']['wallet']['claimId']
        ack = self.request('/wallet/claim/ack', {'claimId': claim_id})
        self.assertEqual(ack['status'], '200 OK')
        empty = self.request('/wallet/claim')
        self.assertEqual(empty['body']['wallet']['claimAmount'], 0)
        self.assertEqual(empty['body']['wallet']['claimId'], '')

    def test_cosmetic_purchase_retry_and_wallet_refresh(self):
        self.request('/google-play/fulfill', {'productId': 'dimensions_shards_5500', 'purchaseToken': 'token'})
        first = self.request('/cosmetics/buy', {'cosmeticId': 'voidfall'})
        second = self.request('/cosmetics/buy', {'cosmeticId': 'voidfall'})
        self.assertEqual(first['body'], second['body'])
        wallet = self.request('/wallet')['body']['wallet']
        self.assertEqual(wallet['balance'], 3500)
        self.assertTrue(wallet['voidfall'])


if __name__ == '__main__':
    unittest.main()
