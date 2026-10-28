# Player and clan level ranking audit

## Findings

The level lists use the account server, independently of Google Play MOST WINS.
Player ordering is level descending, XP descending, then stable player ID. Clan
ordering is the sum of current members' levels, then stable clan ID. Pagination
returns eight entries per page and a global total/self rank.

A coverage gap was reproduced using the unwrapped FirestoreLedger: save two
profiles, at levels 2 and 49, then read rankings only as the first player. Before
the fix, two account profiles existed but the ranking contained only the level-2
player. The public projection was previously a separate HTTP-layer operation.
Existing Firestore social tests wrapped profile saves with an extra sync and
therefore masked the gap. Old profiles not backfilled and an interrupted/failed
post-save sync can be absent or stale.

Clan totals use those public rows. In another reproduction, a clan retained two
members but lost the level-49 member's public projection: memberCount was two
while totalLevel fell from 51 to 2. Repairing the projection restored 51 without
changing clan membership or identity.

The Unity tabs also dropped a load if another social request was busy, and cached
results could remain old until manual refresh. They now request once idle,
refresh on saved account/profile changes, and refresh visible rankings every
30 seconds while preserving the current page.

## Changes and validation

Wallet initialization/profile saves atomically maintain public player metadata and
preserve existing public IDs. The old wallet-response sync re-reads current data,
so an old reply cannot rewind XP. Ranking requests do not scan private profiles.

Six new strict fake-Firestore tests cover all 17 players across three pages,
atomic rollback, repair during a no-op save, read-only auditing, idempotent repair,
stable IDs, stale responses and restored clan totals. Existing social, account,
wallet, purchase, naming and clan tests passed (170 Python tests across the
selected modules). The production C# client/menu regression also passed, including
deferred tab loads and account/profile/timed refresh.

Tests use controlled database/engine boundaries, not a deployed Firestore project,
Unity rendering or device networking.

## Deployment and existing profiles

Deploy the account-server code as well as the updated client. Then run with the
existing Firestore project, namespace and service credentials:

```sh
PYTHONPATH=Server/commerce python Server/commerce/directory_audit.py --project PROJECT --namespace dimensions-front
PYTHONPATH=Server/commerce python Server/commerce/directory_audit.py --project PROJECT --namespace dimensions-front --apply
```

The first command reports profile, missing, stale and current counts without
writes. The second repairs missing/stale projections; each repair re-reads the
current committed profile. A second audit should report no missing/stale records.
This maintenance scan reads stored accounts; it is separate from player-facing
ranking requests.

No live server credentials were available in this workspace. Live counts,
deployment and repair were not performed. Players whose progress has never been
saved to a server account cannot be recovered from the server directory alone.
