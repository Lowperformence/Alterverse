"""Real SQLite purchase handling for the two BOLT-7 weapon products."""
import tempfile,unittest
from pathlib import Path
from ledger import Ledger,ITEMS

def values(wallet):return {entry['key']:entry.get('number') for entry in wallet['profile']}
class Bolt7WeaponPurchaseTests(unittest.TestCase):
 def test_two_products_use_existing_price_and_hero_keys(self):
  for variant in (0,1):
   item=ITEMS[f'weapon_10_{variant}'];self.assertEqual(item['price'],500)
   self.assertTrue(item.get('available',True));self.assertFalse(item.get('consumable',False))
   writes={w['key']:w['number'] for w in item['writes']};self.assertEqual(writes[f'DF_WeaponSkinOwnedItem_10_{variant}'],1)
   self.assertEqual(writes['DF_WeaponSkinSelected_10'],variant);self.assertEqual(writes['DF_WeaponSkinEquipped_10'],1)
   self.assertTrue(all('_10' in key for key in writes))
 def test_purchase_retry_switch_and_account_isolation(self):
  with tempfile.TemporaryDirectory() as directory:
   ledger=Ledger(Path(directory)/'weapons.db');wallet=ledger.wallet('account-a')
   wallet=ledger.save_profile('account-a',[{'key':'DF_Shards','kind':0,'number':1000}],wallet['profileRevision'])
   for variant in (0,1):
    before=wallet;operation=str(variant+1)*32;sku=f'weapon_10_{variant}'
    wallet=ledger.purchase('account-a',sku,operation,before['profileRevision'])
    self.assertEqual(values(wallet)['DF_Shards'],500*(1-variant));self.assertEqual(values(wallet)[f'DF_WeaponSkinOwnedItem_10_{variant}'],1)
    self.assertEqual(values(wallet)['DF_WeaponSkinSelected_10'],variant)
    retry=ledger.purchase('account-a',sku,operation,before['profileRevision']);self.assertEqual(values(retry),values(wallet))
   self.assertNotIn('DF_WeaponSkinOwnedItem_10_0',values(ledger.wallet('account-b')))
   self.assertNotIn('DF_WeaponSkinOwnedItem_0_0',values(wallet))
 def test_insufficient_balance_preserves_funds(self):
  with tempfile.TemporaryDirectory() as directory:
   ledger=Ledger(Path(directory)/'weapons.db');wallet=ledger.wallet('account-a')
   wallet=ledger.save_profile('account-a',[{'key':'DF_Shards','kind':0,'number':499}],wallet['profileRevision'])
   with self.assertRaises(ValueError):ledger.purchase('account-a','weapon_10_0','a'*32,wallet['profileRevision'])
   after=ledger.wallet('account-a');self.assertEqual(values(after)['DF_Shards'],499)
   self.assertNotIn('DF_WeaponSkinOwnedItem_10_0',values(after))

if __name__=='__main__':unittest.main()

