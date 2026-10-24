"""Loopback-only HTTP integration fixture. No cloud credentials or real data."""
import hashlib
import json
from unittest.mock import patch
from wsgiref.simple_server import make_server, WSGIRequestHandler

from clan_chat import ChatConfig
from firestore_ledger import FirestoreLedger
from firestore_social import FirestoreSocial
from node_social import NodeAccountLedger, NodeSocialApplication
from test_firestore_social import Client, transactional
from test_node_social import account

client=Client()
patch('google.cloud.firestore.transactional',transactional).start()
alice=hashlib.sha256(b'alice').hexdigest();bob=hashlib.sha256(b'bob').hexdigest()
legacy=FirestoreLedger(namespace='http-fixture',client=client)
legacy.save_profile(bob,account(bob)['profile'],0)
old_social=FirestoreSocial(legacy,chat_config=ChatConfig(enabled=False))
old=old_social.dispatch(bob,'/social/clan/create',{
    'operationId':'5'*32,'expectedProfileRevision':legacy.wallet(bob)['profileRevision'],
    'descriptionId':0,'open':True})
client.data['players/'+alice]=account(alice,earned=200)
client.data['players/'+bob]=client.data['http-fixture-accounts/'+bob].copy()
ledger=NodeAccountLedger('http-fixture',client=client)
ledger.verify_existing_clans()
app=NodeSocialApplication(ledger,lambda token:alice if token=='Bearer alice-token' else None,chat_config=ChatConfig(enabled=False))

class Quiet(WSGIRequestHandler):
    def log_message(self,*args):pass

with make_server('127.0.0.1',0,app,handler_class=Quiet) as server:
    print(json.dumps({'port':server.server_port,'oldClanId':old['clan']['clanId']}),flush=True)
    server.serve_forever()
