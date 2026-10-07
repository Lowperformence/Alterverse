"""Firestore persistence adapter for the shared, tested social rules.

Each request materializes only its relationship/clan scope into disposable SQLite,
then applies row differences inside a Firestore transaction. Directory searches and
rankings scan public metadata, never private cloud profiles. Actor/target/clan locks
serialize quota and membership decisions across Cloud Run instances. Unique clan
name/tag documents protect even concurrent creation of previously absent names.
"""
import hashlib
import json
import secrets
import tempfile
import unicodedata
from pathlib import Path
from google.cloud import firestore
from ledger import Ledger
from social import Social, public_values

TABLES = {
    'social_player': ('account','player_id','name','name_search','xp','level','avatar'),
    'social_friend': ('a','b','sender','accepted'),
    'social_block': ('account','target'),
    'social_clan': ('id','name','name_search','tag','description','open'),
    'social_member': ('account','clan','role'),
    'social_application': ('account','clan'),
    'social_rate': ('account','window','count'),
}
KEYS = {'social_player':('account',),'social_friend':('a','b'),'social_block':('account','target'),
        'social_clan':('id',),'social_member':('account',),'social_application':('account','clan'),
        'social_rate':('account',)}
READ_ROUTES = {'/social/state','/social/players','/social/levels','/social/clans','/social/clan-levels'}


def key_for(table, row):
    parts = [row[k] for k in KEYS[table]]
    if len(parts) == 1:
        return parts[0]
    return hashlib.sha256(json.dumps(parts, separators=(',',':')).encode()).hexdigest()


