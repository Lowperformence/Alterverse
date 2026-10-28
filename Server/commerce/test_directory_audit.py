"""Coverage of real ledger writes, without the old test-only social-sync wrapper."""
import copy
import unittest
import uuid
from unittest.mock import patch

from clan_chat import ChatConfig
from directory_audit import audit_directory
from firestore_ledger import FirestoreLedger
from firestore_social import FirestoreSocial
from social import level_for_xp
from test_firestore_social import Client, Transaction, transactional


class DirectoryTests(unittest.TestCase):
    def setUp(self):
        self.client = Client()
        self.patch = patch('google.cloud.firestore.transactional', transactional)
        self.patch.start()
        self.addCleanup(self.patch.stop)
        # Deliberately use the unwrapped production ledger.
        self.ledger = FirestoreLedger(client=self.client, namespace='directory-test')
        self.social = FirestoreSocial(self.ledger, chat_config=ChatConfig(enabled=False))

    def save(self, who, xp, revision=0):
        return self.ledger.save_profile(who, [
            {'key': 'DF_AccountXP', 'kind': 1, 'text': str(xp)},
            {'key': 'DF_Shards', 'kind': 0, 'number': 1000},
        ], revision)

    def call(self, who, route='/social/levels', **data):
        return self.social.dispatch(who, route, data)

    def test_saved_players_are_indexed_without_social_requests_and_all_pages_are_complete(self):
        for i in range(17):
            self.save('player-' + str(i), i * 100)
        pages = [self.call('player-0', offset=offset) for offset in (0, 8, 16)]
        players = [p for page in pages for p in page['players']]
        self.assertEqual([len(page['players']) for page in pages], [8, 8, 1])
        self.assertEqual([page['total'] for page in pages], [17, 17, 17])
        self.assertEqual(len({p['playerId'] for p in players}), 17)
        self.assertEqual([p['rank'] for p in players], list(range(1, 18)))
        self.assertEqual([p['xp'] for p in players], list(range(1600, -1, -100)))
        self.assertEqual(pages[0]['selfRank'], 17)

    def test_profile_and_directory_write_roll_back_together(self):
        saved = self.save('a', 102)
        before = copy.deepcopy(self.client.data)
        original = Transaction.set
        def broken(tx, ref, value):
            if '-social-player/' in ref.path:
                raise RuntimeError('Injected directory write failure')
            original(tx, ref, value)
        with patch.object(Transaction, 'set', broken), self.assertRaises(RuntimeError):
            self.save('a', 10000, saved['profileRevision'])
        self.assertEqual(self.client.data, before)

    def test_noop_save_repairs_a_missing_projection_without_bumping_profile_revision(self):
        saved = self.save('a', 10000)
        ref = self.ledger.players.document('a')
        del self.client.data[ref.path]
        current = self.ledger.save_profile('a', saved['profile'], saved['profileRevision'])
        self.assertEqual(current['profileRevision'], saved['profileRevision'])
        self.assertEqual(ref.get().to_dict()['level'], level_for_xp(10000))

    def test_audit_does_not_write_and_repair_preserves_stable_ids_and_private_profile(self):
        self.save('viewer', 102)
        self.save('older', 10000)
        ref = self.ledger.players.document('older')
        del self.client.data[ref.path]
        self.save('stale', 1000)
        stale = self.ledger.players.document('stale')
        stable_id = stale.get().to_dict()['player_id']
        self.client.data[stale.path]['xp'] = 0
        before = copy.deepcopy(self.client.data)
        self.assertEqual(self.call('viewer')['total'], 2)
        # The read of a valid caller may refresh its name claim, but auditing itself is read-only.
        before = copy.deepcopy(self.client.data)
        self.assertEqual(audit_directory(self.ledger), dict(profiles=3, missing=1, stale=1, current=1, repaired=0))
        self.assertEqual(self.client.data, before)
        account_rows = {p: v for p, v in before.items() if '-accounts/' in p}
        self.assertEqual(audit_directory(self.ledger, apply=True)['repaired'], 2)
        self.assertEqual(self.call('viewer')['total'], 3)
        self.assertEqual(stale.get().to_dict()['player_id'], stable_id)
        self.assertEqual({p: v for p, v in self.client.data.items() if '-accounts/' in p}, account_rows)
        self.assertEqual(audit_directory(self.ledger)['current'], 3)
        self.assertEqual(audit_directory(self.ledger, apply=True)['repaired'], 0)

    def test_missing_member_projection_understates_clan_level_and_repair_restores_sum(self):
        a = self.save('leader', 102)
        self.save('member', 10000)
        clan = self.call('leader', '/social/clan/create', operationId=uuid.uuid4().hex,
                         expectedProfileRevision=a['profileRevision'])['clan']['clanId']
        self.call('member', '/social/clan/join', clanId=clan)
        expected = level_for_xp(102) + level_for_xp(10000)
        self.assertEqual(self.call('leader', '/social/clan-levels')['clans'][0]['totalLevel'], expected)
        del self.client.data[self.ledger.players.document('member').path]
        incomplete = self.call('leader', '/social/clan-levels')['clans'][0]
        self.assertEqual(incomplete['memberCount'], 2)
        self.assertEqual(incomplete['totalLevel'], level_for_xp(102))
        self.assertEqual(audit_directory(self.ledger, apply=True)['repaired'], 1)
        repaired = self.call('leader', '/social/clan-levels')['clans'][0]
        self.assertEqual(repaired['totalLevel'], expected)
        self.assertEqual(repaired['memberCount'], 2)
        self.assertEqual(repaired['clanId'], clan)

    def test_old_wallet_response_cannot_rewind_xp_and_public_projection_contains_only_public_fields(self):
        first = self.save('a', 102)
        self.save('a', 10000, first['profileRevision'])
        self.social.sync_wallet('a', first)
        row = self.ledger.players.document('a').get().to_dict()
        self.assertEqual(row['xp'], 10000)
        self.assertEqual(set(row), {'account', 'player_id', 'name', 'name_search', 'xp', 'level', 'avatar'})


if __name__ == '__main__':
    unittest.main()
