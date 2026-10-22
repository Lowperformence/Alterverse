"""The Blender test hat uses the existing transactional purchase/account path."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import tempfile
import unittest
from ledger import Ledger, ITEMS

SKU = 'ufo_test_hat'
OWNED = 'DF_HeadwearOwned_UfoTestHat'

class UfoTestHatTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.path = Path(self.folder.name) / 'ledger.db'
        self.ledger = Ledger(self.path)

    def tearDown(self): self.folder.cleanup()

    @staticmethod
    def value(wallet, key):
        return next((e.get('number') for e in wallet['profile'] if e['key'] == key), 0)

    def fund(self, amount):
        wallet = self.ledger.wallet('tester')
        return self.ledger.save_profile('tester', [dict(key='DF_Shards', kind=0, number=amount),
            dict(key='DF_AccountXP', kind=0, number=777)], wallet['profileRevision'])

    def test_exactly_one_shard_grants_ownership_without_auto_equip(self):
        self.assertEqual(ITEMS[SKU]['price'], 1)
        wallet = self.fund(1)
        bought = self.ledger.purchase('tester', SKU, 'a'*32, wallet['profileRevision'])
        self.assertEqual(self.value(bought, 'DF_Shards'), 0)
        self.assertEqual(self.value(bought, OWNED), 1)
        self.assertEqual(self.value(bought, 'DF_AccountXP'), 777)
        self.assertFalse(any(e['key'].startswith('DF_HeadwearEquipped_') for e in bought['profile']))

    def test_zero_shards_rejects_atomically(self):
        wallet = self.fund(0)
        with self.assertRaisesRegex(ValueError, 'Insufficient shards'):
            self.ledger.purchase('tester', SKU, 'b'*32, wallet['profileRevision'])
        self.assertEqual(self.ledger.wallet('tester'), wallet)

    def test_retry_and_repurchase_never_charge_again(self):
        wallet = self.fund(2)
        bought = self.ledger.purchase('tester', SKU, 'c'*32, wallet['profileRevision'])
        self.assertEqual(self.value(bought, 'DF_Shards'), 1)
        self.assertEqual(self.ledger.purchase('tester', SKU, 'c'*32, wallet['profileRevision']), bought)
        again = self.ledger.purchase('tester', SKU, 'd'*32, bought['profileRevision'])
        self.assertEqual(self.value(again, 'DF_Shards'), 1)

    def test_concurrent_retry_is_one_purchase(self):
        wallet = self.fund(2)
        def buy(_): return self.ledger.purchase('tester', SKU, 'e'*32, wallet['profileRevision'])
        with ThreadPoolExecutor(max_workers=4) as pool: list(pool.map(buy, range(4)))
        self.assertEqual(self.value(self.ledger.wallet('tester'), 'DF_Shards'), 1)

    def test_equipment_survives_restart_and_is_account_scoped(self):
        wallet = self.fund(1)
        bought = self.ledger.purchase('tester', SKU, 'f'*32, wallet['profileRevision'])
        entries = bought['profile'] + [dict(key='DF_HeadwearEquipped_5', kind=0, number=8)]
        self.ledger.save_profile('tester', entries, bought['profileRevision'])
        restored = Ledger(self.path).wallet('tester')
        self.assertEqual(self.value(restored, OWNED), 1)
        self.assertEqual(self.value(restored, 'DF_HeadwearEquipped_5'), 8)
        self.assertEqual(self.value(self.ledger.wallet('another-account'), OWNED), 0)

if __name__ == '__main__': unittest.main()
