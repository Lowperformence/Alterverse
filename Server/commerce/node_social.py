"""Run the existing clan rules against the Node account store, on loopback only.

Node continues to own purchases, subscriptions, profile saves and name claims.
This adapter reads those same accounts and reuses the existing social namespace.
"""
import json
import os
import re

from application import CommerceApplication
from authentication import UnityPlayerAuthenticator
from firestore_ledger import FirestoreLedger
from safe_identity import profile_name, valid_player_name
from social import SocialForbidden
from google.cloud import firestore


class NodeAccountLedger(FirestoreLedger):
    def __init__(self, namespace, project_id=None, client=None):
        if not isinstance(namespace, str) or not re.fullmatch(r'[a-z0-9][a-z0-9-]{0,62}', namespace):
            raise ValueError('Explicit existing social namespace required')
        if client is None:
            # The inspected Node source uses new Firestore() without an override.
            # Never select another project's similarly named players collection.
            client = firestore.Client()
            if project_id and project_id != client.project:
                raise ValueError('Clan and Node account data must use the same Firestore project; verify the previous deployment')
        super().__init__(project_id=project_id, namespace=namespace, client=client)
        self.accounts = self.client.collection('players')
        self.social_namespace = namespace

    def wallet(self, account):
        # Do not create another account, migrate a name or use a different wallet.
        snap = self.accounts.document(account).get()
        raw = snap.to_dict() if snap.exists else None
        if not isinstance(raw, dict):
            raise RuntimeError('Load the account wallet before using clans')
        for key in ('balance', 'revision', 'profileRevision'):
            if type(raw.get(key)) is not int or raw[key] < 0:
                raise RuntimeError('Node account schema is not compatible')
        if (not isinstance(raw.get('profile'), list) or not isinstance(raw.get('owned'), list)
                or not all(isinstance(sku, str) for sku in raw['owned'])
                or not valid_player_name(profile_name(raw['profile']))):
            raise RuntimeError('Finish account profile synchronization before using clans')
        return self._snapshot(account, self._data(snap))

    def verify_existing_clans(self, required=True):
        if required and not any(self.client.collection(self.social_namespace + '-social-clan').limit(1).stream()):
            raise RuntimeError('No clans found in the configured namespace; verify the previous deployment before starting')


class NodeSocialApplication(CommerceApplication):
    def __init__(self, ledger, authenticate, chat_config=None):
        super().__init__(ledger, authenticate, None, '', chat_config=chat_config)

    def decorate_wallet(self, account, wallet):
        # The Node response decorator supplies the existing billing flags.
        wallet['accountHash'] = account
        return wallet

    @staticmethod
    def reply(start_response, status, value):
        start_response(status, [('Content-Type', 'application/json'), ('Cache-Control', 'no-store')])
        return [json.dumps(value, separators=(',', ':')).encode()]

    def __call__(self, env, start_response):
        path = env.get('PATH_INFO', '')
        if path == '/internal/health' and env.get('REQUEST_METHOD') == 'GET':
            return self.reply(start_response, '200 OK', {'ok': True, 'clans': True})
        if path in ('/social/_sync', '/social/_access'):
            if env.get('REQUEST_METHOD') != 'POST':
                return self.reply(start_response, '405 Method Not Allowed', {'fulfilled': False})
            account = self.authenticate(env.get('HTTP_AUTHORIZATION', ''))
            if not account:
                return self.reply(start_response, '401 Unauthorized', {'fulfilled': False})
            try:
                self.social.check_account_allowed(account)
                if path == '/social/_sync':
                    self.social.sync_wallet(account)
                return self.reply(start_response, '200 OK', {'fulfilled': True})
            except (SocialForbidden, PermissionError):
                return self.reply(start_response, '403 Forbidden', {'fulfilled': False})
            except Exception:
                return self.reply(start_response, '503 Service Unavailable', {'fulfilled': False})
        # This process cannot become a second billing/profile endpoint.
        if not isinstance(path, str) or not path.startswith('/social/'):
            return self.reply(start_response, '404 Not Found', {'fulfilled': False})
        return super().__call__(env, start_response)


def create_application(environ=None, client=None, authenticate=None):
    env = os.environ if environ is None else environ
    namespace = env.get('DF_SOCIAL_NAMESPACE', '')
    ledger = NodeAccountLedger(namespace, project_id=env.get('DF_FIRESTORE_PROJECT_ID') or None, client=client)
    # An incorrect namespace must not silently present an empty replacement clan store.
    allow_empty = env.get('DF_SOCIAL_ALLOW_EMPTY_NAMESPACE', '0')
    if allow_empty not in ('0', '1'):
        raise ValueError('DF_SOCIAL_ALLOW_EMPTY_NAMESPACE must be 0 or 1')
    ledger.verify_existing_clans(required=allow_empty != '1')
    authenticate = authenticate or UnityPlayerAuthenticator(env['DF_UNITY_PROJECT_ID'])
    return NodeSocialApplication(ledger, authenticate)
