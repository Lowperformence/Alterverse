"""Durable Google-verified shard buffer.
Verified purchases are claimed idempotently into the game's single shard balance.
No client-supplied prices, shard amounts or user IDs may be trusted by the HTTP adapter.
"""
import json
import sqlite3
from pathlib import Path

CATALOG_PATH = Path(__file__).resolve().parents[2] / 'Assets/Alterverse/Resources/Commerce/ShardCatalog.json'
CATALOG = json.loads(CATALOG_PATH.read_text())
PACKS = {p['id']: p['amount'] for p in CATALOG['packs']}
COSMETICS = {p['id']: p['price'] for p in CATALOG['cosmetics']}

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
            ''')
    def connect(self):
        db = sqlite3.connect(self.path, timeout=15)
        db.execute('PRAGMA synchronous=FULL')
        return db
    @staticmethod
    def ensure(db, account):
        if not account: raise ValueError('Authenticated account required')
        db.execute('INSERT OR IGNORE INTO wallet(account) VALUES (?)', (account,))
    @staticmethod
    def snapshot(db, account):
        balance, revision = db.execute('SELECT balance,revision FROM wallet WHERE account=?', (account,)).fetchone()
        owned = [r[0] for r in db.execute('SELECT sku FROM cosmetic WHERE account=?', (account,))]
        return {'balance': balance, 'revision': revision, 'voidfall': 'voidfall' in owned,
                'avatars': [int(s[7:]) for s in owned if s.startswith('avatar_')]}
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
            db.execute('INSERT INTO purchase VALUES (?,?,?,?,?)', (token, account, product, quantity, amount))
            db.execute('UPDATE wallet SET balance=balance+?,revision=revision+1 WHERE account=?', (amount, account))
            return self.snapshot(db, account)
    def claim(self, account):
        """Move the current verified server balance into one retry-safe client claim."""
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.ensure(db, account)
            pending = db.execute(
                'SELECT id,amount FROM claim WHERE account=? AND acknowledged=0 ORDER BY id LIMIT 1',
                (account,)).fetchone()
            if pending is None:
                balance = db.execute('SELECT balance FROM wallet WHERE account=?', (account,)).fetchone()[0]
                if balance > 0:
                    db.execute('UPDATE wallet SET balance=0,revision=revision+1 WHERE account=?', (account,))
                    cursor = db.execute('INSERT INTO claim(account,amount,acknowledged) VALUES (?,?,0)', (account,balance))
                    pending = (cursor.lastrowid,balance)

            result = self.snapshot(db, account)
            result['claimId'] = str(pending[0]) if pending is not None else ''
            result['claimAmount'] = pending[1] if pending is not None else 0
            return result

    def acknowledge_claim(self, account, claim_id):
        if not isinstance(claim_id, str) or not claim_id.isdigit():
            raise ValueError('Invalid claim')
        claim = int(claim_id)
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.ensure(db, account)
            row = db.execute('SELECT acknowledged FROM claim WHERE id=? AND account=?', (claim,account)).fetchone()
            if row is None:
                raise ValueError('Unknown claim')
            if row[0] == 0:
                db.execute('UPDATE claim SET acknowledged=1 WHERE id=? AND account=?', (claim,account))
            result = self.snapshot(db, account)
            result['claimId'] = ''
            result['claimAmount'] = 0
            return result

    def buy(self, account, sku):
        if sku not in COSMETICS: raise ValueError('Unknown cosmetic')
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
