"""Private clan communication rules shared by SQLite and Firestore transactions.

No client supplied account, name, timestamp, role or administrator flag is trusted.
Only server-issued public IDs leave this module. Email runs after commit, never here.
"""
from dataclasses import dataclass
import hashlib
import json
import os
import re
import secrets
import time
import unicodedata

CHAT_VERSION = '1'
LEADER_VERSION = '1'
CHAT_RULES = ('No insults, threats or harassment. No racism, hate speech, discrimination or '
              'sexual harassment. No spam, fraud, other people\'s personal data or illegal content. '
              'No links or contact details. Protect your privacy; report and block suspicious players.')
LEADER_RULES = ('I take responsibility for everyday moderation of my clan chat and enforce the '
                'chat rules. I can warn, mute, remove and ban members from rejoining. I report '
                'serious violations I become aware of to the game administration. Repeated failure '
                'to moderate may result in removal of my leadership or closure of the clan.')
REASONS = ('Insults / Harassment', 'Threats', 'Hate / Discrimination',
           'Sexual harassment', 'Spam / Fraud', 'Personal data',
           'Illegal content', 'Other rule violation')
MESSAGE_COLUMNS = ('id', 'clan', 'account', 'name', 'text', 'created_at')
CHAT_TABLES = {
    'social_message': MESSAGE_COLUMNS,
    'chat_consent': ('account', 'kind', 'version', 'accepted_at', 'age_band'),
    'chat_sanction': ('clan', 'account', 'muted_until', 'banned', 'warnings'),
    'chat_rate': ('account', 'kind', 'window', 'count', 'last_at', 'fingerprint'),
    'chat_report': ('id', 'clan', 'message_id', 'reporter', 'target', 'reason',
                    'created_at', 'evidence', 'status', 'reviewed_at'),
    'chat_audit': ('id', 'clan', 'actor', 'target', 'action', 'created_at'),
    'chat_user_ban': ('account', 'until_at'),
    'chat_outbox': ('id', 'attempts', 'next_at', 'lease_until', 'sent_at'),
    'chat_room': ('clan', 'revision'),
}
CHAT_KEYS = {
    'social_message': ('id',), 'chat_consent': ('account', 'kind', 'version'),
    'chat_sanction': ('clan', 'account'), 'chat_rate': ('account', 'kind'),
    'chat_report': ('id',), 'chat_audit': ('id',), 'chat_user_ban': ('account',),
    'chat_outbox': ('id',),
    'chat_room': ('clan',),
}


@dataclass(frozen=True)
class ChatConfig:
    enabled: bool = True
    admins: frozenset = frozenset()
    message_days: int = 30
    report_days: int = 180
    words: tuple = ('fuck', 'bitch', 'cunt', 'arschloch', 'hurensohn', 'heil hitler', 'kill yourself')
    admin_email: str = ''

    @classmethod
    def from_env(cls):
        def days(key, default):
            value = int(os.environ.get(key, str(default)))
            if not 1 <= value <= 365: raise ValueError(key + ' must be between 1 and 365')
            return value
        return cls(enabled=os.environ.get('DF_ENABLE_CLAN_CHAT', '1') == '1',
                   admins=frozenset(a.strip() for a in os.environ.get('DF_CHAT_ADMIN_ACCOUNTS', '').split(',') if a.strip()),
                   message_days=days('DF_CHAT_MESSAGE_DAYS', 30),
                   report_days=days('DF_CHAT_REPORT_DAYS', 180),
                   words=tuple(w.strip().casefold() for w in os.environ.get('DF_CHAT_FILTER_WORDS', ','.join(cls.words)).split(',') if w.strip()),
                   admin_email=os.environ.get('DF_CHAT_ADMIN_EMAIL', '').strip())


