"""Transactional purchase checks for the four 100-shard shop hats."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import tempfile
import unittest
from ledger import Ledger, ITEMS

HATS={'ufo_test_hat':('UfoTestHat',8),'wizard_hat':('WizardHat',9),'propeller_cap':('PropellerCap',10),'mushroom_hat':('MushroomHat',11)}

class ShopHatTests(unittest.TestCase):
    def setUp(self):
        self.folder=tempfile.TemporaryDirectory()
        self.path=Path(self.folder.name)/'ledger.db'
        self.ledger=Ledger(self.path)
    def tearDown(self): self.folder.cleanup()
    @staticmethod
    def value(wallet,key): return next((e.get('number') for e in wallet['profile'] if e['key']==key),0)
    def fund(self,account,amount,owned=None):
        wallet=self.ledger.wallet(account)
        entries=[dict(key='DF_Shards',kind=0,number=amount),dict(key='DF_AccountXP',kind=0,number=777)]
        if owned: entries.append(dict(key=owned,kind=0,number=1))
        return self.ledger.save_profile(account,entries,wallet['profileRevision'])
    def test_exact_100_shards_unlocks_without_auto_equip(self):
        for sku,(resource,_) in HATS.items():
            with self.subTest(sku=sku):
                self.assertEqual(ITEMS[sku]['price'],100)
                w=self.fund(sku,100);b=self.ledger.purchase(sku,sku,'a'*32,w['profileRevision'])
                self.assertEqual(self.value(b,'DF_Shards'),0)
                self.assertEqual(self.value(b,'DF_HeadwearOwned_'+resource),1)
                self.assertEqual(self.value(b,'DF_AccountXP'),777)
                self.assertFalse(any(e['key'].startswith('DF_HeadwearEquipped_') for e in b['profile']))
    def test_99_shards_fails_atomically(self):
        for sku in HATS:
            with self.subTest(sku=sku):
                w=self.fund(sku,99)
                with self.assertRaisesRegex(ValueError,'Insufficient shards'): self.ledger.purchase(sku,sku,'b'*32,w['profileRevision'])
                self.assertEqual(self.ledger.wallet(sku),w)
    def test_retry_and_repurchase_charge_once(self):
        for sku in HATS:
            with self.subTest(sku=sku):
                w=self.fund(sku,200);b=self.ledger.purchase(sku,sku,'c'*32,w['profileRevision'])
                self.assertEqual(self.value(b,'DF_Shards'),100)
                self.assertEqual(self.ledger.purchase(sku,sku,'c'*32,w['profileRevision']),b)
                self.assertEqual(self.value(self.ledger.purchase(sku,sku,'d'*32,b['profileRevision']),'DF_Shards'),100)
    def test_concurrent_retry_charges_once(self):
        for sku in HATS:
            with self.subTest(sku=sku):
                w=self.fund(sku,200)
                def buy(_): return self.ledger.purchase(sku,sku,'e'*32,w['profileRevision'])
                with ThreadPoolExecutor(max_workers=4) as pool: list(pool.map(buy,range(4)))
                self.assertEqual(self.value(self.ledger.wallet(sku),'DF_Shards'),100)
    def test_ownership_equipment_persists_and_is_account_scoped(self):
        for sku,(resource,cosmetic) in HATS.items():
            with self.subTest(sku=sku):
                w=self.fund(sku,100);b=self.ledger.purchase(sku,sku,'f'*32,w['profileRevision'])
                self.ledger.save_profile(sku,b['profile']+[dict(key='DF_HeadwearEquipped_5',kind=0,number=cosmetic)],b['profileRevision'])
                restored=Ledger(self.path).wallet(sku)
                self.assertEqual(self.value(restored,'DF_HeadwearOwned_'+resource),1)
                self.assertEqual(self.value(restored,'DF_HeadwearEquipped_5'),cosmetic)
                self.assertEqual(self.value(self.ledger.wallet('another-account'),'DF_HeadwearOwned_'+resource),0)
    def test_existing_ownership_is_preserved_after_price_change(self):
        for sku,(resource,_) in HATS.items():
            with self.subTest(sku=sku):
                key='DF_HeadwearOwned_'+resource;w=self.fund(sku,7,key)
                b=self.ledger.purchase(sku,sku,'1'*32,w['profileRevision'])
                self.assertEqual(self.value(b,'DF_Shards'),7);self.assertEqual(self.value(b,key),1)
    def test_beta_cap_is_not_in_shard_catalog(self):
        self.assertFalse(any(w.get('key')=='DF_HeadwearOwned_BetaCap' for item in ITEMS.values() for w in item.get('writes',[])))

if __name__=='__main__': unittest.main()
