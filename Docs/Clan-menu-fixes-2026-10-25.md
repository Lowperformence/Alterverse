# Clan, menu and leaderboard fixes

The clan-image picker previously enumerated shop entries, hiding owned starter and
reward avatars. It now uses `AvatarCollection.OwnedIds()` and opens the Avatars tab
when browsing more pictures. The account backend ownership gate must also be updated;
changing only the picker is insufficient for starter/reward pictures.

## Client behavior

- Chat/moderation agreements use their own large panel, 24-unit scrollable text and
  a 36-unit visible checkbox inside a 56-unit clickable row. The clan header does
  not overlap the rules. Consent remains explicit; no age group is preselected.
- Age-group wording explains that an exact age or birthdate is not stored. Existing
  eligibility is unchanged. The app's Play Console audience settings still need to
  be reviewed before changing the gate; age 13 is not a universal compliance rule.
- A failed chat request shows its message and a retry button instead of an indefinite
  Connecting label. The composer requires a successful room read.
- Insufficient-Shards purchase actions open Store > Shards without starting a purchase.
  Owned equipment, unreleased content and pending clan receipts retain their guards.
- Google ranking upload waits for a dirty account save, then native score submission,
  then raw/formatted score readback on manual sync/open. The native UI opens afterward.
  A zero readback is not labelled a successful synchronization. Google score formatting
  and publication settings remain separate live checks.
- Hero previews retain at most three model/loadout variants. The procedural 2048×1152
  background keeps the same pixel formulas and quality; CPU preparation runs on one
  cancellable worker. Textures/sprites are installed on Unity's main thread. The last
  completed background stays visible while the new one is prepared. WebGL retains its
  synchronous fallback. Initial model construction and GPU upload still need profiling.

## Existing Node account backend

`Server/commerce/node/enable-owned-clan-avatars.mjs` patches the deployed standalone
`2026-10-24.clans.node.1` index, preserving the original source and package.json:

```sh
node Server/commerce/node/enable-owned-clan-avatars.mjs current-index.js corrected-index.js
node --check corrected-index.js
node Server/commerce/node/test-owned-clan-avatars.mjs current-index.js
```

The patch accepts starter IDs 0–5, the owned founder avatar 6 and other explicitly
owned avatars/rewards. Leader-only authorization is retained. It does not alter
wallets, prices, receipts, account authentication, storage or dependencies. The Python
account implementation uses the same ownership checks for future deployments.
The corrected standalone health revision is `2026-10-25.clans.node.2`.

## Live chat investigation

The running `.node.1` backend serves clans. Chat failure after accepting terms has
not yet been diagnosed from a real authenticated request/log. Its first message read
requires this Firestore composite index, already defined in
`Server/commerce/firestore-chat.indexes.json`:

- Collection group: `dimensions-front-social-message`
- Query scope: collection
- `clan`: ascending
- `created_at`: descending

Inspect the existing database's index state and Cloud Run logs before calling the
chat issue resolved. Do not reset clan documents or accounts. If absent, create the
index and wait for Ready. No AAB is needed for that database configuration change.
The outbox's separate `sent_at`/`next_at` index is for administrator email delivery.

## Validation and release boundary

Passed locally with controlled engine/native-service boundaries:

- Chat client/menu compilation, consent/age restrictions, failure display, avatar
  picker and Store navigation behaviors.
- 13 Google score ordering/account/readback checks, including a new win during upload,
  account changes, failed saves and raw score vs display formatting.
- 12 preview cache/background scheduling checks, including bounded model reuse,
  canceled obsolete background work and main-thread-only texture/sprite creation.
- 32 Python social tests, including starter/founder/reward ownership and unchanged wallets.
- Standalone Node source-preservation/ownership/leader-only/unchanged-wallet checks.

No native Android/Unity rendering, frame-time measurement, real Google score submission
or authenticated live chat check has been performed here. UI/client fixes require the
next AAB. The Node avatar change requires deploying the corrected account index; it is
not part of the Edgegap game-server image and is not live merely because this commit exists.

## Additional Hotzone spawn finding

At inspected branch head `0efceadb8ea95769dde5a02706e0d1416af66f22`,
`HotZoneTank.SpawnReward` calls `Initialize` synchronously. It builds the model
and authored surfaces before returning; first `AuthoredHotZoneArt.Apply` loads
and parses `AuthoredHotZone/Parts.json` (2,264,090 bytes), fits meshes and prepares
materials. This is a plausible frame-time spike, not a measured cause of a specific
network ping. Hotzone preload/spawn changes are not included in this menu/clan patch;
coordinate them with the concurrent Hotzone VFX work and verify on-device.
