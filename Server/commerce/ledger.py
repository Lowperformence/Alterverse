"""Durable account profiles and Google-verified paid shard ledger.
The single displayed balance combines server-saved earned progress and verified purchases.
No client-supplied prices, shard amounts or user IDs may be trusted by the HTTP adapter.
"""
import json
from datetime import date, datetime, timezone
from zoneinfo import ZoneInfo
import sqlite3
from pathlib import Path

CATALOG_PATH = Path(__file__).resolve().parents[2] / 'Assets/Alterverse/Resources/Commerce/ShardCatalog.json'
CATALOG = json.loads(CATALOG_PATH.read_text())
PACKS = {p['id']: p['amount'] for p in CATALOG['packs']}
COSMETICS = {p['id']: p['price'] for p in CATALOG['cosmetics']}
CONSUMABLES = {p['id'] for p in CATALOG['cosmetics'] if p.get('consumable')}
MAX_BALANCE = 2147483647
ITEMS = {p['id']: p for p in CATALOG['cosmetics']}

class ProfileConflict(Exception):
    pass


def validate_profile(entries):
    if not isinstance(entries, list) or len(entries) > 2048: raise ValueError('Invalid profile')
    clean = []; seen = set()
    for entry in entries:
        if not isinstance(entry, dict): raise ValueError('Invalid profile entry')
        key, kind = entry.get('key'), entry.get('kind')
        if not isinstance(key, str) or not key.startswith('DF_') or len(key) > 160 or key in seen:
            raise ValueError('Invalid profile key')
        if key.startswith(('DF_ProfileTransaction', 'DF_Cloud', 'DF_Commerce', 'DF_ShardClaim_', 'DF_AccountProfile')):
            raise ValueError('Device-only profile key')
        seen.add(key)
        if type(kind) is not int: raise ValueError('Invalid profile type')
        value = {'key':key, 'kind':kind}
        if kind == 0:
            number = entry.get('number')
            if type(number) is not int or not -2147483648 <= number <= MAX_BALANCE: raise ValueError('Invalid integer')
            if key == 'DF_Shards' and number < 0: raise ValueError('Invalid earned balance')
            value['number'] = number
        elif kind == 1:
            text = entry.get('text')
            if not isinstance(text, str) or len(text) > 65536: raise ValueError('Invalid string')
            value['text'] = text
        elif kind == 2:
            import math
            real = entry.get('real')
            if type(real) not in (int,float) or not math.isfinite(real): raise ValueError('Invalid float')
            value['real'] = real
        else: raise ValueError('Invalid profile type')
        if key == 'DF_Shards' and kind != 0: raise ValueError('Invalid earned balance type')
        if key in ('DF_LoginStreakDay_v1', 'DF_LoginStreakIndex_v1', 'DF_DailyQuestWonMaps_v1'):
            if kind != 0: raise ValueError('Invalid daily progression type')
            if key == 'DF_LoginStreakIndex_v1' and not 1 <= number <= 7:
                raise ValueError('Invalid login streak')
            if key == 'DF_DailyQuestWonMaps_v1' and not 0 <= number <= (1 << 13) - 1:
                raise ValueError('Invalid won-map mask')
            if key == 'DF_LoginStreakDay_v1':
                try: claimed_day = date(number // 10000, number // 100 % 100, number % 100)
                except (ValueError, OverflowError): raise ValueError('Invalid login day')
                if claimed_day > datetime.now(timezone.utc).astimezone(ZoneInfo('Europe/Berlin')).date():
                    raise ValueError('Future login day')
        clean.append(value)
    return clean


class Ledger:
    def __init__(self, path):
        self.path = str(path)
        with self.connect() as db:
            db.executescript('''
            CREATE TABLE IF NOT EXISTS wallet (account TEXT PRIMARY KEY, balance INTEGER NOT NULL DEFAULT 0 CHECK(balance>=0), revision INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS purchase (token TEXT PRIMARY KEY, account TEXT NOT NULL, product TEXT NOT NULL, quantity INTEGER NOT NULL, credited INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS cosmetic (account TEXT NOT NULL, sku TEXT NOT NULL, PRIMARY KEY(account,sku));
            CREATE TABLE IF NOT EXISTS claim (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                account TEXT NOT NULL,
                amount INTEGER NOT NULL CHECK(amount>=0),
                acknowledged INTEGER NOT NULL DEFAULT 0 CHECK(acknowledged IN (0,1))
            );
            CREATE UNIQUE INDEX IF NOT EXISTS claim_pending_per_account ON claim(account) WHERE acknowledged=0;
            CREATE TABLE IF NOT EXISTS profile (
                account TEXT PRIMARY KEY, revision INTEGER NOT NULL DEFAULT 0,
                data TEXT NOT NULL DEFAULT '[]'
            );
            CREATE TABLE IF NOT EXISTS commerce_operation (
                account TEXT NOT NULL, id TEXT NOT NULL, sku TEXT NOT NULL,
                local_contribution INTEGER NOT NULL, kind TEXT NOT NULL,
                PRIMARY KEY(account,id)
            );
            ''')
    def connect(self):
        db = sqlite3.connect(self.path, timeout=15)
        db.execute('PRAGMA synchronous=FULL')
        return db
    @staticmethod
    def ensure(db, account):
        if not account: raise ValueError('Authenticated account required')
        db.execute('INSERT OR IGNORE INTO wallet(account) VALUES (?)', (account,))
        db.execute('INSERT OR IGNORE INTO profile(account) VALUES (?)', (account,))
    @staticmethod
    def snapshot(db, account):
        balance, revision = db.execute('SELECT balance,revision FROM wallet WHERE account=?', (account,)).fetchone()
        owned = [r[0] for r in db.execute('SELECT sku FROM cosmetic WHERE account=?', (account,))]
        profile_revision, profile_data = db.execute('SELECT revision,data FROM profile WHERE account=?', (account,)).fetchone()
        # Keep public level/name data current on profile saves and wallet loads.
        if db.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='social_player'").fetchone():
            from social import sync_player
            sync_player(db, account, profile_data)
        return {'profileRevision':profile_revision, 'profile':json.loads(profile_data), 'balance': balance, 'revision': revision, 'voidfall': 'voidfall' in owned,
                'avatars': [int(s[7:]) for s in owned if s.startswith('avatar_')],
                'owned': owned}
    def wallet(self, account):
        with self.connect() as db:
            self.ensure(db, account)
            return self.snapshot(db, account)
    def fulfill(self, account, product, token, verified):
        # verified is produced by trusted server Google API lookup, never from request JSON.
        if product not in PACKS or not token: raise ValueError('Unknown product or empty token')
        if verified.get('purchaseState') != 0: raise ValueError('Payment not PURCHASED')
        if verified.get('obfuscatedExternalAccountId') != account: raise ValueError('Purchase account mismatch')
        quantity = verified.get('quantity', 1)
        if not isinstance(quantity, int) or isinstance(quantity, bool) or not 1 <= quantity <= 100: raise ValueError('Invalid quantity')
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.ensure(db, account)
            previous = db.execute('SELECT account,product,quantity FROM purchase WHERE token=?', (token,)).fetchone()
            if previous:
                if previous != (account, product, quantity): raise ValueError('Purchase token already bound')
                return self.snapshot(db, account)
            if verified.get('consumptionState', 0) != 0: raise ValueError('Unknown consumed purchase requires reconciliation')
            amount = PACKS[product] * quantity
            current = db.execute('SELECT balance FROM wallet WHERE account=?', (account,)).fetchone()[0]
            if current + amount > MAX_BALANCE: raise ValueError('Wallet capacity exceeded')
            db.execute('INSERT INTO purchase VALUES (?,?,?,?,?)', (token, account, product, quantity, amount))
            db.execute('UPDATE wallet SET balance=balance+?,revision=revision+1 WHERE account=?', (amount, account))
            return self.snapshot(db, account)
    def claim(self, account):
        raise ValueError('Legacy local shard claims disabled; update the client')

    def acknowledge_claim(self, account, claim_id):
        raise ValueError('Legacy local shard claims disabled; update the client')

    @staticmethod
    def validate_operation(operation_id):
        if not isinstance(operation_id, str) or len(operation_id) != 32 or any(c not in '0123456789abcdef' for c in operation_id):
            raise ValueError('Invalid operation ID')

    def save_profile(self, account, entries, expected_revision):
        entries = validate_profile(entries)
        if type(expected_revision) is not int or expected_revision < 0: raise ValueError('Invalid profile revision')
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.ensure(db, account)
            current = db.execute('SELECT revision,data FROM profile WHERE account=?', (account,)).fetchone()
            if current[0] != expected_revision: raise ProfileConflict()
            by_key = {e['key']:e for e in entries}
            saved = {e['key']:e for e in json.loads(current[1])}
            daily_day, daily_index = 'DF_LoginStreakDay_v1', 'DF_LoginStreakIndex_v1'
            if daily_day not in by_key and daily_index not in by_key:
                for key in (daily_day, daily_index):
                    if key in saved: by_key[key] = saved[key]
            if (daily_day in by_key) != (daily_index in by_key):
                raise ValueError('Incomplete login streak')
            if daily_day in saved and daily_day in by_key:
                old_day, new_day = saved[daily_day]['number'], by_key[daily_day]['number']
                if new_day < old_day: raise ValueError('Login day cannot move backwards')
                if new_day == old_day and by_key[daily_index] != saved.get(daily_index):
                    raise ValueError('Login streak already recorded for this day')
            for (sku,) in db.execute('SELECT sku FROM cosmetic WHERE account=?', (account,)):
                for write in ITEMS.get(sku,{}).get('writes',[]):
                    if 'Owned' in write['key'] or write['key'] == 'DF_BP_BETA01_Premium':
                        by_key[write['key']] = write
            encoded = json.dumps(sorted(by_key.values(), key=lambda e:e['key']), separators=(',', ':'), sort_keys=True)
            if encoded != current[1]:
                db.execute('UPDATE profile SET data=?,revision=revision+1 WHERE account=?', (encoded,account))
            return self.snapshot(db, account)

    def purchase(self, account, sku, operation_id, expected_revision):
        """One transaction debits cloud-backed earned + Google-verified paid shards.
        Prices, debit split and unlock keys are computed exclusively on the server.
        """
        self.validate_operation(operation_id)
        if not isinstance(sku,str) or sku not in ITEMS: raise ValueError('Unknown item')
        if ITEMS[sku].get('available', True) is not True: raise ValueError('Item is not released')
        if type(expected_revision) is not int or expected_revision < 0: raise ValueError('Invalid profile revision')
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.ensure(db, account)
            previous = db.execute('SELECT sku,local_contribution,kind FROM commerce_operation WHERE account=? AND id=?', (account,operation_id)).fetchone()
            if previous:
                if previous[0] != sku or previous[2] != 'buy': raise ValueError('Operation already bound')
                result = self.snapshot(db, account); result['operationId'] = operation_id
                return result
            revision, encoded = db.execute('SELECT revision,data FROM profile WHERE account=?', (account,)).fetchone()
            if revision != expected_revision: raise ProfileConflict()
            entries = {e['key']:e for e in json.loads(encoded)}
            definition = ITEMS[sku]
            writes = definition.get('writes',[])
            already_owned = sku not in CONSUMABLES and (
                db.execute('SELECT 1 FROM cosmetic WHERE account=? AND sku=?', (account,sku)).fetchone() or
                (writes and entries.get(writes[0]['key'],{}).get('number') == 1))
            if not already_owned:
                earned = entries.get('DF_Shards',{}).get('number',0)
                contribution = min(earned,COSMETICS[sku])
                paid = COSMETICS[sku] - contribution
                row = db.execute('UPDATE wallet SET balance=balance-?,revision=revision+1 WHERE account=? AND balance>=?', (paid,account,paid))
                if row.rowcount != 1: raise ValueError('Insufficient shards')
                entries['DF_Shards'] = {'key':'DF_Shards','kind':0,'number':earned-contribution}
                increment_key = definition.get('incrementKey')
                if sku in CONSUMABLES and increment_key:
                    old = entries.get(increment_key,{}).get('number',0)
                    if old >= MAX_BALANCE: raise ValueError('Inventory capacity exceeded')
                    entries[increment_key] = {'key':increment_key,'kind':0,'number':old+1}
                else:
                    for write in writes: entries[write['key']] = write
                    db.execute('INSERT OR IGNORE INTO cosmetic VALUES (?,?)', (account,sku))
                db.execute('UPDATE profile SET data=?,revision=revision+1 WHERE account=?', (json.dumps(list(entries.values()),separators=(',',':'),sort_keys=True),account))
            else:
                contribution = 0
                restored = False
                for write in writes:
                    if ('Owned' in write['key'] or write['key'] == 'DF_BP_BETA01_Premium') and entries.get(write['key']) != write:
                        entries[write['key']] = write; restored = True
                if restored:
                    db.execute('UPDATE profile SET data=?,revision=revision+1 WHERE account=?', (json.dumps(list(entries.values()),separators=(',',':'),sort_keys=True),account))
            db.execute('INSERT INTO commerce_operation VALUES (?,?,?,?,?)', (account,operation_id,sku,contribution,'buy'))
            result = self.snapshot(db, account); result['operationId'] = operation_id
            return result

    def buy(self, account, sku):
        if sku not in COSMETICS or sku in CONSUMABLES: raise ValueError('Unknown cosmetic')
        if ITEMS[sku].get('available', True) is not True: raise ValueError('Item is not released')
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.ensure(db, account)
            if db.execute('SELECT 1 FROM cosmetic WHERE account=? AND sku=?', (account, sku)).fetchone():
                return self.snapshot(db, account)  # Retried purchase is idempotent.
            row = db.execute('UPDATE wallet SET balance=balance-?,revision=revision+1 WHERE account=? AND balance>=?',
                             (COSMETICS[sku], account, COSMETICS[sku]))
            if row.rowcount != 1: raise ValueError('Insufficient shards')
            db.execute('INSERT INTO cosmetic VALUES (?,?)', (account, sku))
            return self.snapshot(db, account)
