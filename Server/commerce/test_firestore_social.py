"""Run shared social scenarios through Firestore transactions with a strict fake.
No production project is accessed. This checks adapter semantics, not service/emulator integration.
"""
import copy
import functools
import threading
from unittest.mock import patch
import test_social
from firestore_ledger import FirestoreLedger
from firestore_social import FirestoreSocial


class Snapshot:
    def __init__(self,ref,value):
        self.reference=ref;self.id=ref.id;self.exists=value is not None;self.value=copy.deepcopy(value)
    def to_dict(self): return copy.deepcopy(self.value)


class Document:
    def __init__(self,client,path): self.client=client;self.path=path;self.id=path.split('/')[-1]
    def get(self,transaction=None):
        data=transaction.read() if transaction else self.client.data
        return Snapshot(self,data.get(self.path))
    def collection(self,name): return Collection(self.client,self.path+'/'+name)


class Collection:
    def __init__(self,client,path,filters=(),orders=(),cap=None,skip=0):
        self.client=client;self.path=path;self.id=path.split('/')[-1];self.filters=filters;self.orders=orders;self.cap=cap;self.skip=skip
    def document(self,name): return Document(self.client,self.path+'/'+name)
    def where(self,field,operator,value):
        assert operator in ('==','<')
        return Collection(self.client,self.path,self.filters+((field,operator,value),),self.orders,self.cap,self.skip)
    def order_by(self,field,direction='ASCENDING'):
        return Collection(self.client,self.path,self.filters,self.orders+((field,direction),),self.cap,self.skip)
    def limit(self,count): return Collection(self.client,self.path,self.filters,self.orders,count,self.skip)
    def offset(self,count): return Collection(self.client,self.path,self.filters,self.orders,self.cap,count)
    def stream(self,transaction=None):
        data=transaction.read() if transaction else self.client.data
        prefix=self.path+'/'
        matches=[]
        for path,value in sorted(data.items()):
            if path.startswith(prefix) and '/' not in path[len(prefix):] and all(value.get(k)==v if op=='==' else value.get(k,0)<v for k,op,v in self.filters):
                matches.append((path,value))
        for field,direction in reversed(self.orders):matches.sort(key=lambda item:item[1].get(field,0),reverse=direction=='DESCENDING')
        for path,value in matches[self.skip:None if self.cap is None else self.skip+self.cap]:yield Snapshot(Document(self.client,path),value)


class Transaction:
    def __init__(self,client): self.client=client;self.pending={};self.original=None
    def read(self):
        if self.pending: raise RuntimeError('Firestore reads must precede all writes')
        return self.original
    def set(self,ref,value): self.pending[ref.path]=copy.deepcopy(value)
    def delete(self,ref): self.pending[ref.path]=None


class Client:
    def __init__(self): self.data={};self.lock=threading.RLock()
    def collection(self,path): return Collection(self,path)
    def transaction(self): return Transaction(self)
    def batch(self):
        client=self
        class Batch:
            def __init__(self): self.deletions=[]
            def delete(self,ref): self.deletions.append(ref.path)
            def commit(self):
                with client.lock:
                    for path in self.deletions:client.data.pop(path,None)
        return Batch()


def transactional(function):
    @functools.wraps(function)
    def run(tx,*args,**kwargs):
        with tx.client.lock:
            tx.original=copy.deepcopy(tx.client.data);tx.pending={}
            result=function(tx,*args,**kwargs)
            for path,value in tx.pending.items():
                if value is None: tx.client.data.pop(path,None)
                else: tx.client.data[path]=value
            return result
    return run


class FirestoreSocialTests(test_social.SocialTests):
    def setUp(self):
        self.client=Client()
        self.decorator=patch('google.cloud.firestore.transactional',transactional)
        self.decorator.start()
        super().setUp()

    def tearDown(self):
        super().tearDown();self.decorator.stop()

    def make_ledger(self):
        ledger=FirestoreLedger(client=self.client)
        original=ledger.save_profile
        def save(account,entries,revision):
            result=original(account,entries,revision)
            FirestoreSocial(ledger).sync_wallet(account,result)
            return result
        ledger.save_profile=save
        return ledger

    def make_social(self,ledger): return FirestoreSocial(ledger, chat_config=test_social.ChatConfig(enabled=False))

