"""Durable purchase/account restoration through both ledger adapters.

Firestore uses the strict transactional fixture, not a production database.
Unity/Google sign-in and real Play checkout still require signed Android acceptance.
"""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from ledger import Ledger
from firestore_ledger import FirestoreLedger
from test_firestore_social import Client, transactional


class PurchaseRestorationTests(unittest.TestCase):
    def test_paid_history_survives_zero_balance(self):
        with tempfile.TemporaryDirectory() as folder, patch('google.cloud.firestore.transactional', transactional):
            client = Client()
            for factory in (lambda: Ledger(Path(folder) / 'zero.sqlite3'), lambda: FirestoreLedger(client=client)):
                with self.subTest(adapter=type(factory()).__name__):
                    ledger = factory()
                    ledger.fulfill('account-a', 'dimensions_shards_500', 'spent-token',
                                   {'purchaseState': 0, 'obfuscatedExternalAccountId': 'account-a'})
                    ledger.purchase('account-a', 'avatar_27', 'b' * 32, 0)
                    revision = ledger.wallet('account-a')['profileRevision']
                    spent = ledger.purchase('account-a', 'shield', 'c' * 32, revision)
                    self.assertEqual(spent['balance'], 0)
                    restored = factory().wallet('account-a')
                    self.assertGreater(restored['revision'], 0)
                    self.assertIn('avatar_27', restored['owned'])

    def test_receipt_and_entitlements_survive_new_client_and_paid_spend(self):
        with tempfile.TemporaryDirectory() as folder, patch('google.cloud.firestore.transactional', transactional):
            client = Client()
            database = Path(folder) / 'accounts.sqlite3'
            for factory in (lambda: Ledger(database), lambda: FirestoreLedger(client=client)):
                with self.subTest(adapter=type(factory()).__name__):
                    ledger = factory()
                    receipt = {'purchaseState': 0, 'obfuscatedExternalAccountId': 'google-linked-unity-a',
                               'quantity': 1, 'consumptionState': 0}
                    first = ledger.fulfill('google-linked-unity-a', 'dimensions_shards_5500', 'paid-token', receipt)
                    self.assertEqual(first['balance'], 5500)
                    bought = ledger.purchase('google-linked-unity-a', 'voidfall', 'a' * 32, 0)
                    self.assertTrue(bought['voidfall'])
                    self.assertEqual(bought['balance'], 3500)
                    # Google has consumed the receipt after durable delivery; a lost response
                    # or restored client must not fail it or credit the original pack twice.
                    receipt['consumptionState'] = 1
                    restored = factory()
                    replay = restored.fulfill('google-linked-unity-a', 'dimensions_shards_5500', 'paid-token', receipt)
                    self.assertEqual(replay['balance'], 3500)
                    self.assertTrue(replay['voidfall'])
                    self.assertEqual(restored.wallet('google-linked-unity-a'), replay)
                    self.assertEqual(restored.wallet('different-unity-account')['balance'], 0)
                    self.assertFalse(restored.wallet('different-unity-account')['voidfall'])
                    with self.assertRaises(ValueError):
                        restored.fulfill('different-unity-account', 'dimensions_shards_5500', 'paid-token', receipt)
                    with self.assertRaises(ValueError):
                        restored.fulfill('google-linked-unity-a', 'dimensions_shards_5500', 'unknown-consumed-token', receipt)


if __name__ == '__main__':
    unittest.main()
