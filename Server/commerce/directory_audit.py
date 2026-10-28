"""Audit saved-account coverage of level rankings; --apply repairs public metadata.

Default is read-only. Run with the existing account server's Firestore project,
namespace and service credentials. Output contains counts, never private profiles
or account identifiers. This maintenance scan is not part of a ranking request.
"""
import argparse
import json

from firestore_ledger import FirestoreLedger
from social import public_values


def audit_directory(ledger, apply=False):
    counts = dict(profiles=0, missing=0, stale=0, current=0, repaired=0)
    for profile in ledger.accounts.stream():
        counts['profiles'] += 1
        public = ledger.players.document(profile.id).get()
        old = public.to_dict() if public.exists else {}
        name, name_search, xp, level, avatar = public_values(ledger._data(profile)['profile'], profile.id)
        expected = dict(account=profile.id, name=name, name_search=name_search,
                        xp=xp, level=level, avatar=avatar)
        if not public.exists:
            counts['missing'] += 1
        elif not old.get('player_id') or any(old.get(k) != v for k, v in expected.items()):
            counts['stale'] += 1
        else:
            counts['current'] += 1
            continue
        if apply:
            # Read fresh inside the transaction; never replay the scanned profile.
            ledger.wallet(profile.id)
            counts['repaired'] += 1
    return counts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True)
    parser.add_argument('--namespace', default='dimensions-front')
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    ledger = FirestoreLedger(project_id=args.project, namespace=args.namespace)
    print(json.dumps(dict(mode='repair' if args.apply else 'audit',
                          **audit_directory(ledger, apply=args.apply)), sort_keys=True))


if __name__ == '__main__':
    main()
