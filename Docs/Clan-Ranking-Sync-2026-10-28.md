# Clan chat and ranking synchronization

The configured HTTPS account endpoint responded with health revision
`2026-10-24.clans.node.1` during this investigation. This verifies the public
health route, not authenticated profile saves, chat or Firestore configuration.
No account, endpoint, identity provider or database was reset.

## Client ranking reads

Both player levels and clan levels try the normal profile flush before reading.
If saving fails, read the authoritative values already on the server and display
`Showing saved server data. Account synchronization pending.` Local changes stay
dirty; a ranking request sends no XP, balances or replacement profile. Clan creation
continues to require a successful save before its paid operation can begin.

The previous fix covered clan levels and membership state; this update also covers
player levels. It requires a new Unity client build. It does not make failed saves
successful or make newer local progression appear on the server.

## Reproduced Node profile compatibility failure

Both retained Node source exports used by the standalone builder validate
`DF_DailyQuestWonMaps_v1` against `(1 << 11) - 1`. The current client builds its
mask from all 14 `ArenaVariant` values; the Python server validator already allows
`(1 << 14) - 1`. A set bit for map 11, 12 or 13 therefore makes the old Node
validator reject the entire save as `invalid_profile`. This failure was reproduced
locally; it is not proof that the user's live profile contains those bits or that
the running source is otherwise byte-identical to these exports.

`Server/commerce/node/repair-clan-sync.mjs` applies a bounded incremental repair
to a fresh export of the existing standalone Node source. It updates only the map
mask, adds the existing explicit missing-chat-index diagnostic and changes the
health revision to `2026-10-28.clans.sync.1`. It refuses unknown validator boundaries,
unexpected masks and a non-standalone backend. Reapplication is idempotent. The
input remains intact and an existing output file is never overwritten.

From the repository root:

```sh
node Server/commerce/node/test-clan-sync.mjs
node Server/commerce/node/repair-clan-sync.mjs CURRENT_INDEX.js CORRECTED_INDEX.js
node --check CORRECTED_INDEX.js
```

Compare the result against the current exported source before updating the existing
Cloud Run Node service. Keep its existing package manifest, runtime, entry point,
service identity, environment and secret references. This patch performs no
deployment, data migration, debit, account relinking or database reset. The
standalone builder alone still reproduces its original reviewed source; apply this
incremental repair after building from that source as well.

## Chat remains a separate live check

The generic chat HTTP 503 does not establish its cause. The first history read uses
the `dimensions-front-social-message` collection and requires the composite index
`clan ASC, created_at DESC`. Inspect authenticated Cloud Run errors and the current
index state before changing database configuration. If it is absent, the existing
operator helper inspects first, then can create only this index:

```sh
python3 Tools/ClanChat/setup_firestore.py --backend node --chat-index-only
python3 Tools/ClanChat/setup_firestore.py --backend node --chat-index-only --execute
```

The helper requires authenticated gcloud. It does not deploy a replacement backend,
alter TTL or touch outbox configuration in this mode. Wait for the index to be READY
and test an authenticated clan read/send. An index diagnostic alone is not a repair
of the underlying index.

## Validation and remaining work

- The managed production-client/menu regression passes, including player and clan
  reads after a failed save, retained local edits, server failure messages and the
  clan-creation save/debit guard.
- 63 Node checks reproduce the old map-11 failure, accept every one of the 14 maps
  and their combined mask, reject out-of-range/malformed values, retain other
  validation and prove all unrelated source is unchanged for both exports.
- The generated corrected backend passes the JavaScript syntax check. Existing
  chat-index diagnostic tests and 10 database-setup checks pass.

No live source deployment, authenticated Cloud log/index inspection, Unity build or
Android test has been performed. Those steps still require operator access.
