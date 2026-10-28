# Online content, clan chat and ranking repair

Scope: missing online content (specifically the insertion freighter), BOLT-7 walking,
a grenade-like effect before the match, clan chat and leaderboard access. No balance,
art assets, purchases or unrelated features changed.

## Causes and changes

- The dedicated server built its world and advanced the opening before phones had
  built/revealed theirs. The full opening could finish behind the loading surface.
  The opening pause now exists before actors are constructed on every peer. The
  server waits for authenticated, round-bound arena-ready messages from all human
  players; bots require none. A 90-second load failure cancels before gameplay and
  voids an already registered ranked match. Readiness resets with the world.
- BOLT-7 bypasses `HeroVisualMotion`, but only that presenter received buffered
  network poses. Its own rig therefore sampled discontinuous packet movement,
  switching Walk/Idle between packets. Its visual root now consumes the same
  source-timed buffer; collision and owner movement remain unchanged. Spawn idle
  no longer locks out the first walk cycle.
- Creating the opening in a later Update left a possible first simulation frame
  before the pause on a dedicated server. Moving it before actors closes that gap.
  BOLT-7 VFX clones are also staged under an inactive parent, preventing active
  prefab variants from auto-playing particles/audio during pool warmup. These are
  concrete early-effect paths; the user's one observed grenade has no captured
  device log, so its exact origin cannot be proven retrospectively.
- Chat reads previously waited for every dirty profile save. Unrelated failed
  saving could prevent the request altogether. Only profile-dependent social
  routes now flush; chat authorization/consent/moderation remain server-owned.
- Google ranking-open requests were discarded after a save, score upload or score
  readback failure. Browsing now opens after the attempt while retaining the sync
  error. Account switches still cancel the pending operation.

## Existing account service / live boundary

Read-only GET of the configured service root returned
`2026-10-24.clans.node.1`. The supplied standalone Node source reads messages with
`where(clan).orderBy(created_at desc)`, which requires the existing composite-index
definition in `Server/commerce/firestore-chat.indexes.json`.

The previous setup helper assumed Python's `DF_ACCOUNT_STORAGE=firestore`, whereas
the deployed Node source directly constructs Firestore and fixes the namespace.
The helper now has an explicit Node mode and a repair restricted to the message
index, with no TTL, outbox, runtime or deployment changes:

```sh
python Tools/ClanChat/setup_firestore.py --backend node --chat-index-only
python Tools/ClanChat/setup_firestore.py --backend node --chat-index-only --execute
```

Run with an authenticated Google Cloud operator. If the index is CREATING, rerun
after it becomes READY. Existing indexes/data/accounts are preserved. No account
service redeploy is needed for the index. The actual live index state and a real
authenticated chat request could not be inspected from GitHub access alone.

Optional: `Server/commerce/node/repair-chat-index-error.mjs` patches the supplied
standalone index to show `CHAT_INDEX_REQUIRED` instead of a generic failure. Its
test runs against the actual supplied source and verifies unrelated bytes remain
unchanged. This improves diagnostics; creating the index is the actual DB repair.

## Validation and installation

Managed tests exercise the real opening/state-sync, transport, BOLT-7 presenter,
effect pool, chat client, Google leaderboard and handshake classes with controlled
engine/network/native-service boundaries. Tests include slow loading, spoofed/stale
readiness, disconnects, continuous gait between 20 Hz packets, relocation, unchanged
owner movement, failed profile saves and failed Google upload/readback.

Passed locally: insertion/readiness suite, BOLT-7 presentation and VFX suites,
transport suite, world-motion suite, lobby/identity/ranked suite, clan-menu suite,
Google ranking suite, 10 index-setup tests, standalone Node error/source-preservation
checks and all 294 Python commerce tests (the three match-format tests were rerun
after fetching their unchanged matchmaking fixtures).

App and dedicated Linux server must be built from the same repaired commit and
installed/deployed together. Handshake versions advance to dedicated 8, Relay online
4 and co-op 6 so older clients cannot silently enter the new readiness protocol.
No Unity/Android/Linux native build, live deployment, visual device verification or
authenticated production chat/Google-score test was performed in this environment.
