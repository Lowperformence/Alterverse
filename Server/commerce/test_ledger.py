import tempfile, unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from ledger import Ledger

class LedgerTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.ledger=Ledger(Path(self.tmp.name)/'ledger.db')
        self.valid={'purchaseState':0,'obfuscatedExternalAccountId':'account-a','quantity':1,'consumptionState':0}
    def tearDown(self): self.tmp.cleanup()
    def test_new_avatars_keep_price_and_ownership_after_restart(self):
        self.ledger.fulfill('account-a','dimensions_shards_5500','avatar-token',self.valid)
        for avatar_id in range(17,27):
            with self.subTest(avatar_id=avatar_id):
                before=self.ledger.wallet('account-a')['balance']
                self.ledger.buy('account-a',f'avatar_{avatar_id}')
                self.ledger.buy('account-a',f'avatar_{avatar_id}')
                restarted=Ledger(self.ledger.path)
                snapshot=restarted.wallet('account-a')
                self.assertEqual(snapshot['balance'],before-450)
                self.assertIn(avatar_id,snapshot['avatars'])
                self.assertNotIn(avatar_id,restarted.wallet('account-b')['avatars'])
        self.assertEqual(self.ledger.wallet('account-a')['balance'],1000)

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


