# BETA Cappy – implementation and acceptance checks

## Scope

Add a navy 3D baseball cap with cyan stitching and white BETA lettering. One account unlock covers all ten heroes, with an independent headwear selection per hero. Existing wings can remain equipped. The cap has no collider, gameplay stat or progression bonus.

| Premium Battle Pass tier | Cosmetic reward | Existing other rewards |
| --- | --- | --- |
| 25 | Beta Crown avatar (ID 16), moved from tier 50 | Retained |
| 50, final tier | BETA Cappy | Retained: 200 shards and 3 shield cores |

The cap appears in **Inventory > Cosmetics > Skins** after it is unlocked. It cannot be bought for shards or accidentally purchased at its catalog price of zero. No new headwear shop purchases are introduced.

The free track, pass XP curve, prices, 800-shard premium return, combat, existing wing behavior, heroes and maps retain their existing behavior. Changes outside this scope require the owner's agreement.

## Unlock and storage

- Cap ownership: `DF_HeadwearOwned_BetaCap`.
- Per-hero selection: `DF_HeadwearEquipped_<hero ID>`; zero means no headwear, seven means BETA Cappy.
- Claims use the existing profile transaction and account-profile storage path. No account endpoint or server configuration is changed here.
- Existing premium claims are retained. A player who already claimed tier 25 receives the newly moved avatar; a player who already claimed tier 50 receives the cap. This backfill checks premium entitlement, XP and the original claim bit. It grants only missing cosmetics, never replays currency/items, and does not auto-equip either item.
- Owners of the old final avatar keep it. The claim does not remove any old reward.

## Head attachment

Authored heroes use the actual `Head` bone and baked vertices weighted at least 50% to it. Procedural fallback heroes use their `DF_Head` mesh bounds. The cap is sized from those bounds, then parented to the selected head so head motion carries it. Unreadable or missing head geometry produces a fitting error rather than attaching to an estimated body point.

The mesh uses three material sections. Runtime shaders use the project's existing URP Lit/Standard support. The automatic bounds-based fit still requires visual approval on the real imported models, especially helmets, ears, horns and hair.

## Multiplayer

Headwear uses separate V1 request/state channels on the existing hero synchronizer. The server validates round identity and the existing slot-owner authorization before relaying a valid headwear ID. Clients accept state only from the server and validate the entire packet before applying it. These messages do not grant or write account ownership.

Existing movement and match handshake formats are retained. Older servers receive at most three cosmetic probes; other players will only see the cap once both client and match-server builds contain the relay. Older/unregistered clients are not sent the new state channel. This does not add server-side purchase verification beyond the existing cosmetic/account system.

## Automated checks

With a .NET 8 SDK, from the repository root:

```sh
python Tools/Headwear/Regression/run.py --dotnet-root "$DOTNET_ROOT"
python Tools/Shop/PremiumWingsRegression/run.py --dotnet-root "$DOTNET_ROOT"
python Tools/Multiplayer/Regression/run.py --dotnet-root "$DOTNET_ROOT"
```

The headwear suite compiles production selection, catalog, pass rewards, mesh, head fitting and the new message handlers against isolated memory/engine/network fixtures. It checks gating, claim idempotence, legacy backfill, all ten hero selection keys, independent wing/head slots, profile serialization/reload, mesh budgets/indices, synthetic authored/procedural head detection, malformed/stale/unauthorized messages, host/client relay and older-server probe limits. It also runs the existing collection checks. The GitHub workflow additionally runs the existing premium-wing and movement/transport suites.

These are not a Unity compilation, live cloud save test, render test, actual imported-model fit test or live multiplayer session.

## Unity fitting studio

Use the project's Unity **6000.6.1f1** version.

1. Open **DIMENSIONS FRONT > Headwear > BETA Cappy fitting studio**.
2. Run **Check head attachment on all 10 heroes**. The same check is available under **DIMENSIONS FRONT > Checks > BETA Cappy all-hero attachment**.
3. Select every hero. Inspect front, both sides, back and top by dragging the preview. Toggle full-body/head close-up, cap, wings and head tilt.
4. Check that the BETA text faces forward, the visor is visible, and the cap clears the head/helmet/hair without floating. Inspect lighting and text on small screens.

The studio uses isolated preview actors and override selections. It performs no purchases or profile writes and does not edit the current scene. The automatic Unity check checks attachment, repeat apply/unequip, material/vertex budgets and lack of cap colliders; human review must still check clipping and appearance.

## Device and live-session release gate

Use a dedicated test account and a test build; do not reset an existing player's progress.

- Confirm a non-premium account and tier 49 account cannot claim the final cap.
- Claim premium tier 25: avatar is available. Claim premium tier 50: cap is owned, old shard/core rewards occur once, and the cap is not auto-equipped.
- In Inventory > Cosmetics > Skins, equip and unequip the cap for each hero. Keep wings equipped, switch heroes and confirm independent selections.
- Restart the Android app and reconnect the same account; confirm ownership, per-hero headwear, XP, shards, items and existing selections persist. Confirm a different account does not inherit the cap. Test cloud recovery on a second installation only after the first save completes.
- Inspect all ten heroes during idle, movement, attacks, abilities, death and respawn. Check front/side/back, cap lettering, lighting, clipping, camera framing and mobile performance.
- With updated client and match-server builds, test host and joining player wearing/removing the cap, all supported online authority modes, reconnect, respawn and a second round. Both clients must see the expected headwear. Test an older server/client combination retains normal match behavior.
- Inspect Android/Unity logs for fitting, missing-shader, rendering, save or unknown-message errors.

Do not release on the basis of fixture tests alone. This change does not build, merge or deploy a new Android app or Edgegap server.
