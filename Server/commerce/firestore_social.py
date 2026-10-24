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
from safe_identity import (DESCRIPTIONS, valid_clan_name, valid_clan_tag,
                           valid_player_name, profile_name, generate_clan_name, generate_clan_tag)
from pathlib import Path
from google.cloud import firestore
from ledger import Ledger
from social import Social, public_values
from social import SocialForbidden
from clan_chat import (CHAT_TABLES, CHAT_KEYS, ChatConfig, ClanChat, CHAT_VERSION, LEADER_VERSION)
from datetime import datetime, timezone

TABLES = {
    'social_player': ('account','player_id','name','name_search','xp','level','avatar'),
    'social_friend': ('a','b','sender','accepted'),
    'social_block': ('account','target'),
    'social_clan': ('id','name','name_search','tag','description','open','avatar'),
    'social_member': ('account','clan','role'),
    'social_application': ('account','clan'),
    'social_rate': ('account','window','count'),
    'social_creation': ('account','operation_id','clan_id','fingerprint','cost'),
}
KEYS = {'social_player':('account',),'social_friend':('a','b'),'social_block':('account','target'),
        'social_clan':('id',),'social_member':('account',),'social_application':('account','clan'),
        'social_rate':('account',),'social_creation':('account','operation_id')}
READ_ROUTES = {'/social/state','/social/players','/social/levels','/social/clans','/social/clan-levels'}
TABLES.update(CHAT_TABLES)
KEYS.update(CHAT_KEYS)


def key_for(table, row):
    parts = [row[k] for k in KEYS[table]]
    if len(parts) == 1:
        return parts[0]
    return hashlib.sha256(json.dumps(parts, separators=(',',':')).encode()).hexdigest()


