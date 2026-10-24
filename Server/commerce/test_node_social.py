"""Existing clan scenarios plus compatibility with the real Node account schema.

Firestore is a strict transactional fixture; no production project is contacted.
"""
import copy
import hashlib
import io
import json
import os
import unittest
from unittest.mock import patch

from clan_chat import ChatConfig
from firestore_ledger import FirestoreLedger
from firestore_social import FirestoreSocial
from node_social import NodeAccountLedger, NodeSocialApplication, create_application
from test_firestore_social import Client, transactional
import test_firestore_social as social_fixtures
import test_clan_chat as chat_fixtures


def account(name='a', earned=1000, paid=900):
    digits=int(hashlib.sha256(name.encode()).hexdigest(),16)%10000000
    return dict(balance=paid,revision=4,profileRevision=7,owned=['ufo_test_hat','avatar_10'],
                profile=[{'key':'DF_PlayerName','kind':1,'text':f'SolarFox{digits:07d}'},
                         {'key':'DF_AccountXP','kind':1,'text':'102'},
                         {'key':'DF_Shards','kind':0,'number':earned}],
                updatedAt='retained-node-timestamp',futureNodeMetadata={'keep':True})


def fixture_ledger(client):
    ledger=NodeAccountLedger('existing-clans',client=client)
    original_wallet=ledger.wallet
    def wallet(who):
        # Shared Python scenarios assume account creation on first use. Simulate
        # the primary Node wallet doing that, before invoking the real adapter.
        if 'players/'+who not in client.data:
            value=account(who);value.update(balance=0,revision=0,profileRevision=0,owned=[])
            client.data['players/'+who]=value
        return original_wallet(who)
    ledger.wallet=wallet
    original=ledger.save_profile
    def save(who,entries,revision):
        if 'players/'+who not in client.data:
            value=account(who);value.update(balance=0,revision=0,profileRevision=0,owned=[])
            client.data['players/'+who]=value
        result=original(who,entries,revision)
        FirestoreSocial(ledger).sync_wallet(who,result)
        return result
    ledger.save_profile=save  # Test data setup only; this process exposes no profile route.
    return ledger


class NodeClanRulesTests(social_fixtures.FirestoreSocialTests):
    def make_ledger(self): return fixture_ledger(self.client)


class NodeClanChatRulesTests(chat_fixtures.FirestoreClanChatTests):
    def make_ledger(self): return fixture_ledger(self.client)


