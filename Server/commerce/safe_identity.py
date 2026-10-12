"""Closed, project-authored English identity catalog shared with the Unity client.

Full reviewed names plus seven ASCII digits: no submitted text or arbitrary word
combinations. A catalog format check is the authority, not a profanity blacklist.
"""
import hashlib
import json
from pathlib import Path
import re
import secrets

CATALOG_PATH = Path(__file__).resolve().parents[2] / 'Assets/Alterverse/Resources/Social/SafeIdentityCatalog.json'
CATALOG = json.loads(CATALOG_PATH.read_text())
PLAYER_NAMES = tuple(CATALOG['playerNames'])
CLAN_NAMES = tuple(CATALOG['clanNames'])
DESCRIPTIONS = tuple(CATALOG['clanDescriptions'])
NAME_KEY = 'DF_PlayerName'
NAME_SPACE = len(PLAYER_NAMES) * 10 ** CATALOG['suffixDigits']
PLAYER_PATTERN = re.compile(r'(' + '|'.join(re.escape(n) for n in PLAYER_NAMES) + r')[0-9]{7}')
CLAN_PATTERN = re.compile(r'(' + '|'.join(re.escape(n) for n in CLAN_NAMES) + r') [0-9]{7}')
TAG_PATTERN = re.compile(r'C[0-9]{7}')


def valid_player_name(value):
    return isinstance(value, str) and PLAYER_PATTERN.fullmatch(value) is not None


def valid_clan_name(value):
    return isinstance(value, str) and CLAN_PATTERN.fullmatch(value) is not None


def valid_clan_tag(value):
    return isinstance(value, str) and TAG_PATTERN.fullmatch(value) is not None


def generate_player_name(previous=''):
    for _ in range(64):
        value = secrets.choice(PLAYER_NAMES) + f'{secrets.randbelow(10_000_000):07d}'
        if value != previous:
            return value
    raise ValueError('Name generation unavailable; try again')


def generate_clan_name(previous=''):
    for _ in range(64):
        value = secrets.choice(CLAN_NAMES) + f' {secrets.randbelow(10_000_000):07d}'
        if value != previous:
            return value
    raise ValueError('Clan name generation unavailable; try again')


def generate_clan_tag():
    return f'C{secrets.randbelow(10_000_000):07d}'


def fallback_player_name(identity):
    digest = int.from_bytes(hashlib.sha256(str(identity).encode()).digest(), 'big')
    return PLAYER_NAMES[digest % len(PLAYER_NAMES)] + f'{digest // len(PLAYER_NAMES) % 10_000_000:07d}'


def visible_player_name(value, identity):
    return value if valid_player_name(value) else fallback_player_name(identity)


def profile_name(entries):
    return next((e.get('text') for e in entries if e.get('key') == NAME_KEY), None)


def with_player_name(entries, name):
    values = {e['key']: e for e in entries}
    values[NAME_KEY] = {'key': NAME_KEY, 'kind': 1, 'text': name}
    return sorted(values.values(), key=lambda e: e['key'])


def reserve_player_name(db, account, preferred=None):
    """Claim current names atomically using SQLite's UNIQUE index.

Invalid legacy/client text cannot replace an assigned name. A collision selects
another catalog name; only current claims are retained, so rerolls release names.
"""
    current = db.execute('SELECT name FROM safe_player_name WHERE account=?', (account,)).fetchone()
    candidate = preferred if valid_player_name(preferred) else current[0] if current else generate_player_name()
    for _ in range(64):
        owner = db.execute('SELECT account FROM safe_player_name WHERE name=?', (candidate,)).fetchone()
        if not owner or owner[0] == account:
            db.execute('INSERT INTO safe_player_name(account,name) VALUES(?,?) ON CONFLICT(account) DO UPDATE SET name=excluded.name', (account, candidate))
            return candidate
        candidate = generate_player_name(candidate)
    raise ValueError('Name allocation unavailable; try again')


def normalize_sqlite_profile(db, account):
    revision, encoded = db.execute('SELECT revision,data FROM profile WHERE account=?', (account,)).fetchone()
    entries = json.loads(encoded)
    name = reserve_player_name(db, account, profile_name(entries))
    if any(e.get('key') == NAME_KEY and e.get('kind') == 1 and e.get('text') == name for e in entries):
        return name
    clean = json.dumps(with_player_name(entries, name), separators=(',', ':'), sort_keys=True)
    if clean != encoded:
        # Initial identity is part of the initial profile, not a client progression
        # edit. Existing saves get a revision bump to prevent stale overwrites.
        db.execute('UPDATE profile SET data=?,revision=revision+? WHERE account=?', (clean, int(bool(entries)), account))
    return name


def description_for(data):
    index = data.get('descriptionId', 0)
    if type(index) is not int or not 0 <= index < len(DESCRIPTIONS):
        raise ValueError('Choose a preset clan description')
    legacy = data.get('description')
    if legacy not in (None, '') and legacy not in DESCRIPTIONS:
        raise ValueError('Custom clan descriptions are no longer available')
    return DESCRIPTIONS[index]


def validate_clan_identity(data):
    name, tag = data.get('name'), data.get('tag')
    if not valid_clan_name(name):
        raise ValueError('Use a generated clan name')
    if not valid_clan_tag(tag):
        raise ValueError('Use a generated clan tag')
    return name, tag, description_for(data)


def migrate_sqlite_clans(db):
    for identifier, name, tag, description in db.execute('SELECT id,name,tag,description FROM social_clan').fetchall():
        if valid_clan_name(name) and valid_clan_tag(tag) and description in DESCRIPTIONS:
            continue
        if not valid_clan_name(name):
            for _ in range(64):
                name = generate_clan_name()
                if not db.execute('SELECT 1 FROM social_clan WHERE name_search=? AND id<>?', (name.casefold(), identifier)).fetchone():
                    break
            else:
                raise ValueError('Clan name allocation unavailable')
        if not valid_clan_tag(tag):
            for _ in range(64):
                tag = generate_clan_tag()
                if not db.execute('SELECT 1 FROM social_clan WHERE tag=? AND id<>?', (tag, identifier)).fetchone():
                    break
            else:
                raise ValueError('Clan tag allocation unavailable')
        if description not in DESCRIPTIONS:
            description = DESCRIPTIONS[0]
        db.execute('UPDATE social_clan SET name=?,name_search=?,tag=?,description=? WHERE id=?', (name, name.casefold(), tag, description, identifier))
