"""HTTP boundary tests with isolated wallets and a fake Google transport; no live purchases."""
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch
from google.api_core.exceptions import FailedPrecondition
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

    def test_timmy_purchase_is_unavailable_without_debiting_verified_shards(self):
        self.request('/google-play/fulfill',{'productId':'dimensions_shards_5500','purchaseToken':'timmy-token'})
        before=self.app.ledger.wallet('account-a')
        for path,payload in (
            ('/wallet/spend',{'cosmeticId':'hero_timmy','operationId':'e'*32,'expectedProfileRevision':before['profileRevision']}),
            ('/cosmetics/buy',{'cosmeticId':'hero_timmy'}),
        ):
            result=self.request(path,payload)
            self.assertEqual(result['status'],'400 Bad Request')
            self.assertEqual(self.app.ledger.wallet('account-a'),before)

    def test_wasp_purchase_is_unavailable_without_debiting_verified_shards(self):
        self.request('/google-play/fulfill',{'productId':'dimensions_shards_1200','purchaseToken':'wasp-token'})
        before=self.app.ledger.wallet('account-a')
        for path,payload in (
            ('/wallet/spend',{'cosmeticId':'arena_Wasp76b','operationId':'e'*32,'expectedProfileRevision':before['profileRevision']}),
            ('/cosmetics/buy',{'cosmeticId':'arena_Wasp76b'}),
        ):
            result=self.request(path,payload)
            self.assertEqual(result['status'],'400 Bad Request')
            self.assertEqual(self.app.ledger.wallet('account-a'),before)

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

    def test_missing_chat_index_returns_actionable_message_without_provider_details(self):
        private='The query requires an index: https://console.firebase.google.com/project/private-project?token=secret'
        with patch.object(self.app.social,'dispatch',side_effect=FailedPrecondition(private)):
            result=self.request('/social/chat/read')
        self.assertEqual(result['status'],'503 Service Unavailable')
        self.assertEqual(result['body']['code'],'CHAT_INDEX_REQUIRED')
        self.assertIn('database index',result['body']['message'])
        self.assertNotIn('private-project',json.dumps(result['body']))
        self.assertNotIn('secret',json.dumps(result['body']))
        self.assertEqual(result['headers']['Cache-Control'],'no-store')

    def test_other_database_preconditions_do_not_claim_a_missing_index(self):
        with patch.object(self.app.social,'dispatch',side_effect=FailedPrecondition('transaction precondition')):
            result=self.request('/social/chat/read')
        self.assertEqual(result['status'],'503 Service Unavailable')
        self.assertEqual(result['body'],{'fulfilled':False})

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

    def test_verified_shards_remain_on_server_and_legacy_claims_are_rejected(self):
        self.request('/google-play/fulfill', {'productId':'dimensions_shards_500','purchaseToken':'claim-token'})
        self.assertEqual(self.request('/wallet/claim')['status'],'400 Bad Request')
        self.assertEqual(self.request('/wallet/claim/ack', {'claimId':'1'})['status'],'400 Bad Request')
        self.assertEqual(self.request('/wallet')['body']['wallet']['balance'],500)

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