class NodeAccountCompatibilityTests(unittest.TestCase):
    def setUp(self):
        self.client=Client()
        self.decorator=patch('google.cloud.firestore.transactional',transactional)
        self.decorator.start();self.addCleanup(self.decorator.stop)
        self.a='a'*64;self.b='b'*64
        self.client.data['players/'+self.a]=account(self.a,earned=200)
        self.client.data['players/'+self.b]=account(self.b)
        self.ledger=NodeAccountLedger('old-clans',client=self.client)
        self.app=NodeSocialApplication(self.ledger,lambda token: {'Bearer a':self.a,'Bearer b':self.b}.get(token),chat_config=ChatConfig(enabled=False))

    def request(self,path,body=None,token='Bearer a',method='POST'):
        raw=json.dumps(body or {}).encode();result=[]
        content=b''.join(self.app({'REQUEST_METHOD':method,'PATH_INFO':path,'CONTENT_LENGTH':str(len(raw)),
            'wsgi.input':io.BytesIO(raw),'HTTP_AUTHORIZATION':token},lambda status,headers:result.append((status,headers))))
        return int(result[0][0].split()[0]),json.loads(content)

    def create(self,**overrides):
        request=dict(operationId='1'*32,expectedProfileRevision=7,open=True,descriptionId=0)
        request.update(overrides)
        return self.request('/social/clan/create',request)

    def test_read_uses_same_players_document_without_name_or_balance_changes(self):
        before=copy.deepcopy(self.client.data)
        wallet=self.ledger.wallet(self.a)
        self.assertEqual(wallet['balance'],900)
        self.assertEqual(self.client.data,before)
        self.assertEqual(self.ledger.accounts.id,'players')
        self.assertEqual(self.app.social.collections['social_clan'].id,'old-clans-social-clan')

    def test_existing_clan_membership_and_ids_survive_adapter_change(self):
        old=FirestoreLedger(namespace='old-clans',client=self.client)
        old.save_profile(self.a,account(self.a)['profile'],0)
        old_social=FirestoreSocial(old,chat_config=ChatConfig(enabled=False))
        result=old_social.dispatch(self.a,'/social/clan/create',dict(operationId='2'*32,expectedProfileRevision=old.wallet(self.a)['profileRevision'],descriptionId=0,open=True))
        clan=result['clan'];identifier=result['me']['playerId']
        before=copy.deepcopy(self.client.data['players/'+self.a])
        status,state=self.request('/social/state')
        self.assertEqual(status,200)
        self.assertEqual({k:v for k,v in state['social']['clan'].items() if k!='members'},
                         {k:v for k,v in clan.items() if k!='members'})
        self.assertEqual([(p['playerId'],p['role']) for p in state['social']['clan']['members']],
                         [(p['playerId'],p['role']) for p in clan['members']])
        self.assertEqual(state['social']['me']['playerId'],identifier)
        self.assertEqual(self.client.data['players/'+self.a],before)

    def test_create_debits_earned_then_paid_once_and_preserves_node_metadata_and_receipts(self):
        self.client.data['players/'+self.a+'/operations/old']={'kind':'buy','sku':'ufo_test_hat'}
        self.client.data['purchases/existing-token']={'accountHash':self.a,'credited':True}
        status,result=self.create()
        self.assertEqual(status,200)
        wallet=result['social']['wallet'];self.assertEqual(wallet['balance'],600)
        self.assertEqual(wallet['accountHash'],self.a)
        self.assertNotIn('billingReady',wallet)  # Original Node decorator owns this field.
        saved=self.client.data['players/'+self.a]
        self.assertEqual(saved['revision'],5);self.assertEqual(saved['profileRevision'],8)
        self.assertEqual(saved['owned'],['ufo_test_hat','avatar_10'])
        self.assertEqual(saved['futureNodeMetadata'],{'keep':True})
        self.assertEqual(saved['updatedAt'],'retained-node-timestamp')
        self.assertEqual(self.client.data['purchases/existing-token'],{'accountHash':self.a,'credited':True})
        self.assertEqual(self.client.data['players/'+self.a+'/operations/old']['sku'],'ufo_test_hat')
        status,replay=self.create()
        self.assertEqual(status,200);self.assertEqual(replay['social']['wallet']['balance'],600)
        self.assertEqual(replay['social']['clan']['clanId'],result['social']['clan']['clanId'])

    def test_stale_creation_is_rejected_without_any_debit_or_clan(self):
        before=copy.deepcopy(self.client.data['players/'+self.a])
        status,_=self.create(expectedProfileRevision=6)
        self.assertEqual(status,409)
        self.assertEqual(self.client.data['players/'+self.a],before)
        self.assertFalse(any(p.startswith('old-clans-social-clan/') for p in self.client.data))

    def test_forged_account_body_cannot_select_another_wallet(self):
        before=copy.deepcopy(self.client.data['players/'+self.b])
        status,result=self.create(accountHash=self.b,account=self.b,playerId=self.b)
        self.assertEqual(status,200);self.assertEqual(result['social']['wallet']['accountHash'],self.a)
        self.assertEqual(self.client.data['players/'+self.b],before)

    def test_invalid_authentication_never_modifies_account_or_membership(self):
        before=copy.deepcopy(self.client.data)
        for route in ('/social/state','/social/clan/create','/social/_sync','/social/_access'):
            self.assertEqual(self.request(route,token='Bearer wrong')[0],401)
        self.assertEqual(self.client.data,before)

    def test_no_second_billing_or_profile_api_is_exposed(self):
        before=copy.deepcopy(self.client.data)
        for route in ('/wallet','/profile/save','/wallet/spend','/google-play/fulfill','/google-play/subscription','/google-play/rtdn'):
            self.assertEqual(self.request(route,{'purchaseToken':'not-a-real-token'})[0],404)
        self.assertEqual(self.client.data,before)

    def test_missing_or_malformed_account_is_not_created_or_repaired_by_bridge(self):
        for value in (None,{},{**account(), 'balance':True},{**account(),'profileRevision':'7'},
                      {**account(),'profile':[]},{**account(),'owned':[None]}):
            with self.subTest(value=value):
                if value is None:self.client.data.pop('players/'+self.a,None)
                else:self.client.data['players/'+self.a]=value
                before=copy.deepcopy(self.client.data)
                with self.assertRaises(RuntimeError):self.ledger.wallet(self.a)
                self.assertEqual(self.client.data,before)

    def test_directory_sync_reads_committed_profile_and_preserves_old_public_id(self):
        _,first=self.request('/social/state');identifier=first['social']['me']['playerId']
        saved=self.client.data['players/'+self.a]
        saved['profile'].append({'key':'DF_PlayGamesDisplayName','kind':1,'text':'Current Pilot'})
        next(e for e in saved['profile'] if e['key']=='DF_AccountXP')['text']='1000'
        self.assertEqual(self.request('/social/_sync',{'accountHash':self.b})[0],200)
        _,state=self.request('/social/state')
        self.assertEqual(state['social']['me']['playerId'],identifier)
        self.assertEqual(state['social']['me']['name'],'Current Pilot')
        self.assertGreater(state['social']['me']['level'],2)

    def test_suspension_access_gate_is_account_authenticated(self):
        collection=self.app.social.collections['chat_user_ban']
        self.client.data[collection.path+'/'+self.a]={'account':self.a,'until_at':-1}
        self.assertEqual(self.request('/social/_access')[0],403)
        self.assertEqual(self.request('/social/_sync')[0],403)
        self.assertEqual(self.request('/social/_access',token='Bearer b')[0],200)

    def test_health_is_non_sensitive_and_non_social_routes_are_closed(self):
        status,health=self.request('/internal/health',method='GET',token='')
        self.assertEqual((status,health),(200,{'ok':True,'clans':True}))
        self.assertEqual(self.request('/social/_access',method='GET')[0],405)

    def test_wrong_or_empty_namespace_is_a_startup_blocker(self):
        with self.assertRaisesRegex(RuntimeError,'No clans found'):self.ledger.verify_existing_clans()
        for namespace in ('','../players','players/other','CamelCase'):
            with self.assertRaises(ValueError):NodeAccountLedger(namespace,client=self.client)
        with self.assertRaises(RuntimeError):
            create_application({'DF_SOCIAL_NAMESPACE':'old-clans','DF_UNITY_PROJECT_ID':'project'},client=self.client,authenticate=lambda _:self.a)
        app=create_application({'DF_SOCIAL_NAMESPACE':'old-clans','DF_SOCIAL_ALLOW_EMPTY_NAMESPACE':'1'},client=self.client,authenticate=lambda _:self.a)
        self.assertIsInstance(app,NodeSocialApplication)
        self.create()
        self.ledger.verify_existing_clans()  # Same namespace now contains the real fixture clan.

    def test_maintenance_uses_same_node_account_adapter_and_social_namespace(self):
        from chat_maintenance import configured_application
        with patch.dict(os.environ,{'DF_SOCIAL_NAMESPACE':'old-clans'}),patch('node_social.create_application',return_value=self.app) as create:
            self.assertIs(configured_application(),self.app)
        create.assert_called_once_with()

    def test_other_project_cannot_select_a_different_players_ledger(self):
        self.client.project='actual-node-project'
        before=copy.deepcopy(self.client.data)
        with patch('google.cloud.firestore.Client',return_value=self.client):
            with self.assertRaisesRegex(ValueError,'same Firestore project'):
                NodeAccountLedger('old-clans',project_id='other-project')
            matched=NodeAccountLedger('old-clans',project_id='actual-node-project')
        self.assertIs(matched.client,self.client)
        self.assertEqual(self.client.data,before)


if __name__=='__main__':unittest.main()