def initialize(db):
    db.executescript('''
        CREATE TABLE IF NOT EXISTS chat_consent(account TEXT NOT NULL,kind TEXT NOT NULL,
            version TEXT NOT NULL,accepted_at INTEGER NOT NULL,age_band TEXT NOT NULL,
            PRIMARY KEY(account,kind,version));
        CREATE TABLE IF NOT EXISTS chat_sanction(clan TEXT NOT NULL,account TEXT NOT NULL,
            muted_until INTEGER NOT NULL,banned INTEGER NOT NULL,warnings INTEGER NOT NULL,
            PRIMARY KEY(clan,account));
        CREATE TABLE IF NOT EXISTS chat_rate(account TEXT NOT NULL,kind TEXT NOT NULL,
            window INTEGER NOT NULL,count INTEGER NOT NULL,last_at INTEGER NOT NULL,
            fingerprint TEXT NOT NULL,PRIMARY KEY(account,kind));
        CREATE TABLE IF NOT EXISTS chat_report(id TEXT PRIMARY KEY,clan TEXT NOT NULL,
            message_id TEXT NOT NULL,reporter TEXT NOT NULL,target TEXT NOT NULL,reason TEXT NOT NULL,
            created_at INTEGER NOT NULL,evidence TEXT NOT NULL,status TEXT NOT NULL,reviewed_at INTEGER NOT NULL);
        CREATE INDEX IF NOT EXISTS chat_report_time ON chat_report(created_at DESC,id DESC);
        CREATE TABLE IF NOT EXISTS chat_audit(id TEXT PRIMARY KEY,clan TEXT NOT NULL,
            actor TEXT NOT NULL,target TEXT NOT NULL,action TEXT NOT NULL,created_at INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS chat_user_ban(account TEXT PRIMARY KEY,until_at INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS chat_outbox(id TEXT PRIMARY KEY,attempts INTEGER NOT NULL,
            next_at INTEGER NOT NULL,lease_until INTEGER NOT NULL,sent_at INTEGER NOT NULL);
        CREATE INDEX IF NOT EXISTS chat_outbox_due ON chat_outbox(next_at,lease_until);
        CREATE TABLE IF NOT EXISTS chat_room(clan TEXT PRIMARY KEY,revision TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS chat_audit_time ON chat_audit(created_at);
    ''')


