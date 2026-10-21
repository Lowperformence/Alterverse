"""Google-verified monthly grants. Currency amounts come only from the shared catalog.

Each successful Google order is credited once, atomically with the wallet. RTDN
rechecks Google, so a push payload or client receipt can never choose a grant.
"""
import base64
import hashlib
import json
import re
import time
from datetime import datetime, timezone
from urllib.parse import quote

from ledger import CATALOG, MAX_BALANCE

SUBSCRIPTION = CATALOG['subscription']
ACTIVE = {'SUBSCRIPTION_STATE_ACTIVE', 'SUBSCRIPTION_STATE_CANCELED', 'SUBSCRIPTION_STATE_IN_GRACE_PERIOD'}
STATES = ACTIVE | {'SUBSCRIPTION_STATE_PENDING', 'SUBSCRIPTION_STATE_PAUSED',
                   'SUBSCRIPTION_STATE_ON_HOLD', 'SUBSCRIPTION_STATE_EXPIRED',
                   'SUBSCRIPTION_STATE_PENDING_PURCHASE_CANCELED'}


def positive_money(money):
    if not isinstance(money, dict) or not re.fullmatch('[A-Z]{3}', money.get('currencyCode', '')):
        raise ValueError('Verified order amount required')
    units, nanos = money.get('units', '0'), money.get('nanos', 0)
    if not isinstance(units, (str, int)) or not re.fullmatch(r'-?\d{1,20}', str(units)) or type(nanos) is not int or abs(nanos) >= 1000000000:
        raise ValueError('Invalid verified order amount')
    return int(units) * 1000000000 + nanos > 0


def verified_subscription(product, verified, account=None, now=None):
    if product != SUBSCRIPTION['id'] or not isinstance(verified, dict):
        raise ValueError('Unknown subscription')
    owner = verified.get('externalAccountIdentifiers', {}).get('obfuscatedExternalAccountId')
    if not isinstance(owner, str) or not re.fullmatch('[a-f0-9]{64}', owner):
        raise ValueError('Linked account required')
    if account is not None and account != owner:
        raise PermissionError('Purchase account mismatch')
    items = verified.get('lineItems')
    if not isinstance(items, list) or len(items) != 1 or not isinstance(items[0], dict):
        raise ValueError('Unsupported subscription items')
    item = items[0]
    offer = item.get('offerDetails', {})
    if (item.get('productId') != product or offer.get('basePlanId') != SUBSCRIPTION['basePlanId']
            or offer.get('offerId') or not isinstance(item.get('autoRenewingPlan'), dict)):
        raise ValueError('Unsupported subscription plan or offer')
    state = verified.get('subscriptionState')
    if state not in STATES:
        raise ValueError('Unknown subscription state')
    expiry = item.get('expiryTime')
    if expiry is None and state in {'SUBSCRIPTION_STATE_PENDING', 'SUBSCRIPTION_STATE_PENDING_PURCHASE_CANCELED'}:
        expires = 0
    else:
        try:
            parsed = datetime.fromisoformat(expiry.replace('Z', '+00:00'))
            if parsed.tzinfo is None: raise ValueError('Timezone required')
            expires = parsed.timestamp()
        except (AttributeError, TypeError, ValueError, OverflowError):
            raise ValueError('Invalid subscription expiry') from None
    current = time.time() if now is None else now
    active = state in ACTIVE and expires > current
    # latestOrderId may name a pending payment. Never use it for a currency grant.
    order = item.get('latestSuccessfulOrderId') if active else None
    if active and (not isinstance(order, str) or not order.strip() or len(order) > 256):
        raise ValueError('Successful paid order required')
    return {'account': owner, 'product': product, 'order': order,
            'expires': expires, 'state': state, 'active': active,
            'renewing': item.get('autoRenewingPlan', {}).get('autoRenewEnabled') is True}


