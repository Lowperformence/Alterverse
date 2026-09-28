import tempfile, unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from ledger import Ledger

class LedgerTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.ledger=Ledger(Path(self.tmp.name)/'ledger.db')
        self.valid={'purchaseState':0,'obfuscatedExternalAccountId':'account-a','quantity':1,'consumptionState':0}
    def tearDown(self): self.tmp.cleanup()
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
    def test_unified_claim_is_retry_safe_and_acknowledged_once(self):
        self.ledger.fulfill('account-a','dimensions_shards_500','claim-token',self.valid)
        first=self.ledger.claim('account-a')
        second=self.ledger.claim('account-a')
        self.assertEqual(first['claimAmount'],500)
        self.assertEqual(first['claimId'],second['claimId'])
        self.assertEqual(first['balance'],0)
        self.ledger.acknowledge_claim('account-a',first['claimId'])
        empty=self.ledger.claim('account-a')
        self.assertEqual(empty['claimAmount'],0)
        self.assertEqual(empty['claimId'],'')
    def test_new_purchase_waits_behind_existing_pending_claim_without_loss(self):
        self.ledger.fulfill('account-a','dimensions_shards_500','claim-a',self.valid)
        first=self.ledger.claim('account-a')
        self.ledger.fulfill('account-a','dimensions_shards_500','claim-b',self.valid)
        same=self.ledger.claim('account-a')
        self.assertEqual(first['claimId'],same['claimId'])
        self.assertEqual(self.ledger.wallet('account-a')['balance'],500)
        self.ledger.acknowledge_claim('account-a',first['claimId'])
        second=self.ledger.claim('account-a')
        self.assertNotEqual(first['claimId'],second['claimId'])
        self.assertEqual(second['claimAmount'],500)
        self.assertEqual(second['balance'],0)
    def test_unknown_consumed_token_does_not_credit(self):
        with self.assertRaises(ValueError):self.ledger.fulfill('account-a','dimensions_shards_500','token',dict(self.valid,consumptionState=1))
if __name__=='__main__':unittest.main()
