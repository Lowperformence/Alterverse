import argparse
import unittest
from deploy_account import deployment_command, SERVICE


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.args = argparse.Namespace(project='existing-project', matchmaker_url='https://match.example',
            matchmaker_secret='matchmaker:1', admission_secret='admission:2', server_secret='server:3', season='S1')
        self.service = {'spec': {'template': {'spec': {'containers': [{'env': [
            {'name': 'DF_ACCOUNT_STORAGE', 'value': 'firestore'},
            {'name': 'DF_UNITY_PROJECT_ID', 'value': 'existing-unity-project'},
            {'name': 'DF_FIRESTORE_NAMESPACE', 'value': 'existing-accounts'}]}]}}}}

    def test_deploy_updates_existing_service_without_replacing_account_settings(self):
        command = deployment_command(self.args, self.service, '/repo')
        self.assertEqual(command[3], SERVICE)
        self.assertIn('--update-env-vars', command)
        self.assertIn('--update-secrets', command)
        self.assertNotIn('--set-env-vars', command)
        self.assertNotIn('--set-secrets', command)
        self.assertNotIn('DF_FIRESTORE_NAMESPACE', ' '.join(command))
        self.assertIn('DF_RANKED_SERVER_KEY=server:3', command[-1])

    def test_refuses_storage_migration(self):
        self.service['spec']['template']['spec']['containers'][0]['env'][0]['value'] = 'sqlite'
        with self.assertRaisesRegex(ValueError, 'Firestore'):
            deployment_command(self.args, self.service, '/repo')

    def test_refuses_shared_secret_and_unpinned_version(self):
        for ref in ('admission:2', 'server:latest', 'a-secret-value'):
            self.args.server_secret = ref
            with self.subTest(ref=ref), self.assertRaises(ValueError):
                deployment_command(self.args, self.service, '/repo')

    def test_refuses_environment_flag_injection(self):
        self.args.matchmaker_url = 'https://match.example,DF_ACCOUNT_STORAGE=sqlite'
        with self.assertRaises(ValueError):
            deployment_command(self.args, self.service, '/repo')
