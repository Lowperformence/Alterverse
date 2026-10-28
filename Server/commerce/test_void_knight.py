"""Void Knight hero launch; optional weapon colorways remain previews."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock,patch
from ledger import Ledger,ITEMS
from firestore_ledger import FirestoreLedger

class VoidKnightWeaponTests(unittest.TestCase):
    def test_hero_launch_price_and_persisted_ownership(self):
        item=ITEMS['hero_void_knight']
        self.assertEqual(item['price'],1600)
        self.assertIs(item['available'],True)
        self.assertEqual(item['writes'],[{'key':'DF_HeroOwned_VoidKnight','kind':0,'number':1}])

    def test_released_hero_purchase_is_atomic_and_persists(self):
        with tempfile.TemporaryDirectory() as tmp:
            ledger=Ledger(Path(tmp)/'ledger.db');before=ledger.wallet('launch-account')
            before=ledger.save_profile('launch-account',[{'key':'DF_Shards','kind':0,'number':1600}],before['profileRevision'])
            after=ledger.purchase('launch-account','hero_void_knight','a'*32,before['profileRevision'])
            self.assertEqual({e['key']:e.get('number') for e in after['profile']}['DF_Shards'],0)
            self.assertEqual({e['key']:e.get('number') for e in after['profile']}['DF_HeroOwned_VoidKnight'],1)
            self.assertEqual(Ledger(Path(tmp)/'ledger.db').wallet('launch-account'),{k:v for k,v in after.items() if k!='operationId'})
            self.assertEqual(ledger.purchase('launch-account','hero_void_knight','a'*32,before['profileRevision']),after)

    def test_timmy_remains_locked(self):
        self.assertIs(ITEMS['hero_timmy']['available'],False)

    def test_future_release_mixed_payment_ownership_retry_and_restore(self):
        # Test-only catalog override; the shipped product remains unavailable.
        with patch.dict(ITEMS,{'hero_void_knight':dict(ITEMS['hero_void_knight'],available=True)}):
            with tempfile.TemporaryDirectory() as tmp:
                ledger=Ledger(Path(tmp)/'ledger.db')
                verified={'purchaseState':0,'obfuscatedExternalAccountId':'account-a','quantity':1,'consumptionState':0}
                before=ledger.fulfill('account-a','dimensions_shards_1200','hero-token',verified)
                before=ledger.save_profile('account-a',[{'key':'DF_Shards','kind':0,'number':700}],before['profileRevision'])
                after=ledger.purchase('account-a','hero_void_knight','e'*32,before['profileRevision'])
                self.assertEqual(after['balance'],300) # 700 earned + 900 paid spent.
                entries={e['key']:e.get('number') for e in after['profile']}
                self.assertEqual(entries['DF_Shards'],0)
                self.assertEqual(entries['DF_HeroOwned_VoidKnight'],1)
                self.assertIn('hero_void_knight',after['owned'])
                retry=ledger.purchase('account-a','hero_void_knight','e'*32,before['profileRevision'])
                self.assertEqual(retry,after)
                again=ledger.purchase('account-a','hero_void_knight','f'*32,after['profileRevision'])
                self.assertEqual(again['balance'],300)
                restored=ledger.save_profile('account-a',[{'key':'DF_Shards','kind':0,'number':0}],again['profileRevision'])
                self.assertEqual({e['key']:e.get('number') for e in restored['profile']}['DF_HeroOwned_VoidKnight'],1)

    def test_future_release_insufficient_funds_grants_nothing(self):
        with patch.dict(ITEMS,{'hero_void_knight':dict(ITEMS['hero_void_knight'],available=True)}):
            with tempfile.TemporaryDirectory() as tmp:
                ledger=Ledger(Path(tmp)/'ledger.db');before=ledger.wallet('account-a')
                before=ledger.save_profile('account-a',[{'key':'DF_Shards','kind':0,'number':1599}],before['profileRevision'])
                with self.assertRaisesRegex(ValueError,'Insufficient shards'):
                    ledger.purchase('account-a','hero_void_knight','e'*32,before['profileRevision'])
                self.assertEqual(ledger.wallet('account-a'),before)

    def test_two_locked_variants_use_existing_price_and_correct_ownership_keys(self):
        for variant in range(2):
            item=ITEMS[f'weapon_9_{variant}']
            self.assertEqual(item['price'],500)
            self.assertIs(item['available'],False)
            writes={e['key']:e['number'] for e in item['writes']}
            self.assertEqual(writes[f'DF_WeaponSkinOwnedItem_9_{variant}'],1)
            self.assertEqual(writes['DF_WeaponSkinSelected_9'],variant)
            self.assertEqual(writes['DF_WeaponSkinEquipped_9'],1)

    def test_sqlite_rejects_both_purchase_paths_without_wallet_change(self):
        with tempfile.TemporaryDirectory() as tmp:
            ledger=Ledger(Path(tmp)/'ledger.db')
            verified={'purchaseState':0,'obfuscatedExternalAccountId':'account-a','quantity':1,'consumptionState':0}
            ledger.fulfill('account-a','dimensions_shards_5500','void-token',verified)
            before=ledger.wallet('account-a')
            for variant in range(2):
                sku=f'weapon_9_{variant}'
                for attempt in (
                    lambda:ledger.purchase('account-a',sku,'e'*32,before['profileRevision']),
                    lambda:ledger.buy('account-a',sku),
                ):
                    with self.assertRaisesRegex(ValueError,'not released'):attempt()
                    self.assertEqual(ledger.wallet('account-a'),before)

    def test_firestore_rejects_before_database_access(self):
        ledger=FirestoreLedger.__new__(FirestoreLedger)
        ledger.client=Mock();ledger.accounts=Mock()
        for variant in range(2):
            sku=f'weapon_9_{variant}'
            for attempt in (
                lambda:ledger.purchase('account-a',sku,'e'*32,0),
                lambda:ledger.buy('account-a',sku),
            ):
                with self.assertRaisesRegex(ValueError,'not released'):attempt()
        self.assertEqual(ledger.client.mock_calls,[])
        self.assertEqual(ledger.accounts.mock_calls,[])

    def test_existing_weapon_products_remain_available(self):
        for hero in range(8):
            for variant in range(2):
                item=ITEMS[f'weapon_{hero}_{variant}']
                self.assertEqual(item['price'],500)
                self.assertIs(item.get('available',True),True)

if __name__=='__main__':unittest.main()

