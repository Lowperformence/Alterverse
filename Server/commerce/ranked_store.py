"""Atomic ranked records, separate from client-editable cloud profiles."""
import json
import sqlite3


class SQLiteRankedStore:
    def __init__(self, path):
        self.path = str(path)
        with sqlite3.connect(self.path) as db:
            db.execute('CREATE TABLE IF NOT EXISTS ranked_record (id TEXT PRIMARY KEY, data TEXT NOT NULL, score INTEGER NOT NULL DEFAULT -1)')

    def atomic(self, operation):
        with sqlite3.connect(self.path, timeout=30) as db:
            db.execute('BEGIN IMMEDIATE')
            class Records:
                def get(self, key):
                    row = db.execute('SELECT data FROM ranked_record WHERE id=?', (key,)).fetchone()
                    return json.loads(row[0]) if row else None
                def set(self, key, value):
                    db.execute('INSERT INTO ranked_record VALUES (?,?,?) ON CONFLICT(id) DO UPDATE SET data=excluded.data,score=excluded.score',
                               (key, json.dumps(value), value.get('listedScore', -1)))
            return operation(Records())

    def leaders(self, season):
        with sqlite3.connect(self.path) as db:
            rows = db.execute('SELECT data FROM ranked_record WHERE substr(id,1,?)=? AND score>=0 ORDER BY score DESC,id LIMIT 50',
                              (len(season + ':p:'), season + ':p:')).fetchall()
            return [json.loads(row[0]) for row in rows]


class FirestoreRankedStore:
    def __init__(self, ledger):
        self.client = ledger.client
        self.root = self.client.collection(ledger.accounts.id + '-ranked')

    def atomic(self, operation):
        from google.cloud import firestore
        @firestore.transactional
        def run(tx):
            root = self.root
            writes = {}
            class Records:
                def get(self, key):
                    if key in writes:
                        return writes[key]
                    snap = root.document(key).get(transaction=tx)
                    return snap.to_dict() if snap.exists else None
                def set(self, key, value):
                    writes[key] = value
            result = operation(Records())
            # Firestore requires all reads before the first write.
            for key, value in writes.items():
                tx.set(root.document(key), value)
                if ':p:' in key and value.get('games', 0) >= 5:
                    # Maintain leaderboard in the same transaction as the authoritative rating.
                    tx.set(root.document(value['season']).collection('leaders').document(value['account']), value)
            return result
        return run(self.client.transaction())

    def leaders(self, season):
        # One collection per season avoids a composite index requirement.
        return [s.to_dict() for s in self.root.document(season).collection('leaders').order_by('mmr', direction='DESCENDING').limit(50).stream()]

