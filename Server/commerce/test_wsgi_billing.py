"""Regression tests for the Cloud Run billing bootstrap without real credentials.

The WSGI module creates its production application at import time. Load only
create_application's AST so tests cannot open a database or contact Google.
"""
import ast
import logging
import os
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch


class MissingCredentials(Exception):
    pass


class BillingStartupTests(unittest.TestCase):
    def setUp(self):
        source_path = Path(__file__).with_name('wsgi.py')
        tree = ast.parse(source_path.read_text(encoding='utf-8'), filename=str(source_path))
        factory = next(node for node in tree.body
                       if isinstance(node, ast.FunctionDef) and node.name == 'create_application')
        self.auth_default = Mock(return_value=('server-credentials', 'cloud-project'))
        self.authorized_session = Mock(return_value='trusted-google-session')
        self.commerce = Mock(return_value='application')
        self.namespace = {
            'os': os,
            'Path': Path,
            'logging': logging,
            'google': SimpleNamespace(auth=SimpleNamespace(default=self.auth_default)),
            'DefaultCredentialsError': MissingCredentials,
            'AuthorizedSession': self.authorized_session,
            'FirestoreLedger': Mock(return_value='durable-firestore-ledger'),
            'Ledger': Mock(return_value='durable-sqlite-ledger'),
            'UnityPlayerAuthenticator': Mock(return_value='verified-unity-login'),
            'CommerceApplication': self.commerce,
        }
        exec(compile(ast.Module(body=[factory], type_ignores=[]),
                     str(source_path), 'exec'), self.namespace)

    def start(self, enabled='0', package='com.lowperformence.dimensionsfront'):
        config = {
            'DF_UNITY_PROJECT_ID': 'unity-project',
            'DF_ACCOUNT_STORAGE': 'firestore',
            'DF_ENABLE_GOOGLE_PLAY': enabled,
            'DF_ANDROID_PACKAGE_NAME': package,
        }
        with patch.dict(os.environ, config, clear=True):
            return self.namespace['create_application']()

    def test_disabled_billing_preserves_wallet_and_never_loads_google_credentials(self):
        self.assertEqual(self.start(enabled='0'), 'application')
        self.auth_default.assert_not_called()
        self.authorized_session.assert_not_called()
        self.assertEqual(self.commerce.call_args.args[2:], (None, ''))

    def test_enabling_billing_initializes_google_publisher_session(self):
        self.assertEqual(self.start(enabled='1'), 'application')
        self.auth_default.assert_called_once_with(
            scopes=['https://www.googleapis.com/auth/androidpublisher'])
        self.authorized_session.assert_called_once_with('server-credentials')
        self.assertEqual(self.commerce.call_args.args[2:],
                         ('trusted-google-session', 'com.lowperformence.dimensionsfront'))

    def test_missing_package_fails_closed_without_disabling_account_server(self):
        self.assertEqual(self.start(enabled='1', package=''), 'application')
        self.auth_default.assert_not_called()
        self.assertEqual(self.commerce.call_args.args[2:], (None, ''))

    def test_missing_credentials_fails_closed_without_disabling_account_server(self):
        self.auth_default.side_effect = MissingCredentials()
        self.assertEqual(self.start(enabled='1'), 'application')
        self.assertEqual(self.commerce.call_args.args[2],
                         None)
        self.authorized_session.assert_not_called()

    def test_subscription_flag_requires_the_verified_push_configuration(self):
        config = {'DF_UNITY_PROJECT_ID': 'unity-project', 'DF_ACCOUNT_STORAGE': 'firestore',
                  'DF_ENABLE_GOOGLE_PLAY': '1', 'DF_ANDROID_PACKAGE_NAME': 'com.lowperformence.dimensionsfront',
                  'DF_ENABLE_SHARD_SUBSCRIPTIONS': '1'}
        with patch.dict(os.environ, config, clear=True):
            with self.assertRaises(KeyError): self.namespace['create_application']()

    def test_subscription_flag_wires_the_durable_ledger_and_exact_push_identity(self):
        config = {'DF_UNITY_PROJECT_ID': 'unity-project', 'DF_ACCOUNT_STORAGE': 'firestore',
                  'DF_ENABLE_GOOGLE_PLAY': '1', 'DF_ANDROID_PACKAGE_NAME': 'com.lowperformence.dimensionsfront',
                  'DF_ENABLE_SHARD_SUBSCRIPTIONS': '1',
                  'DF_RTDN_AUDIENCE': 'https://account.example/google-play/rtdn',
                  'DF_RTDN_SERVICE_ACCOUNT': 'push@project.iam.gserviceaccount.com'}
        with patch.dict(os.environ, config, clear=True), \
             patch('subscriptions.GooglePushAuthenticator') as push, \
             patch('subscriptions.SubscriptionService') as subscriptions:
            self.namespace['create_application']()
            push.assert_called_once_with(config['DF_RTDN_AUDIENCE'], config['DF_RTDN_SERVICE_ACCOUNT'])
            subscriptions.assert_called_once_with('durable-firestore-ledger', 'trusted-google-session',
                                                  config['DF_ANDROID_PACKAGE_NAME'], push.return_value)
            self.assertIs(self.commerce.call_args.kwargs['subscriptions'], subscriptions.return_value)


if __name__ == '__main__':
    unittest.main()