class FirestoreSocial:
    def __init__(self, ledger, chat_config=None):
        self.ledger = ledger
        self.chat_config = chat_config or ChatConfig.from_env()
        self.client = ledger.client
        prefix = getattr(ledger, 'social_namespace', None) or ledger.accounts.id.removesuffix('-accounts')
        self.collections = {table:self.client.collection(prefix+'-'+table.replace('_','-')) for table in TABLES}
        self.unique = self.client.collection(prefix+'-social-clan-unique')

    def sync_wallet(self, account, wallet=None):
        """Read current committed profile, so an older HTTP reply cannot rewind directory XP."""
        self.ledger.wallet(account)
        @firestore.transactional
        def sync(tx):
            profile = self.ledger.accounts.document(account).get(transaction=tx)
            ref = self.collections['social_player'].document(account)
            snap = ref.get(transaction=tx)
            old = snap.to_dict() if snap.exists else {}
            values = public_values(self.ledger._data(profile)['profile'], account)
            row = dict(zip(TABLES['social_player'], (account,old.get('player_id') or secrets.token_hex(10),*values)))
            if row != old:
                tx.set(ref,row)
        sync(self.client.transaction())

    def dispatch(self, account, route, data):
        # Public discovery, friend messaging and legacy chat routes stay disabled.
        # Enforce this on the server as well as hiding controls in the Unity UI.
        if (route.startswith(('/social/clan/message', '/social/message', '/social/friend/'))
                or route in ('/social/players', '/social/clan/identity')
                or not self.chat_config.enabled and route in ('/social/block', '/social/unblock')):
            raise ValueError('Communication is disabled in this version.')
        admin_route = route.startswith('/social/chat/admin/')
        if admin_route and account not in self.chat_config.admins:
            raise SocialForbidden('Administrator required')
        if route == '/social/chat/read' and data.get('knownRevision'):
            unchanged = self.heartbeat(account, data)
            if unchanged is not None: return unchanged
        self.ledger.wallet(account)
        # Shared rules own input validation and all account-derived authorization.
        request_data = dict(data)
        @firestore.transactional
        def apply(tx):
            data = dict(request_data)
            rows = {table:{} for table in TABLES}
            def read(table, identifier):
                if identifier in rows[table]:
                    return rows[table][identifier]
                snapshot = self.collections[table].document(identifier).get(transaction=tx)
                if snapshot.exists:
                    value = snapshot.to_dict()
                    rows[table][identifier] = {c:value.get(c,-1) if c=='avatar' and table=='social_clan' else value[c] for c in TABLES[table]}
                    return rows[table][identifier]
                return None
            def query(table, field=None, value=None):
                collection = self.collections[table]
                source = collection.where(field,'==',value) if field else collection
                for snapshot in source.stream(transaction=tx):
                    value = snapshot.to_dict()
                    rows[table][snapshot.id] = {c:value.get(c,-1) if c=='avatar' and table=='social_clan' else value[c] for c in TABLES[table]}

            profile = self.ledger.accounts.document(account).get(transaction=tx)
            caller_account = self.ledger._data(profile)
            caller_profile = caller_account['profile']
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
            query('social_creation','account',account)
            clans = {r['clan'] for r in rows['social_member'].values()}
            identifier = data.get('clanId')
            if isinstance(identifier,str) and len(identifier)==20: clans.add(identifier)
            for identifier in clans:
                read('social_clan',identifier)
                query('social_member','clan',identifier)
                query('social_application','clan',identifier)
            # Sensitive chat history is scoped using authoritative membership before
            # querying any messages. An injected foreign clan ID never loads its room.
            own_member = rows['social_member'].get(account)
            if route.startswith('/social/chat/') and not admin_route and route not in ('/social/chat/consent', '/social/chat/leader-consent'):
                if not own_member or data.get('clanId') not in (None, '', own_member['clan']):
                    raise SocialForbidden('Private clan room')
            for who in actors:
                query('chat_consent', 'account', who)
                query('chat_rate', 'account', who)
                read('chat_user_ban', who)
            for identifier in clans:
                query('chat_sanction', 'clan', identifier)
                read('chat_room', identifier)
            # Leadership transfer checks the recipient's recorded agreement too.
            for who in actors:
                query('chat_consent', 'account', who)
            if route in ('/social/chat/read', '/social/chat/send', '/social/chat/report', '/social/chat/moderate'):
                room = own_member['clan']
                source = self.collections['social_message'].where('clan', '==', room).order_by('created_at', direction=firestore.Query.DESCENDING).limit(100)
                for snapshot in source.stream(transaction=tx):
                    value = snapshot.to_dict()
                    rows['social_message'][snapshot.id] = {c:value[c] for c in TABLES['social_message']}
                message_id = data.get('messageId')
                if isinstance(message_id, str) and len(message_id) == 20:
                    row = read('social_message', message_id)
                    if row and row['clan'] != room: raise SocialForbidden('Private clan room')
                operation = data.get('operationId')
                if route == '/social/chat/send' and isinstance(operation, str) and len(operation) == 20:
                    read('social_message', hashlib.sha256((account + ':' + operation).encode()).hexdigest()[:20])
                if route == '/social/chat/report':
                    subject = message_id or target
                    if subject:
                        report_id = hashlib.sha256(json.dumps([account, room, subject]).encode()).hexdigest()[:20]
                        read('chat_report', report_id)
                        read('chat_outbox', report_id)
            if admin_route:
                if route == '/social/chat/admin/reports':
                    offset = data.get('offset', 0)
                    if type(offset) is not int or not 0 <= offset <= 1000: raise ValueError('Invalid report page')
                    source = self.collections['chat_report'].order_by('created_at', direction=firestore.Query.DESCENDING).offset(offset).limit(8)
                    for snapshot in source.stream(transaction=tx):
                        value = snapshot.to_dict()
                        rows['chat_report'][snapshot.id] = {c:value[c] for c in TABLES['chat_report']}
                    # Scoped SQLite contains only this page; pagination is applied here.
                    data = dict(data, offset=0)
                else:
                    report_id = data.get('reportId')
                    if isinstance(report_id, str) and len(report_id) == 20:
                        report = read('chat_report', report_id)
                        if report:
                            target = report['target']; actors.add(target);clans.add(report['clan'])
                            read('social_message', report['message_id']) if report['message_id'] else None
                for identifier in clans:
                    read('social_clan', identifier)
                    read('chat_room', identifier)
                    query('social_member', 'clan', identifier)
                    query('social_application', 'clan', identifier)
                for who in actors:
                    read('social_member', who);read('chat_user_ban', who)
                    query('chat_consent', 'account', who)
                if data.get('action') == 'remove' and isinstance(data.get('messageId'), str) and len(data['messageId']) == 20:
                    read('social_message', data['messageId'])
            # Applicants and friends need public values, not another account's private profile.
            accounts = set(actors)
            for relation in rows['social_friend'].values(): accounts.update((relation['a'],relation['b']))
            for relation in rows['social_block'].values(): accounts.update((relation['account'],relation['target']))
            for relation in rows['social_member'].values(): accounts.add(relation['account'])
            for relation in rows['social_application'].values(): accounts.add(relation['account'])
            for message in rows['social_message'].values(): accounts.add(message['account'])
            for report in rows['chat_report'].values(): accounts.update((report['target'], report['reporter']))
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
            generated_clan_identity = None
            if route == '/social/clan/create':
                # The request may contain a fabricated name, but only this
                # server-generated pair is used by shared rules and unique claims.
                for _ in range(64):
                    name, tag = generate_clan_name(), generate_clan_tag()
                    name_claim = lock_unique('name', name.casefold())
                    tag_claim = lock_unique('tag', tag)
                    if not name_claim and not tag_claim:
                        generated_clan_identity = (name, tag)
                        break
                if generated_clan_identity is None:
                    raise ValueError('Clan identity allocation unavailable')
            elif route == '/social/clan/identity':
                # Old identity endpoints are already denied, including on the server.
                raise ValueError('Communication is disabled in this version.')
            for clan in list(rows['social_clan'].values()):
                lock_unique('name',clan['name_search']);lock_unique('tag',clan['tag'])

            before = {table:dict(values) for table,values in rows.items()}
            staged_names = {c['name_search'] for c in rows['social_clan'].values()}
            staged_tags = {c['tag'] for c in rows['social_clan'].values()}
            # Lazily migrate legacy clan metadata in the same transaction as its
            # globally indexed name/tag claims. No public free text survives reads.
            for identifier, original in list(rows['social_clan'].items()):
                clan = dict(original)
                if not valid_clan_name(clan['name']):
                    for _ in range(64):
                        candidate = generate_clan_name()
                        locked = lock_unique('name', candidate.casefold())
                        if (not locked or locked['clan'] == identifier) and candidate.casefold() not in staged_names:
                            staged_names.add(candidate.casefold())
                            clan['name'] = candidate
                            clan['name_search'] = candidate.casefold()
                            break
                    else:
                        raise ValueError('Clan name allocation unavailable')
                if not valid_clan_tag(clan['tag']):
                    for _ in range(64):
                        candidate = generate_clan_tag()
                        locked = lock_unique('tag', candidate)
                        if (not locked or locked['clan'] == identifier) and candidate not in staged_tags:
                            staged_tags.add(candidate)
                            clan['tag'] = candidate
                            break
                    else:
                        raise ValueError('Clan tag allocation unavailable')
                if clan['description'] not in DESCRIPTIONS:
                    clan['description'] = DESCRIPTIONS[0]
                rows['social_clan'][identifier] = clan
            with tempfile.TemporaryDirectory(prefix='df-social-') as directory:
                ledger = Ledger(Path(directory)/'scope.sqlite3')
                rules = Social(ledger, generated_clan_identity=generated_clan_identity, chat_config=self.chat_config)
                with ledger.connect() as db:
                    for table,columns in TABLES.items():
                        for row in rows[table].values():
                            db.execute('INSERT INTO '+table+' ('+','.join(columns)+') VALUES ('+','.join('?' for _ in columns)+')',tuple(row[c] for c in columns))
                    db.execute('INSERT INTO profile(account,revision,data) VALUES(?,?,?)',(account,caller_account['profileRevision'],json.dumps(caller_profile)))
                    # Scoped SQLite must honor the cloud server's existing name
                    # rather than generating a new name for each Firestore request.
                    cloud_name = profile_name(caller_profile)
                    if valid_player_name(cloud_name):
                        db.execute('INSERT INTO safe_player_name(account,name) VALUES(?,?)',
                                   (account, cloud_name))
                    db.execute('INSERT INTO wallet(account,balance,revision) VALUES(?,?,?)',(account,caller_account['balance'],caller_account['revision']))
                    for sku in caller_account['owned']:
                        db.execute('INSERT INTO cosmetic(account,sku) VALUES(?,?)',(account,sku))
                result = rules.dispatch(account,route,data)
                if route == '/social/chat/admin/reports': result['offset'] = request_data.get('offset', 0)
                after = {}
                with ledger.connect() as db:
                    for table,columns in TABLES.items():
                        values = [dict(zip(columns,r)) for r in db.execute('SELECT '+','.join(columns)+' FROM '+table)]
                        after[table] = {key_for(table,r):r for r in values}
            # Clan creation and its earned/paid debit commit in this same transaction.
            if route=='/social/clan/create' and result.get('wallet') is not None:
                wallet=result['wallet']
                updated=dict(caller_account,balance=wallet['balance'],revision=wallet['revision'],profileRevision=wallet['profileRevision'],profile=wallet['profile'])
                if updated!=caller_account:
                    # Account adapters may retain metadata beyond the ledger fields.
                    tx.set(self.ledger.accounts.document(account),dict(profile.to_dict() or {}, **updated))
            # All reads above finish before any Firestore write is queued.
            for table in TABLES:
                for identifier in before[table].keys()-after[table].keys():
                    tx.delete(self.collections[table].document(identifier))
                for identifier,row in after[table].items():
                    if before[table].get(identifier)!=row:
                        persisted = dict(row)
                        if table in ('social_message', 'chat_report', 'chat_audit'):
                            days = self.chat_config.message_days if table == 'social_message' else self.chat_config.report_days
                            persisted['expiresAt'] = datetime.fromtimestamp((row['created_at'] + days * 86400000) / 1000, timezone.utc)
                        if table == 'chat_outbox' and identifier in after['chat_report']:
                            persisted['expiresAt'] = datetime.fromtimestamp((after['chat_report'][identifier]['created_at'] + self.chat_config.report_days * 86400000) / 1000, timezone.utc)
                        tx.set(self.collections[table].document(identifier),persisted)
            if route not in READ_ROUTES:
                # These document writes guard caps/unique membership against query phantoms.
                for who in actors:
                    row = after['social_player'].get(who)
                    if row: tx.set(self.collections['social_player'].document(who),row)
                for identifier in clans:
                    row = after['social_clan'].get(identifier)
                    if row: tx.set(self.collections['social_clan'].document(identifier),row)
            for identifier in before['social_clan'].keys() | after['social_clan'].keys():
                old, new = before['social_clan'].get(identifier), after['social_clan'].get(identifier)
                for kind, field in (('name', 'name_search'), ('tag', 'tag')):
                    old_value = old[field] if old else None
                    new_value = new[field] if new else None
                    if old_value == new_value:
                        continue
                    if old_value is not None:
                        unique_id = kind+'-'+hashlib.sha256(old_value.encode()).hexdigest()
                        ref, claim = unique_refs[unique_id]
                        if claim and claim.get('clan') == identifier:
                            tx.delete(ref)
                    if new_value is not None:
                        unique_id = kind+'-'+hashlib.sha256(new_value.encode()).hexdigest()
                        tx.set(unique_refs[unique_id][0], {'clan':identifier})
            return result
        return apply(self.client.transaction())

    def check_account_allowed(self, account):
        snapshot = self.collections['chat_user_ban'].document(account).get()
        if snapshot.exists:
            until = snapshot.to_dict()['until_at']
            if until == -1 or until > ClanChat.now(): raise SocialForbidden('Account suspended')

    def heartbeat(self, account, data):
        """Cheap unchanged-room poll; recheck membership, ban and versioned consent."""
        if not self.chat_config.enabled: raise ValueError('Communication is disabled in this version.')
        @firestore.transactional
        def check(tx):
            def read(table, identifier):
                snap = self.collections[table].document(identifier).get(transaction=tx)
                return snap.to_dict() if snap.exists else None
            member = read('social_member', account)
            ban = read('chat_user_ban', account)
            if not member or ban and (ban['until_at'] == -1 or ban['until_at'] > ClanChat.now()):
                raise SocialForbidden('Clan access revoked')
            clan = member['clan']
            if data.get('clanId') not in (None, '', clan): raise SocialForbidden('Private clan room')
            kinds = [('chat', CHAT_VERSION)] + ([('leader', LEADER_VERSION)] if member['role'] == 'leader' else [])
            for kind, version in kinds:
                if not read('chat_consent', key_for('chat_consent', dict(account=account, kind=kind, version=version))):
                    raise SocialForbidden('Accept the current terms first')
            room = read('chat_room', clan)
            revision = room['revision'] if room else 'initial'
            if revision == data.get('knownRevision'):
                return dict(unchanged=True, chatRevision=revision, clanId=clan,
                            expiresBefore=ClanChat.now() - self.chat_config.message_days * 86400000)
            return None
        return check(self.client.transaction())


if __name__ == '__main__':
    # Optional one-off indexing of cloud profiles created before social deployment.
    import os
    from firestore_ledger import FirestoreLedger
    ledger = FirestoreLedger(project_id=os.environ.get('DF_FIRESTORE_PROJECT_ID'),namespace=os.environ.get('DF_FIRESTORE_NAMESPACE','dimensions-front'))
    social = FirestoreSocial(ledger)
    for snapshot in ledger.accounts.stream():
        social.sync_wallet(snapshot.id)
