"""Execute the patched Node account handler against transactional Firestore substitutes."""
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import unittest

ROOT=Path(__file__).resolve().parents[2]

class FriendGifts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.worker=subprocess.Popen(['node',str(ROOT/'Tools/ClanRestore/standalone_rpc.mjs')],env=dict(os.environ,DF_TEST_FRIEND_GIFTS='1'),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
    @classmethod
    def tearDownClass(cls):
        cls.worker.stdin.close();cls.worker.wait(timeout=10)
        error=cls.worker.stderr.read()
        if cls.worker.returncode:raise RuntimeError(error)
        cls.worker.stdout.close();cls.worker.stderr.close()
    def setUp(self):
        self.a=hashlib.sha256(b'alice').hexdigest();self.b=hashlib.sha256(b'bob').hexdigest();self.c=hashlib.sha256(b'carol').hexdigest()
        self.state={}
        for i,account in enumerate([self.a,self.b,self.c]):
            profile=[dict(key='DF_PlayerName',kind=1,text=f'SolarFox{i+1:07d}'),dict(key='DF_Shards',kind=0,number=1000)]
            self.state['players/'+account]=dict(balance=2000,revision=3,profileRevision=4,profile=profile,owned=['avatar_10'],futureMetadata='preserved')
            self.state['dimensions-front-social-player/'+account]=dict(account=account,player_id=str(i+1)*20,name=f'SolarFox{i+1:07d}',name_search=f'solarfox{i+1:07d}',level=1,xp=0,avatar=0)
    def call(self,account,route,body=None,**kwargs):
        value=dict(data=self.state,account=account,route=route,body=body or {},**kwargs)
        self.worker.stdin.write(json.dumps(value)+'\n');self.worker.stdin.flush()
        line=self.worker.stdout.readline()
        if not line:raise RuntimeError('Node worker stopped: '+self.worker.stderr.read())
        result=json.loads(line);self.state=result['data'];return result
    def good(self,account,route,body=None,**kwargs):
        value=self.call(account,route,body,**kwargs)
        self.assertTrue(value['ok'],value.get('stack',value));return value['result']
    def wallet(self,account):return self.state['players/'+account]
    def earned(self,account):return next(v['number'] for v in self.wallet(account)['profile'] if v['key']=='DF_Shards')
    def request_and_accept(self):
        self.good(self.a,'/social/friend/request',dict(query='SolarFox0000002'))
        self.good(self.b,'/social/friend/accept',dict(playerId='1'*20))
    def send(self,sticker=1,operation='a'*32,**kwargs):
        return self.good(self.a,'/social/sticker/send',dict(playerId='2'*20,stickerId=sticker,operationId=operation,expectedProfileRevision=self.wallet(self.a)['profileRevision'],**kwargs))
    def test_real_friend_requests_both_accounts(self):
        state=self.good(self.a,'/social/friend/request',dict(query='SolarFox0000002'))
        self.assertEqual([v['playerId'] for v in state['outgoing']],['2'*20])
        self.assertEqual(state['friends'],[])
        denied=self.call(self.a,'/social/friend/accept',dict(playerId='2'*20))
        self.assertFalse(denied['ok']);self.assertEqual(denied['status'],400)
        accepted=self.good(self.b,'/social/friend/accept',dict(playerId='1'*20))
        self.assertEqual([v['playerId'] for v in accepted['friends']],['1'*20])
        self.assertEqual([v['playerId'] for v in self.good(self.a,'/social/friend/state')['friends']],['2'*20])
    def test_delivery_debit_recipient_inbox_and_repeated_claim(self):
        self.request_and_accept();before=copy.deepcopy(self.wallet(self.b))
        sent=self.send();self.assertEqual(self.earned(self.a),900)
        self.assertEqual(sent['receivedStickers'],[])
        self.assertEqual(self.wallet(self.b),before)
        inbox=self.good(self.b,'/social/friend/state')['receivedStickers'];self.assertEqual(len(inbox),1)
        gift=inbox[0]['giftId'];self.assertEqual(inbox[0]['senderName'],'SolarFox0000001')
        first=self.good(self.b,'/social/sticker/claim',dict(giftId=gift,expectedProfileRevision=4))
        self.assertEqual(self.earned(self.b),1020);self.assertEqual(first['receivedStickers'],[])
        self.good(self.b,'/social/sticker/claim',dict(giftId=gift,expectedProfileRevision=4))
        self.assertEqual(self.earned(self.b),1020);self.assertEqual(self.wallet(self.b)['profileRevision'],5)
        self.assertEqual(self.wallet(self.b)['futureMetadata'],'preserved')
    def test_lost_reply_retry_cannot_debit_or_deliver_twice(self):
        self.request_and_accept();self.send();before=copy.deepcopy(self.wallet(self.a))
        self.good(self.a,'/social/sticker/send',dict(playerId='2'*20,stickerId=1,operationId='a'*32,expectedProfileRevision=4))
        self.assertEqual(self.wallet(self.a),before)
        self.assertEqual(len(self.good(self.b,'/social/friend/state')['receivedStickers']),1)
        denied=self.call(self.a,'/social/sticker/send',dict(playerId='2'*20,stickerId=2,operationId='a'*32,expectedProfileRevision=4))
        self.assertFalse(denied['ok']);self.assertEqual(self.wallet(self.a),before)
    def test_all_six_tiers_and_paid_balance_split(self):
        self.request_and_accept()
        for i,price in enumerate([50,100,250,500,1000,2500]):
            value=self.wallet(self.a);value['profile'][next(i for i,v in enumerate(value['profile']) if v['key']=='DF_Shards')]['number']=10;value['balance']=3000
            self.send(i,operation=f'{i+1:032x}')
            self.assertEqual(self.earned(self.a),0);self.assertEqual(self.wallet(self.a)['balance'],3010-price)
            inbox=self.good(self.b,'/social/friend/state')['receivedStickers'];gift=inbox[0]['giftId']
            previous=self.earned(self.b)
            self.good(self.b,'/social/sticker/claim',dict(giftId=gift,expectedProfileRevision=self.wallet(self.b)['profileRevision']))
            self.assertEqual(self.earned(self.b),previous+price//5)
    def test_nonfriend_self_forged_id_and_claim_isolation(self):
        for target in ['2'*20,'1'*20,'plr_fake']:
            before=copy.deepcopy(self.wallet(self.a))
            self.assertFalse(self.call(self.a,'/social/sticker/send',dict(playerId=target,stickerId=1,operationId='a'*32,expectedProfileRevision=4))['ok'])
            self.assertEqual(self.wallet(self.a),before)
        self.request_and_accept();self.send();gift=self.good(self.b,'/social/friend/state')['receivedStickers'][0]['giftId']
        for account in [self.a,self.c]:self.assertFalse(self.call(account,'/social/sticker/claim',dict(giftId=gift,expectedProfileRevision=4))['ok'])
        self.assertEqual(self.earned(self.b),1000)
    def test_stale_revision_insufficient_balance_and_invalid_tier(self):
        self.request_and_accept()
        for changes in [dict(expectedProfileRevision=3),dict(stickerId=-1),dict(stickerId=True),dict(stickerId=6),dict(operationId='bad')]:
            body=dict(playerId='2'*20,stickerId=1,operationId='a'*32,expectedProfileRevision=4);body.update(changes)
            before=copy.deepcopy(self.wallet(self.a));self.assertFalse(self.call(self.a,'/social/sticker/send',body)['ok']);self.assertEqual(self.wallet(self.a),before)
        self.wallet(self.a)['balance']=0
        for entry in self.wallet(self.a)['profile']:
            if entry['key']=='DF_Shards':entry['number']=10
        before=copy.deepcopy(self.wallet(self.a));self.assertFalse(self.call(self.a,'/social/sticker/send',dict(playerId='2'*20,stickerId=1,operationId='a'*32,expectedProfileRevision=4))['ok']);self.assertEqual(self.wallet(self.a),before)
    def test_atomic_concurrent_claim(self):
        self.request_and_accept();self.send();gift=self.good(self.b,'/social/friend/state')['receivedStickers'][0]['giftId']
        result=self.good(self.b,'',mode='parallel',requests=[dict(account=self.b,route='/social/sticker/claim',body=dict(giftId=gift,expectedProfileRevision=4)) for _ in range(2)])
        self.assertTrue(all(item['ok'] for item in result),result);self.assertEqual(self.earned(self.b),1020)
    def test_atomic_concurrent_spend(self):
        self.request_and_accept();self.wallet(self.a)['balance']=0
        for entry in self.wallet(self.a)['profile']:
            if entry['key']=='DF_Shards':entry['number']=50
        result=self.good(self.a,'',mode='parallel',requests=[dict(account=self.a,route='/social/sticker/send',body=dict(playerId='2'*20,stickerId=0,operationId=f'{i:032x}',expectedProfileRevision=4)) for i in [1,2]])
        self.assertEqual(sum(item['ok'] for item in result),1,result);self.assertEqual(self.earned(self.a),0)
        self.assertEqual(len(self.good(self.b,'/social/friend/state')['receivedStickers']),1)
    def test_http_authentication_and_no_private_account_ids(self):
        denied=self.good(self.a,'/social/friend/state',mode='http',token=None)
        self.assertEqual(denied['statusCode'],401)
        self.request_and_accept()
        response=self.good(self.a,'/social/friend/state',mode='http',token='alice')
        self.assertEqual(response['statusCode'],200);self.assertEqual(response['headers']['Cache-Control'],'no-store')
        body=json.dumps(response['body']);self.assertNotIn(self.b,body);self.assertNotIn('futureMetadata',body)
    def test_failure_before_commit_cannot_leave_a_partial_debit(self):
        self.request_and_accept();before=copy.deepcopy(self.wallet(self.a))
        reply=self.call(self.a,'/social/sticker/send',dict(playerId='2'*20,stickerId=1,operationId='a'*32,expectedProfileRevision=4),failRead='players/'+self.b)
        self.assertFalse(reply['ok']);self.assertEqual(self.wallet(self.a),before)
        self.assertEqual(self.good(self.b,'/social/friend/state')['receivedStickers'],[])
    def test_block_and_account_suspension_prevent_delivery(self):
        self.request_and_accept()
        pair=json.dumps([self.b,self.a],separators=(',',':'))
        key=hashlib.sha256(pair.encode()).hexdigest()
        path='dimensions-front-social-block/'+key
        self.state[path]=dict(account=self.b,target=self.a)
        before=copy.deepcopy(self.wallet(self.a))
        denied=self.call(self.a,'/social/sticker/send',dict(playerId='2'*20,stickerId=1,operationId='a'*32,expectedProfileRevision=4))
        self.assertFalse(denied['ok']);self.assertEqual(denied['status'],403);self.assertEqual(self.wallet(self.a),before)
        del self.state[path]
        self.state['dimensions-front-chat-user-ban/'+self.b]=dict(until_at=-1)
        self.assertFalse(self.call(self.a,'/social/sticker/send',dict(playerId='2'*20,stickerId=1,operationId='a'*32,expectedProfileRevision=4))['ok'])
        self.assertEqual(self.wallet(self.a),before)
    def test_delivered_gift_and_retry_survive_friend_removal(self):
        self.request_and_accept();self.send()
        gift=self.good(self.b,'/social/friend/state')['receivedStickers'][0]['giftId']
        self.good(self.a,'/social/friend/remove',dict(playerId='2'*20))
        self.good(self.a,'/social/sticker/send',dict(playerId='2'*20,stickerId=1,operationId='a'*32,expectedProfileRevision=4))
        self.assertEqual(self.earned(self.a),900)
        self.good(self.b,'/social/sticker/claim',dict(giftId=gift,expectedProfileRevision=4))
        self.assertEqual(self.earned(self.b),1020)
    def test_full_inbox_and_balance_overflow_preserve_money(self):
        self.request_and_accept()
        for i in range(50):self.state[f'players/{self.b}/sticker-gifts/{i:064x}']=dict(recipient=self.b,senderName='Sender',senderHero=0,stickerId=1,createdAt=i,claimed=False)
        before=copy.deepcopy(self.wallet(self.a))
        denied=self.call(self.a,'/social/sticker/send',dict(playerId='2'*20,stickerId=1,operationId='a'*32,expectedProfileRevision=4))
        self.assertFalse(denied['ok']);self.assertEqual(self.wallet(self.a),before)
        for entry in self.wallet(self.b)['profile']:
            if entry['key']=='DF_Shards':entry['number']=2147483647
        denied=self.call(self.b,'/social/sticker/claim',dict(giftId=f'{0:064x}',expectedProfileRevision=4))
        self.assertFalse(denied['ok']);self.assertEqual(self.earned(self.b),2147483647)
        self.assertFalse(self.state[f'players/{self.b}/sticker-gifts/{0:064x}']['claimed'])

    def invite(self, **changes):
        body=dict(playerId='2'*20,partyCode='ABCD12',partySessionId='party_session_123')
        body.update(changes)
        return self.call(self.a,'/social/party/invite',body)
    def test_party_invite_requires_accepted_friend_and_keeps_wallets(self):
        self.assertFalse(self.invite()['ok'])
        self.request_and_accept();before=copy.deepcopy([self.wallet(self.a),self.wallet(self.b)])
        self.assertTrue(self.invite()['ok'])
        invites=self.good(self.b,'/social/friend/state')['partyInvites']
        self.assertEqual(len(invites),1);self.assertEqual(invites[0]['partyCode'],'ABCD12')
        self.assertNotIn(self.a,json.dumps(invites));self.assertNotIn(self.b,json.dumps(invites))
        self.assertEqual([self.wallet(self.a),self.wallet(self.b)],before)
    def test_party_invitation_decline_is_private_and_repeated_send_is_bounded(self):
        self.request_and_accept()
        self.assertTrue(self.invite()['ok']);self.assertTrue(self.invite()['ok'])
        invites=self.good(self.b,'/social/friend/state')['partyInvites'];self.assertEqual(len(invites),1)
        gift=invites[0]['inviteId']
        self.good(self.c,'/social/party/decline',dict(inviteId=gift))
        self.assertEqual(len(self.good(self.b,'/social/friend/state')['partyInvites']),1)
        self.good(self.b,'/social/party/decline',dict(inviteId=gift))
        self.assertEqual(self.good(self.b,'/social/friend/state')['partyInvites'],[])
    def test_party_invite_rate_limit_and_expiration(self):
        self.request_and_accept()
        for i in range(10):self.assertTrue(self.invite(partySessionId=f'party_session_{i:08d}')['ok'])
        self.assertFalse(self.invite(partySessionId='another_session_123')['ok'])
        for key,value in self.state.items():
            if '/party-invites/' in key:value['expiresAt']=0
        self.assertEqual(self.good(self.b,'/social/friend/state')['partyInvites'],[])
        del self.state['dimensions-front-party-rate/'+self.a]
        self.assertTrue(self.invite()['ok'])
        self.assertEqual(len([key for key in self.state if '/party-invites/' in key]),1)
    def test_party_invites_validate_inputs_and_account_suspension(self):
        self.request_and_accept()
        for changes in [dict(partyCode='../bad'),dict(partyCode='abc'),dict(partySessionId='bad'),dict(playerId='not-real')]:
            self.assertFalse(self.invite(**changes)['ok'])
        self.state['dimensions-front-chat-user-ban/'+self.b]=dict(until_at=-1)
        self.assertFalse(self.invite()['ok'])

if __name__=='__main__':unittest.main()
