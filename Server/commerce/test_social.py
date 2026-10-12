"""Social permissions, durability, public rankings and concurrent membership tests."""
import io
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
from chat_filter import HIDDEN_MESSAGE, HIDDEN_NAME, REJECTION_MESSAGE


class SocialTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.path = Path(self.tmp.name) / 'account.db'
        self.ledger = self.make_ledger()
        # Exercise migration of an existing profile before social tables exist.
        self.ledger.save_profile('a', self.profile('Same Name', 102), 0)
        self.social = self.make_social(self.ledger)
        self.ledger.save_profile('b', self.profile('Same Name', 0), 0)
        self.ledger.save_profile('c', self.profile('Charlie', 1000), 0)
        self.ids = {a: self.call(a)['me']['playerId'] for a in 'abc'}

    def make_ledger(self): return Ledger(self.path)

    def make_social(self, ledger): return Social(ledger)

    def tearDown(self):
        self.tmp.cleanup()

    @staticmethod
    def profile(name, xp):
        return [{'key':'DF_PlayerName','kind':1,'text':name}, {'key':'DF_AccountXP','kind':1,'text':str(xp)}, {'key':'DF_Shards','kind':0,'number':1000}]

    def call(self, who, route='/social/state', **data):
        return self.social.dispatch(who, route, data)

    def create(self, who='a', name='Planet Guards', tag='PG', opened=True):
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

    def test_search_duplicate_names_ids_and_no_sql_wildcards(self):
        results = self.call('c', '/social/players', query='same')['players']
        self.assertEqual(len(results), 2)
        self.assertNotEqual(results[0]['playerId'], results[1]['playerId'])
        self.assertEqual(self.call('a', '/social/players', query=self.ids['b'])['players'][0]['playerId'], self.ids['b'])
        self.assertEqual(self.call('a', '/social/players', query="%' OR 1=1--")['players'], [])
        with self.assertRaises(ValueError): self.call('a', '/social/players', query='a')

    def test_friend_request_accept_retry_remove_and_self_rejection(self):
        for _ in range(2): self.call('a','/social/friend/request',playerId=self.ids['b'])
        self.assertEqual(len(self.call('b')['incoming']), 1)
        self.assertEqual(len(self.call('a')['outgoing']), 1)
        with self.assertRaises(ValueError): self.call('a','/social/friend/accept',playerId=self.ids['b'])
        for _ in range(2): self.call('b','/social/friend/accept',playerId=self.ids['a'])
        self.assertEqual(len(self.call('a')['friends']), 1)
        self.assertEqual(len(self.call('b')['friends']), 1)
        self.call('a','/social/friend/remove',playerId=self.ids['b'])
        self.assertEqual(self.call('b')['friends'], [])
        with self.assertRaises(ValueError): self.call('a','/social/friend/request',playerId=self.ids['a'])

    def test_decline_cancel_and_crossed_requests_require_explicit_accept(self):
        self.call('a','/social/friend/request',playerId=self.ids['b'])
        self.call('b','/social/friend/request',playerId=self.ids['a'])
        self.assertEqual(self.call('a')['friends'], [])
        self.call('b','/social/friend/remove',playerId=self.ids['a'])
        self.assertEqual(self.call('a')['outgoing'], [])
        self.call('a','/social/friend/request',playerId=self.ids['b'])
        self.call('a','/social/friend/remove',playerId=self.ids['b'])
        self.assertEqual(self.call('b')['incoming'], [])

    def test_block_removes_relationship_hides_search_and_prevents_requests_both_ways(self):
        self.call('a','/social/friend/request',playerId=self.ids['b'])
        self.call('b','/social/friend/accept',playerId=self.ids['a'])
        self.call('b','/social/block',playerId=self.ids['a'])
        self.assertEqual(self.call('a')['friends'], [])
        self.assertEqual(self.call('a','/social/players',query=self.ids['b'])['players'], [])
        self.assertEqual(self.call('b','/social/players',query=self.ids['a'])['players'], [])
        for who, target in [('a','b'),('b','a')]:
            with self.assertRaises(ValueError): self.call(who,'/social/friend/request',playerId=self.ids[target])
        self.call('b','/social/unblock',playerId=self.ids['a'])
        self.assertEqual(self.call('b')['blocked'], [])
        self.call('a','/social/friend/request',playerId=self.ids['b'])

    def test_levels_order_tie_break_pagination_and_saved_profile_updates(self):
        self.ledger.save_profile('b',self.profile('Beta',103),1)
        rows=self.call('a','/social/levels')
        self.assertEqual([p['playerId'] for p in rows['players']], [self.ids['c'],self.ids['b'],self.ids['a']])
        self.assertEqual(rows['selfRank'],3)
        self.assertEqual(self.call('a','/social/levels',offset=1)['players'][0]['rank'],2)
        self.ledger.save_profile('b',self.profile('Beta',102),2)
        rows=self.call('a','/social/levels')['players']
        self.assertEqual([p['playerId'] for p in rows[1:]], sorted([self.ids['a'],self.ids['b']]))

    def test_clan_create_case_insensitive_uniqueness_and_open_join(self):
        clan=self.create()
        for name,tag in [('planet guards','ZZ'),('Other Clan','pg')]:
            with self.assertRaises(ValueError): self.create('b',name,tag)
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

    def test_pending_and_friend_caps_and_rate_limit(self):
        with patch('social.MAX_REQUESTS',1):
            self.call('a','/social/friend/request',playerId=self.ids['b'])
            with self.assertRaises(ValueError): self.call('c','/social/friend/request',playerId=self.ids['b'])
        self.call('b','/social/friend/accept',playerId=self.ids['a'])
        with patch('social.MAX_FRIENDS',1):
            with self.assertRaises(ValueError): self.call('c','/social/friend/request',playerId=self.ids['b'])
        with patch('social.time.time',return_value=100000):
            for _ in range(30): self.call('c','/social/unblock',playerId=self.ids['a'])
            with self.assertRaisesRegex(ValueError,'Too many actions'): self.call('c','/social/unblock',playerId=self.ids['a'])
        with patch('social.time.time',return_value=100060):
            self.call('c','/social/unblock',playerId=self.ids['a'])

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
        self.assertEqual(state['messages'],[])

    def test_creation_cost_earned_paid_split_and_durable_retry(self):
        self.ledger.save_profile('a',[{'key':'DF_Shards','kind':0,'number':200}],1)
        self.ledger.fulfill('a','dimensions_shards_500','clan-token',{'purchaseState':0,'obfuscatedExternalAccountId':'a'})
        revision=self.ledger.wallet('a')['profileRevision']
        data={'name':'Paid Clan','tag':'PAY','operationId':uuid.uuid4().hex,'expectedProfileRevision':revision,'cost':-1,'account':'b'}
        first=self.social.dispatch('a','/social/clan/create',data)
        self.assertEqual(first['wallet']['balance'],200)
        earned=next(e['number'] for e in first['wallet']['profile'] if e['key']=='DF_Shards')
        self.assertEqual(earned,0)
        second=self.make_social(self.make_ledger()).dispatch('a','/social/clan/create',data)
        self.assertEqual(first['clan']['clanId'],second['clan']['clanId'])
        self.assertEqual(second['wallet']['balance'],200)
        self.assertEqual(first['wallet']['profileRevision'],second['wallet']['profileRevision'])
        with self.assertRaises(ValueError): self.social.dispatch('a','/social/clan/create',dict(data,name='Other Clan'))
        self.assertEqual(self.ledger.wallet('b')['balance'],0)

    def test_creation_failure_and_profile_conflict_never_debit(self):
        self.ledger.save_profile('a',[{'key':'DF_Shards','kind':0,'number':499}],1)
        data={'name':'Poor Clan','tag':'POOR','operationId':uuid.uuid4().hex,'expectedProfileRevision':2}
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
                self.call('a','/social/clan/create',name='Clan '+str(index),tag='T'+str(index),operationId=uuid.uuid4().hex,expectedProfileRevision=revision)
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
        with self.assertRaises(ValueError): self.call('a','/social/clan/icon',avatar=0)
        with self.assertRaises(SocialForbidden): self.call('b','/social/clan/icon',avatar=10)
        self.call('a','/social/clan/icon',avatar=10)
        self.assertEqual(self.call('b')['clan']['avatar'],10)
        self.assertEqual(self.call('c','/social/clan-levels')['clans'][0]['avatar'],10)
        self.assertEqual(self.make_social(self.make_ledger()).dispatch('a','/social/state',{})['clan']['avatar'],10)
        self.call('a','/social/clan/transfer',playerId=self.ids['b'])
        self.assertEqual(self.call('b')['clan']['avatar'],10)
        with self.assertRaises(ValueError): self.call('b','/social/clan/icon',avatar=10)

    def test_chat_membership_retry_author_permissions_and_cleanup(self):
        clan=self.create()
        self.call('b','/social/clan/join',clanId=clan)
        message_id=uuid.uuid4().hex
        for _ in range(2): self.call('a','/social/clan/message',messageId=message_id,text='Welcome to the clan!')
        self.assertEqual(len(self.call('b')['messages']),1)
        self.assertEqual(self.call('c')['messages'],[])
        with self.assertRaises(ValueError): self.call('c','/social/clan/message',messageId=uuid.uuid4().hex,text='Outsider')
        with self.assertRaises(ValueError): self.call('a','/social/clan/message',messageId=message_id,text='Changed text')
        with self.assertRaises(SocialForbidden): self.call('b','/social/clan/message/remove',messageId=message_id)
        own=uuid.uuid4().hex
        self.call('b','/social/clan/message',messageId=own,text='Hi leader')
        self.call('b','/social/clan/message/remove',messageId=own)
        self.assertEqual(len(self.call('a')['messages']),1)
        own=uuid.uuid4().hex
        self.call('b','/social/clan/message',messageId=own,text='Second message')
        self.call('a','/social/clan/message/remove',messageId=own)
        restored=self.make_social(self.make_ledger()).dispatch('b','/social/state',{})
        self.assertEqual(restored['messages'][0]['text'],'Welcome to the clan!')
        self.call('a','/social/clan/kick',playerId=self.ids['b'])
        self.assertEqual(self.call('b')['messages'],[])
        with self.assertRaises(ValueError): self.call('b','/social/clan/message',messageId=uuid.uuid4().hex,text='No longer a member')
        self.call('a','/social/clan/leave')
        self.assertEqual(self.call('a')['messages'],[])

    def test_chat_validation_retention_and_order(self):
        self.create()
        for text in ('','  ','x'*241,'control\x00text'):
            with self.assertRaises(ValueError): self.call('a','/social/clan/message',messageId=uuid.uuid4().hex,text=text)
        with patch('social.MAX_MESSAGES',3),patch('social.time.time',return_value=1000):
            for index in range(4): self.call('a','/social/clan/message',messageId=uuid.uuid4().hex,text='Message '+str(index))
            rows=self.call('a')['messages']
            self.assertEqual([m['text'] for m in rows],['Message 1','Message 2','Message 3'])
            self.assertEqual([m['createdAt'] for m in rows],sorted(set(m['createdAt'] for m in rows)))

    def test_chat_filter_rejects_before_storage_and_allows_corrected_retry(self):
        clan = self.create()
        self.call('b', '/social/clan/join', clanId=clan)
        message_id = uuid.uuid4().hex
        for text in ('Du bist ein 1d10t', 'send nudes', 'heil hitler',
                     'kill yourself', 'f.u.c.k'):
            with self.subTest(text=text), self.assertRaisesRegex(ValueError, '^Message blocked:'):
                self.call('a', '/social/clan/message', messageId=message_id,
                          text=text, filterEnabled=False, moderated=True)
            self.assertEqual(self.call('b')['messages'], [])
        for _ in range(2):
            state = self.call('a', '/social/clan/message', messageId=message_id,
                              text='GG, well played!')
        self.assertEqual(len(state['messages']), 1)
        self.assertEqual(state['messages'][0]['text'], 'GG, well played!')

    def test_chat_filter_hides_retained_abuse_and_sender_without_deleting_history(self):
        clan = self.create()
        self.call('b', '/social/clan/join', clanId=clan)
        message_id = uuid.uuid4().hex
        raw = (message_id, clan, 'a', 'Hurensohn', 'send nudes', 1000)
        if hasattr(self.ledger, 'connect'):
            with self.ledger.connect() as db:
                db.execute('INSERT INTO social_message VALUES(?,?,?,?,?,?)', raw)
        else:
            ref = self.social.collections['social_message'].document(message_id)
            self.client.data[ref.path] = dict(zip(
                ('id', 'clan', 'account', 'name', 'text', 'created_at'), raw))
        restored = self.make_social(self.make_ledger()).dispatch('b', '/social/state', {})
        message = restored['messages'][0]
        self.assertEqual(message['text'], HIDDEN_MESSAGE)
        self.assertEqual(message['name'], HIDDEN_NAME)
        self.assertEqual(message['messageId'], message_id)
        self.assertEqual(message['createdAt'], 1000)
        if hasattr(self.ledger, 'connect'):
            with self.ledger.connect() as db:
                self.assertEqual(db.execute('SELECT text FROM social_message WHERE id=?',
                                            (message_id,)).fetchone()[0], 'send nudes')
        else:
            self.assertEqual(self.client.data[ref.path]['text'], 'send nudes')
        self.call('a', '/social/clan/message/remove', messageId=message_id)
        self.assertEqual(self.call('b')['messages'], [])

    def test_creation_retry_after_leaving_does_not_create_or_charge_again(self):
        revision=self.ledger.wallet('a')['profileRevision']
        data={'name':'Once Only','tag':'ONCE','operationId':uuid.uuid4().hex,'expectedProfileRevision':revision}
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
        self.app=CommerceApplication(Path(self.tmp.name)/'db',lambda header: header[7:] if header.startswith('Bearer ') else None,None,None)

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
        a=self.request('a','/social/clan/create',{'name':'Planet Guards','tag':'PG','operationId':uuid.uuid4().hex,'expectedProfileRevision':1})['body']['social']
        b=self.request('b','/social/clan/join',{'clanId':a['clan']['clanId']})['body']['social']
        c=self.request('c','/social/clan/join',{'clanId':a['clan']['clanId']})['body']['social']
        forbidden=self.request('b','/social/clan/kick',{'playerId':c['me']['playerId'],'account':'a'})
        self.assertEqual(forbidden['status'],'403 Forbidden')
        self.assertEqual(self.request('c','/social/state')['body']['social']['clan']['memberCount'],3)

    def test_chat_filter_http_error_and_no_message_written(self):
        self.app.ledger.save_profile('a', [{'key':'DF_Shards','kind':0,'number':1000}], 0)
        self.request('a', '/social/clan/create', {
            'name':'HTTP Clan', 'tag':'HTTP', 'operationId':uuid.uuid4().hex,
            'expectedProfileRevision':1})
        message_id = uuid.uuid4().hex
        rejected = self.request('a', '/social/clan/message', {
            'messageId':message_id, 'text':'send nudes', 'filterEnabled':False})
        self.assertEqual(rejected['status'], '400 Bad Request')
        self.assertFalse(rejected['body']['fulfilled'])
        self.assertEqual(rejected['body']['message'], REJECTION_MESSAGE)
        self.assertNotIn('send nudes', json.dumps(rejected['body']))
        self.assertEqual(self.request('a', '/social/state')['body']['social']['messages'], [])
        accepted = self.request('a', '/social/clan/message', {
            'messageId':message_id, 'text':'GG, well played!'})
        self.assertEqual(accepted['status'], '200 OK')
        self.assertEqual(len(accepted['body']['social']['messages']), 1)

    def test_paid_creation_response_contains_only_callers_wallet_and_conflict_is_409(self):
        self.app.ledger.save_profile('a',[{'key':'DF_Shards','kind':0,'number':700}],0)
        data={'name':'HTTP Clan','tag':'HTTP','operationId':uuid.uuid4().hex,'expectedProfileRevision':0,'price':0}
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
