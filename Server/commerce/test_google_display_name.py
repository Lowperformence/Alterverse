"""Google display aliases persist without renaming or transferring an account."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from ledger import Ledger, validate_profile
from firestore_ledger import FirestoreLedger
from firestore_social import FirestoreSocial
from social import Social
from clan_chat import ChatConfig
from ranked import Ranked
from safe_identity import (NAME_KEY, PLAY_GAMES_NAME_KEY, profile_name,
                           profile_display_name, valid_display_name, visible_player_name)
from test_firestore_social import Client, transactional
from test_ranked import Gateway


class GoogleDisplayNameTests(unittest.TestCase):
    def test_unicode_and_transport_bounds(self):
        for name in ('Chris', 'Chris Ä龍🎮', 'Café', '龍' * 20, '🎮' * 15, 'A' * 32):
            self.assertTrue(valid_display_name(name), name)
            self.assertEqual(visible_player_name(name, 'account'), name)
        for name in (None, '', True, ' name', 'name ', '<b>name</b>', 'a\nb',
                     'a\u202eb', '\ud800', 'Cafe\u0301', '龍' * 21, '🎮' * 16, 'A' * 33):
            self.assertFalse(valid_display_name(name), repr(name))
            with self.assertRaises(ValueError):
                validate_profile([dict(key=PLAY_GAMES_NAME_KEY, kind=1, text=name)])
        with self.assertRaises(ValueError):
            validate_profile([dict(key=PLAY_GAMES_NAME_KEY, kind=0, number=2)])

    def test_alias_cloud_restore_social_and_ranked_on_both_stores(self):
        with tempfile.TemporaryDirectory() as directory, patch('google.cloud.firestore.transactional', transactional):
            for ledger in (Ledger(Path(directory) / 'google.db'), FirestoreLedger(client=Client())):
                with self.subTest(adapter=type(ledger).__name__):
                    wallet = ledger.wallet('owner')
                    canonical = profile_name(wallet['profile'])
                    profile = wallet['profile'] + [dict(key=PLAY_GAMES_NAME_KEY, kind=1, text='Chris Ä龍🎮'),
                        dict(key='DF_Shards', kind=0, number=777), dict(key='DF_AccountXP', kind=1, text='12345')]
                    saved = ledger.save_profile('owner', profile, wallet['profileRevision'])
                    self.assertEqual(profile_name(saved['profile']), canonical)
                    self.assertEqual(profile_display_name(saved['profile']), 'Chris Ä龍🎮')
                    restored = ledger.wallet('owner')
                    self.assertEqual(restored, saved)
                    self.assertNotEqual(profile_display_name(ledger.wallet('other')['profile']), 'Chris Ä龍🎮')
                    config = ChatConfig(enabled=False)
                    social = Social(ledger, chat_config=config) if hasattr(ledger, 'connect') else FirestoreSocial(ledger, chat_config=config)
                    before = social.dispatch('owner', '/social/state', {})['me']
                    self.assertEqual(before['name'], 'Chris Ä龍🎮')
                    ranked = Ranked(ledger, Gateway(), 'x' * 32)
                    ranked.search('owner', 'FORGED REQUEST NAME')
                    row = ranked.store.atomic(lambda tx: tx.get(ranked.pk('owner')))
                    self.assertEqual(row['displayName'], 'Chris Ä龍🎮')
                    ranked.cancel('owner')
                    # Only the display alias changes; ownership and progression remain fixed.
                    revised = [dict(e, text='Neuer Name') if e['key'] == PLAY_GAMES_NAME_KEY else e for e in saved['profile']]
                    saved = ledger.save_profile('owner', revised, saved['profileRevision'])
                    after = social.dispatch('owner', '/social/state', {})['me']
                    self.assertEqual(after['name'], 'Neuer Name')
                    self.assertEqual(after['playerId'], before['playerId'])
                    self.assertEqual(after['xp'], before['xp'])
                    self.assertEqual(profile_name(saved['profile']), canonical)
                    self.assertEqual(next(e['number'] for e in saved['profile'] if e['key'] == 'DF_Shards'), 777)
                    # Removing an alias falls back to the same canonical identity.
                    profile = [e for e in saved['profile'] if e['key'] != PLAY_GAMES_NAME_KEY]
                    saved = ledger.save_profile('owner', profile, saved['profileRevision'])
                    self.assertEqual(profile_display_name(saved['profile']), canonical)

    def test_display_name_does_not_replace_canonical_claim(self):
        entries = [dict(key=NAME_KEY, kind=1, text='SolarFox0000001'),
                   dict(key=PLAY_GAMES_NAME_KEY, kind=1, text='Chris')]
        self.assertEqual(profile_name(entries), 'SolarFox0000001')
        self.assertEqual(profile_display_name(entries), 'Chris')
        entries[-1]['text'] = '<b>bad</b>'
        self.assertEqual(profile_display_name(entries), 'SolarFox0000001')


if __name__ == '__main__':
    unittest.main()
