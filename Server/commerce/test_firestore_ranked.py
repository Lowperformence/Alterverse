"""The same ranked scenarios using strict Firestore transaction substitutes."""
from unittest.mock import patch
from test_firestore_social import Client, Collection, transactional
from firestore_ledger import FirestoreLedger
import test_ranked


class Query:
    def __init__(self, collection, field, direction):
        self.collection, self.field, self.reverse, self.count = collection, field, direction=='DESCENDING', 50
    def limit(self,count): self.count=count; return self
    def stream(self):
        return iter(sorted(self.collection.stream(), key=lambda s:(s.to_dict()[self.field],s.id),reverse=self.reverse)[:self.count])


class FirestoreRankedTests(test_ranked.RankedTests):
    def setUp(self):
        self.client = Client()
        self.tx_patch = patch('google.cloud.firestore.transactional', transactional)
        self.query_patch = patch.object(Collection, 'order_by', lambda c,field,direction:Query(c,field,direction),create=True)
        self.tx_patch.start(); self.query_patch.start()
        super().setUp()
    def make_ledger(self): return FirestoreLedger(client=self.client)
    def tearDown(self):
        super().tearDown(); self.tx_patch.stop(); self.query_patch.stop()
