"""Run the existing Python clan/chat scenarios against the standalone Node implementation.

The legacy test ledger is used only to seed profiles; wire adaptation aliases its
account collection to Node's players collection. No production SDK/auth is used.
"""
import atexit
import copy
import hashlib
from datetime import datetime
import json
from pathlib import Path
import subprocess
import threading
import time
import unittest
from unittest.mock import patch

from clan_chat import ChatConfig,ClanChat
from firestore_ledger import FirestoreLedger
from firestore_social import FirestoreSocial
from firestore_social import key_for
from ledger import ProfileConflict
from social import SocialForbidden
import social
from test_firestore_social import Client,transactional
from test_social import SocialTests
from test_clan_chat import FirestoreClanChatTests
from safe_identity import DESCRIPTIONS

ROOT=Path(__file__).resolve().parents[2]
_lock=threading.RLock()
_worker=None

def encode(value):
    if isinstance(value,datetime):return {'$df_time':value.isoformat()}
    raise TypeError(type(value).__name__)

def decode(value):
    if len(value)==1 and '$df_time' in value:return datetime.fromisoformat(value['$df_time'].replace('Z','+00:00'))
    return value

def rpc(request):
    global _worker
    with _lock:
        if _worker is None:
            _worker=subprocess.Popen(['node',str(ROOT/'Tools/ClanRestore/standalone_rpc.mjs')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
            atexit.register(_worker.terminate)
        _worker.stdin.write(json.dumps(request,default=encode)+'\n');_worker.stdin.flush()
        line=_worker.stdout.readline()
        if not line:raise RuntimeError('Standalone test worker exited: '+_worker.stderr.read())
        return json.loads(line,object_hook=decode)

class StandaloneSocial:
    def __init__(self,ledger,config):
        self.ledger=ledger;self.client=ledger.client;self.chat_config=config
        reference=FirestoreSocial(ledger,chat_config=config)
        self.collections=reference.collections;self.unique=reference.unique
    def dispatch(self,account,route,data):
        return self.request(account,route,data)
    def request(self,account,route='',body=None,mode='dispatch'):
        with self.client.lock:
            if mode=='dispatch':self.ledger.wallet(account)
            prefix=self.ledger.accounts.path+'/'
            state={('players/'+k[len(prefix):] if k.startswith(prefix) else k):v for k,v in self.client.data.items()}
            config=dict(enabled=self.chat_config.enabled,admins=list(self.chat_config.admins),messageDays=self.chat_config.message_days,reportDays=self.chat_config.report_days,words=list(self.chat_config.words),adminEmail=self.chat_config.admin_email,maxMembers=social.MAX_MEMBERS)
            reply=rpc(dict(data=state,account=account,route=route,body=body or {},mode=mode,config=config,now=ClanChat.now()))
            if not reply['ok']:
                if reply['status']==403:raise SocialForbidden(reply['message'])
                if reply['status']==409:raise ProfileConflict()
                if reply['status']==400:raise ValueError(reply['message'])
                raise RuntimeError(reply['stack'])
            self.client.data={((prefix+k[len('players/'):] if k.startswith('players/') else k)):v for k,v in reply['data'].items()}
            self.last_reads=reply['reads'];return reply['result']
    def check_account_allowed(self,account):self.request(account,mode='access')

class StandaloneSocialTests(SocialTests):
    def setUp(self):
        self.client=Client();self.decorator=patch('google.cloud.firestore.transactional',transactional)
        self.decorator.start();self.addCleanup(self.decorator.stop);super().setUp()
    def make_ledger(self):
        ledger=FirestoreLedger(client=self.client)
        save=ledger.save_profile
        def save_profile(account,entries,revision):
            result=save(account,entries,revision);FirestoreSocial(ledger).sync_wallet(account);return result
        ledger.save_profile=save_profile
        return ledger
    def make_social(self,ledger):return StandaloneSocial(ledger,ChatConfig(enabled=False))

class StandaloneChatTests(FirestoreClanChatTests):
    def make_social(self):return StandaloneSocial(self.ledger,self.config)

class StandaloneHttpTests(unittest.TestCase):
    def setUp(self):
        self.a=hashlib.sha256(b'alice').hexdigest();self.b=hashlib.sha256(b'bob').hexdigest();self.c=hashlib.sha256(b'carol').hexdigest()
        self.old='f'*20;self.state={}
        for account in (self.a,self.b,self.c):
            profile=[dict(key='DF_PlayerName',kind=1,text='SolarFox0000001'),dict(key='DF_AccountXP',kind=1,text='0'),dict(key='DF_Shards',kind=0,number=200)]
            self.state['players/'+account]=dict(balance=900,revision=1,profileRevision=2,profile=profile,owned=[],futureNodeMetadata='retained')
        self.state['dimensions-front-accounts/'+self.a]=dict(balance=9999,profileRevision=99)
        self.state['purchases/old-receipt']=dict(accountHash=self.a,credited=500)
        for account,player in ((self.a,'a'*20),(self.b,'b'*20)):
            self.state['dimensions-front-social-player/'+account]=dict(account=account,player_id=player,name='SolarFox0000001',name_search='solarfox0000001',xp=0,level=1,avatar=0)
            self.state['dimensions-front-social-member/'+account]=dict(account=account,clan=self.old,role='member' if account==self.a else 'leader')
        self.state['dimensions-front-social-clan/'+self.old]=dict(id=self.old,name='Solar Guard 0000001',name_search='solar guard 0000001',tag='C0000001',description=DESCRIPTIONS[0],open=1,avatar=-1)
        self.state['dimensions-front-social-message/'+'d'*20]=dict(id='d'*20,clan=self.old,account=self.b,name='SolarFox0000001',text='Existing clan message',created_at=int(time.time()*1000))
        consent=dict(account=self.a,kind='chat',version='1',accepted_at=1,age_band='18+')
        self.state['dimensions-front-chat-consent/'+key_for('chat_consent',consent)]=consent
    def request(self,path,body=None,token='alice',method='POST'):
        reply=rpc(dict(mode='http',data=self.state,route=path,body=body or {},token=token,method=method,failRead=getattr(self,'fail_read',None)))
        self.assertTrue(reply['ok'],reply.get('stack'));self.state=reply['data']
        result=reply['result'];self.assertEqual(result['sends'],1)
        self.assertEqual(result['headers'].get('Cache-Control'),'no-store' if path!='/' else None)
        return result['statusCode'],result.get('body')
    def create(self,**extra):
        self.assertEqual(self.request('/social/clan/leave')[0],200)
        body=dict(operationId='1'*32,expectedProfileRevision=2,descriptionId=0,open=True,accepted=True,consentVersion='1');body.update(extra)
        return self.request('/social/clan/create',body)
    def test_existing_clan_id_membership_chat_and_rank_survive_without_wallet_changes(self):
        before=copy.deepcopy(self.state['players/'+self.a])
        status,result=self.request('/social/state');self.assertEqual(status,200)
        self.assertEqual(result['social']['clan']['clanId'],self.old);self.assertEqual(result['social']['me']['playerId'],'a'*20)
        self.assertEqual(result['social']['clan']['memberCount'],2)
        status,result=self.request('/social/chat/read',dict(clanId=self.old))
        self.assertEqual(status,200);self.assertEqual(result['social']['messages'][0]['text'],'Existing clan message')
        status,result=self.request('/social/clan-levels');self.assertEqual(status,200)
        self.assertEqual(result['social']['selfRank'],1);self.assertEqual(result['social']['clans'][0]['totalLevel'],2)
        self.assertEqual(self.state['players/'+self.a],before)
    def test_create_consent_exact_debit_retry_shop_google_and_profile_share_original_node_account(self):
        legacy=copy.deepcopy(self.state['dimensions-front-accounts/'+self.a]);receipt=copy.deepcopy(self.state['purchases/old-receipt'])
        status,result=self.create();self.assertEqual(status,200)
        social=result['social'];self.assertEqual(social['wallet']['balance'],600);self.assertTrue(social['wallet']['billingReady']);self.assertEqual(social['wallet']['profileRevision'],3)
        self.assertEqual(next(e['number'] for e in social['wallet']['profile'] if e['key']=='DF_Shards'),0)
        body=dict(operationId='1'*32,expectedProfileRevision=2,descriptionId=0,open=True,accepted=True,consentVersion='1')
        status,replay=self.request('/social/clan/create',body);self.assertEqual(status,200)
        self.assertEqual(replay['social']['clan']['clanId'],social['clan']['clanId']);self.assertEqual(replay['social']['wallet']['balance'],600)
        buy=dict(cosmeticId='avatar_10',operationId='2'*32,expectedProfileRevision=3)
        status,bought=self.request('/wallet/spend',buy);self.assertEqual(status,200);self.assertEqual(bought['wallet']['balance'],150)
        self.assertEqual(self.request('/wallet/spend',buy)[1]['wallet']['balance'],150)
        self.assertEqual(self.request('/social/clan/icon',dict(avatar=10))[1]['social']['clan']['avatar'],10)
        purchase=dict(productId='dimensions_shards_500',purchaseToken='new-google-receipt')
        self.assertEqual(self.request('/google-play/fulfill',purchase)[1]['wallet']['balance'],650)
        self.assertEqual(self.request('/google-play/fulfill',purchase)[1]['wallet']['balance'],650)
        saved=self.state['players/'+self.a]
        profile=[e for e in saved['profile'] if e['key']!='DF_AccountXP']+[dict(key='DF_AccountXP',kind=1,text='1000')]
        status,_=self.request('/profile/save',dict(profile=profile,expectedProfileRevision=saved['profileRevision']));self.assertEqual(status,200)
        self.assertGreater(self.request('/social/state')[1]['social']['me']['level'],1)
        self.assertEqual(self.state['dimensions-front-accounts/'+self.a],legacy);self.assertEqual(self.state['purchases/old-receipt'],receipt)
        self.assertEqual(self.state['players/'+self.a]['futureNodeMetadata'],'retained')
    def test_moderator_consent_cannot_be_invented_and_invalid_auth_never_reaches_data(self):
        before=copy.deepcopy(self.state)
        for token in (None,'wrong'):
            self.assertEqual(self.request('/social/clan/create',dict(accepted=True),token=token)[0],401)
            self.assertEqual(self.state,before)
        status,_=self.create(accepted=False);self.assertEqual(status,400)
        self.assertEqual(self.state['players/'+self.a]['balance'],900)
        self.assertFalse(any(k.startswith('dimensions-front-social-creation/') for k in self.state))
    def test_post_profile_directory_failure_does_not_repeat_committed_save(self):
        self.fail_read='dimensions-front-social-player/'+self.a
        saved=self.state['players/'+self.a]
        profile=[e for e in saved['profile'] if e['key']!='DF_AccountXP']+[dict(key='DF_AccountXP',kind=1,text='102')]
        status,result=self.request('/profile/save',dict(profile=profile,expectedProfileRevision=2))
        self.assertEqual(status,200);self.assertEqual(result['wallet']['profileRevision'],3)
        self.assertEqual(self.state['players/'+self.a]['balance'],900)
        self.assertEqual(self.state['dimensions-front-social-player/'+self.a]['xp'],0)
    def test_invalid_social_payload_is_rejected_before_social_writes(self):
        before=copy.deepcopy(self.state)
        self.assertEqual(self.request('/social/state',[1])[0],400)
        self.assertEqual(self.state,before)
    def test_new_account_social_state_can_arrive_before_wallet_initialization(self):
        self.state.pop('players/'+self.c)
        status,result=self.request('/social/state',token='carol')
        self.assertEqual(status,200);self.assertFalse(result['social']['hasClan'])
        self.assertIsNone(result['social']['clan']);self.assertEqual(self.state['players/'+self.c]['balance'],0)

class StandaloneDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.id='a'*20;now=int(time.time()*1000)
        self.state={'dimensions-front-chat-report/'+self.id:dict(id=self.id,clan='b'*20,message_id='c'*20,reason='Threats',created_at=now,evidence='private'),
                    'dimensions-front-chat-outbox/'+self.id:dict(id=self.id,attempts=0,next_at=0,lease_until=0,sent_at=0)}
    def request(self,mode,**options):
        reply=rpc(dict(data=self.state,mode=mode,config=dict(adminEmail='admin@example.test',reportDays=180),**options))
        self.assertTrue(reply['ok'],reply.get('stack'));self.state=reply['data'];return reply['result']
    def test_outbox_lease_excludes_second_worker_and_stale_ack(self):
        item=self.request('reserve');self.assertEqual(item['report']['id'],self.id)
        self.assertIsNone(self.request('reserve'))
        before=copy.deepcopy(self.state)
        self.request('finish',id=self.id,lease=item['lease']-1,delivered=True);self.assertEqual(self.state,before)
        self.request('finish',id=self.id,lease=item['lease'],delivered=False)
        self.assertEqual(self.state['dimensions-front-chat-outbox/'+self.id]['sent_at'],0)
        self.assertGreater(self.state['dimensions-front-chat-outbox/'+self.id]['next_at'],int(time.time()*1000))
    def test_smtp_failure_stays_pending_and_retry_delivers_once(self):
        self.assertEqual(self.request('pump',fakeSendOk=False),dict(delivered=0,retryPending=1))
        report=copy.deepcopy(self.state['dimensions-front-chat-report/'+self.id])
        self.state['dimensions-front-chat-outbox/'+self.id]['next_at']=0
        self.assertEqual(self.request('pump',fakeSendOk=True),dict(delivered=1,retryPending=0))
        self.assertEqual(self.request('pump',fakeSendOk=True),dict(delivered=0,retryPending=0))
        self.assertEqual(self.state['dimensions-front-chat-report/'+self.id],report)

def load_tests(loader,tests,pattern):
    return unittest.TestSuite([loader.loadTestsFromTestCase(StandaloneSocialTests),loader.loadTestsFromTestCase(StandaloneChatTests),loader.loadTestsFromTestCase(StandaloneHttpTests),loader.loadTestsFromTestCase(StandaloneDeliveryTests)])

if __name__=='__main__':unittest.main()
