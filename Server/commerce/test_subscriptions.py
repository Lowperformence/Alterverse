"""Subscription delivery through both durable adapters; Google and Firestore are fixtures."""
import base64
import copy
import io
import json
import tempfile
import time
import unittest
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import Mock, patch

from application import CommerceApplication
from firestore_ledger import FirestoreLedger
from ledger import Ledger, MAX_BALANCE
from subscriptions import (GooglePushAuthenticator, SUBSCRIPTION, SubscriptionLedger,
                           SubscriptionService, verified_subscription)
from test_firestore_social import Client, transactional

A, B = 'a' * 64, 'b' * 64
PRODUCT = 'dimensions_shards_monthly'
PACKAGE = 'com.lowperformence.dimensionsfront'


def receipt(order='GPA.test', owner=A, state='ACTIVE', expiry=None):
    expires = expiry if expiry is not None else time.time() + 3600
    return {'subscriptionState': 'SUBSCRIPTION_STATE_' + state,
            'externalAccountIdentifiers': {'obfuscatedExternalAccountId': owner},
            'acknowledgementState': 'ACKNOWLEDGEMENT_STATE_PENDING',
            'lineItems': [{'productId': PRODUCT,
                           'offerDetails': {'basePlanId': 'monthly'},
                           'autoRenewingPlan': {'autoRenewEnabled': True},
                           'expiryTime': datetime.fromtimestamp(expires, timezone.utc).isoformat(),
                           'latestSuccessfulOrderId': order}]}


def envelope(value):
    return {'message': {'data': base64.b64encode(json.dumps(value).encode()).decode()}}


class SubscriptionTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.patch = patch('google.cloud.firestore.transactional', transactional)
        self.patch.start()
        self.adapters = [Ledger(Path(self.folder.name) / 'wallet.sqlite'), FirestoreLedger(client=Client())]

    def tearDown(self):
        self.patch.stop()
        self.folder.cleanup()

    def test_catalog_has_approved_fixed_amounts(self):
        from ledger import PACKS
        self.assertEqual(PACKS['dimensions_shards_12000'], 12000)
        self.assertEqual((SUBSCRIPTION['id'], SUBSCRIPTION['amount'], SUBSCRIPTION['basePlanId']), (PRODUCT, 1200, 'monthly'))

    def test_monthly_delivery_replay_and_adapter_restart(self):
        for ledger in self.adapters:
            with self.subTest(adapter=type(ledger).__name__):
                storage = SubscriptionLedger(ledger)
                paid = verified_subscription(PRODUCT, receipt(), A)
                self.assertEqual(storage.apply('token', paid, 1)['balance'], 1200)
                storage = SubscriptionLedger(ledger)
                self.assertEqual(storage.apply('token', paid, 2)['balance'], 1200)
                renewed = verified_subscription(PRODUCT, receipt('GPA.test..1'), A)
                self.assertEqual(storage.apply('token', renewed, 3)['balance'], 2400)
                self.assertEqual(storage.apply('token', renewed, 4)['balance'], 2400)
                self.assertTrue(storage.active(A))
                self.assertEqual(ledger.wallet(B)['balance'], 0)

    def test_concurrent_duplicate_notification_credits_one_order(self):
        for ledger in self.adapters:
            storage = SubscriptionLedger(ledger)
            paid = verified_subscription(PRODUCT, receipt(), A)
            with ThreadPoolExecutor(max_workers=6) as pool:
                list(pool.map(lambda n: storage.apply('concurrent-token', paid, n), range(1, 13)))
            self.assertEqual(ledger.wallet(A)['balance'], 1200)

    def test_account_token_and_order_bindings_cannot_change(self):
        for ledger in self.adapters:
            storage = SubscriptionLedger(ledger)
            paid = verified_subscription(PRODUCT, receipt(), A)
            storage.apply('token', paid, 1)
            with self.assertRaises(PermissionError): verified_subscription(PRODUCT, receipt(), B)
            with self.assertRaises(ValueError): storage.apply('token', verified_subscription(PRODUCT, receipt(owner=B)), 2)
            with self.assertRaises(ValueError): storage.apply('other-token', paid, 3)
            self.assertEqual(ledger.wallet(A)['balance'], 1200)
            self.assertEqual(ledger.wallet(B)['balance'], 0)

    def test_unpaid_and_inactive_states_do_not_grant(self):
        for ledger in self.adapters:
            storage = SubscriptionLedger(ledger)
            for state in ('PENDING', 'PAUSED', 'ON_HOLD', 'EXPIRED', 'PENDING_PURCHASE_CANCELED'):
                inactive = verified_subscription(PRODUCT, receipt(state=state), A)
                self.assertEqual(storage.apply(state, inactive, 1)['balance'], 0)
            self.assertFalse(storage.active(A))
            expired = verified_subscription(PRODUCT, receipt(expiry=time.time() - 1), A)
            self.assertEqual(storage.apply('expired-active', expired, 2)['balance'], 0)

    def test_cancelled_paid_period_is_retained_and_grace_does_not_duplicate(self):
        for ledger in self.adapters:
            storage = SubscriptionLedger(ledger)
            cancelled = verified_subscription(PRODUCT, receipt(state='CANCELED'), A)
            self.assertEqual(storage.apply('token', cancelled, 1)['balance'], 1200)
            grace = verified_subscription(PRODUCT, receipt(state='IN_GRACE_PERIOD'), A)
            self.assertEqual(storage.apply('token', grace, 2)['balance'], 1200)
            expired = verified_subscription(PRODUCT, receipt(state='EXPIRED'), A)
            storage.apply('token', expired, 3)
            self.assertFalse(storage.active(A))
            # A late in-flight lookup cannot restore older entitlement state.
            storage.apply('token', cancelled, 2)
            self.assertFalse(storage.active(A))

    def test_bad_plan_free_offer_and_pending_order_are_not_currency(self):
        mutations = [lambda v: v['lineItems'][0]['offerDetails'].update(basePlanId='annual'),
                     lambda v: v['lineItems'][0]['offerDetails'].update(offerId='free-trial'),
                     lambda v: v['lineItems'][0].update(productId='unknown'),
                     lambda v: v['lineItems'][0].pop('latestSuccessfulOrderId'),
                     lambda v: v['lineItems'][0].update(expiryTime='2026-10-21T00:00:00'),
                     lambda v: v.update(lineItems=[])]
        for mutate in mutations:
            value = receipt()
            mutate(value)
            value['latestOrderId'] = 'GPA.pending'
            with self.assertRaises(ValueError): verified_subscription(PRODUCT, value, A)

    def test_overflow_rolls_back_order_so_retry_can_recover(self):
        for ledger in self.adapters:
            storage = SubscriptionLedger(ledger)
            ledger.wallet(A)
            if hasattr(ledger, 'connect'):
                with ledger.connect() as db: db.execute('UPDATE wallet SET balance=? WHERE account=?', (MAX_BALANCE, A))
            else:
                ledger.client.data[ledger.accounts.document(A).path]['balance'] = MAX_BALANCE
            with self.assertRaises(ValueError): storage.apply('token', verified_subscription(PRODUCT, receipt(), A), 1)
            if hasattr(ledger, 'connect'):
                with ledger.connect() as db: db.execute('UPDATE wallet SET balance=0 WHERE account=?', (A,))
            else:
                ledger.client.data[ledger.accounts.document(A).path]['balance'] = 0
            self.assertEqual(storage.apply('token', verified_subscription(PRODUCT, receipt(), A), 2)['balance'], 1200)

    def make_service(self, ledger):
        google = Mock()
        current = {'value': receipt()}
        def lookup(url, **kwargs):
            response = Mock()
            if '/orders/' in url:
                order = url.rsplit('/', 1)[1]
                response.json.return_value = {'orderId': order, 'purchaseToken': 'token', 'state': 'PROCESSED',
                    'total': {'currencyCode': 'EUR', 'units': '7', 'nanos': 490000000},
                    'lineItems': [{'productId': PRODUCT, 'subscriptionDetails': {
                        'basePlanId': 'monthly', 'offerPhaseDetails': {'baseDetails': {}}}}]}
            else:
                response.json.return_value = current['value']
            return response
        google.current = current
        google.get.side_effect = lookup
        authenticate = Mock(return_value=True)
        return SubscriptionService(ledger, google, PACKAGE, authenticate), google, authenticate

    def test_acknowledgement_follows_durable_credit_and_retries_idempotently(self):
        for ledger in self.adapters:
            service, google, _ = self.make_service(ledger)
            def failed_ack(*args, **kwargs):
                self.assertEqual(ledger.wallet(A)['balance'], 1200)
                raise RuntimeError('Google acknowledgement temporarily unavailable')
            google.post.side_effect = failed_ack
            with self.assertRaises(RuntimeError): service.fulfill(PRODUCT, 'token', A)
            google.post.side_effect = None
            self.assertEqual(service.fulfill(PRODUCT, 'token', A)[0]['balance'], 1200)
            google.get.assert_any_call('https://androidpublisher.googleapis.com/androidpublisher/v3/applications/' + PACKAGE + '/purchases/subscriptionsv2/tokens/token', timeout=15)

    def test_rtdn_works_without_unity_session_and_ignores_payload_amount(self):
        for ledger in self.adapters:
            service, google, authenticate = self.make_service(ledger)
            payload = envelope({'packageName': PACKAGE, 'amount': 999999,
                                'subscriptionNotification': {'subscriptionId': PRODUCT, 'purchaseToken': 'token'}})
            service.push('Bearer signed-google-id-token', payload)
            google.current['value'] = receipt('GPA.test..0')
            service.push('Bearer signed-google-id-token', payload)
            service.push('Bearer signed-google-id-token', payload)
            self.assertEqual(ledger.wallet(A)['balance'], 2400)
            authenticate.assert_called_with('Bearer signed-google-id-token')

    def test_rejected_push_identity_never_looks_up_or_grants(self):
        for ledger in self.adapters:
            service, google, authenticate = self.make_service(ledger)
            authenticate.side_effect = PermissionError('Invalid signature')
            with self.assertRaises(PermissionError): service.push('Bearer forged', {})
            google.get.assert_not_called()

    def test_wsgi_subscription_gate_and_authenticated_push(self):
        ledger = self.adapters[0]
        service, google, authenticate = self.make_service(ledger)
        app = CommerceApplication(ledger, lambda h: A if h == 'Bearer player' else None, google, PACKAGE, subscriptions=service)
        def call(path, data, header):
            encoded = json.dumps(data).encode()
            response = {}
            body = b''.join(app({'REQUEST_METHOD': 'POST', 'PATH_INFO': path, 'CONTENT_LENGTH': str(len(encoded)),
                                  'wsgi.input': io.BytesIO(encoded), 'HTTP_AUTHORIZATION': header},
                                 lambda status, headers: response.update(status=status)))
            return response['status'], json.loads(body) if body else None
        status, value = call('/wallet', {}, 'Bearer player')
        self.assertEqual(status, '200 OK')
        self.assertTrue(value['wallet']['subscriptionsReady'])
        self.assertFalse(value['wallet']['subscriptionActive'])
        self.assertEqual(call('/google-play/subscription', {'productId': PRODUCT, 'purchaseToken': 'token'}, '')[0], '401 Unauthorized')
        payload = envelope({'packageName': PACKAGE, 'subscriptionNotification': {'subscriptionId': PRODUCT, 'purchaseToken': 'token'}})
        self.assertEqual(call('/google-play/rtdn', payload, 'Bearer google')[0], '204 No Content')
        self.assertTrue(call('/wallet', {}, 'Bearer player')[1]['wallet']['subscriptionActive'])
        authenticate.side_effect = PermissionError()
        self.assertEqual(call('/google-play/rtdn', payload, 'Bearer forged')[0], '401 Unauthorized')

    def test_missing_months_are_verified_and_recovered_after_outage(self):
        for ledger in self.adapters:
            service, google, _ = self.make_service(ledger)
            google.current['value'] = receipt('GPA.test..2')
            self.assertEqual(service.fulfill(PRODUCT, 'token', A)[0]['balance'], 4800)
            self.assertEqual(service.fulfill(PRODUCT, 'token', A)[0]['balance'], 4800)
            self.assertEqual(sum('/orders/' in call.args[0] for call in google.get.call_args_list), 4)

    def test_long_history_resumes_after_durable_partial_delivery(self):
        for ledger in self.adapters:
            service, google, _ = self.make_service(ledger)
            google.current['value'] = receipt('GPA.test..5')
            with self.assertRaises(RuntimeError): service.fulfill(PRODUCT, 'token', A)
            self.assertEqual(ledger.wallet(A)['balance'], 4800)
            self.assertEqual(service.fulfill(PRODUCT, 'token', A)[0]['balance'], 8400)

    def test_refunded_or_wrong_token_history_never_grants(self):
        for ledger in self.adapters:
            service, google, _ = self.make_service(ledger)
            original = google.get.side_effect
            def refunded(url, **kwargs):
                response = original(url, **kwargs)
                if '/orders/' in url: response.json.return_value['state'] = 'REFUNDED'
                return response
            google.get.side_effect = refunded
            self.assertEqual(service.fulfill(PRODUCT, 'token', A)[0]['balance'], 0)
            def wrong_token(url, **kwargs):
                response = original(url, **kwargs)
                if '/orders/' in url: response.json.return_value['purchaseToken'] = 'other-token'
                return response
            google.get.side_effect = wrong_token
            google.current['value'] = receipt('GPA.test..0')
            with self.assertRaises(ValueError): service.fulfill(PRODUCT, 'token', A)
            self.assertEqual(ledger.wallet(A)['balance'], 0)

    def test_zero_cost_reactivation_is_not_a_new_paid_month(self):
        for ledger in self.adapters:
            service, google, _ = self.make_service(ledger)
            original = google.get.side_effect
            def zero_order(url, **kwargs):
                response = original(url, **kwargs)
                if '/orders/' in url: response.json.return_value['total'] = {'currencyCode': 'EUR'}
                return response
            google.get.side_effect = zero_order
            self.assertEqual(service.fulfill(PRODUCT, 'token', A)[0]['balance'], 0)
            self.assertTrue(service.storage.active(A))
            google.current['value'] = receipt('GPA.test..0')
            google.get.side_effect = original
            self.assertEqual(service.fulfill(PRODUCT, 'token', A)[0]['balance'], 1200)

    def test_missing_implied_base_order_does_not_block_verified_renewal(self):
        for ledger in self.adapters:
            service, google, _ = self.make_service(ledger)
            google.current['value'] = receipt('GPA.test..0')
            original = google.get.side_effect
            def missing_base(url, **kwargs):
                response = original(url, **kwargs)
                if url.endswith('/orders/GPA.test'): response.status_code = 404
                return response
            google.get.side_effect = missing_base
            self.assertEqual(service.fulfill(PRODUCT, 'token', A)[0]['balance'], 1200)
            self.assertEqual(service.fulfill(PRODUCT, 'token', A)[0]['balance'], 1200)

    def test_push_verifier_checks_signed_audience_and_email(self):
        auth = GooglePushAuthenticator('https://server/google-play/rtdn', 'push@project.iam.gserviceaccount.com')
        with patch('google.oauth2.id_token.verify_oauth2_token') as verify:
            verify.return_value = {'email': auth.service_account, 'email_verified': True}
            self.assertTrue(auth('Bearer signed'))
            self.assertEqual(verify.call_args.args[2], auth.audience)
            for claims in ({'email': 'other@project.iam.gserviceaccount.com', 'email_verified': True},
                           {'email': auth.service_account, 'email_verified': False}):
                verify.return_value = claims
                with self.assertRaises(PermissionError): auth('Bearer signed')
            verify.side_effect = ValueError('Bad issuer or signature')
            with self.assertRaises(PermissionError): auth('Bearer unsigned')


if __name__ == '__main__':
    unittest.main()
