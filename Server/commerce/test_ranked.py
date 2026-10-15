import copy
import io
import json
import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from unittest.mock import patch
from ledger import Ledger
from ranked import Ranked, rating_view, TEAM_SIZE, MATCH_PLAYERS
from application import CommerceApplication


class Gateway:
    def __init__(self): self.members = {}; self.counter = 0; self.ratings = []
    def create(self, mmr):
        self.counter += 1
        self.ratings.append(mmr)
        n = str(self.counter)
        result = dict(group_id='g'+n, member_id='u'+n, ticket_id='t'+n, status='SEARCHING')
        self.members[result['group_id']] = result
        return copy.deepcopy(result)
    def poll(self, group, member):
        self.assert_member(group, member)
        return copy.deepcopy(self.members[group])
    def assert_member(self, group, member):
        if self.members[group]['member_id'] != member: raise ValueError('Not group owner')
    def cancel(self, group): self.members.pop(group, None)


class RankedTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.ledger = self.make_ledger()
        self.gateway = Gateway()
        self.now = 1000
        self.ranked = Ranked(self.ledger, self.gateway, 'a'*32, clock=lambda: self.now)
    def make_ledger(self): return Ledger(Path(self.temp.name)/'account.db')
    def tearDown(self): self.temp.cleanup()
    def roster(self):
        return [dict(ticket=(m := self.ranked.search('a'+str(i), 'Hero'+str(i)))['ticket_id'],
                     admission=m['admission'], team=i//TEAM_SIZE) for i in range(MATCH_PLAYERS)]
    def match(self, match='m1'):
        roster = self.roster()
        self.ranked.register(dict(matchId=match, players=roster))
        return roster
    def test_complete_queue_register_settle_restart(self):
        self.match()
        self.ranked.settle(dict(matchId='m1', winner=0))
        self.assertEqual(self.ranked.status('a0')['points'], 1020)
        self.assertEqual(self.ranked.status('a5')['points'], 980)
        other = Ranked(self.ledger, self.gateway, 'a'*32, clock=lambda:self.now)
        self.assertEqual(other.status('a0')['lastResult']['delta'], 20)
        self.assertEqual(other.status('different')['games'], 0)
    def test_result_replay_concurrent_is_exactly_once(self):
        self.match()
        with ThreadPoolExecutor(max_workers=6) as pool:
            list(pool.map(lambda _:self.ranked.settle(dict(matchId='m1', winner=0)), range(12)))
        self.assertEqual(self.ranked.status('a0')['games'], 1)
        self.assertEqual(self.ranked.status('a0')['points'], 1020)
    def test_reinstall_relogin_keeps_rank_and_leaderboard(self):
        for i in range(5):
            self.match('reinstall-'+str(i))
            self.ranked.settle(dict(matchId='reinstall-'+str(i), winner=0))
        before = self.ranked.status('a0')
        leaders = self.ranked.dispatch('a0', '/ranked/leaderboard', {})['leaders']
        # New backend/store objects; client session, local preferences and display
        # name are not inputs to rank recovery. Authentication supplies the account.
        restored = Ranked(self.make_ledger(), self.gateway, 'a'*32, clock=lambda:self.now)
        self.assertEqual(restored.status('a0'), before)
        self.assertEqual(restored.dispatch('a0', '/ranked/leaderboard', {})['leaders'], leaders)
        self.assertEqual(restored.status('a0')['placementsRemaining'], 0)
        self.assertEqual(restored.status('different')['games'], 0)
        restored.search('a0', 'New device name')
        self.assertEqual(self.gateway.ratings[-1], before['points'])
        restored.cancel('a0')
        self.assertEqual(restored.status('a0'), before)
    def test_reinstall_cannot_reset_leaver_points_or_cooldown(self):
        self.match('left-before-uninstall')
        self.ranked.settle(dict(matchId='left-before-uninstall', winner=0, leavers=['t1']))
        before = self.ranked.status('a0')
        self.assertTrue(before['lastResult']['left'])
        self.assertGreater(before['cooldownUntil'], self.now)
        restored = Ranked(self.make_ledger(), self.gateway, 'a'*32, clock=lambda:self.now)
        self.assertEqual(restored.status('a0'), before)
        with self.assertRaisesRegex(ValueError, 'cooldown'):
            restored.search('a0', 'Reinstalled')
    def test_conflicting_result_never_overwrites(self):
        self.match(); self.ranked.settle(dict(matchId='m1', winner=0))
        with self.assertRaises(ValueError): self.ranked.settle(dict(matchId='m1', winner=1))
        self.assertEqual(self.ranked.status('a5')['games'], 1)
    def test_placements_and_leaderboard(self):
        self.assertEqual(self.ranked.dispatch('a0','/ranked/leaderboard',{})['leaders'], [])
        for i in range(5):
            self.match('m'+str(i)); self.ranked.settle(dict(matchId='m'+str(i), winner=0))
        status = self.ranked.status('a0')
        self.assertEqual(status['placementsRemaining'], 0)
        self.assertEqual(status['points'], 1081)
        self.assertEqual(status['rank'], 'Silver 2')
        leaders = self.ranked.dispatch('a0','/ranked/leaderboard',{})['leaders']
        self.assertEqual(len(leaders), 8)
        self.assertEqual(leaders[0]['points'], 1081)
        self.assertNotIn('account', leaders[0])
        self.match('m6'); self.ranked.settle(dict(matchId='m6',winner=0))
        self.assertEqual(self.ranked.status('a0')['points'], 1088)
    def test_forged_expired_and_cross_season_admissions(self):
        roster = self.roster()
        for token in ('bad', roster[0]['admission'][:-1]+'x', None):
            bad = copy.deepcopy(roster); bad[0]['admission'] = token
            with self.assertRaises(PermissionError): self.ranked.register(dict(matchId='m1',players=bad))
        self.now += 601
        with self.assertRaises(PermissionError): self.ranked.register(dict(matchId='m1',players=roster))
        self.now = 1000; self.ranked.season = 'S2'
        with self.assertRaises(PermissionError): self.ranked.register(dict(matchId='m1',players=roster))
    def test_cancel_invalidates_admission(self):
        roster = self.roster(); self.ranked.cancel('a0')
        with self.assertRaises(PermissionError): self.ranked.register(dict(matchId='m1',players=roster))
        self.assertEqual(self.ranked.status('a1')['games'], 0)
    def test_incomplete_duplicate_and_wrong_team_rosters(self):
        roster = self.roster()
        for bad in (roster[:-1], roster[:-1]+[roster[0]], [dict(p,team=0) for p in roster], [dict(p,ticket='wrong') for p in roster]):
            with self.assertRaises(ValueError): self.ranked.register(dict(matchId='m1',players=bad))
        self.assertEqual(self.ranked.status('a0')['games'], 0)
    def test_unregistered_match_is_rejected(self):
        with self.assertRaises(ValueError): self.ranked.settle(dict(matchId='unknown',winner=0))
    def test_fifth_teammate_and_legacy_ten_player_start_are_rejected(self):
        roster = self.roster()
        uneven = copy.deepcopy(roster)
        uneven[TEAM_SIZE]['team'] = 0
        with self.assertRaisesRegex(ValueError, 'balanced 4v4'):
            self.ranked.register(dict(matchId='uneven', players=uneven))
        legacy = copy.deepcopy(roster)
        for i in range(2):
            member = self.ranked.search('extra'+str(i), 'Hero')
            legacy.append(dict(ticket=member['ticket_id'], admission=member['admission'], team=i))
        with self.assertRaisesRegex(ValueError, 'eight humans'):
            self.ranked.register(dict(matchId='legacy', players=legacy))
        self.assertEqual(self.ranked.status('a0')['games'], 0)

    def test_four_player_team_average_controls_elo(self):
        def seed(tx):
            for i in range(MATCH_PLAYERS):
                account = 'a'+str(i)
                tx.set(self.ranked.pk(account), dict(self.ranked.player(account), mmr=1000 if i < TEAM_SIZE else 1200))
        self.ranked.store.atomic(seed)
        self.match('unequal-ratings')
        self.ranked.settle(dict(matchId='unequal-ratings', winner=0))
        self.assertEqual(self.ranked.status('a0')['points'], 1030)
        self.assertEqual(self.ranked.status('a4')['points'], 1170)

    def test_previously_registered_five_player_teams_can_finish_after_upgrade(self):
        # Simulate the previous server registering a match before the rollout.
        legacy = []
        with patch('ranked.TEAM_SIZE', 5), patch('ranked.MATCH_PLAYERS', 10):
            for i in range(10):
                member = self.ranked.search('legacy'+str(i), 'Hero')
                legacy.append(dict(ticket=member['ticket_id'], admission=member['admission'], team=i//5))
            self.ranked.register(dict(matchId='already-running', players=legacy))
        self.ranked.settle(dict(matchId='already-running', winner=0))
        self.assertEqual(self.ranked.status('legacy0')['points'], 1020)
        self.assertEqual(self.ranked.status('legacy5')['points'], 980)
        self.ranked.settle(dict(matchId='already-running', winner=0))
        self.assertEqual(self.ranked.status('legacy0')['games'], 1)

    def test_registered_account_cannot_queue_twice(self):
        roster = self.match()
        with self.assertRaises(ValueError): self.ranked.search('a0','Hero')
        with self.assertRaises(PermissionError): self.ranked.register(dict(matchId='second',players=roster))
    def test_register_replay_is_idempotent(self):
        roster = self.match()
        self.assertTrue(self.ranked.register(dict(matchId='m1',players=roster))['registered'])
        self.assertEqual(self.ranked.status('a0')['games'], 0)
    def test_void_clears_match_without_points(self):
        self.match(); self.ranked.void(dict(matchId='m1')); self.ranked.void(dict(matchId='m1'))
        self.assertEqual(self.ranked.status('a0')['points'], 1000)
        self.assertEqual(self.ranked.status('a0')['games'], 0)
        with self.assertRaises(ValueError): self.ranked.settle(dict(matchId='m1',winner=0))
        self.assertTrue(self.ranked.search('a0','Hero')['ticket_id'])
    def test_leaver_penalty_and_cooldown(self):
        roster = self.match(); self.ranked.settle(dict(matchId='m1',winner=0,leavers=[roster[0]['ticket']]))
        status = self.ranked.status('a0')
        self.assertEqual(status['points'], 990)
        self.assertEqual(status['wins'], 0)
        self.assertEqual(status['cooldownUntil'], self.now+900)
        with self.assertRaises(ValueError): self.ranked.search('a0','Hero')
        self.now += 901
        self.assertTrue(self.ranked.search('a0','Hero')['ticket_id'])
    def test_unknown_leaver_does_not_partially_commit(self):
        self.match()
        with self.assertRaises(ValueError): self.ranked.settle(dict(matchId='m1',winner=0,leavers=['unknown']))
        self.assertEqual(self.ranked.status('a0')['games'], 0)
    def test_client_cannot_set_rating_or_account(self):
        self.ranked.dispatch('a0','/ranked/search',dict(account='a1',mmr=9000,displayName='Hero'))
        self.assertEqual(self.gateway.ratings, [1000])
        self.assertEqual(self.ranked.status('a1')['games'], 0)
    def test_queue_retry_does_not_create_extra_ticket(self):
        first = self.ranked.search('a0','Hero')
        for _ in range(5): self.assertEqual(self.ranked.search('a0','Hero')['ticket_id'],first['ticket_id'])
        self.assertEqual(self.gateway.counter, 1)
    def test_failed_queue_creation_releases_reservation(self):
        with patch.object(self.gateway,'create',side_effect=RuntimeError('outage')):
            with self.assertRaises(RuntimeError): self.ranked.search('a0','Hero')
        self.assertTrue(self.ranked.search('a0','Hero')['ticket_id'])
    def test_new_season_has_separate_rank(self):
        self.match(); self.ranked.settle(dict(matchId='m1',winner=0)); self.ranked.season = 'S2'
        self.assertEqual(self.ranked.status('a0')['points'], 1000)
        self.assertEqual(self.ranked.status('a0')['games'], 0)
    def test_void_before_delayed_registration_prevents_launch(self):
        roster = self.roster()
        self.ranked.void(dict(matchId='late'))
        with self.assertRaises(ValueError): self.ranked.register(dict(matchId='late', players=roster))
        self.assertEqual(self.ranked.status('a0')['games'], 0)
    def test_rank_boundaries(self):
        for points, expected in [(0,'Bronze 3'),(999,'Bronze 1'),(1000,'Silver 3'),(1200,'Gold 3'),(1400,'Platinum 3'),(1600,'Diamond 3'),(1800,'Master 3'),(2000,'Dimensions Legend')]:
            self.assertEqual(rating_view(dict(self.ranked.player('a0'),mmr=points,games=5))['rank'],expected)

    def test_cancel_outage_invalidates_admission_before_gateway_cleanup(self):
        roster = self.roster()
        with patch.object(self.gateway, 'cancel', side_effect=RuntimeError('matchmaker outage')):
            with self.assertRaises(RuntimeError): self.ranked.cancel('a0')
        with self.assertRaises(ValueError): self.ranked.poll('a0')
        with self.assertRaises(PermissionError): self.ranked.register(dict(matchId='cancelled', players=roster))
        fresh = self.ranked.search('a0', 'Hero')
        self.assertNotEqual(fresh['ticket_id'], roster[0]['ticket'])

    def test_admission_is_invalid_at_its_expiration(self):
        roster = self.roster()
        self.now += 600
        with self.assertRaises(PermissionError): self.ranked.register(dict(matchId='expired', players=roster))


class RankedHttpTests(unittest.TestCase):
    setUp = RankedTests.setUp
    tearDown = RankedTests.tearDown
    make_ledger = RankedTests.make_ledger
    roster = RankedTests.roster
    match = RankedTests.match
    def call(self,path,data,auth='player'):
        raw = json.dumps(data).encode(); status=[]
        app = CommerceApplication(self.ledger,lambda token:'a0' if token=='player' else None,None,'',self.ranked,'server-key')
        body = b''.join(app(dict(REQUEST_METHOD='POST',CONTENT_LENGTH=str(len(raw)),PATH_INFO=path,
                                HTTP_AUTHORIZATION=auth,**{'wsgi.input':io.BytesIO(raw)}),lambda code,headers:status.append(code)))
        return status[0],json.loads(body)
    def test_player_cannot_submit_or_register_result(self):
        for path in ('/ranked/server/register','/ranked/server/result','/ranked/server/void'):
            self.assertEqual(self.call(path,{})[0],'401 Unauthorized')
    def test_authenticated_status_and_queue(self):
        self.assertEqual(self.call('/ranked/status',{})[1]['ranked']['points'],1000)
        self.assertEqual(self.call('/ranked/search',{'mmr':9000})[0],'200 OK')
        self.assertEqual(self.gateway.ratings,[1000])
    def test_server_only_settlement_and_conflicting_retry(self):
        self.match()
        self.assertEqual(self.call('/ranked/server/result',dict(matchId='m1',winner=0),'Bearer server-key')[0],'200 OK')
        self.assertEqual(self.call('/ranked/server/result',dict(matchId='m1',winner=1),'Bearer server-key')[0],'400 Bad Request')
    def test_unsigned_access_denied(self):
        self.assertEqual(self.call('/ranked/status',{},'')[0],'401 Unauthorized')


if __name__ == '__main__': unittest.main()
