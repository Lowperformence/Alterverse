import unittest
from ranked_configuration import validate_ranked_environment


class RankedConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.env = dict(DF_ENABLE_RANKED='1', DF_ACCOUNT_STORAGE='firestore',
                        K_SERVICE='existing-account-server', DF_RANKED_MATCHMAKER_URL='https://match.example/v1',
                        DF_RANKED_MATCHMAKER_TOKEN='private-token', DF_RANKED_ADMISSION_KEY='a' * 32,
                        DF_RANKED_SERVER_KEY='b' * 32, DF_RANKED_SEASON='S1')

    def test_existing_firestore_configuration(self):
        validate_ranked_environment(self.env)

    def test_disabled_ranked_does_not_change_casual_configuration(self):
        validate_ranked_environment({'K_SERVICE': 'existing-account-server'})

    def test_cloud_run_rejects_ephemeral_sqlite(self):
        self.env.update(DF_ACCOUNT_STORAGE='sqlite', DF_ACCOUNT_DB='/tmp/account.db')
        with self.assertRaisesRegex(ValueError, 'Cloud Run'):
            validate_ranked_environment(self.env)

    def test_self_hosted_sqlite_remains_supported(self):
        self.env.pop('K_SERVICE')
        self.env.update(DF_ACCOUNT_STORAGE='sqlite', DF_ACCOUNT_DB='/persistent/account.db')
        validate_ranked_environment(self.env)

    def test_separate_server_credentials(self):
        self.env['DF_RANKED_SERVER_KEY'] = self.env['DF_RANKED_ADMISSION_KEY']
        with self.assertRaisesRegex(ValueError, 'different'):
            validate_ranked_environment(self.env)

    def test_short_keys_are_rejected_without_logging_values(self):
        for name in ('DF_RANKED_SERVER_KEY', 'DF_RANKED_ADMISSION_KEY'):
            env = dict(self.env, **{name: 'private-short-key'})
            with self.assertRaises(ValueError) as error:
                validate_ranked_environment(env)
            self.assertNotIn('private-short-key', str(error.exception))

    def test_invalid_urls(self):
        for url in ('http://match.example', 'https://', 'https://token@match.example',
                    'https://match.example?token=secret', 'https://match.example#fragment'):
            with self.subTest(url=url), self.assertRaises(ValueError):
                validate_ranked_environment(dict(self.env, DF_RANKED_MATCHMAKER_URL=url))

    def test_missing_token_and_invalid_season(self):
        for change in ({'DF_RANKED_MATCHMAKER_TOKEN': ''}, {'DF_RANKED_SEASON': 'S/2'}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                validate_ranked_environment(dict(self.env, **change))