class SubscriptionLedger:
    def __init__(self, ledger):
        self.ledger = ledger
        self.sql = hasattr(ledger, 'connect')
        if self.sql:
            with ledger.connect() as db:
                db.executescript('''
                CREATE TABLE IF NOT EXISTS subscription_token (
                    token TEXT PRIMARY KEY, account TEXT NOT NULL, product TEXT NOT NULL,
                    expires REAL NOT NULL, state TEXT NOT NULL, observed REAL NOT NULL);
                CREATE TABLE IF NOT EXISTS subscription_order (
                    id TEXT PRIMARY KEY, account TEXT NOT NULL, product TEXT NOT NULL,
                    token TEXT NOT NULL, credited INTEGER NOT NULL);
                CREATE INDEX IF NOT EXISTS subscription_account ON subscription_token(account);
                ''')
        else:
            prefix = ledger.accounts.id.removesuffix('-accounts')
            self.tokens = ledger.client.collection(prefix + '-subscription-tokens')
            self.orders = ledger.client.collection(prefix + '-subscription-orders')
            self.states = ledger.client.collection(prefix + '-subscriptions')

    @staticmethod
    def digest(value):
        return hashlib.sha256(value.encode('utf-8')).hexdigest()

    def order_known(self, account, product, token, order):
        if self.sql:
            with self.ledger.connect() as db:
                row = db.execute('SELECT account,product,token FROM subscription_order WHERE id=?', (order,)).fetchone()
        else:
            snap = self.orders.document(self.digest(order)).get()
            data = snap.to_dict() or {}
            row = (data.get('account'), data.get('product'), data.get('token')) if snap.exists else None
        if row and row != (account, product, self.digest(token)):
            raise ValueError('Order already bound')
        return row is not None

    def apply(self, token, value, observed):
        account, product, order = value['account'], value['product'], value['order']
        token_id = self.digest(token)
        if self.sql:
            with self.ledger.connect() as db:
                db.execute('BEGIN IMMEDIATE')
                self.ledger.ensure(db, account)
                previous = db.execute('SELECT account,product,observed FROM subscription_token WHERE token=?', (token_id,)).fetchone()
                if previous and previous[:2] != (account, product): raise ValueError('Token already bound')
                if previous and previous[2] > observed: return self.ledger.snapshot(db, account)
                paid = db.execute('SELECT account,product,token FROM subscription_order WHERE id=?', (order,)).fetchone() if order else None
                if paid and paid != (account, product, token_id): raise ValueError('Order already bound')
                if order and not paid:
                    amount = SUBSCRIPTION['amount'] if value.get('credit', True) else 0
                    balance = db.execute('SELECT balance FROM wallet WHERE account=?', (account,)).fetchone()[0]
                    if balance + amount > MAX_BALANCE: raise ValueError('Wallet capacity exceeded')
                    db.execute('INSERT INTO subscription_order VALUES (?,?,?,?,?)', (order, account, product, token_id, amount))
                    db.execute('UPDATE wallet SET balance=balance+?,revision=revision+1 WHERE account=?', (amount, account))
                db.execute('INSERT INTO subscription_token VALUES (?,?,?,?,?,?) ON CONFLICT(token) DO UPDATE SET expires=excluded.expires,state=excluded.state,observed=excluded.observed',
                           (token_id, account, product, value['expires'], value['state'], observed))
                return self.ledger.snapshot(db, account)
        from google.cloud import firestore
        self.ledger.wallet(account)
        account_ref = self.ledger.accounts.document(account)
        token_ref = self.tokens.document(token_id)
        state_ref = self.states.document(account).collection('tokens').document(token_id)
        order_ref = self.orders.document(self.digest(order)) if order else None

        @firestore.transactional
        def apply(tx):
            previous = token_ref.get(transaction=tx)
            paid = order_ref.get(transaction=tx) if order_ref else None
            data = self.ledger._data(account_ref.get(transaction=tx))
            old = previous.to_dict() or {}
            if previous.exists and (old.get('account'), old.get('product')) != (account, product): raise ValueError('Token already bound')
            if old.get('observed', 0) > observed: return data
            if paid and paid.exists:
                bound = paid.to_dict() or {}
                if (bound.get('account'), bound.get('product'), bound.get('token')) != (account, product, token_id): raise ValueError('Order already bound')
            if order and not (paid and paid.exists):
                amount = SUBSCRIPTION['amount'] if value.get('credit', True) else 0
                if data['balance'] + amount > MAX_BALANCE: raise ValueError('Wallet capacity exceeded')
                data['balance'] += amount
                data['revision'] += 1
                tx.set(account_ref, data)
                tx.set(order_ref, {'account': account, 'product': product, 'token': token_id, 'credited': amount})
            record = {'account': account, 'product': product, 'expires': value['expires'], 'state': value['state'], 'observed': observed}
            tx.set(token_ref, record)
            tx.set(state_ref, record)
            return data
        return self.ledger._snapshot(account, apply(self.ledger.client.transaction()))

    def active(self, account):
        now = time.time()
        if self.sql:
            with self.ledger.connect() as db:
                return any(expires > now and state in ACTIVE for expires, state in
                           db.execute('SELECT expires,state FROM subscription_token WHERE account=?', (account,)))
        return any((snap.to_dict() or {}).get('expires', 0) > now and
                   (snap.to_dict() or {}).get('state') in ACTIVE for snap in
                   self.states.document(account).collection('tokens').stream())


