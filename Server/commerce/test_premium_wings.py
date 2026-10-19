"""New wing SKUs use the existing account ledger, including earned/paid splits."""
import tempfile
import unittest
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from ledger import Ledger, ITEMS


class PremiumWingTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory()
        self.path=Path(self.tmp.name)/'ledger.db'
        self.ledger=Ledger(self.path)

    def tearDown(self):self.tmp.cleanup()

    def value(self,snapshot,key):
        return next((e.get('number') for e in snapshot['profile'] if e['key']==key),None)

    def fund(self,account,earned,paid=False):
        wallet=self.ledger.wallet(account)
        if paid:
            proof=dict(purchaseState=0,obfuscatedExternalAccountId=account,quantity=1,consumptionState=0)
            self.ledger.fulfill(account,'dimensions_shards_5500','paid-'+account,proof)
            wallet=self.ledger.wallet(account)
        return self.ledger.save_profile(account,[dict(key='DF_Shards',kind=0,number=earned)],wallet['profileRevision'])

    def test_exact_balance_purchase_does_not_auto_equip_and_restores_after_restart(self):
        for sku,price,key in [('angel_wings',11000,'AngelWings'),('rgb_rift_wings',15000,'RgbRiftWings')]:
            with self.subTest(sku=sku):
                account=sku
                self.assertEqual(ITEMS[sku]['price'],price)
                wallet=self.fund(account,price)
                bought=self.ledger.purchase(account,sku,'a'*32,wallet['profileRevision'])
                self.assertEqual(self.value(bought,'DF_Shards'),0)
                self.assertEqual(self.value(bought,'DF_SkinOwned_'+key),1)
                self.assertFalse(any(e['key'].startswith('DF_Skin_HeroEquipped_') for e in bought['profile']))
                # Ownership and explicit per-hero loadout persist in the same account profile.
                entries=bought['profile']+[dict(key='DF_Skin_HeroEquipped_6',kind=0,number=5 if sku=='angel_wings' else 6)]
                saved=self.ledger.save_profile(account,entries,bought['profileRevision'])
                restored=Ledger(self.path).wallet(account)
                self.assertEqual(self.value(restored,'DF_SkinOwned_'+key),1)
                self.assertEqual(self.value(restored,'DF_Skin_HeroEquipped_6'),5 if sku=='angel_wings' else 6)
                again=self.ledger.purchase(account,sku,'b'*32,saved['profileRevision'])
                self.assertEqual(self.value(again,'DF_Shards'),0)
                self.assertEqual(again['balance'],0)

    def test_one_shard_short_is_atomic_for_each_new_sku(self):
        for sku,price in [('angel_wings',11000),('rgb_rift_wings',15000)]:
            with self.subTest(sku=sku):
                wallet=self.fund(sku,price-1)
                with self.assertRaisesRegex(ValueError,'Insufficient shards'):
                    self.ledger.purchase(sku,sku,'c'*32,wallet['profileRevision'])
                after=self.ledger.wallet(sku)
                self.assertEqual(after,wallet)
                self.assertNotIn(sku,after['owned'])

    def test_mixed_earned_and_paid_and_operation_retry(self):
        for sku,price in [('angel_wings',11000),('rgb_rift_wings',15000)]:
            with self.subTest(sku=sku):
                wallet=self.fund(sku,price-5500,paid=True)
                bought=self.ledger.purchase(sku,sku,'d'*32,wallet['profileRevision'])
                self.assertEqual(bought['balance'],0)
                self.assertEqual(self.value(bought,'DF_Shards'),0)
                self.assertEqual(self.ledger.purchase(sku,sku,'d'*32,wallet['profileRevision']),bought)
                self.assertNotIn(sku,self.ledger.wallet('another-account')['owned'])

    def test_concurrent_retry_cannot_double_charge(self):
        wallet=self.fund('rgb',30000)
        def buy(_):return self.ledger.purchase('rgb','rgb_rift_wings','e'*32,wallet['profileRevision'])
        with ThreadPoolExecutor(max_workers=4) as pool:results=list(pool.map(buy,range(4)))
        self.assertTrue(all(self.value(r,'DF_Shards')==15000 for r in results))
        self.assertEqual(self.ledger.wallet('rgb')['owned'],['rgb_rift_wings'])


if __name__=='__main__':unittest.main()
