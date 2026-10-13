import tempfile, unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from ledger import Ledger, ITEMS

class LedgerTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.ledger=Ledger(Path(self.tmp.name)/'ledger.db')
        self.valid={'purchaseState':0,'obfuscatedExternalAccountId':'account-a','quantity':1,'consumptionState':0}
    def tearDown(self): self.tmp.cleanup()
    def test_timmy_is_listed_at_1900_but_cannot_spend_or_unlock_before_release(self):
        self.assertEqual(ITEMS['hero_timmy']['price'],1900)
        self.assertIs(ITEMS['hero_timmy']['available'],False)
        self.ledger.fulfill('account-a','dimensions_shards_5500','timmy-token',self.valid)
        wallet=self.ledger.wallet('account-a')
        wallet=self.ledger.save_profile('account-a',[{'key':'DF_Shards','kind':0,'number':2500}],wallet['profileRevision'])
        before=self.ledger.wallet('account-a')
        for attempt in (
            lambda:self.ledger.purchase('account-a','hero_timmy','e'*32,before['profileRevision']),
            lambda:self.ledger.buy('account-a','hero_timmy'),
        ):
            with self.assertRaisesRegex(ValueError,'not released'):attempt()
            self.assertEqual(self.ledger.wallet('account-a'),before)
        self.assertNotIn('hero_timmy',before['owned'])
        self.assertNotIn('DF_HeroOwned_Timmy',{e['key'] for e in before['profile']})

    def test_firestore_timmy_gate_rejects_before_accessing_database(self):
        from firestore_ledger import FirestoreLedger
        from unittest.mock import Mock
        ledger=FirestoreLedger.__new__(FirestoreLedger)
        ledger.client=Mock();ledger.accounts=Mock()
        for attempt in (
            lambda:ledger.purchase('account-a','hero_timmy','e'*32,0),
            lambda:ledger.buy('account-a','hero_timmy'),
        ):
            with self.assertRaisesRegex(ValueError,'not released'):attempt()
        ledger.client.transaction.assert_not_called()
        ledger.accounts.document.assert_not_called()

    def test_existing_premium_heroes_still_purchase_at_their_original_prices(self):
        self.ledger.fulfill('account-a','dimensions_shards_5500','hero-token',self.valid)
        for index,(sku,price,key) in enumerate((('hero_traveler',1500,'DF_HeroOwned_Traveler'),('hero_nerok',2200,'DF_HeroOwned_Nerok'))):
            before=self.ledger.wallet('account-a')
            after=self.ledger.purchase('account-a',sku,f'{index+100:032x}',before['profileRevision'])
            self.assertEqual(after['balance'],before['balance']-price)
            self.assertEqual({e['key']:e.get('number') for e in after['profile']}[key],1)

    def test_wasp_purchase_hold_preserves_wallet_and_existing_entitlement(self):
        self.assertEqual(ITEMS['arena_Wasp76b']['price'],900)
        self.assertIs(ITEMS['arena_Wasp76b']['available'],False)
        for online in (False,True):
            for owned in (False,True):
                account=f'wasp-{online}-{owned}'
                if online:self.ledger.fulfill(account,'dimensions_shards_1200',account,dict(self.valid,obfuscatedExternalAccountId=account))
                wallet=self.ledger.wallet(account)
                entries=[{'key':'DF_Shards','kind':0,'number':1800}]
                if owned:entries.append({'key':'DF_ArenaOwned_Wasp76b','kind':0,'number':1})
                self.ledger.save_profile(account,entries,wallet['profileRevision'])
                before=self.ledger.wallet(account)
                for attempt in (
                    lambda:self.ledger.purchase(account,'arena_Wasp76b','f'*32,before['profileRevision']),
                    lambda:self.ledger.buy(account,'arena_Wasp76b'),
                ):
                    with self.assertRaisesRegex(ValueError,'not released'):attempt()
                    self.assertEqual(self.ledger.wallet(account),before)

    def test_firestore_wasp_gate_rejects_before_accessing_database(self):
        from firestore_ledger import FirestoreLedger
        from unittest.mock import Mock
        ledger=FirestoreLedger.__new__(FirestoreLedger)
        ledger.client=Mock();ledger.accounts=Mock()
        for attempt in (
            lambda:ledger.purchase('account-a','arena_Wasp76b','e'*32,0),
            lambda:ledger.buy('account-a','arena_Wasp76b'),
        ):
            with self.assertRaisesRegex(ValueError,'not released'):attempt()
        ledger.client.transaction.assert_not_called()
        ledger.accounts.document.assert_not_called()

    def test_new_avatars_keep_price_and_ownership_after_restart(self):
        self.ledger.fulfill('account-a','dimensions_shards_5500','avatar-token',self.valid)
        for avatar_id in range(17,27):
            with self.subTest(avatar_id=avatar_id):
                before=self.ledger.wallet('account-a')['balance']
                self.ledger.buy('account-a',f'avatar_{avatar_id}')
                self.ledger.buy('account-a',f'avatar_{avatar_id}')
                restarted=Ledger(self.ledger.path)
                snapshot=restarted.wallet('account-a')
                self.assertEqual(snapshot['balance'],before-ITEMS[f'avatar_{avatar_id}']['price'])
                self.assertIn(avatar_id,snapshot['avatars'])
                self.assertNotIn(avatar_id,restarted.wallet('account-b')['avatars'])
        self.assertEqual(self.ledger.wallet('account-a')['balance'],1350)

    def test_new_shop_profile_avatars_keep_correct_prices_and_unlock_once(self):
        account = 'account-a'
        self.ledger.fulfill(account, 'dimensions_shards_5500', 'new-profile-avatar-token', self.valid)
        for index, avatar_id in enumerate((43, 44, 45, 46)):
            with self.subTest(avatar_id=avatar_id):
                sku = f'avatar_{avatar_id}'
                self.assertEqual(ITEMS[sku]['price'], 450 if avatar_id == 43 else 400)
                before = self.ledger.wallet(account)
                bought = self.ledger.purchase(account, sku, f'{600 + index:032x}', before['profileRevision'])
                self.assertEqual(bought['balance'], before['balance'] - ITEMS[sku]['price'])
                self.assertIn(avatar_id, bought['avatars'])
                repeated = self.ledger.purchase(account, sku, f'{700 + index:032x}', bought['profileRevision'])
                self.assertEqual(repeated['balance'], bought['balance'])
        restored = Ledger(self.ledger.path).wallet(account)
        self.assertEqual(restored['balance'], 3850)
        for avatar_id in (43, 44, 45, 46):
            self.assertIn(avatar_id, restored['avatars'])

    def test_all_character_profile_pictures_are_400_shards(self):
        character_avatars = (17, 18, 19, 20, 21, 22, 23, 32, 33, 34, 35, 36, 38, 40, 44, 45, 46)
        for avatar_id in character_avatars:
            with self.subTest(avatar_id=avatar_id):
                self.assertEqual(ITEMS[f'avatar_{avatar_id}']['price'], 400)
        # Slogans and abstract badges are separate from character portrait items.
        for avatar_id in (10, 11, 12, 13, 14, 15, 24, 25, 26, 39, 41, 42, 43):
            self.assertNotEqual(ITEMS[f'avatar_{avatar_id}']['price'], 400)

    def test_new_avatar_rejects_insufficient_shards(self):
        with self.assertRaises(ValueError):self.ledger.buy('account-a','avatar_17')
        snapshot=self.ledger.wallet('account-a')
        self.assertEqual(snapshot['balance'],0)
        self.assertNotIn(17,snapshot['avatars'])

    def test_replayed_callbacks_and_restart_credit_once(self):
        with ThreadPoolExecutor(max_workers=6) as pool:
            list(pool.map(lambda _:self.ledger.fulfill('account-a','dimensions_shards_1200','token-1',self.valid),range(20)))
        second=Ledger(self.ledger.path)
        self.assertEqual(second.wallet('account-a')['balance'],1200)
        self.assertEqual(second.fulfill('account-a','dimensions_shards_1200','token-1',self.valid)['revision'],1)
    def test_pending_cancelled_and_wrong_account_cannot_grant(self):
        for state in (1,2):
            with self.assertRaises(ValueError):self.ledger.fulfill('account-a','dimensions_shards_1200','token',dict(self.valid,purchaseState=state))
        with self.assertRaises(ValueError):self.ledger.fulfill('account-b','dimensions_shards_1200','token',self.valid)
        self.assertEqual(self.ledger.wallet('account-a')['balance'],0)
    def test_cosmetic_retry_debits_once(self):
        self.ledger.fulfill('account-a','dimensions_shards_5500','token',self.valid)
        self.ledger.buy('account-a','voidfall');self.ledger.buy('account-a','voidfall')
        self.assertEqual(self.ledger.wallet('account-a')['balance'],3500)
        self.assertTrue(self.ledger.wallet('account-a')['voidfall'])
        with self.assertRaises(ValueError):self.ledger.buy('account-a','beta_supporter_wings')
    def test_token_cannot_move_between_accounts(self):
        self.ledger.fulfill('account-a','dimensions_shards_500','token',self.valid)
        with self.assertRaises(ValueError):self.ledger.fulfill('account-b','dimensions_shards_500','token',dict(self.valid,obfuscatedExternalAccountId='account-b'))
    def test_legacy_claims_cannot_move_paid_value_off_server(self):
        self.ledger.fulfill('account-a','dimensions_shards_500','claim-token',self.valid)
        with self.assertRaises(ValueError): self.ledger.claim('account-a')
        with self.assertRaises(ValueError): self.ledger.acknowledge_claim('account-a','1')
        restarted=Ledger(self.ledger.path)
        self.assertEqual(restarted.wallet('account-a')['balance'],500)

    def test_multiple_purchases_remain_in_one_durable_account_wallet(self):
        self.ledger.fulfill('account-a','dimensions_shards_500','claim-a',self.valid)
        self.ledger.fulfill('account-a','dimensions_shards_500','claim-b',self.valid)
        self.assertEqual(Ledger(self.ledger.path).wallet('account-a')['balance'],1000)

    def test_all_battle_items_cost_100_and_increment_inventory(self):
        self.ledger.fulfill('account-a','dimensions_shards_500','battle-items-token',self.valid)
        expected={
            'regen':'DF_BattleItem_1',
            'shield':'DF_BattleItem_2',
            'velocity':'DF_BattleItem_3',
            'team_heal':'DF_BattleItem_4',
        }
        for index,(sku,key) in enumerate(expected.items()):
            wallet=self.ledger.wallet('account-a')
            result=self.ledger.purchase('account-a',sku,f'{index:032x}',wallet['profileRevision'])
            profile={entry['key']:entry for entry in result['profile']}
            self.assertEqual(profile[key]['number'],1)
        self.assertEqual(self.ledger.wallet('account-a')['balance'],100)

        wallet=self.ledger.wallet('account-a')
        result=self.ledger.purchase('account-a','team_heal','f'*32,wallet['profileRevision'])
        profile={entry['key']:entry for entry in result['profile']}
        self.assertEqual(profile['DF_BattleItem_4']['number'],2)
        self.assertEqual(result['balance'],0)

        retried=self.ledger.purchase('account-a','team_heal','f'*32,result['profileRevision'])
        profile={entry['key']:entry for entry in retried['profile']}
        self.assertEqual(profile['DF_BattleItem_4']['number'],2)
        self.assertEqual(retried['balance'],0)

    def test_unknown_consumed_token_does_not_credit(self):
        with self.assertRaises(ValueError):self.ledger.fulfill('account-a','dimensions_shards_500','token',dict(self.valid,consumptionState=1))
if __name__=='__main__':unittest.main()
