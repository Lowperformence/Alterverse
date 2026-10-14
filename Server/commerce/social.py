"""Authenticated public directory, friendships and clans in the account database.
Public IDs are random and expose neither Unity subjects nor commerce account hashes.
XP remains saved client progress, not a verified competitive score.
"""
import hashlib
import json
import re
import secrets
import time

from ledger import ITEMS, ProfileConflict
from clan_chat import ClanChat, ChatConfig, initialize as initialize_chat
from safe_identity import (DESCRIPTIONS, normalize_sqlite_profile,
                           visible_player_name, validate_clan_identity, migrate_sqlite_clans,
                           generate_clan_name, generate_clan_tag, description_for)

CLAN_CREATION_COST = 500
MAX_MEMBERS = 50
MAX_FRIENDS = 100
MAX_REQUESTS = 50
MAX_XP = 100 * 999 + 2 * 999 * 999 + (999 ** 3) // 250

class SocialForbidden(Exception):
    pass


def level_for_xp(xp):
    lo, hi = 1, 1000
    while lo < hi:
        mid = (lo + hi + 1) // 2
        n = mid - 1
        if 100 * n + 2 * n * n + n ** 3 // 250 <= xp:
            lo = mid
        else:
            hi = mid - 1
    return lo


def public_values(entries, identity='player'):
    entries = {e['key']: e for e in entries}
    name = entries.get('DF_PlayerName', {}).get('text', 'Traveler')
    name = visible_player_name(name, identity)
    raw = entries.get('DF_AccountXP', {}).get('text', '0')
    try:
        # Bound parsing before integer conversion of potentially huge profile strings.
        xp = max(0, min(MAX_XP, int(raw))) if len(raw) <= 20 else 0
    except (ValueError, TypeError):
        xp = 0
    avatar = entries.get('DF_Avatar', {}).get('number', 0)
    if type(avatar) is not int or not 0 <= avatar <= 1000:
        avatar = 0
    return name, name.casefold(), xp, level_for_xp(xp), avatar


def sync_player(db, account, encoded):
    name = normalize_sqlite_profile(db, account)
    entries = json.loads(db.execute('SELECT data FROM profile WHERE account=?', (account,)).fetchone()[0])
    name, name_search, xp, level, avatar = public_values(entries, account)
    db.execute('''INSERT INTO social_player(account,player_id,name,name_search,xp,level,avatar)
                  VALUES(?,?,?,?,?,?,?) ON CONFLICT(account) DO UPDATE SET
                  name=excluded.name,name_search=excluded.name_search,xp=excluded.xp,
                  level=excluded.level,avatar=excluded.avatar''',
               (account, secrets.token_hex(10), name, name_search, xp, level, avatar))


