"""Solo 4v4 ranked: authenticated queue, ticket-bound admission and server-only settlement."""
import base64
import hashlib
import hmac
import json
import math
import re
import time
import uuid
from ranked_store import SQLiteRankedStore, FirestoreRankedStore
from safe_identity import profile_display_name, visible_player_name

PROFILE = 'dimensions-front-ranked'
TEAM_SIZE = 4
MATCH_PLAYERS = 2 * TEAM_SIZE
PLACEMENTS = 5
TIERS = ('Bronze', 'Silver', 'Gold', 'Platinum', 'Diamond', 'Master', 'Dimensions Legend')


def identifier(value):
    if not isinstance(value, str) or not re.fullmatch(r'[A-Za-z0-9_.:-]{1,128}', value):
        raise ValueError('Invalid ranked identifier')
    return value


def rating_view(player):
    points = player['mmr']
    tier = 0 if points < 1000 else min(6, 1 + (points - 1000) // 200)
    floor = 0 if tier == 0 else 800 + tier * 200
    width = 1000 if tier == 0 else 200
    division = max(1, 3 - min(2, (points - floor) * 3 // width))
    placed = player['games'] >= PLACEMENTS
    return dict(season=player['season'], points=points, games=player['games'], wins=player['wins'],
                placementsRemaining=max(0, PLACEMENTS - player['games']),
                rank=(TIERS[tier] + (f' {division}' if tier < 6 else '')) if placed else 'Unranked',
                cooldownUntil=player.get('cooldownUntil', 0), lastResult=player.get('lastResult'))


class EdgegapRankedGateway:
    def __init__(self, endpoint, token, session=None):
        import requests
        if not endpoint.startswith('https://') or not token:
            raise ValueError('Ranked matchmaker configuration required')
        self.endpoint, self.token = endpoint.rstrip('/'), token
        self.session = session or requests.Session()

    def request(self, method, path, data=None):
        response = self.session.request(method, self.endpoint + path, json=data,
                                        headers={'Authorization': self.token}, timeout=15)
        response.raise_for_status()
        return response.json() if response.content else {}

    def create(self, mmr):
        return self.request('POST', '/groups', {'profile': PROFILE, 'is_ready': True,
                                               'attributes': {'elo_rating': mmr, 'queue': 'ranked'}})

    def poll(self, group, member):
        return self.request('GET', f'/groups/{identifier(group)}/members/{identifier(member)}')

    def cancel(self, group):
        try:
            return self.request('DELETE', '/groups/' + identifier(group))
        except Exception as error:
            if getattr(getattr(error, 'response', None), 'status_code', None) != 404: raise
            return {}


class Ranked:
    def __init__(self, ledger, gateway, signing_key, season='S1', clock=time.time):
        if len(signing_key) < 32:
            raise ValueError('Ranked admission key must contain at least 32 characters')
        self.store = SQLiteRankedStore(ledger.path) if hasattr(ledger, 'path') else FirestoreRankedStore(ledger)
        self.ledger = ledger
        self.gateway, self.key, self.season, self.clock = gateway, signing_key.encode(), identifier(season), clock

    def player(self, account):
        return dict(account=account, season=self.season, mmr=1000, games=0, wins=0, cooldownUntil=0,
                    listedScore=-1, displayName='TRAVELER', active=None, lastResult=None)

    def pk(self, account):
        return self.season + ':p:' + identifier(account)

    def status(self, account):
        return rating_view(self.store.atomic(lambda tx: tx.get(self.pk(account)) or self.player(account)))

    def admission(self, account, ticket, mmr, expires):
        payload = json.dumps(dict(account=account, ticket=ticket, mmr=mmr, season=self.season, exp=expires),
                             sort_keys=True, separators=(',', ':')).encode()
        body = base64.urlsafe_b64encode(payload).rstrip(b'=').decode()
        signature = hmac.new(self.key, body.encode(), hashlib.sha256).hexdigest()
        return body + '.' + signature

    def verify(self, token):
        if not isinstance(token, str) or len(token) > 1024:
            raise PermissionError('Invalid admission')
        try:
            body, signature = token.rsplit('.', 1)
            expected = hmac.new(self.key, body.encode(), hashlib.sha256).hexdigest()
            if not hmac.compare_digest(signature, expected):
                raise PermissionError('Invalid admission')
            data = json.loads(base64.urlsafe_b64decode(body + '=' * (-len(body) % 4)))
            if data['season'] != self.season or data['exp'] <= self.clock():
                raise PermissionError('Expired admission')
            identifier(data['account']); identifier(data['ticket'])
            return data
        except (ValueError, KeyError, TypeError):
            raise PermissionError('Invalid admission')

    def search(self, account, name):
        now, reservation = int(self.clock()), uuid.uuid4().hex
        assigned_name = profile_display_name(self.ledger.wallet(account)['profile'])
        def reserve(tx):
            p = tx.get(self.pk(account)) or self.player(account)
            if p.get('activeMatch'):
                match = tx.get(p['activeMatch'])
                if match and not match.get('result') and now - match['started'] < 7200:
                    raise ValueError('Finish your current ranked match')
                p.pop('activeMatch', None)
            if p['cooldownUntil'] > now:
                raise ValueError('Ranked cooldown active')
            active = p.get('active')
            if active and active['expires'] > now:
                return p, False
            p['active'] = dict(reservation=reservation, expires=now + 600)
            p['displayName'] = assigned_name
            tx.set(self.pk(account), p)
            return p, True
        player, create = self.store.atomic(reserve)
        if create:
            try:
                member = self.gateway.create(player['mmr'])
                group, member_id = identifier(member['group_id']), identifier(member['member_id'])
            except Exception:
                self._release(account, reservation)
                raise
            def save(tx):
                p = tx.get(self.pk(account))
                if not p or not p.get('active') or p['active']['reservation'] != reservation:
                    return False
                p['active'].update(group=group, member=member_id)
                tx.set(self.pk(account), p)
                return True
            if not self.store.atomic(save):
                self.gateway.cancel(group)
                raise ValueError('Ranked search cancelled')
        return self.poll(account)

    def _release(self, account, reservation):
        def clear(tx):
            p = tx.get(self.pk(account))
            if p and p.get('active') and p['active']['reservation'] == reservation:
                p['active'] = None
                tx.set(self.pk(account), p)
        self.store.atomic(clear)

    def poll(self, account):
        p = self.store.atomic(lambda tx: tx.get(self.pk(account)))
        active = p.get('active') if p else None
        if not active or active['expires'] <= self.clock():
            raise ValueError('No active ranked search')
        if not active.get('group'):
            return {'status': 'PREPARING'}
        try:
            member = self.gateway.poll(active['group'], active['member'])
        except Exception as error:
            if getattr(getattr(error, 'response', None), 'status_code', None) == 404:
                self._release(account, active['reservation'])
                raise ValueError('Ranked search expired. Search again.')
            raise
        ticket = member.get('ticket_id')
        if ticket:
            ticket = identifier(ticket)
            # Persist the exact owned ticket. Never bind an account supplied by the game client.
            def bind(tx):
                current = tx.get(self.pk(account))
                if not current or not current.get('active') or current['active']['reservation'] != active['reservation']:
                    raise ValueError('Ranked search cancelled')
                current['active']['ticket'] = ticket
                tx.set(self.pk(account), current)
            self.store.atomic(bind)
            member['admission'] = self.admission(account, ticket, p['mmr'], active['expires'])
        return member

    def cancel(self, account):
        p = self.store.atomic(lambda tx: tx.get(self.pk(account)))
        active = p.get('active') if p else None
        if active:
            # Revoke admission before a slow/failing external deletion. A cancelled
            # player must never remain eligible for a delayed match registration.
            self._release(account, active['reservation'])
            if active.get('group'):
                self.gateway.cancel(active['group'])
        return {'status': 'CANCELLED'}

    def register(self, data):
        match_id = identifier(data.get('matchId'))
        roster = data.get('players')
        if not isinstance(roster, list) or len(roster) != MATCH_PLAYERS:
            raise ValueError('Ranked requires eight humans')
        players, accounts, tickets = [], set(), set()
        for entry in roster:
            token = self.verify(entry.get('admission'))
            if type(entry.get('team')) is not int or entry['team'] not in (0, 1) or entry.get('ticket') != token['ticket']:
                raise ValueError('Ranked team or ticket mismatch')
            if token['account'] in accounts or token['ticket'] in tickets:
                raise ValueError('Duplicate ranked player')
            accounts.add(token['account']); tickets.add(token['ticket'])
            players.append(dict(account=token['account'], ticket=token['ticket'], team=entry['team'], mmr=token['mmr']))
        if sum(p['team'] == 0 for p in players) != TEAM_SIZE:
            raise ValueError('Ranked requires balanced 4v4 teams')
        players.sort(key=lambda p: p['ticket'])
        key = self.season + ':m:' + match_id
        def register(tx):
            old = tx.get(key)
            if old:
                if old.get('voided'): raise ValueError('Voided match cannot restart')
                if old['players'] != players:
                    raise ValueError('Match roster already registered')
                return {'registered': True}
            profiles = [tx.get(self.pk(p['account'])) for p in players]
            for p, profile in zip(players, profiles):
                if not profile or profile.get('activeMatch') or not profile.get('active') or profile['active'].get('ticket') != p['ticket'] or profile['mmr'] != p['mmr']:
                    raise PermissionError('Admission is no longer active')
            tx.set(key, dict(players=players, started=int(self.clock()), result=None))
            for p, profile in zip(players, profiles):
                profile['active'], profile['activeMatch'] = None, key
                tx.set(self.pk(p['account']), profile)
            return {'registered': True}
        return self.store.atomic(register)

    def settle(self, data):
        key = self.season + ':m:' + identifier(data.get('matchId'))
        winner, leavers = data.get('winner'), data.get('leavers', [])
        if type(winner) is not int or winner not in (0, 1) or not isinstance(leavers, list):
            raise ValueError('Invalid ranked result')
        leavers = sorted(set(identifier(t) for t in leavers))
        result = dict(winner=winner, leavers=leavers)
        def settle(tx):
            match = tx.get(key)
            if not match:
                raise ValueError('Unregistered ranked match')
            if match.get('voided'): raise ValueError('Voided match cannot settle')
            if match.get('result'):
                if match['result'] != result:
                    raise ValueError('Conflicting ranked result')
                return []
            roster = match['players']
            if not set(leavers).issubset({p['ticket'] for p in roster}):
                raise ValueError('Unknown leaver')
            profiles = [tx.get(self.pk(p['account'])) for p in roster]
            # Use the registered roster size so already-running legacy matches can still settle.
            means = [sum(p['mmr'] for p in roster if p['team'] == team) /
                     sum(p['team'] == team for p in roster) for team in (0, 1)]
            for p, profile in zip(roster, profiles):
                if not profile or profile.get('activeMatch') != key:
                    raise ValueError('Ranked profile match mismatch')
                expected = 1 / (1 + 10 ** ((means[1-p['team']] - means[p['team']]) / 400))
                won = p['team'] == winner
                k = 40 if profile['games'] < PLACEMENTS else 24
                delta = int(math.floor(k * (int(won) - expected) + 0.5))
                left = p['ticket'] in leavers
                if left:
                    delta = min(-10, delta - 10)
                    profile['cooldownUntil'] = int(self.clock()) + 900
                before = profile['mmr']
                profile['mmr'] = max(0, before + delta)
                profile['games'] += 1
                profile['wins'] += int(won and not left)
                profile.pop('activeMatch', None)
                profile['listedScore'] = profile['mmr'] if profile['games'] >= PLACEMENTS else -1
                profile['lastResult'] = dict(matchId=data['matchId'], delta=profile['mmr']-before, winner=winner, left=left)
            match['result'] = result
            tx.set(key, match)
            for p, profile in zip(roster, profiles):
                tx.set(self.pk(p['account']), profile)
            return profiles
        self.store.atomic(settle)
        return {'settled': True}

    def dispatch(self, account, path, data):
        if path == '/ranked/status': return self.status(account)
        if path == '/ranked/search': return self.search(account, data.get('displayName'))
        if path == '/ranked/poll': return self.poll(account)
        if path == '/ranked/cancel': return self.cancel(account)
        if path == '/ranked/leaderboard':
            return {'season': self.season, 'leaders': [dict(displayName=visible_player_name(p['displayName'], p['account']), **rating_view(p)) for p in self.store.leaders(self.season)]}
        raise ValueError('Unknown ranked route')

    def void(self, data):
        key = self.season + ':m:' + identifier(data.get('matchId'))
        def clear(tx):
            match = tx.get(key)
            if not match:
                # Tombstone also rejects a delayed registration request after an aborted launch.
                tx.set(key, dict(players=[], started=int(self.clock()), result=None, voided=True))
                return {'voided': True}
            if match.get('result'): raise ValueError('Settled match cannot be voided')
            profiles = [tx.get(self.pk(p['account'])) for p in match['players']]
            match['voided'] = True
            tx.set(key, match)
            for p, profile in zip(match['players'], profiles):
                if profile and profile.get('activeMatch') == key:
                    profile.pop('activeMatch', None)
                    tx.set(self.pk(p['account']), profile)
            return {'voided': True}
        return self.store.atomic(clear)

