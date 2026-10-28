"""Social permissions, durability, public rankings and concurrent membership tests."""
import io
import hashlib
import json
import tempfile
import unittest
import uuid
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from unittest.mock import patch
from application import CommerceApplication
from ledger import Ledger
from social import Social, SocialForbidden, MAX_XP, level_for_xp
from clan_chat import ChatConfig
from safe_identity import DESCRIPTIONS, valid_player_name, valid_clan_name, valid_clan_tag


class SocialTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.path = Path(self.tmp.name) / 'account.db'
        self.ledger = self.make_ledger()
        # Exercise migration of an existing profile before social tables exist.
        self.ledger.save_profile('a', self.profile('SolarFox0000001', 102), 0)
        self.social = self.make_social(self.ledger)
        self.ledger.save_profile('b', self.profile('SolarFox0000002', 0), 0)
        self.ledger.save_profile('c', self.profile('LunarOwl0000003', 1000), 0)
        self.ids = {a: self.call(a)['me']['playerId'] for a in 'abc'}

    def make_ledger(self): return Ledger(self.path)

    def make_social(self, ledger): return Social(ledger, chat_config=ChatConfig(enabled=False))

    def tearDown(self):
        self.tmp.cleanup()

    @staticmethod
    def profile(name, xp):
        return [{'key':'DF_PlayerName','kind':1,'text':name}, {'key':'DF_AccountXP','kind':1,'text':str(xp)}, {'key':'DF_Shards','kind':0,'number':1000}]

    def call(self, who, route='/social/state', **data):
        return self.social.dispatch(who, route, data)

    def create(self, who='a', name='Solar Guard 0000001', tag='C0000001', opened=True):
        if not valid_clan_name(name):
            suffix=int(hashlib.sha256(name.casefold().encode()).hexdigest(),16)%10000000
            name=f'Solar Guard {suffix:07d}'
        if not valid_clan_tag(tag):
            suffix=int(hashlib.sha256(tag.upper().encode()).hexdigest(),16)%10000000
            tag=f'C{suffix:07d}'
        return self.call(who, '/social/clan/create', name=name, tag=tag, open=opened, operationId=uuid.uuid4().hex,expectedProfileRevision=self.ledger.wallet(who)['profileRevision'])['clan']['clanId']

    def test_migration_and_stable_public_identity_without_private_profile(self):
        first = self.call('a')
        restored = self.make_social(self.make_ledger()).dispatch('a', '/social/state', {})
        self.assertEqual(first['me'], restored['me'])
        self.assertEqual(first['me']['level'], 2)
        encoded = json.dumps(first)
        self.assertNotIn('account', encoded)
        self.assertNotIn('DF_', encoded)
        self.assertNotEqual(first['me']['playerId'], 'a')
        self.assertEqual(len(first['me']['playerId']), 20)

    def test_curve_matches_client_at_every_level_and_cap(self):
        for level in range(1,1001):
            n = level-1
            xp = 100*n + 2*n*n + n*n*n//250
            self.assertEqual(level_for_xp(xp), level)
            if level > 1:
                self.assertEqual(level_for_xp(xp-1), level-1)
        self.ledger.save_profile('b', self.profile('Beta', 99999999999999), 1)
        self.assertEqual(self.call('b')['me']['xp'], MAX_XP)
        self.assertEqual(self.call('b')['me']['level'], 1000)

    def test_player_directory_is_disabled(self):
        for who in 'abc':
            for query in ('solarfox', self.ids['b'], "https://discord.gg/test"):
                with self.assertRaisesRegex(ValueError, 'disabled'):
                    self.call(who, '/social/players', query=query)
    def test_friend_requests_and_actions_are_disabled(self):
        for action in ('request', 'accept', 'remove'):
            with self.assertRaisesRegex(ValueError, 'disabled'):
                self.call('a', '/social/friend/' + action, playerId=self.ids['b'])
        for key in ('friends', 'incoming', 'outgoing', 'blocked'):
            self.assertEqual(self.call('a')[key], [])
    def test_no_friend_requests_even_with_both_players_consent(self):
        for who, target in (('a', 'b'), ('b', 'a')):
            with self.assertRaisesRegex(ValueError, 'disabled'):
                self.call(who, '/social/friend/request', playerId=self.ids[target])
            self.assertEqual(self.call(who)['incoming'], [])
            self.assertEqual(self.call(who)['outgoing'], [])
    def test_block_and_unblock_routes_cannot_be_communication(self):
        for route in ('/social/block', '/social/unblock'):
            with self.assertRaisesRegex(ValueError, 'disabled'):
                self.call('a', route, playerId=self.ids['b'])
        for key in ('friends','incoming','outgoing','blocked'):
            self.assertEqual(self.call('b')[key], [])
    def test_levels_order_tie_break_pagination_and_saved_profile_updates(self):
        self.ledger.save_profile('b',self.profile('Beta',103),1)
        rows=self.call('a','/social/levels')
        self.assertEqual([p['playerId'] for p in rows['players']], [self.ids['c'],self.ids['b'],self.ids['a']])
        self.assertEqual(rows['selfRank'],3)
        self.assertEqual(self.call('a','/social/levels',offset=1)['players'][0]['rank'],2)
        self.ledger.save_profile('b',self.profile('Beta',102),2)
        rows=self.call('a','/social/levels')['players']
        self.assertEqual([p['playerId'] for p in rows[1:]], sorted([self.ids['a'],self.ids['b']]))

    def test_clan_names_are_server_assigned_unique_and_open_join_works(self):
        clan=self.create()
        first=self.call('a')['clan']
        other=self.create('c','Solar Guard 0000001','C0000001')
        second=self.call('c')['clan']
        self.assertNotEqual(first['name'],second['name'])
        self.assertNotEqual(first['tag'],second['tag'])
        self.assertTrue(valid_clan_name(first['name']))
        self.assertTrue(valid_clan_tag(first['tag']))
        self.call('b','/social/clan/join',clanId=clan)
        self.call('b','/social/clan/join',clanId=clan)
        state=self.call('a')
        self.assertEqual(state['clan']['memberCount'],2)
        self.assertEqual(state['role'],'leader')
        self.assertEqual(self.call('b')['role'],'member')
        with self.assertRaises(ValueError): self.create('b','Another Clan','AC')

    def test_applications_permissions_accept_reject_and_cancel(self):
        clan=self.create(opened=False)
        self.call('b','/social/clan/join',clanId=clan)
        self.assertIsNone(self.call('b')['clan'])
        self.assertEqual(self.call('b')['pendingClans'],[clan])
        self.assertEqual(len(self.call('a')['applications']),1)
        self.call('b','/social/clan/cancel',clanId=clan)
        self.assertEqual(self.call('a')['applications'],[])
        self.call('b','/social/clan/join',clanId=clan)
        self.call('a','/social/clan/reject',playerId=self.ids['b'])
        self.assertEqual(self.call('b')['pendingClans'],[])
        self.call('b','/social/clan/join',clanId=clan)
        self.call('a','/social/clan/accept',playerId=self.ids['b'])
        self.call('c','/social/clan/join',clanId=clan)
        self.assertEqual(self.call('b')['applications'],[])
        with self.assertRaises(SocialForbidden): self.call('b','/social/clan/accept',playerId=self.ids['c'])
        self.call('a','/social/clan/role',playerId=self.ids['b'],role='officer')
        self.call('b','/social/clan/accept',playerId=self.ids['c'])
        self.assertEqual(self.call('c')['pendingClans'],[])

    def test_member_and_officer_permissions_and_cross_clan_targets(self):
        clan=self.create()
        self.call('b','/social/clan/join',clanId=clan)
        self.call('c','/social/clan/join',clanId=clan)
        for route in ('kick','role','transfer'):
            with self.assertRaises(SocialForbidden): self.call('b','/social/clan/'+route,playerId=self.ids['c'],role='officer')
        self.call('a','/social/clan/role',playerId=self.ids['b'],role='officer')
        with self.assertRaises(SocialForbidden): self.call('b','/social/clan/kick',playerId=self.ids['a'])
        self.call('a','/social/clan/role',playerId=self.ids['c'],role='officer')
        with self.assertRaises(SocialForbidden): self.call('b','/social/clan/kick',playerId=self.ids['c'])
        self.call('a','/social/clan/role',playerId=self.ids['c'],role='member')
        self.call('b','/social/clan/kick',playerId=self.ids['c'])
        self.assertIsNone(self.call('c')['clan'])
        self.create('c','Other Clan','OC')
        with self.assertRaises(ValueError): self.call('a','/social/clan/transfer',playerId=self.ids['c'])

    def test_transfer_leave_and_last_member_deletes_clan(self):
        clan=self.create()
        self.call('b','/social/clan/join',clanId=clan)
        with self.assertRaises(ValueError): self.call('a','/social/clan/leave')
        self.call('a','/social/clan/transfer',playerId=self.ids['b'])
        self.assertEqual(self.call('b')['role'],'leader')
        self.assertEqual(self.call('a')['role'],'member')
        self.call('a','/social/clan/leave')
        self.call('b','/social/clan/leave')
        self.assertEqual(self.call('c','/social/clan-levels')['clans'],[])
        with self.assertRaises(ValueError): self.call('c','/social/clan/join',clanId=clan)

    def test_clan_rank_sum_updates_on_progress_leave_and_restart(self):
        clan=self.create()
        self.call('b','/social/clan/join',clanId=clan)
        second=self.create('c','Other Clan','OC')
        rows=self.call('a','/social/clan-levels')
        self.assertEqual(rows['clans'][0]['clanId'],second)
        self.assertEqual(rows['selfRank'],2)
        self.ledger.save_profile('b',self.profile('Beta',10000),1)
        rows=self.call('a','/social/clan-levels')
        self.assertEqual(rows['clans'][0]['clanId'],clan)
        expected=self.call('a')['me']['level']+self.call('b')['me']['level']
        self.assertEqual(rows['clans'][0]['totalLevel'],expected)
        restored=self.make_social(self.make_ledger()).dispatch('a','/social/clan-levels',{})
        self.assertEqual(rows,restored)
        self.call('b','/social/clan/leave')
        self.assertEqual(self.call('a')['clan']['totalLevel'],2)

    def test_concurrent_joins_cannot_exceed_clan_cap_or_join_two_clans(self):
        clan=self.create()
        second=self.create('c','Other Clan','OC')
        with patch('social.MAX_MEMBERS',2):
            def attempt(who):
                try: self.call(who,'/social/clan/join',clanId=clan); return True
                except ValueError: return False
            self.call('d')
            with ThreadPoolExecutor(max_workers=2) as pool:
                results=list(pool.map(attempt,['b','d']))
            self.assertEqual(sum(results),1)
            self.assertEqual(self.call('a')['clan']['memberCount'],2)
        self.call('e')
        def join(identifier):
            try: self.call('e','/social/clan/join',clanId=identifier); return True
            except ValueError: return False
        with ThreadPoolExecutor(max_workers=2) as pool:
            self.assertEqual(sum(pool.map(join,[clan,second])),1)

    def test_clan_action_rate_limit_without_friend_routes(self):
        clan = self.create('a')
        # A harmless, repeatable server-authorized clan cancel counts as an action.
        with patch('social.time.time', return_value=100000):
            for _ in range(30):
                self.call('c', '/social/clan/cancel', clanId=clan)
            with self.assertRaisesRegex(ValueError, 'Too many actions'):
                self.call('c', '/social/clan/cancel', clanId=clan)
        with patch('social.time.time', return_value=100060):
            self.call('c', '/social/clan/cancel', clanId=clan)
    def test_input_validation(self):
        for data in ({'offset':True},{'offset':-1},{'offset':[]},{'offset':1000001}):
            with self.assertRaises(ValueError): self.social.dispatch('a','/social/levels',data)
        for identifier in (None, [], 'a', '0'*20):
            with self.assertRaises(ValueError): self.call('a','/social/friend/request',playerId=identifier)
        for data in ({'name':'ab','tag':'AA'}, {'name':'A <clan>','tag':'AA'}, {'name':'Valid Clan','tag':'!x'},
                     {'name':'Valid Clan','tag':'AA','open':1}, {'name':'Valid Clan','tag':'AA','description':'x'*161}):
            with self.assertRaises(ValueError): self.social.dispatch('a','/social/clan/create',data)
        with self.assertRaises(ValueError): self.call('a','/social/unknown')

    def test_new_account_is_not_automatically_in_a_clan(self):
        state=self.call('new-account')
        self.assertIsNone(state['clan'])
        self.assertFalse(state['hasClan'])
        self.assertEqual(state['role'],'')
        self.assertEqual(state['clanCreationCost'],500)
        self.assertFalse(state['chatEnabled'])
        self.assertNotIn('messages',state)

    def test_creation_cost_earned_paid_split_and_durable_retry(self):
        self.ledger.save_profile('a',[{'key':'DF_Shards','kind':0,'number':200}],1)
        self.ledger.fulfill('a','dimensions_shards_500','clan-token',{'purchaseState':0,'obfuscatedExternalAccountId':'a'})
        revision=self.ledger.wallet('a')['profileRevision']
        data={'name':'Solar Guard 0000001','tag':'C0000001','operationId':uuid.uuid4().hex,'expectedProfileRevision':revision,'cost':-1,'account':'b'}
        first=self.social.dispatch('a','/social/clan/create',data)
        self.assertEqual(first['wallet']['balance'],200)
        earned=next(e['number'] for e in first['wallet']['profile'] if e['key']=='DF_Shards')
        self.assertEqual(earned,0)
        second=self.make_social(self.make_ledger()).dispatch('a','/social/clan/create',data)
        self.assertEqual(first['clan']['clanId'],second['clan']['clanId'])
        self.assertEqual(second['wallet']['balance'],200)
        self.assertEqual(first['wallet']['profileRevision'],second['wallet']['profileRevision'])
        repeated=self.social.dispatch('a','/social/clan/create',dict(data,name='Other Clan',tag='CUSTOM'))
        self.assertEqual(repeated['clan']['clanId'],first['clan']['clanId'])
        self.assertEqual(self.ledger.wallet('b')['balance'],0)

    def test_creation_failure_and_profile_conflict_never_debit(self):
        self.ledger.save_profile('a',[{'key':'DF_Shards','kind':0,'number':499}],1)
        data={'name':'Solar Guard 0000001','tag':'C0000001','operationId':uuid.uuid4().hex,'expectedProfileRevision':2}
        before=self.ledger.wallet('a')
        with self.assertRaises(ValueError): self.social.dispatch('a','/social/clan/create',data)
        self.assertEqual(before,self.ledger.wallet('a'))
        self.assertIsNone(self.call('a')['clan'])
        from ledger import ProfileConflict
        with self.assertRaises(ProfileConflict): self.social.dispatch('a','/social/clan/create',dict(data,expectedProfileRevision=1))
        self.assertEqual(before,self.ledger.wallet('a'))

    def test_concurrent_creation_charges_once(self):
        revision=self.ledger.wallet('a')['profileRevision']
        def create(index):
            try:
                self.call('a','/social/clan/create',name=f'Solar Guard {index:07d}',tag=f'C{index:07d}',operationId=uuid.uuid4().hex,expectedProfileRevision=revision)
                return True
            except ValueError: return False
        with ThreadPoolExecutor(max_workers=2) as pool:
            self.assertEqual(sum(pool.map(create,[1,2])),1)
        earned=next(e['number'] for e in self.ledger.wallet('a')['profile'] if e['key']=='DF_Shards')
        self.assertEqual(earned,500)

    def test_leader_icon_ownership_persistence_and_public_listing(self):
        self.ledger.purchase('a','avatar_10',uuid.uuid4().hex,1)
        clan=self.create()
        self.call('b','/social/clan/join',clanId=clan)
        with self.assertRaises(ValueError): self.call('a','/social/clan/icon',avatar=11)
        self.call('a','/social/clan/icon',avatar=0)
        self.assertEqual(self.call('a')['clan']['avatar'],0)
        with self.assertRaises(SocialForbidden): self.call('b','/social/clan/icon',avatar=10)
        self.call('a','/social/clan/icon',avatar=10)
        self.assertEqual(self.call('b')['clan']['avatar'],10)
        self.assertEqual(self.call('c','/social/clan-levels')['clans'][0]['avatar'],10)
        self.assertEqual(self.make_social(self.make_ledger()).dispatch('a','/social/state',{})['clan']['avatar'],10)
        self.call('a','/social/clan/transfer',playerId=self.ids['b'])
        self.assertEqual(self.call('b')['clan']['avatar'],10)
        with self.assertRaises(ValueError): self.call('b','/social/clan/icon',avatar=10)

    def test_clan_icon_starter_founder_reward_and_ownership_guards(self):
        clan=self.create()
        for avatar in range(6):
            self.call('a','/social/clan/icon',avatar=avatar)
            self.assertEqual(self.call('a')['clan']['avatar'],avatar)
        for avatar in (6,16,10,-1,1001,True,'0'):
            with self.subTest(avatar=avatar),self.assertRaises(ValueError):
                self.call('a','/social/clan/icon',avatar=avatar)
        state=self.ledger.wallet('a')
        self.ledger.save_profile('a',state['profile']+[
            {'key':'DF_FoundersPack_Owned','kind':0,'number':1},
            {'key':'DF_AvatarOwned_16','kind':0,'number':1}],state['profileRevision'])
        wallet=self.ledger.wallet('a')
        for avatar in (6,16):
            self.call('a','/social/clan/icon',avatar=avatar)
            self.assertEqual(self.make_social(self.make_ledger()).dispatch('a','/social/state',{})['clan']['avatar'],avatar)
        self.assertEqual(self.ledger.wallet('a'),wallet)
        self.call('b','/social/clan/join',clanId=clan)
        with self.assertRaises(SocialForbidden):self.call('b','/social/clan/icon',avatar=0)

    def test_all_communication_routes_are_disabled_for_every_player(self):
        clan=self.create()
        self.call('b','/social/clan/join',clanId=clan)
        for who in 'abc':
            for route in ('/social/clan/message','/social/clan/message/remove',
                          '/social/clan/message/report','/social/message','/social/message/read'):
                with self.subTest(who=who,route=route), self.assertRaisesRegex(ValueError,'Communication is disabled'):
                    self.call(who,route,text='Hello',messageId=uuid.uuid4().hex,playerId=self.ids['b'])
            state=self.call(who)
            self.assertFalse(state['chatEnabled'])
            self.assertNotIn('messages',state)

    def test_legacy_messages_are_never_returned_or_modified_by_reads(self):
        clan=self.create()
        message_id=uuid.uuid4().hex
        raw=(message_id,clan,'a','Old Name','Legacy private text',1000)
        if hasattr(self.ledger,'connect'):
            with self.ledger.connect() as db:
                db.execute('INSERT INTO social_message VALUES(?,?,?,?,?,?)',raw)
        else:
            ref=self.client.collection(self.ledger.accounts.id.removesuffix('-accounts')+'-social-message').document(message_id)
            self.client.data[ref.path]=dict(zip(('id','clan','account','name','text','created_at'),raw))
        state=self.make_social(self.make_ledger()).dispatch('a','/social/state',{})
        self.assertNotIn('messages',state)
        self.assertNotIn('Legacy private text',json.dumps(state))
        if hasattr(self.ledger,'connect'):
            with self.ledger.connect() as db:
                self.assertEqual(db.execute('SELECT text FROM social_message WHERE id=?',(message_id,)).fetchone()[0],raw[4])
        else:
            self.assertEqual(self.client.data[ref.path]['text'],raw[4])

    def test_clan_identity_is_immutable_after_creation(self):
        clan=self.create()
        self.call('b','/social/clan/join',clanId=clan)
        before=self.call('a')['clan']
        wallet=self.ledger.wallet('a')
        for data in (
            dict(name='Lunar Crew 7654321',tag='C7654321',descriptionId=3),
            dict(name='Custom Clan',tag='CUSTOM',description='https://discord.gg/example'),
        ):
            for who in ('a','b'):
                with self.assertRaisesRegex(ValueError,'disabled'):
                    self.call(who,'/social/clan/identity',**data)
        self.assertEqual(self.call('a')['clan'],before)
        self.assertEqual(self.ledger.wallet('a'),wallet)
    def test_legacy_clan_metadata_migrates_without_charging_or_losing_members(self):
        clan=self.create()
        self.call('b','/social/clan/join',clanId=clan)
        before=self.ledger.wallet('a')
        if hasattr(self.ledger,'connect'):
            with self.ledger.connect() as db:
                db.execute('UPDATE social_clan SET name=?,name_search=?,tag=?,description=? WHERE id=?',
                           ('Legacy Clan','legacy clan','OLD','https://discord.gg/test',clan))
        else:
            ref=self.social.collections['social_clan'].document(clan)
            self.client.data[ref.path].update(name='Legacy Clan',name_search='legacy clan',tag='OLD',description='https://discord.gg/test')
        restored=self.make_social(self.make_ledger()).dispatch('a','/social/state',{})
        self.assertEqual(restored['clan']['clanId'],clan)
        self.assertEqual(restored['clan']['memberCount'],2)
        self.assertEqual(restored['role'],'leader')
        self.assertTrue(valid_clan_name(restored['clan']['name']))
        self.assertTrue(valid_clan_tag(restored['clan']['tag']))
        self.assertEqual(restored['clan']['description'],DESCRIPTIONS[0])
        self.assertNotIn('https://',json.dumps(restored))
        self.assertEqual(self.ledger.wallet('a'),before)
        self.assertEqual(self.call('a')['clan'],restored['clan'])

    def test_clan_names_are_unique_and_cannot_be_renamed(self):
        first=self.create()
        second=self.create('c','Lunar Crew 0000002','C0000002')
        self.assertNotEqual(first,second)
        self.assertNotEqual(self.call('a')['clan']['name'], self.call('c')['clan']['name'])
        with self.assertRaisesRegex(ValueError,'disabled'):
            self.call('a','/social/clan/identity',name='Lunar Crew 0000002',tag='C0000003',descriptionId=0)
        self.assertEqual(self.call('c')['clan']['clanId'],second)
    def test_creation_retry_after_leaving_does_not_create_or_charge_again(self):
        revision=self.ledger.wallet('a')['profileRevision']
        data={'name':'Solar Guard 0000001','tag':'C0000001','operationId':uuid.uuid4().hex,'expectedProfileRevision':revision}
        self.social.dispatch('a','/social/clan/create',data)
        self.call('a','/social/clan/leave')
        before=self.ledger.wallet('a')
        retried=self.make_social(self.make_ledger()).dispatch('a','/social/clan/create',data)
        self.assertIsNone(retried['clan'])
        self.assertEqual(before,self.ledger.wallet('a'))

    def test_legacy_clan_image_migrates_without_changing_membership(self):
        clan=self.create()
        if hasattr(self.ledger,'connect'):
            with self.ledger.connect() as db: db.execute('ALTER TABLE social_clan DROP COLUMN avatar')
        else:
            ref=self.social.collections['social_clan'].document(clan)
            self.client.data[ref.path].pop('avatar')
        restored=self.make_social(self.make_ledger()).dispatch('a','/social/state',{})
        self.assertEqual(restored['clan']['clanId'],clan)
        self.assertEqual(restored['clan']['avatar'],-1)
        self.assertTrue(restored['hasClan'])


class SocialHttpTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory()
        self.app=CommerceApplication(Path(self.tmp.name)/'db',lambda header: header[7:] if header.startswith('Bearer ') else None,None,None,chat_config=ChatConfig(enabled=False))

    def tearDown(self): self.tmp.cleanup()

    def request(self,who,path,data=None,raw=None):
        body=json.dumps(data or {}).encode() if raw is None else raw
        env={'REQUEST_METHOD':'POST','PATH_INFO':path,'CONTENT_LENGTH':str(len(body)),
             'wsgi.input':io.BytesIO(body),'HTTP_AUTHORIZATION':'Bearer '+who if who else ''}
        result={}
        def start(status,headers): result.update(status=status,headers=dict(headers))
        result['body']=json.loads(b''.join(self.app(env,start)))
        return result

    def test_auth_public_response_ignores_spoofed_identity_and_payload_score(self):
        self.assertEqual(self.request('','/social/state')['status'],'401 Unauthorized')
        state=self.request('real','/social/state',{'account':'fake','level':1000,'xp':999999})
        self.assertEqual(state['status'],'200 OK')
        self.assertEqual(state['body']['social']['me']['level'],1)
        self.assertEqual(state['headers']['Cache-Control'],'no-store')
        self.assertNotIn('accountHash',json.dumps(state['body']))

    def test_invalid_payload_unknown_route_and_forbidden_management(self):
        for raw in (b'[]',b'null',b'{',b'"x"'):
            self.assertEqual(self.request('a','/social/state',raw=raw)['status'],'400 Bad Request')
        self.assertEqual(self.request('a','/social/unknown')['status'],'400 Bad Request')
        self.app.ledger.save_profile('a',[{'key':'DF_Shards','kind':0,'number':1000}],0)
        a=self.request('a','/social/clan/create',{'name':'Solar Guard 0000001','tag':'C0000001','operationId':uuid.uuid4().hex,'expectedProfileRevision':1})['body']['social']
        b=self.request('b','/social/clan/join',{'clanId':a['clan']['clanId']})['body']['social']
        c=self.request('c','/social/clan/join',{'clanId':a['clan']['clanId']})['body']['social']
        forbidden=self.request('b','/social/clan/kick',{'playerId':c['me']['playerId'],'account':'a'})
        self.assertEqual(forbidden['status'],'403 Forbidden')
        self.assertEqual(self.request('c','/social/state')['body']['social']['clan']['memberCount'],3)

    def test_disabled_communication_http_error_and_no_history(self):
        for route in ('/social/clan/message','/social/clan/message/remove','/social/message'):
            rejected=self.request('a',route,{'text':'Hello','messageId':uuid.uuid4().hex})
            self.assertEqual(rejected['status'],'400 Bad Request')
            self.assertFalse(rejected['body']['fulfilled'])
            self.assertEqual(rejected['body']['message'],'Communication is disabled in this version.')
        state=self.request('a','/social/state')['body']['social']
        self.assertFalse(state['chatEnabled'])
        self.assertNotIn('messages',state)

    def test_paid_creation_response_contains_only_callers_wallet_and_conflict_is_409(self):
        self.app.ledger.save_profile('a',[{'key':'DF_Shards','kind':0,'number':700}],0)
        data={'name':'Solar Guard 0000001','tag':'C0000001','operationId':uuid.uuid4().hex,'expectedProfileRevision':0,'price':0}
        self.assertEqual(self.request('a','/social/clan/create',data)['status'],'409 Conflict')
        data['expectedProfileRevision']=1
        reply=self.request('a','/social/clan/create',data)
        self.assertEqual(reply['status'],'200 OK')
        wallet=reply['body']['social']['wallet']
        self.assertEqual(wallet['accountHash'],'a')
        self.assertEqual(next(e['number'] for e in wallet['profile'] if e['key']=='DF_Shards'),200)
        self.assertTrue(reply['body']['social']['hasClan'])
        b=self.request('b','/social/state')['body']['social']
        self.assertNotIn('wallet',b)
        self.assertIsNone(b['clan'])