class FirestoreSocial:
    def __init__(self, ledger):
        self.ledger = ledger
        self.client = ledger.client
        prefix = ledger.accounts.id.removesuffix('-accounts')
        self.collections = {table:self.client.collection(prefix+'-'+table.replace('_','-')) for table in TABLES}
        self.unique = self.client.collection(prefix+'-social-clan-unique')

    def sync_wallet(self, account, wallet=None):
        """Read current committed profile, so an older HTTP reply cannot rewind directory XP."""
        @firestore.transactional
        def sync(tx):
            profile = self.ledger.accounts.document(account).get(transaction=tx)
            ref = self.collections['social_player'].document(account)
            snap = ref.get(transaction=tx)
            old = snap.to_dict() if snap.exists else {}
            values = public_values(self.ledger._data(profile)['profile'])
            row = dict(zip(TABLES['social_player'], (account,old.get('player_id') or secrets.token_hex(10),*values)))
            if row != old:
                tx.set(ref,row)
        sync(self.client.transaction())

    def dispatch(self, account, route, data):
        # Shared rules own input validation and all account-derived authorization.
        @firestore.transactional
        def apply(tx):
            rows = {table:{} for table in TABLES}
            def read(table, identifier):
                if identifier in rows[table]:
                    return rows[table][identifier]
                snapshot = self.collections[table].document(identifier).get(transaction=tx)
                if snapshot.exists:
                    value = snapshot.to_dict()
                    rows[table][identifier] = {c:value[c] for c in TABLES[table]}
                    return rows[table][identifier]
                return None
            def query(table, field=None, value=None):
                collection = self.collections[table]
                source = collection.where(field,'==',value) if field else collection
                for snapshot in source.stream(transaction=tx):
                    value = snapshot.to_dict()
                    rows[table][snapshot.id] = {c:value[c] for c in TABLES[table]}

            profile = self.ledger.accounts.document(account).get(transaction=tx)
            caller_profile = self.ledger._data(profile)['profile']
            caller = read('social_player',account)
            target = None
            player_id = data.get('playerId')
            if isinstance(player_id,str) and len(player_id)==20:
                query('social_player','player_id',player_id)
                target = next((p['account'] for p in rows['social_player'].values() if p['player_id']==player_id),None)
            actors = {account}
            if target: actors.add(target)
            for who in actors:
                read('social_member',who)
                for field in ('a','b'): query('social_friend',field,who)
                for field in ('account','target'): query('social_block',field,who)
                query('social_application','account',who)
            read('social_rate',account)
            clans = {r['clan'] for r in rows['social_member'].values()}
            identifier = data.get('clanId')
            if isinstance(identifier,str) and len(identifier)==20: clans.add(identifier)
            for identifier in clans:
                read('social_clan',identifier)
                query('social_member','clan',identifier)
                query('social_application','clan',identifier)
            # Applicants and friends need public values, not another account's private profile.
            accounts = set(actors)
            for relation in rows['social_friend'].values(): accounts.update((relation['a'],relation['b']))
            for relation in rows['social_block'].values(): accounts.update((relation['account'],relation['target']))
            for relation in rows['social_member'].values(): accounts.add(relation['account'])
            for relation in rows['social_application'].values(): accounts.add(relation['account'])
            for who in accounts:
                read('social_player',who)
                # Required to enforce one-clan membership when accepting an applicant.
                read('social_member',who)
            if route in ('/social/levels','/social/players'):
                query('social_player')
            if route in ('/social/clans','/social/clan-levels'):
                query('social_clan');query('social_member');query('social_player')

            unique_refs = {}
            def lock_unique(kind,value):
                unique_id = kind+'-'+hashlib.sha256(value.encode()).hexdigest()
                ref = self.unique.document(unique_id)
                snap = ref.get(transaction=tx)
                unique_refs[unique_id] = (ref,snap.to_dict() if snap.exists else None)
                return snap.to_dict() if snap.exists else None
            if route == '/social/clan/create':
                name,tag = data.get('name'),data.get('tag')
                if isinstance(name,str) and isinstance(tag,str):
                    for kind,value in [('name',unicodedata.normalize('NFKC',name).strip().casefold()),('tag',tag.upper())]:
                        locked = lock_unique(kind,value)
                        if locked: read('social_clan',locked['clan'])
            for clan in list(rows['social_clan'].values()):
                lock_unique('name',clan['name_search']);lock_unique('tag',clan['tag'])

            before = {table:dict(values) for table,values in rows.items()}
            with tempfile.TemporaryDirectory(prefix='df-social-') as directory:
                ledger = Ledger(Path(directory)/'scope.sqlite3')
                rules = Social(ledger)
                with ledger.connect() as db:
                    for table,columns in TABLES.items():
                        for row in rows[table].values():
                            db.execute('INSERT INTO '+table+' ('+','.join(columns)+') VALUES ('+','.join('?' for _ in columns)+')',tuple(row[c] for c in columns))
                    db.execute('INSERT INTO profile(account,data) VALUES(?,?)',(account,json.dumps(caller_profile)))
                result = rules.dispatch(account,route,data)
                after = {}
                with ledger.connect() as db:
                    for table,columns in TABLES.items():
                        values = [dict(zip(columns,r)) for r in db.execute('SELECT '+','.join(columns)+' FROM '+table)]
                        after[table] = {key_for(table,r):r for r in values}
            # All reads above finish before any Firestore write is queued.
            for table in TABLES:
                for identifier in before[table].keys()-after[table].keys():
                    tx.delete(self.collections[table].document(identifier))
                for identifier,row in after[table].items():
                    if before[table].get(identifier)!=row:
                        tx.set(self.collections[table].document(identifier),row)
            if route not in READ_ROUTES:
                # These document writes guard caps/unique membership against query phantoms.
                for who in actors:
                    row = after['social_player'].get(who)
                    if row: tx.set(self.collections['social_player'].document(who),row)
                for identifier in clans:
                    row = after['social_clan'].get(identifier)
                    if row: tx.set(self.collections['social_clan'].document(identifier),row)
            for identifier in before['social_clan'].keys()-after['social_clan'].keys():
                clan = before['social_clan'][identifier]
                for kind,value in [('name',clan['name_search']),('tag',clan['tag'])]:
                    unique_id = kind+'-'+hashlib.sha256(value.encode()).hexdigest()
                    tx.delete(unique_refs[unique_id][0])
            for identifier in after['social_clan'].keys()-before['social_clan'].keys():
                clan = after['social_clan'][identifier]
                for kind,value in [('name',clan['name_search']),('tag',clan['tag'])]:
                    unique_id = kind+'-'+hashlib.sha256(value.encode()).hexdigest()
                    tx.set(unique_refs[unique_id][0],{'clan':identifier})
            return result
        return apply(self.client.transaction())


if __name__ == '__main__':
    # Optional one-off indexing of cloud profiles created before social deployment.
    import os
    from firestore_ledger import FirestoreLedger
    ledger = FirestoreLedger(project_id=os.environ.get('DF_FIRESTORE_PROJECT_ID'),namespace=os.environ.get('DF_FIRESTORE_NAMESPACE','dimensions-front'))
    social = FirestoreSocial(ledger)
    for snapshot in ledger.accounts.stream():
        social.sync_wallet(snapshot.id)
