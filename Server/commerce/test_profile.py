import io
import json
import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from unittest.mock import Mock
from ledger import Ledger, ProfileConflict, MAX_BALANCE
from application import CommerceApplication


def number(key,value): return {'key':key,'kind':0,'number':value}
def text(key,value): return {'key':key,'kind':1,'text':value}
def profile_value(snapshot,key):
    return next((e.get('number',e.get('text',e.get('real'))) for e in snapshot['profile'] if e['key']==key),None)

class ProfileTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory(); self.path=Path(self.tmp.name)/'wallet.db'; self.ledger=Ledger(self.path)
        self.valid={'purchaseState':0,'obfuscatedExternalAccountId':'a','quantity':1,'consumptionState':0}
    def tearDown(self): self.tmp.cleanup()
    def grant(self,pack='dimensions_shards_1200',token='token'): self.ledger.fulfill('a',pack,token,self.valid)
    def test_full_account_restores_after_restart_and_is_isolated(self):
        entries=[text('DF_AccountXP','12345'),text('DF_PlayerName','Chris'),number('DF_Shards',300),number('DF_BattleItem_2',2),number('DF_ArenaOwned_Noxara',1),text('DF_BP_BETA01_FreeClaims','1100'),{'key':'DF_MasterVolume','kind':2,'real':.75}]
        self.ledger.save_profile('a',entries,0)
        restored=Ledger(self.path).wallet('a')
        self.assertEqual(profile_value(restored,'DF_AccountXP'),'12345')
        self.assertEqual(profile_value(restored,'DF_BattleItem_2'),2)
        self.assertEqual(profile_value(restored,'DF_MasterVolume'),.75)
        self.assertEqual(self.ledger.wallet('b')['profile'],[])
    def test_daily_progression_restores_and_stays_account_scoped(self):
        entries=[number('DF_Shards',12),number('DF_LoginStreakDay_v1',20260101),number('DF_LoginStreakIndex_v1',4),number('DF_DailyQuestWonMaps_v1',31)]
        self.ledger.save_profile('a',entries,0)
        restored=Ledger(self.path).wallet('a')
        for entry in entries: self.assertEqual(profile_value(restored,entry['key']),entry['number'])
        self.assertEqual(self.ledger.wallet('b')['profile'],[])
        self.assertEqual(restored['balance'],0) # daily rewards never credit the Google-paid wallet

    def test_invalid_daily_progression_rejected(self):
        invalid=[number('DF_LoginStreakIndex_v1',0),number('DF_LoginStreakIndex_v1',8),number('DF_LoginStreakIndex_v1',True),text('DF_LoginStreakIndex_v1','7'),number('DF_DailyQuestWonMaps_v1',-1),number('DF_DailyQuestWonMaps_v1',2048),text('DF_DailyQuestWonMaps_v1','31'),number('DF_LoginStreakDay_v1',20260230),number('DF_LoginStreakDay_v1',99991231)]
        for entry in invalid:
            with self.subTest(entry=entry), self.assertRaises(ValueError): self.ledger.save_profile('a',[entry],0)
        self.assertEqual(self.ledger.wallet('a')['profile'],[])

    def test_login_marker_cannot_be_deleted_or_rewound(self):
        entries=[number('DF_LoginStreakDay_v1',20260102),number('DF_LoginStreakIndex_v1',2)]
        saved=self.ledger.save_profile('a',entries,0)
        preserved=self.ledger.save_profile('a',[],saved['profileRevision'])
        self.assertEqual(profile_value(preserved,'DF_LoginStreakDay_v1'),20260102)
        bad=[[number('DF_LoginStreakDay_v1',20260101),number('DF_LoginStreakIndex_v1',1)], [number('DF_LoginStreakDay_v1',20260102),number('DF_LoginStreakIndex_v1',7)], [number('DF_LoginStreakDay_v1',20260103)]]
        for entries in bad:
            with self.subTest(entries=entries), self.assertRaises(ValueError):
                self.ledger.save_profile('a',entries,preserved['profileRevision'])

    def test_two_devices_cannot_both_save_a_daily_payout(self):
        self.ledger.save_profile('a',[number('DF_Shards',10)],0)
        payout=[number('DF_Shards',22),number('DF_LoginStreakDay_v1',20260101),number('DF_LoginStreakIndex_v1',4)]
        self.ledger.save_profile('a',payout,1)
        with self.assertRaises(ProfileConflict): self.ledger.save_profile('a',payout,1)
        self.assertEqual(profile_value(self.ledger.wallet('a'),'DF_Shards'),22)

    def test_stale_device_cannot_overwrite_newer_profile(self):
        self.ledger.save_profile('a',[text('DF_AccountXP','100')],0)
        with self.assertRaises(ProfileConflict): self.ledger.save_profile('a',[text('DF_AccountXP','1')],0)
        self.assertEqual(profile_value(self.ledger.wallet('a'),'DF_AccountXP'),'100')
    def test_concurrent_profile_saves_have_one_winner(self):
        def save(i):
            try: self.ledger.save_profile('a',[text('DF_AccountXP',str(i))],0); return True
            except ProfileConflict: return False
        with ThreadPoolExecutor(max_workers=8) as pool: result=list(pool.map(save,range(20)))
        self.assertEqual(sum(result),1)
    def test_mixed_purchase_debits_exact_price_and_restores_ownership(self):
        self.grant(); self.ledger.save_profile('a',[number('DF_Shards',200)],0)
        result=self.ledger.purchase('a','avatar_10','1'*32,1)
        self.assertEqual(result['balance'],950)
        self.assertEqual(profile_value(result,'DF_Shards'),0)
        self.assertEqual(profile_value(result,'DF_AvatarOwned_10'),1)
        self.assertEqual(profile_value(result,'DF_Avatar'),10)
        again=Ledger(self.path).purchase('a','avatar_10','1'*32,0)
        self.assertEqual(again['balance'],950)
        self.assertEqual(again['profileRevision'],result['profileRevision'])
    def test_concurrent_retries_do_not_duplicate_consumable(self):
        self.grant()
        with ThreadPoolExecutor(max_workers=8) as pool:
            list(pool.map(lambda _:self.ledger.purchase('a','shield','2'*32,0),range(40)))
        result=self.ledger.wallet('a'); self.assertEqual(result['balance'],1100)
        self.assertEqual(profile_value(result,'DF_BattleItem_2'),1)
    def test_same_operation_cannot_buy_another_item(self):
        self.grant(); self.ledger.purchase('a','avatar_10','3'*32,0)
        with self.assertRaises(ValueError): self.ledger.purchase('a','avatar_11','3'*32,1)
    def test_insufficient_funds_roll_back_all_changes(self):
        original=self.ledger.save_profile('a',[number('DF_Shards',99)],0)
        with self.assertRaises(ValueError): self.ledger.purchase('a','shield','4'*32,1)
        self.assertEqual(self.ledger.wallet('a'),original)
    def test_second_device_cannot_spend_stale_earned_balance(self):
        self.ledger.save_profile('a',[number('DF_Shards',500)],0)
        self.ledger.purchase('a','avatar_10','5'*32,1)
        with self.assertRaises(ProfileConflict): self.ledger.purchase('a','avatar_11','6'*32,1)
        self.assertEqual(profile_value(self.ledger.wallet('a'),'DF_Shards'),50)
    def test_ownership_survives_profile_save_omission(self):
        self.grant(); bought=self.ledger.purchase('a','avatar_10','7'*32,0)
        saved=self.ledger.save_profile('a',[number('DF_Shards',0)],bought['profileRevision'])
        self.assertEqual(profile_value(saved,'DF_AvatarOwned_10'),1)
    def test_same_item_new_operation_never_debits_again(self):
        self.grant(); first=self.ledger.purchase('a','avatar_10','8'*32,0)
        second=self.ledger.purchase('a','avatar_10','9'*32,first['profileRevision'])
        self.assertEqual(first['balance'],second['balance'])
    def test_malformed_profiles_and_device_journals_are_rejected(self):
        invalid=[None,{},[number('DF_Shards',-1)],[number('DF_Shards',True)],[number('DF_Shards',MAX_BALANCE+1)],[text('DF_Shards','100')],[number('OTHER',1)],[text('DF_ProfileTransaction_v1','malicious')],[number('DF_Shards',1),number('DF_Shards',2)],[{'key':'DF_MasterVolume','kind':2,'real':float('nan')}]]
        for entries in invalid:
            with self.subTest(entries=entries), self.assertRaises(ValueError): self.ledger.save_profile('a',entries,0)
    def test_profile_data_cannot_credit_paid_wallet(self):
        self.ledger.save_profile('a',[number('DF_PaidBalance',MAX_BALANCE),number('DF_Shards',100)],0)
        self.assertEqual(self.ledger.wallet('a')['balance'],0)
    def test_overflow_cannot_credit_or_consume_token(self):
        self.ledger.wallet('a')
        with self.ledger.connect() as db: db.execute('UPDATE wallet SET balance=? WHERE account=?',(MAX_BALANCE-100,'a'))
        with self.assertRaises(ValueError): self.grant()
        with self.ledger.connect() as db: self.assertEqual(db.execute('SELECT count(*) FROM purchase').fetchone()[0],0)
    def test_server_catalog_covers_all_purchase_types(self):
        self.grant('dimensions_shards_5500'); self.grant('dimensions_shards_5500','token2')
        for i,sku in enumerate(['hero_traveler','hero_nerok','arena_Noxara','weapon_0_1','battlepass_beta01','shield']):
            before=self.ledger.wallet('a'); self.ledger.purchase('a',sku,format(i+100,'032x'),before['profileRevision'])
        result=Ledger(self.path).wallet('a')
        for key in ['DF_HeroOwned_Traveler','DF_HeroOwned_Nerok','DF_ArenaOwned_Noxara','DF_WeaponSkinOwnedItem_0_1','DF_BP_BETA01_Premium','DF_BattleItem_2']:
            self.assertEqual(profile_value(result,key),1)

    def test_nerok_unlock_costs_2200_and_survives_retries_and_profile_sync(self):
        self.grant()
        self.ledger.save_profile('a',[number('DF_Shards',1000)],0)
        bought=self.ledger.purchase('a','hero_nerok','a'*32,1)
        self.assertEqual(bought['balance'],0)
        self.assertEqual(profile_value(bought,'DF_Shards'),0)
        self.assertEqual(profile_value(bought,'DF_HeroOwned_Nerok'),1)
        replay=Ledger(self.path).purchase('a','hero_nerok','a'*32,0)
        self.assertEqual(replay['profileRevision'],bought['profileRevision'])
        restored=self.ledger.save_profile('a',[],bought['profileRevision'])
        self.assertEqual(profile_value(restored,'DF_HeroOwned_Nerok'),1)
        self.assertIsNone(profile_value(self.ledger.wallet('b'),'DF_HeroOwned_Nerok'))

    def test_nerok_unlock_rejects_insufficient_shards_without_ownership(self):
        self.grant()
        before=self.ledger.wallet('a')
        with self.assertRaises(ValueError):
            self.ledger.purchase('a','hero_nerok','b'*32,before['profileRevision'])
        self.assertEqual(self.ledger.wallet('a'),before)

    def test_nerok_weapon_purchases_restore_and_equip_both_colorways(self):
        self.grant()
        for variant in (0,1):
            before=self.ledger.wallet('a')
            bought=self.ledger.purchase('a',f'weapon_7_{variant}',format(200+variant,'032x'),before['profileRevision'])
            restored=Ledger(self.path).wallet('a')
            self.assertEqual(profile_value(restored,f'DF_WeaponSkinOwnedItem_7_{variant}'),1)
            self.assertEqual(profile_value(restored,'DF_WeaponSkinSelected_7'),variant)
            self.assertEqual(profile_value(restored,'DF_WeaponSkinEquipped_7'),1)
        self.assertEqual(restored['balance'],200)
        self.assertEqual(profile_value(restored,'DF_WeaponSkinOwnedItem_7_0'),1)
        self.assertEqual(profile_value(restored,'DF_WeaponSkinOwnedItem_7_1'),1)

class ProfileHttpTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory(); self.app=CommerceApplication(Path(self.tmp.name)/'db',lambda h:'a' if h=='Bearer valid' else None,Mock(),'package')
    def tearDown(self):self.tmp.cleanup()
    def request(self,path,data,auth='Bearer valid'):
        body=json.dumps(data).encode();out={}
        env={'REQUEST_METHOD':'POST','PATH_INFO':path,'CONTENT_LENGTH':str(len(body)),'wsgi.input':io.BytesIO(body),'HTTP_AUTHORIZATION':auth}
        payload=b''.join(self.app(env,lambda s,h:out.update(status=s)));out['body']=json.loads(payload);return out
    def test_profile_routes_auth_and_conflict(self):
        data={'profile':[text('DF_AccountXP','200')],'expectedProfileRevision':0,'account':'b'}
        self.assertEqual(self.request('/profile/save',data,auth='')['status'],'401 Unauthorized')
        self.assertEqual(self.request('/profile/save',data)['status'],'200 OK')
        self.assertEqual(self.request('/profile/save',data)['status'],'409 Conflict')
        self.assertEqual(self.app.ledger.wallet('b')['profile'],[])
    def test_spend_ignores_client_price_amount_and_unlock_payload(self):
        self.app.ledger.save_profile('a',[number('DF_Shards',500)],0)
        data={'cosmeticId':'avatar_10','operationId':'a'*32,'expectedProfileRevision':1,'price':1,'amount':999999,'localContribution':999999,'writes':[number('DF_Shards',999999)]}
        result=self.request('/wallet/spend',data)
        self.assertEqual(result['status'],'200 OK')
        self.assertEqual(profile_value(result['body']['wallet'],'DF_Shards'),50)
        self.assertEqual(self.request('/wallet/spend',data)['body'],result['body'])
    def test_malformed_requests_are_not_retryable_internal_errors(self):
        for data in ({},{'expectedProfileRevision':False,'profile':[]},{'expectedProfileRevision':0,'profile':{}}):
            self.assertEqual(self.request('/profile/save',data)['status'],'400 Bad Request')
        for data in ({},{'cosmeticId':'shield','operationId':42,'expectedProfileRevision':0}):
            self.assertEqual(self.request('/wallet/spend',data)['status'],'400 Bad Request')

if __name__=='__main__':unittest.main()