class Social:
    def __init__(self, ledger, generated_clan_identity=None, chat_config=None):
        self.ledger = ledger
        self.generated_clan_identity = generated_clan_identity
        self.chat = ClanChat(self, chat_config or ChatConfig.from_env())
        with ledger.connect() as db:
            db.executescript('''
            CREATE TABLE IF NOT EXISTS social_player(
                account TEXT PRIMARY KEY, player_id TEXT UNIQUE NOT NULL,
                name TEXT NOT NULL, name_search TEXT NOT NULL, xp INTEGER NOT NULL,
                level INTEGER NOT NULL, avatar INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS social_level_rank ON social_player(level DESC,xp DESC,player_id);
            CREATE TABLE IF NOT EXISTS social_friend(
                a TEXT NOT NULL,b TEXT NOT NULL,sender TEXT NOT NULL,
                accepted INTEGER NOT NULL DEFAULT 0 CHECK(accepted IN(0,1)),
                PRIMARY KEY(a,b),CHECK(a<b),CHECK(sender=a OR sender=b));
            CREATE TABLE IF NOT EXISTS social_block(
                account TEXT NOT NULL,target TEXT NOT NULL,PRIMARY KEY(account,target),CHECK(account<>target));
            CREATE TABLE IF NOT EXISTS social_clan(
                id TEXT PRIMARY KEY,name TEXT NOT NULL,name_search TEXT NOT NULL UNIQUE,
                tag TEXT NOT NULL UNIQUE,description TEXT NOT NULL,open INTEGER NOT NULL CHECK(open IN(0,1)));
            CREATE TABLE IF NOT EXISTS social_member(
                account TEXT PRIMARY KEY,clan TEXT NOT NULL,role TEXT NOT NULL CHECK(role IN('leader','officer','member')));
            CREATE INDEX IF NOT EXISTS social_member_clan ON social_member(clan);
            CREATE TABLE IF NOT EXISTS social_application(
                account TEXT NOT NULL,clan TEXT NOT NULL,PRIMARY KEY(account,clan));
            CREATE TABLE IF NOT EXISTS social_rate(account TEXT PRIMARY KEY,window INTEGER NOT NULL,count INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS social_creation(account TEXT NOT NULL,operation_id TEXT NOT NULL,clan_id TEXT NOT NULL,fingerprint TEXT NOT NULL,cost INTEGER NOT NULL,PRIMARY KEY(account,operation_id));
            CREATE TABLE IF NOT EXISTS social_message(id TEXT PRIMARY KEY,clan TEXT NOT NULL,account TEXT NOT NULL,name TEXT NOT NULL,text TEXT NOT NULL,created_at INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS social_clan_messages ON social_message(clan,created_at DESC,id DESC);
            ''')
            columns = {r[1] for r in db.execute('PRAGMA table_info(social_clan)')}
            if 'avatar' not in columns:
                db.execute('ALTER TABLE social_clan ADD COLUMN avatar INTEGER NOT NULL DEFAULT -1')
            initialize_chat(db)
            for account, encoded in db.execute('SELECT account,data FROM profile').fetchall():
                sync_player(db, account, encoded)
            migrate_sqlite_clans(db)

    @staticmethod
    def player(db, account):
        row = db.execute('SELECT player_id,name,level,xp,avatar FROM social_player WHERE account=?', (account,)).fetchone()
        if not row:
            raise ValueError('Player not found')
        result = dict(zip(('playerId','name','level','xp','avatar'), row))
        result['name'] = visible_player_name(result['name'], result['playerId'])
        return result

    @staticmethod
    def target(db, data):
        identifier = data.get('playerId')
        if not isinstance(identifier, str) or not re.fullmatch('[a-f0-9]{20}', identifier):
            raise ValueError('Invalid player ID')
        row = db.execute('SELECT account FROM social_player WHERE player_id=?', (identifier,)).fetchone()
        if not row:
            raise ValueError('Player not found')
        return row[0]

    @staticmethod
    def clan_id(data):
        identifier = data.get('clanId')
        if not isinstance(identifier, str) or not re.fullmatch('[a-f0-9]{20}', identifier):
            raise ValueError('Invalid clan ID')
        return identifier

    @staticmethod
    def membership(db, account):
        return db.execute('SELECT clan,role FROM social_member WHERE account=?', (account,)).fetchone()

    @staticmethod
    def page(data):
        offset = data.get('offset', 0)
        if type(offset) is not int or not 0 <= offset <= 1000000:
            raise ValueError('Invalid page')
        return offset

    @staticmethod
    def query(data):
        query = data.get('query', '')
        if not isinstance(query, str) or len(query) > 40:
            raise ValueError('Invalid search')
        return query.strip().casefold()

    def clan(self, db, identifier):
        row = db.execute('SELECT id,name,tag,description,open,avatar FROM social_clan WHERE id=?', (identifier,)).fetchone()
        if not row:
            raise ValueError('Clan not found')
        result = dict(zip(('clanId','name','tag','description','open','avatar'), row))
        result['open'] = bool(result['open'])
        result['descriptionId'] = DESCRIPTIONS.index(result['description'])
        members = []
        for account, role in db.execute("SELECT account,role FROM social_member WHERE clan=? ORDER BY CASE role WHEN 'leader' THEN 0 WHEN 'officer' THEN 1 ELSE 2 END,account", (identifier,)):
            members.append(dict(self.player(db, account), role=role))
        result.update(members=members, memberCount=len(members), totalLevel=sum(p['level'] for p in members))
        return result

    def snapshot(self, db, account):
        # Friend messaging stays disabled; clan chat can expose the caller's blocks.
        friends, incoming, outgoing, blocked = [], [], [], []
        member = self.membership(db, account)
        clan = self.clan(db, member[0]) if member else None
        applications = []
        if member and member[1] in ('leader', 'officer'):
            applications = [self.player(db, row[0]) for row in db.execute('SELECT account FROM social_application WHERE clan=? ORDER BY account', (member[0],))]
        pending = [row[0] for row in db.execute('SELECT clan FROM social_application WHERE account=? ORDER BY clan', (account,))]
        if self.chat.config.enabled:
            blocked = [self.player(db, r[0]) for r in db.execute('SELECT target FROM social_block WHERE account=? ORDER BY target', (account,))]
        return {**self.chat.snapshot(db, account), 'hasClan':member is not None,'clanCreationCost':CLAN_CREATION_COST,'clanProtocolVersion':4 if self.chat.config.enabled else 3,
                'identityProtocolVersion':1,'clanDescriptions':list(DESCRIPTIONS),
                'me': self.player(db, account), 'friends': friends, 'incoming': incoming,
                'outgoing': outgoing, 'blocked': blocked, 'clan': clan, 'role': member[1] if member else '',
                'applications': applications, 'pendingClans': pending}

    def dispatch(self, account, route, data):
        # Public discovery, friend messaging and legacy chat routes stay disabled.
        # Enforce this on the server as well as hiding controls in the Unity UI.
        if (route.startswith(('/social/clan/message', '/social/message', '/social/friend/'))
                or route in ('/social/players', '/social/clan/identity')
                or not self.chat.config.enabled and route in ('/social/block', '/social/unblock')):
            raise ValueError('Communication is disabled in this version.')
        with self.ledger.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.ledger.ensure(db, account)
            self.chat.account_allowed(db, account)
            encoded = db.execute('SELECT data FROM profile WHERE account=?', (account,)).fetchone()[0]
            sync_player(db, account, encoded)
            if route.startswith('/social/chat/'):
                return self.chat.dispatch(db, account, route, data)
            if route == '/social/state':
                return self.snapshot(db, account)
            if route == '/social/players':
                query, offset = self.query(data), self.page(data)
                if len(query) < 2:
                    raise ValueError('Enter at least two characters')
                rows = db.execute('''SELECT account FROM social_player p WHERE account<>?
                    AND (instr(name_search,?)>0 OR player_id=?)
                    AND NOT EXISTS(SELECT 1 FROM social_block b WHERE (b.account=? AND b.target=p.account) OR (b.account=p.account AND b.target=?))
                    ORDER BY name_search,player_id LIMIT 8 OFFSET ?''', (account, query, query, account, account, offset)).fetchall()
                return {'players': [self.player(db, row[0]) for row in rows], 'offset': offset}
            if route == '/social/levels':
                offset = self.page(data)
                rank_sql = 'SELECT account,ROW_NUMBER() OVER(ORDER BY level DESC,xp DESC,player_id) AS rank FROM social_player'
                rows = db.execute(rank_sql + ' ORDER BY rank LIMIT 8 OFFSET ?', (offset,)).fetchall()
                own = db.execute('SELECT rank FROM (' + rank_sql + ') WHERE account=?', (account,)).fetchone()[0]
                return {'players': [dict(self.player(db, row[0]), rank=row[1]) for row in rows],
                        'selfRank': own, 'total': db.execute('SELECT COUNT(*) FROM social_player').fetchone()[0],
                        'me': self.player(db, account), 'offset': offset}
            if route in ('/social/clans', '/social/clan-levels'):
                offset, query = self.page(data), self.query(data)
                sql = '''SELECT c.id,c.name,c.tag,c.description,c.open,c.avatar,COUNT(m.account),COALESCE(SUM(p.level),0) AS score
                    FROM social_clan c LEFT JOIN social_member m ON m.clan=c.id
                    LEFT JOIN social_player p ON p.account=m.account'''
                rank_sql = 'SELECT *,ROW_NUMBER() OVER(ORDER BY score DESC,id) AS rank FROM (' + sql + ' GROUP BY c.id)'
                if route == '/social/clans':
                    rows = db.execute(sql + ' WHERE instr(c.name_search,?)>0 OR instr(lower(c.tag),?)>0 GROUP BY c.id ORDER BY score DESC,c.id LIMIT 8 OFFSET ?', (query, query, offset)).fetchall()
                else:
                    rows = db.execute(rank_sql + ' ORDER BY rank LIMIT 8 OFFSET ?', (offset,)).fetchall()
                clans = [dict(zip(('clanId','name','tag','description','open','avatar','memberCount','totalLevel','rank'), r)) for r in rows]
                for clan in clans:
                    clan['open'] = bool(clan['open'])
                member = self.membership(db, account)
                own = db.execute('SELECT rank FROM (' + rank_sql + ') WHERE id=?', (member[0],)).fetchone()[0] if member else 0
                return {'clans': clans, 'selfRank': own, 'offset': offset,
                        'total': db.execute('SELECT COUNT(*) FROM social_clan').fetchone()[0]}
            allowed = {'/social/friend/request','/social/friend/accept','/social/friend/remove',
                       '/social/block','/social/unblock','/social/clan/create','/social/clan/join',
                       '/social/clan/cancel','/social/clan/accept','/social/clan/reject','/social/clan/leave',
                       '/social/clan/kick','/social/clan/role','/social/clan/transfer',
                       '/social/clan/icon','/social/clan/identity'}
            if route not in allowed:
                raise ValueError('Unknown social route')
            now = int(time.time()) // 60
            rate = db.execute('SELECT window,count FROM social_rate WHERE account=?', (account,)).fetchone()
            count = rate[1] if rate and rate[0] == now else 0
            if count >= 30:
                raise ValueError('Too many actions; try again in a minute')
            db.execute('INSERT INTO social_rate VALUES(?,?,?) ON CONFLICT(account) DO UPDATE SET window=excluded.window,count=excluded.count', (account, now, count + 1))
            if route.startswith('/social/friend/') or route in ('/social/block', '/social/unblock'):
                self.friend_action(db, account, route, data)
            else:
                self.clan_action(db, account, route, data)
            result = self.snapshot(db, account)
            if route == '/social/clan/create':
                result['wallet'] = self.ledger.snapshot(db, account)
            return result

    @staticmethod
    def friend_count(db, account):
        return db.execute('SELECT COUNT(*) FROM social_friend WHERE (a=? OR b=?) AND accepted=1', (account, account)).fetchone()[0]

    def friend_action(self, db, account, route, data):
        other = self.target(db, data)
        if other == account:
            raise ValueError('Cannot select yourself')
        a, b = sorted((account, other))
        if route == '/social/block':
            if not db.execute('SELECT 1 FROM social_block WHERE account=? AND target=?', (account, other)).fetchone() and db.execute('SELECT COUNT(*) FROM social_block WHERE account=?', (account,)).fetchone()[0] >= 200:
                raise ValueError('Block list full')
            db.execute('INSERT OR IGNORE INTO social_block VALUES(?,?)', (account, other))
            db.execute('DELETE FROM social_friend WHERE a=? AND b=?', (a, b))
            for who in (account, other):
                member = self.membership(db, who)
                if member: self.chat.touch(db, member[0])
            return
        if route == '/social/unblock':
            db.execute('DELETE FROM social_block WHERE account=? AND target=?', (account, other))
            for who in (account, other):
                member = self.membership(db, who)
                if member: self.chat.touch(db, member[0])
            return
        if route == '/social/friend/remove':
            db.execute('DELETE FROM social_friend WHERE a=? AND b=?', (a, b))
            return
        if db.execute('SELECT 1 FROM social_block WHERE (account=? AND target=?) OR (account=? AND target=?)', (account, other, other, account)).fetchone():
            raise ValueError('Player unavailable')
        row = db.execute('SELECT sender,accepted FROM social_friend WHERE a=? AND b=?', (a, b)).fetchone()
        if row and row[1]:
            return
        if self.friend_count(db, account) >= MAX_FRIENDS or self.friend_count(db, other) >= MAX_FRIENDS:
            raise ValueError('Friend list full')
        if route == '/social/friend/accept':
            if not row or row[0] == account:
                raise ValueError('Incoming request required')
            db.execute('UPDATE social_friend SET accepted=1 WHERE a=? AND b=?', (a, b))
        else:
            if row:
                return
            for who in (account, other):
                if db.execute('SELECT COUNT(*) FROM social_friend WHERE (a=? OR b=?) AND accepted=0', (who, who)).fetchone()[0] >= MAX_REQUESTS:
                    raise ValueError('Too many pending requests')
            db.execute('INSERT INTO social_friend VALUES(?,?,?,0)', (a, b, account))

    def clan_action(self, db, account, route, data):
        member = self.membership(db, account)
        if route == '/social/clan/create':
            self.chat.creation_consent(db, account, data)
            opened = data.get('open', True)
            operation = data.get('operationId')
            self.ledger.validate_operation(operation)
            if type(opened) is not bool:
                raise ValueError('Invalid join policy')
            # Retry old pending receipts before validating newly supported
            # description settings: legacy requests could contain free text.
            previous = db.execute('SELECT fingerprint FROM social_creation WHERE account=? AND operation_id=?',
                                  (account, operation)).fetchone()
            description = description_for(data) if not previous else None
            # Only non-message settings bind new operations. Submitted names/tags
            # never become visible or influence random name allocation.
            fingerprint = hashlib.sha256(
                json.dumps([description, opened], ensure_ascii=False).encode()).hexdigest()
            if previous:
                # Accept old app versions' idempotency receipts without replaying
                # old names, debits or creation.
                legacy_name, legacy_tag = data.get('name'), data.get('tag')
                legacy_description = data.get('description', '')
                raw = hashlib.sha256(json.dumps(
                    [legacy_name, legacy_tag.upper() if isinstance(legacy_tag, str) else legacy_tag,
                     legacy_description, opened], ensure_ascii=False).encode()).hexdigest()
                canonical = None
                try:
                    n, t, desc = validate_clan_identity(data)
                    canonical = hashlib.sha256(
                        json.dumps([n, t, desc, opened], ensure_ascii=False).encode()).hexdigest()
                except ValueError:
                    pass
                # New protocol receipts use the catalog description, not null.
                try:
                    preset = description_for(data)
                    generated_receipt = hashlib.sha256(
                        json.dumps([preset, opened], ensure_ascii=False).encode()).hexdigest()
                except ValueError:
                    generated_receipt = None
                if previous[0] not in (generated_receipt, raw, canonical):
                    raise ValueError('Clan operation already bound')
                return
            if member:
                raise ValueError('Leave your current clan first')
            revision = data.get('expectedProfileRevision')
            if type(revision) is not int or revision < 0:
                raise ValueError('Invalid profile revision')
            current_revision, encoded = db.execute(
                'SELECT revision,data FROM profile WHERE account=?', (account,)).fetchone()
            if revision != current_revision:
                raise ProfileConflict()
            # This is the only assignment path. The Firestore adapter reserves
            # the generated pair transactionally; SQLite uses its UNIQUE indices.
            if self.generated_clan_identity is not None:
                name, tag = self.generated_clan_identity
                if db.execute('SELECT 1 FROM social_clan WHERE name_search=? OR tag=?',
                              (name.casefold(), tag)).fetchone():
                    raise ValueError('Generated clan identity is unavailable; retry')
            else:
                for _ in range(64):
                    name, tag = generate_clan_name(), generate_clan_tag()
                    if not db.execute('SELECT 1 FROM social_clan WHERE name_search=? OR tag=?',
                                      (name.casefold(), tag)).fetchone():
                        break
                else:
                    raise ValueError('Clan identity allocation unavailable')
            identifier = secrets.token_hex(10)
            entries = {e['key']: e for e in json.loads(encoded)}
            earned = entries.get('DF_Shards', {}).get('number', 0)
            contribution = min(earned, CLAN_CREATION_COST)
            paid = CLAN_CREATION_COST - contribution
            debit = db.execute(
                'UPDATE wallet SET balance=balance-?,revision=revision+1 WHERE account=? AND balance>=?',
                (paid, account, paid))
            if debit.rowcount != 1:
                raise ValueError('Clan creation costs 500 Shards; insufficient balance')
            entries['DF_Shards'] = {'key':'DF_Shards','kind':0,'number':earned-contribution}
            db.execute('UPDATE profile SET data=?,revision=revision+1 WHERE account=?',
                       (json.dumps(sorted(entries.values(),key=lambda e:e['key']),
                                   separators=(',',':'),sort_keys=True),account))
            db.execute('INSERT INTO social_creation VALUES(?,?,?,?,?)',
                       (account,operation,identifier,fingerprint,CLAN_CREATION_COST))
            db.execute(
                'INSERT INTO social_clan(id,name,name_search,tag,description,open) VALUES(?,?,?,?,?,?)',
                (identifier, name, name.casefold(), tag, description, int(opened)))
            self.add_member(db, account, identifier, 'leader')
            return
        if route in ('/social/clan/join', '/social/clan/cancel'):
            identifier = self.clan_id(data)
            clan = db.execute('SELECT open FROM social_clan WHERE id=?', (identifier,)).fetchone()
            if not clan:
                raise ValueError('Clan not found')
            if route == '/social/clan/cancel':
                db.execute('DELETE FROM social_application WHERE account=? AND clan=?', (account, identifier))
                return
            self.chat.eligible(db, account, identifier)
            if member:
                if member[0] == identifier:
                    return
                raise ValueError('Leave your current clan first')
            if clan[0]:
                self.add_member(db, account, identifier)
            else:
                if db.execute('SELECT COUNT(*) FROM social_application WHERE account=?', (account,)).fetchone()[0] >= 5:
                    raise ValueError('At most five pending clan applications')
                if db.execute('SELECT COUNT(*) FROM social_application WHERE clan=?', (identifier,)).fetchone()[0] >= 100:
                    raise ValueError('Clan application list full')
                db.execute('INSERT OR IGNORE INTO social_application VALUES(?,?)', (account, identifier))
            return
        if not member:
            raise ValueError('Clan membership required')
        identifier, role = member
        if route == '/social/clan/identity':
            if role != 'leader': raise SocialForbidden('Leader required')
            name, tag, description = validate_clan_identity(data)
            if db.execute('SELECT 1 FROM social_clan WHERE id<>? AND (name_search=? OR tag=?)',
                          (identifier,name.casefold(),tag)).fetchone():
                raise ValueError('Clan name or tag already in use; reroll again')
            db.execute('UPDATE social_clan SET name=?,name_search=?,tag=?,description=? WHERE id=?',
                       (name,name.casefold(),tag,description,identifier))
            return
        if route == '/social/clan/icon':
            if role != 'leader': raise SocialForbidden('Leader required')
            avatar = data.get('avatar')
            if type(avatar) is not int or ('avatar_'+str(avatar)) not in ITEMS:
                raise ValueError('Choose an owned shop icon')
            entries = {e['key']:e for e in json.loads(db.execute('SELECT data FROM profile WHERE account=?',(account,)).fetchone()[0])}
            owned = entries.get('DF_AvatarOwned_'+str(avatar),{}).get('number') == 1 or db.execute('SELECT 1 FROM cosmetic WHERE account=? AND sku=?',(account,'avatar_'+str(avatar))).fetchone()
            if not owned: raise ValueError('Purchase this icon before choosing it for your clan')
            db.execute('UPDATE social_clan SET avatar=? WHERE id=?',(avatar,identifier))
            return
        if route == '/social/clan/leave':
            count = db.execute('SELECT COUNT(*) FROM social_member WHERE clan=?', (identifier,)).fetchone()[0]
            if role == 'leader' and count > 1:
                raise ValueError('Transfer leadership before leaving')
            db.execute('DELETE FROM social_member WHERE account=?', (account,))
            if count == 1:
                db.execute('DELETE FROM social_application WHERE clan=?', (identifier,))
                db.execute('DELETE FROM social_message WHERE clan=?',(identifier,))
                db.execute('DELETE FROM social_clan WHERE id=?', (identifier,))
            return
        other = self.target(db, data)
        if other == account:
            raise ValueError('Select another member')
        target_member = self.membership(db, other)
        if route in ('/social/clan/accept', '/social/clan/reject'):
            if role not in ('leader', 'officer'):
                raise SocialForbidden('Clan management required')
            pending = db.execute('SELECT 1 FROM social_application WHERE account=? AND clan=?', (other, identifier)).fetchone()
            if not pending:
                if route == '/social/clan/accept' and target_member and target_member[0] == identifier:
                    return
                raise ValueError('Application not found')
            if route == '/social/clan/accept':
                self.add_member(db, other, identifier)
            else:
                db.execute('DELETE FROM social_application WHERE account=? AND clan=?', (other, identifier))
            return
        if not target_member or target_member[0] != identifier:
            raise ValueError('Member not found in your clan')
        if route == '/social/clan/kick':
            if role == 'member' or target_member[1] == 'leader' or (role == 'officer' and target_member[1] != 'member'):
                raise SocialForbidden('Cannot remove this member')
            db.execute('DELETE FROM social_member WHERE account=?', (other,))
        elif route == '/social/clan/role':
            new_role = data.get('role')
            if role != 'leader':
                raise SocialForbidden('Leader required')
            if new_role not in ('member', 'officer') or target_member[1] == 'leader':
                raise ValueError('Invalid role')
            db.execute('UPDATE social_member SET role=? WHERE account=?', (new_role, other))
        elif route == '/social/clan/transfer':
            if role != 'leader':
                raise SocialForbidden('Leader required')
            if self.chat.config.enabled and not self.chat.consent(db, other, 'leader'):
                raise ValueError('New leader must accept moderation terms first')
            db.execute("UPDATE social_member SET role='member' WHERE account=?", (account,))
            db.execute("UPDATE social_member SET role='leader' WHERE account=?", (other,))

    def add_member(self, db, account, identifier, role='member'):
        self.chat.eligible(db, account, identifier)
        if self.membership(db, account):
            raise ValueError('Player already belongs to a clan')
        if db.execute('SELECT COUNT(*) FROM social_member WHERE clan=?', (identifier,)).fetchone()[0] >= MAX_MEMBERS:
            raise ValueError('Clan full (50 members)')
        db.execute('INSERT INTO social_member VALUES(?,?,?)', (account, identifier, role))
        db.execute('DELETE FROM social_application WHERE account=?', (account,))

    def check_account_allowed(self, account):
        with self.ledger.connect() as db:
            self.chat.account_allowed(db, account)