class ClanChat:
    def __init__(self, social, config):
        self.social, self.config = social, config

    @staticmethod
    def now(): return int(time.time() * 1000)

    @staticmethod
    def identifier(value):
        if not isinstance(value, str) or not re.fullmatch('[a-f0-9]{20}', value):
            raise ValueError('Invalid message or report ID')
        return value

    def deny(self, message):
        from social import SocialForbidden
        raise SocialForbidden(message)

    def account_allowed(self, db, account):
        row = db.execute('SELECT until_at FROM chat_user_ban WHERE account=?', (account,)).fetchone()
        if row and (row[0] == -1 or row[0] > self.now()): self.deny('Account suspended')

    def require_admin(self, account):
        if account not in self.config.admins: self.deny('Administrator required')

    def consent(self, db, account, kind):
        version = CHAT_VERSION if kind == 'chat' else LEADER_VERSION
        row = db.execute('SELECT accepted_at,age_band FROM chat_consent WHERE account=? AND kind=? AND version=?',
                         (account, kind, version)).fetchone()
        return row

    def accept(self, db, account, data, kind):
        version = CHAT_VERSION if kind == 'chat' else LEADER_VERSION
        if data.get('accepted') is not True or data.get('consentVersion') != version:
            raise ValueError('Please actively accept the current terms.')
        age = data.get('ageBand', '') if kind == 'chat' else ''
        if kind == 'chat' and age not in ('13-17', '18+'):
            raise ValueError('Chat requires an age group of 13 or older.')
        old = self.consent(db, account, kind)
        if old and old[1] != age: raise ValueError('Your saved age group cannot be changed here.')
        db.execute('INSERT OR IGNORE INTO chat_consent VALUES(?,?,?,?,?)',
                   (account, kind, version, self.now(), age))

    def creation_consent(self, db, account, data):
        if self.config.enabled:
            self.accept(db, account, data, 'leader')

    def leader(self, db, account):
        member = self.social.membership(db, account)
        if not member or member[1] != 'leader': self.deny('Clan leader required')
        if not self.consent(db, account, 'leader'): self.deny('Accept the moderation terms first.')
        return member[0]

    def eligible(self, db, account, clan):
        row = db.execute('SELECT banned FROM chat_sanction WHERE clan=? AND account=?', (clan, account)).fetchone()
        if row and row[0]: self.deny('You are banned from rejoining this clan.')
        self.account_allowed(db, account)

    def scope(self, db, account, data):
        member = self.social.membership(db, account)
        if not member: self.deny('Clan membership required')
        # IDs are optional for the caller's own room, but any supplied ID must match.
        if data.get('clanId') not in (None, '', member[0]): self.deny('Private clan room')
        if not self.consent(db, account, 'chat'): self.deny('Accept the chat rules first.')
        if member[1] == 'leader' and not self.consent(db, account, 'leader'):
            self.deny('Accept the moderation terms first.')
        self.eligible(db, account, member[0])
        return member[0]

    def snapshot(self, db, account):
        chat, leader = self.consent(db, account, 'chat'), self.consent(db, account, 'leader')
        member = self.social.membership(db, account)
        sanction = db.execute('SELECT muted_until,warnings FROM chat_sanction WHERE clan=? AND account=?',
                              (member[0], account)).fetchone() if member else None
        return dict(chatEnabled=self.config.enabled, chatProtocolVersion=1,
                    chatRulesVersion=CHAT_VERSION, leaderRulesVersion=LEADER_VERSION,
                    chatRules=CHAT_RULES, leaderRules=LEADER_RULES, reportReasons=list(REASONS),
                    chatAccepted=bool(chat), leaderAccepted=bool(leader),
                    chatAcceptedAt=chat[0] if chat else 0, leaderAcceptedAt=leader[0] if leader else 0,
                    mutedUntil=sanction[0] if sanction else 0, warnings=sanction[1] if sanction else 0,
                    isChatAdmin=account in self.config.admins, maxMessageLength=500)

    def rate(self, db, account, kind, maximum, spacing=0, fingerprint=''):
        now = self.now(); window = now // 60000
        row = db.execute('SELECT window,count,last_at,fingerprint FROM chat_rate WHERE account=? AND kind=?',
                         (account, kind)).fetchone()
        count = row[1] if row and row[0] == window else 0
        if count >= maximum or row and now - row[2] < spacing:
            raise ValueError('Too many actions. Please wait a moment.')
        if fingerprint and row and fingerprint == row[3] and now - row[2] < 30000:
            raise ValueError('Repeated message. Please wait a moment.')
        db.execute('INSERT INTO chat_rate VALUES(?,?,?,?,?,?) ON CONFLICT(account,kind) DO UPDATE SET '
                   'window=excluded.window,count=excluded.count,last_at=excluded.last_at,fingerprint=excluded.fingerprint',
                   (account, kind, window, count + 1, now, fingerprint))

    def text(self, value):
        if not isinstance(value, str): raise ValueError('Message required.')
        value = unicodedata.normalize('NFKC', value).strip()
        if not 1 <= len(value) <= 500: raise ValueError('Messages must contain 1–500 characters.')
        if any(unicodedata.category(c).startswith('C') for c in value):
            raise ValueError('Control characters are not allowed.')
        # All players receive the stricter protection: no clickable URLs, email or phones.
        if (re.search(r'(?:https?\s*:|www\s*\.|[a-z0-9-]+\s*[.]\s*(?:com|de|net|org|gg|io|ru|xyz)\b|\b[a-z0-9._%+-]+@[a-z0-9.-]+|(?:\+?\d[\d ()/-]*){7})', value, re.I)):
            raise ValueError('Links and contact details are not allowed in clan chat.')
        folded = value.casefold()
        compact = re.sub(r'[^a-zäöüß]', '', folded)
        for word in self.config.words:
            if re.search(r'(?<!\w)' + re.escape(word) + r'(?!\w)', folded) or re.sub(r'[^a-zäöüß]', '', word) in compact:
                raise ValueError('This message contains a blocked expression.')
        return value

    def messages(self, db, account, clan):
        threshold = self.now() - self.config.message_days * 86400000
        rows = db.execute('''SELECT m.id,m.name,m.text,m.created_at,p.player_id FROM social_message m
            JOIN social_player p ON p.account=m.account WHERE m.clan=? AND m.created_at>=?
            AND NOT EXISTS(SELECT 1 FROM social_block b WHERE
                (b.account=? AND b.target=m.account) OR (b.account=m.account AND b.target=?))
            ORDER BY m.created_at DESC,m.id DESC LIMIT 50''', (clan, threshold, account, account)).fetchall()
        return [dict(zip(('messageId', 'name', 'text', 'createdAt', 'playerId'), row)) for row in reversed(rows)]

    def audit(self, db, clan, actor, target, action):
        db.execute('INSERT INTO chat_audit VALUES(?,?,?,?,?,?)',
                   (secrets.token_hex(10), clan, actor, target, action, self.now()))

    def revision(self, db, clan):
        row = db.execute('SELECT revision FROM chat_room WHERE clan=?', (clan,)).fetchone()
        return row[0] if row else 'initial'

    def touch(self, db, clan):
        if clan:
            db.execute('INSERT OR REPLACE INTO chat_room VALUES(?,?)', (clan, secrets.token_hex(10)))

    def report(self, db, account, data, clan):
        reason = data.get('reason')
        if reason not in REASONS: raise ValueError('Choose a report reason.')
        message_id = data.get('messageId') or ''
        if message_id:
            self.identifier(message_id)
            row = db.execute('SELECT account,name,text,created_at FROM social_message WHERE id=? AND clan=?',
                             (message_id, clan)).fetchone()
            if not row or row[3] < self.now() - self.config.message_days * 86400000:
                self.deny('Message unavailable')
            target = row[0]
            evidence = json.dumps(dict(name=row[1], text=row[2], createdAt=row[3],
                                       playerId=self.social.player(db, target)['playerId']), ensure_ascii=False)
        else:
            target = self.social.target(db, data)
            member = self.social.membership(db, target)
            if not member or member[0] != clan: self.deny('Player unavailable')
            evidence = json.dumps(self.social.player(db, target), ensure_ascii=False)
        if target == account: raise ValueError('You cannot report yourself.')
        identifier = hashlib.sha256(json.dumps([account, clan, message_id or target]).encode()).hexdigest()[:20]
        if db.execute('SELECT 1 FROM chat_report WHERE id=?', (identifier,)).fetchone(): return identifier
        self.rate(db, account, 'report', 5)
        db.execute('INSERT INTO chat_report VALUES(?,?,?,?,?,?,?,?,?,?)',
                   (identifier, clan, message_id, account, target, reason, self.now(), evidence, 'open', 0))
        if self.config.admin_email:
            db.execute('INSERT INTO chat_outbox VALUES(?,?,?,?,?)', (identifier, 0, self.now(), 0, 0))
        return identifier

    def moderate(self, db, account, data):
        clan = self.leader(db, account)
        if data.get('clanId') not in (None, '', clan): self.deny('Private clan room')
        other = self.social.target(db, data)
        member = self.social.membership(db, other)
        if other == account or not member or member[0] != clan: self.deny('Clan member required')
        action = data.get('action')
        if action not in ('warn', 'mute', 'unmute', 'ban'): raise ValueError('Unknown moderation action')
        old = db.execute('SELECT muted_until,banned,warnings FROM chat_sanction WHERE clan=? AND account=?',
                         (clan, other)).fetchone() or (0, 0, 0)
        until, banned, warnings = old
        if action == 'warn': warnings += 1
        elif action == 'mute':
            minutes = data.get('minutes', 60)
            if type(minutes) is not int or not 1 <= minutes <= 10080: raise ValueError('Invalid mute duration')
            until = self.now() + minutes * 60000
        elif action == 'unmute': until = 0
        else:
            banned = 1
            db.execute('DELETE FROM social_member WHERE account=?', (other,))
            db.execute('DELETE FROM social_application WHERE account=? AND clan=?', (other, clan))
        db.execute('INSERT OR REPLACE INTO chat_sanction VALUES(?,?,?,?,?)', (clan, other, until, banned, warnings))
        self.audit(db, clan, account, other, action)
        self.touch(db, clan)

    def admin(self, db, account, route, data):
        self.require_admin(account)
        if route == '/social/chat/admin/reports':
            offset = self.social.page(data)
            rows = db.execute('SELECT id,clan,message_id,reporter,target,reason,created_at,evidence,status,reviewed_at '
                              'FROM chat_report WHERE created_at>=? ORDER BY created_at DESC,id DESC LIMIT 8 OFFSET ?',
                              (self.now() - self.config.report_days * 86400000, offset)).fetchall()
            reports = []
            for row in rows:
                report = dict(zip(('reportId', 'clanId', 'messageId', 'reporter', 'target', 'reason', 'createdAt', 'evidence', 'status', 'reviewedAt'), row))
                report['reporterId'] = self.social.player(db, report.pop('reporter'))['playerId']
                report['playerId'] = self.social.player(db, report.pop('target'))['playerId']
                report['evidence'] = json.loads(report['evidence'])
                reports.append(report)
            return dict(reports=reports, offset=offset)
        if route != '/social/chat/admin/action': raise ValueError('Unknown administration route')
        self.rate(db, account, 'admin', 30)
        action = data.get('action')
        if action not in ('remove', 'suspend', 'unsuspend', 'close', 'transfer', 'resolve'):
            raise ValueError('Unknown administration action')
        report = None
        if data.get('reportId'):
            report = db.execute('SELECT clan,message_id,target FROM chat_report WHERE id=?',
                                (self.identifier(data['reportId']),)).fetchone()
            if not report: raise ValueError('Report unavailable')
        clan = report[0] if report else self.social.clan_id(data)
        target = report[2] if report else (self.social.target(db, data) if data.get('playerId') else '')
        if action == 'remove':
            message = report[1] if report else self.identifier(data.get('messageId'))
            db.execute('DELETE FROM social_message WHERE id=? AND clan=?', (message, clan))
        elif action in ('suspend', 'unsuspend'):
            if not target or target in self.config.admins: self.deny('Cannot suspend an administrator')
            db.execute('INSERT OR REPLACE INTO chat_user_ban VALUES(?,?)', (target, -1 if action == 'suspend' else 0))
        elif action == 'close':
            db.execute('DELETE FROM social_member WHERE clan=?', (clan,))
            db.execute('DELETE FROM social_application WHERE clan=?', (clan,))
            db.execute('DELETE FROM social_message WHERE clan=?', (clan,))
            db.execute('DELETE FROM social_clan WHERE id=?', (clan,))
        elif action == 'transfer':
            target = self.social.target(db, data)
            member = self.social.membership(db, target)
            if not member or member[0] != clan or not self.consent(db, target, 'leader'):
                raise ValueError('New leader must be a member and accept moderation terms first')
            db.execute("UPDATE social_member SET role='member' WHERE clan=? AND role='leader'", (clan,))
            db.execute("UPDATE social_member SET role='leader' WHERE account=?", (target,))
        elif not report: raise ValueError('Report required')
        if report:
            db.execute("UPDATE chat_report SET status='resolved',reviewed_at=? WHERE id=?", (self.now(), data['reportId']))
        self.audit(db, clan, account, target, 'admin:' + action)
        self.touch(db, clan)
        return dict(adminAction=action)

    def cleanup(self, db):
        now = self.now()
        db.execute('DELETE FROM social_message WHERE created_at<?', (now - self.config.message_days * 86400000,))
        db.execute('DELETE FROM chat_outbox WHERE id IN(SELECT id FROM chat_report WHERE created_at<?)',
                   (now - self.config.report_days * 86400000,))
        db.execute('DELETE FROM chat_report WHERE created_at<?', (now - self.config.report_days * 86400000,))
        db.execute('DELETE FROM chat_audit WHERE created_at<?', (now - self.config.report_days * 86400000,))
        db.execute('DELETE FROM chat_rate WHERE last_at<?', (now - 86400000,))

    def dispatch(self, db, account, route, data):
        if not self.config.enabled: raise ValueError('Communication is disabled in this version.')
        if route.startswith('/social/chat/admin/'): return self.admin(db, account, route, data)
        if route in ('/social/chat/consent', '/social/chat/leader-consent'):
            self.rate(db, account, 'consent', 20)
            self.accept(db, account, data, 'chat' if route.endswith('/consent') else 'leader')
            return self.social.snapshot(db, account)
        clan = self.scope(db, account, data)
        if route == '/social/chat/read' and data.get('knownRevision') == self.revision(db, clan):
            return dict(unchanged=True, chatRevision=self.revision(db, clan), clanId=clan,
                        expiresBefore=self.now() - self.config.message_days * 86400000)
        result = {}
        if route == '/social/chat/send':
            text = self.text(data.get('text'))
            sanction = db.execute('SELECT muted_until FROM chat_sanction WHERE clan=? AND account=?', (clan, account)).fetchone()
            if sanction and sanction[0] > self.now(): self.deny('You are muted in clan chat.')
            operation = self.identifier(data.get('operationId'))
            identifier = hashlib.sha256((account + ':' + operation).encode()).hexdigest()[:20]
            old = db.execute('SELECT clan,account,text FROM social_message WHERE id=?', (identifier,)).fetchone()
            if old:
                if old != (clan, account, text): raise ValueError('Message operation already bound')
            else:
                self.rate(db, account, 'send', 12, 1500, hashlib.sha256(text.casefold().encode()).hexdigest())
                db.execute('INSERT INTO social_message VALUES(?,?,?,?,?,?)',
                           (identifier, clan, account, self.social.player(db, account)['name'], text, self.now()))
                self.touch(db, clan)
            result['sentMessageId'] = identifier
        elif route == '/social/chat/report': result['reportId'] = self.report(db, account, data, clan)
        elif route == '/social/chat/moderate':
            self.rate(db, account, 'moderate', 30)
            self.moderate(db, account, data)
        elif route != '/social/chat/read': raise ValueError('Unknown chat route')
        result.update(self.social.snapshot(db, account))
        result['messages'] = self.messages(db, account, clan)
        result['chatRevision'] = self.revision(db, clan)
        result['clanId'] = clan
        result['expiresBefore'] = self.now() - self.config.message_days * 86400000
        return result
