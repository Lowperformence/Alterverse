"""BOLT-7 shard unlocks through SQLite and the strict Firestore transaction fixture."""
import io,json,tempfile,unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from unittest.mock import patch
from application import CommerceApplication
from ledger import Ledger, ITEMS, ProfileConflict
from firestore_ledger import FirestoreLedger
from test_firestore_social import Client, transactional
SKU='hero_bolt7';KEY='DF_HeroOwned_Bolt7'
def number(key,value):return {'key':key,'kind':0,'number':value}
def value(wallet,key):return next((e.get('number') for e in wallet['profile'] if e['key']==key),None)
class Bolt7PurchaseTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.database=Path(self.tmp.name)/'bolt7.db';self.client=Client()
  self.tx=patch('google.cloud.firestore.transactional',transactional);self.tx.start()
  self.factories=(lambda:Ledger(self.database),lambda:FirestoreLedger(client=self.client))
 def tearDown(self):self.tx.stop();self.tmp.cleanup()
 def fund(self,ledger,earned,paid=False):
  wallet=ledger.wallet('account-a')
  if paid:wallet=ledger.fulfill('account-a','dimensions_shards_1200','bolt-token',{'purchaseState':0,'obfuscatedExternalAccountId':'account-a'})
  return ledger.save_profile('account-a',[number('DF_Shards',earned)],wallet['profileRevision'])
 def test_catalog_price_release_and_permanent_grant(self):
  self.assertEqual(ITEMS[SKU]['price'],1800);self.assertIs(ITEMS[SKU].get('available',True),True)
  self.assertEqual(ITEMS[SKU]['writes'],[number(KEY,1)]);self.assertFalse(ITEMS[SKU].get('consumable',False))
 def test_exact_earned_price_grants_and_spends_once(self):
  for factory in self.factories:
   ledger=factory()
   with self.subTest(adapter=type(ledger).__name__):
    before=self.fund(ledger,1800);after=ledger.purchase('account-a',SKU,'a'*32,before['profileRevision'])
    self.assertEqual(value(after,'DF_Shards'),0);self.assertEqual(after['balance'],0)
    self.assertEqual(value(after,KEY),1);self.assertIn(SKU,after['owned'])
    retry=ledger.purchase('account-a',SKU,'a'*32,before['profileRevision']);self.assertEqual(retry['balance'],after['balance']);self.assertEqual(value(retry,KEY),1);self.assertEqual(value(retry,'DF_Shards'),0)
    again=ledger.purchase('account-a',SKU,'b'*32,retry['profileRevision'])
    self.assertEqual(again['balance'],0);self.assertEqual(value(again,'DF_Shards'),0)
 def test_mixed_wallet_retry_restore_and_account_isolation(self):
  for factory in self.factories:
   ledger=factory()
   with self.subTest(adapter=type(ledger).__name__):
    before=self.fund(ledger,700,paid=True);after=ledger.purchase('account-a',SKU,'a'*32,before['profileRevision'])
    self.assertEqual(after['balance'],100);self.assertEqual(value(after,'DF_Shards'),0);self.assertEqual(value(after,KEY),1)
    restarted=factory();restored=restarted.wallet('account-a')
    self.assertEqual(value(restored,KEY),1);self.assertIn(SKU,restored['owned'])
    retry=restarted.purchase('account-a',SKU,'a'*32,before['profileRevision']);self.assertEqual(retry['balance'],100);self.assertEqual(value(retry,KEY),1)
    saved=restarted.save_profile('account-a',[number('DF_Shards',0)],restored['profileRevision'])
    self.assertEqual(value(saved,KEY),1);self.assertEqual(saved['balance'],100)
    other=restarted.wallet('account-b');self.assertNotIn(SKU,other['owned']);self.assertIsNone(value(other,KEY))
 def test_paid_only_purchase_uses_verified_wallet(self):
  for factory in self.factories:
   ledger=factory()
   with self.subTest(adapter=type(ledger).__name__):
    ledger.fulfill('account-a','dimensions_shards_5500','paid-bolt-token',{'purchaseState':0,'obfuscatedExternalAccountId':'account-a'})
    before=ledger.wallet('account-a');after=ledger.purchase('account-a',SKU,'a'*32,before['profileRevision'])
    self.assertEqual(after['balance'],3700);self.assertEqual(value(after,KEY),1)
 def test_1799_shards_grants_nothing_and_changes_nothing(self):
  for factory in self.factories:
   ledger=factory()
   with self.subTest(adapter=type(ledger).__name__):
    before=self.fund(ledger,1799)
    with self.assertRaisesRegex(ValueError,'Insufficient shards'):ledger.purchase('account-a',SKU,'a'*32,before['profileRevision'])
    self.assertEqual(ledger.wallet('account-a'),before)
 def test_concurrent_purchase_cannot_double_charge(self):
  for factory in self.factories:
   ledger=factory()
   with self.subTest(adapter=type(ledger).__name__):
    before=self.fund(ledger,4000)
    def buy(operation):
     try:return ledger.purchase('account-a',SKU,operation,before['profileRevision'])
     except ProfileConflict:return None
    with ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(buy,('a'*32,'b'*32)))
    after=ledger.wallet('account-a');self.assertEqual(value(after,'DF_Shards'),2200);self.assertEqual(value(after,KEY),1)
 def test_http_flow_ignores_client_price_and_unlock_payload(self):
  for factory in self.factories:
   ledger=factory()
   with self.subTest(adapter=type(ledger).__name__):
    before=self.fund(ledger,2000);app=CommerceApplication(ledger,lambda _:'account-a',None,'')
    payload=json.dumps({'cosmeticId':SKU,'operationId':'a'*32,'expectedProfileRevision':before['profileRevision'],'price':1,'writes':[number('DF_Shards',999999)]}).encode();statuses=[]
    response=b''.join(app({'REQUEST_METHOD':'POST','PATH_INFO':'/wallet/spend','CONTENT_LENGTH':str(len(payload)),'wsgi.input':io.BytesIO(payload),'HTTP_AUTHORIZATION':'Bearer fixture'},lambda status,headers:statuses.append(status)))
    self.assertEqual(statuses,['200 OK']);wallet=json.loads(response)['wallet']
    self.assertEqual(value(wallet,'DF_Shards'),200);self.assertEqual(value(wallet,KEY),1);self.assertEqual(wallet['operationId'],'a'*32)
if __name__=='__main__':unittest.main()
