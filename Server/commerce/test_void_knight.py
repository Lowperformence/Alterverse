"""Void Knight weapon listings are previews until the hero's later release."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock
from ledger import Ledger,ITEMS
from firestore_ledger import FirestoreLedger

class VoidKnightWeaponTests(unittest.TestCase):
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