class GooglePushAuthenticator:
    def __init__(self, audience, service_account):
        if not audience.startswith('https://') or not service_account.endswith('.gserviceaccount.com'):
            raise ValueError('Exact HTTPS push audience and service account required')
        self.audience, self.service_account = audience, service_account

    def __call__(self, header):
        from google.oauth2 import id_token
        from google.auth.transport.requests import Request
        try:
            if not isinstance(header, str) or not header.startswith('Bearer '): raise ValueError('Bearer required')
            claims = id_token.verify_oauth2_token(header[7:], Request(), self.audience)
            if claims.get('email') != self.service_account or claims.get('email_verified') is not True:
                raise ValueError('Push identity mismatch')
        except (ValueError, KeyError):
            raise PermissionError('Verified push identity required') from None
        return True


class SubscriptionService:
    def __init__(self, ledger, google, package, push_authenticate):
        self.storage = SubscriptionLedger(ledger)
        self.google, self.package, self.push_authenticate = google, package, push_authenticate

    @property
    def ready(self):
        return self.google is not None and bool(self.package) and self.push_authenticate is not None

    def deliver_history(self, base, token, value, latest, observed):
        # Google renewal IDs append ..0, ..1, etc. Verify every missing order
        # independently; never infer payment from the suffix or elapsed months.
        match = re.fullmatch(r'(GPA\.[A-Za-z0-9.-]+?)\.\.(\d+)', latest)
        if match:
            stem, last = match.group(1), int(match.group(2))
            if last > 1000: raise ValueError('Order history requires reconciliation')
            candidates = [stem] + [stem + '..' + str(n) for n in range(last + 1)]
        else:
            candidates = [latest]
        missing = [order for order in candidates if not self.storage.order_known(value['account'], value['product'], token, order)]
        # Keep a single HTTP request bounded. A retry continues from durable
        # order records, including after a process crash or a long RTDN outage.
        for order in missing[:4]:
            response = self.google.get(base + '/orders/' + quote(order, safe=''), timeout=15)
            # Legacy/test histories can omit an implied base order. Only an
            # independently verified, existing order can ever receive currency.
            if getattr(response, 'status_code', None) == 404 and order != latest:
                continue
            response.raise_for_status()
            paid = response.json()
            items = paid.get('lineItems')
            if paid.get('orderId') != order or paid.get('purchaseToken') != token or not isinstance(items, list) or len(items) != 1:
                raise ValueError('Order binding mismatch')
            item = items[0]
            details = item.get('subscriptionDetails', {})
            phase = details.get('offerPhaseDetails', {})
            if (item.get('productId') != value['product'] or details.get('basePlanId') != SUBSCRIPTION['basePlanId']
                    or details.get('offerId') or not ('baseDetails' in phase or details.get('offerPhase') == 'BASE')):
                raise ValueError('Unsupported paid order plan')
            state = paid.get('state')
            if state not in {'PROCESSED', 'CANCELED', 'REFUNDED', 'PARTIALLY_REFUNDED'}:
                raise RuntimeError('Order not settled; retry later')
            # Reactivating before expiry can create a processed €0 order.
            # Such a reactivation must not be farmed for another monthly grant.
            paid_value = positive_money(paid.get('total'))
            if not paid_value and paid.get('developerRevenueInBuyerCurrency') is not None:
                paid_value = positive_money(paid['developerRevenueInBuyerCurrency'])
            grant = dict(value, order=order, credit=state == 'PROCESSED' and paid_value)
            self.storage.apply(token, grant, observed)
        if len(missing) > 4: raise RuntimeError('Continuing subscription reconciliation')

    def fulfill(self, product, token, account=None):
        if not self.ready: raise RuntimeError('Subscriptions unavailable')
        if product != SUBSCRIPTION['id']: raise ValueError('Unknown subscription')
        if not isinstance(token, str) or not token.strip() or len(token) > 8192: raise ValueError('Invalid purchase token')
        observed = time.time()
        base = 'https://androidpublisher.googleapis.com/androidpublisher/v3/applications/' + quote(self.package, safe='')
        response = self.google.get(base + '/purchases/subscriptionsv2/tokens/' + quote(token, safe=''), timeout=15)
        response.raise_for_status()
        verified = response.json()
        value = verified_subscription(product, verified, account)
        latest = verified['lineItems'][0].get('latestSuccessfulOrderId')
        if value['state'] not in {'SUBSCRIPTION_STATE_PENDING', 'SUBSCRIPTION_STATE_PENDING_PURCHASE_CANCELED'} and latest:
            if not isinstance(latest, str) or len(latest) > 256: raise ValueError('Invalid order ID')
            self.deliver_history(base, token, value, latest, observed)
        # Entitlement state is refreshed even when every paid order is already
        # delivered, or the subscription is paused/expired and has no new order.
        wallet = self.storage.apply(token, dict(value, order=None), observed)
        # Acknowledge only after durable, idempotent delivery. An acknowledgement
        # failure retries the whole path without granting the same order twice.
        if value['active'] and verified.get('acknowledgementState') == 'ACKNOWLEDGEMENT_STATE_PENDING':
            response = self.google.post(base + '/purchases/subscriptions/' + quote(product, safe='') +
                                        '/tokens/' + quote(token, safe='') + ':acknowledge', json={}, timeout=15)
            response.raise_for_status()
        return wallet, value['account']

    def push(self, header, envelope):
        if not self.ready: raise RuntimeError('Subscriptions unavailable')
        if self.push_authenticate(header) is not True:
            raise PermissionError('Verified push identity required')
        message = envelope.get('message', {})
        try:
            payload = json.loads(base64.b64decode(message['data'], validate=True))
        except (ValueError, KeyError, TypeError):
            raise ValueError('Invalid notification') from None
        if not isinstance(payload, dict) or payload.get('packageName') != self.package:
            raise ValueError('Notification package mismatch')
        if 'testNotification' in payload: return
        notice = payload.get('subscriptionNotification')
        if notice is None: return  # Other product types are handled independently.
        if not isinstance(notice, dict): raise ValueError('Invalid subscription notification')
        if notice.get('subscriptionId') != SUBSCRIPTION['id']: return
        self.fulfill(notice['subscriptionId'], notice.get('purchaseToken'))
