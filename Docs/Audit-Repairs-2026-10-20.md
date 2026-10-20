# Gameplay, online combat and account audit repairs — 20 October 2026

Base: `68d827ea3eb4491f0296d03bd301a81d32faa8c7`, `dev/vertical-slice`.

## Resulting behavior

- Hot Zone rewards use the same enemy classification when selecting and damaging a target. Lane soldiers and siege minions store their team on `LaneMinion`, so the former `TeamMember`-only hit check discarded every such hit. Friendly, dead and distant targets remain protected.
- Nonhero combat commands now carry their stable `CoopWorldEntityKey`. The server resolves that exact entity and rejects missing/dead identities rather than choosing a nearby unit around the old position. Hero targets still use their roster slot. Rejected target intentions are acknowledged so their predicted cooldown can recover. The command writer accommodates the additional bounded key.
- Owner movement uses a distance reserve that accrues with elapsed server time at the authoritative running speed. The reserve is bounded to 250 ms of movement plus 0.18 units. It is shared across all received packets, rather than granting an additional 0.18 units per message. Four valid 20 Hz poses delivered together after a 200 ms receive stall no longer incur rejection at the tested speeds. Sustained double-speed input at 20/60/120 Hz cannot acquire extra running distance. Roots, server movement and vortex pulling clear the reserve.
- Online end-of-match MVP uses complete authoritative result statistics. Live clients use their replicated hero K/D/A until those statistics arrive. Server/solo MVP combines hero-only K/D/A with damage/healing/objective telemetry; soldier quest kills do not inflate the score.
- Targeted and area ally healing passes its actual source to `Health.Heal`. Only effective HP restoration is credited to the healer. Existing two-argument/self-heal callers remain compatible.
- The lobby/ranked regression runner includes the current snapshot history/cadence dependencies. Its world-snapshot fixture includes source time, and its Hot Zone expectation now requires host snapshots to create replicas.
- Google Play fulfillment, profile saves, wallet refreshes, shard purchases and social spending share a client-side serialization boundary. Multiple pending Play purchases are verified in order. This prevents response/status races between concurrent financial operations. Completion/cancellation releases the boundary and account-generation checks continue rejecting old-account responses.

## Compatibility

Basic/ability command names are now `DF_COOP_BASIC_V7` / `DF_COOP_ABILITY_V7`. Dedicated admission advances to protocol 7; Relay admission advances to protocol 5. Older formats are rejected before entering a partially compatible match. Existing hero snapshot V7 and world snapshot V3 source timing is retained.

**Build and deploy the Android client and dedicated server from this same revision.** Publishing source does not update an AAB, Play release, account service or running Edgegap image.

## Verification

The following production-code suites pass against controlled engine/SDK/HTTP fixtures:

| Suite | Result |
| --- | --- |
| Combat/skill entry, including keyed nonhero command round trip | 1,319 assertions |
| Held attack focus/facing | 12,954 assertions |
| Ranged cones | 699 assertions |
| World motion/source timestamps | 2,960 checks |
| Owner transport, burst reception, sustained speed and status | 284 checks |
| Hot Zone reward lifecycle and final K/D/A | 34 checks |
| Lobby, identity, admission and ranked lifecycle | 10,041 checks |
| Online VFX/combat/presentation | 4,306 checks |
| Target identity, minion hits, healing attribution and MVP | 15 scenarios |
| Delayed wallet/profile/purchase/account/journal operations | 15 scenarios |
| Verified Google display-name/account wiring | 21 Android-fixture and 3 desktop checks |
| Wing reward receipts | Passed |
| Commerce server tests, SQLite and transactional Firestore fixtures | 252 tests |

Five independent negative controls reintroduce the incorrect healing attribution, minion hit rejection, nearest-position target substitution, local-telemetry MVP and concurrent fulfillment. Each fails its matching behavioral regression. The movement suite separately tests sustained double-speed traffic and shared burst allowance.

New account scenarios exercise multiple pending fulfillment callbacks, a profile flush during verification, income during a save, a purchase response lost after server commit, restart/retry with the same operation ID, account switching while a response is pending, persisted dirty cache, and account-scoped journal recovery. No live account, database or real purchase was used. Existing server tests cover token replay/account binding, atomic debits, purchase restoration, revision conflicts and ownership preservation.

## Remaining acceptance

No Unity player compilation, signed Android AAB, real Google Play checkout/restore, native collision/rendering or live deployment test was available in this environment. This is not a claim that the currently installed app or hosted server has changed.

The exact cause of the previously reported completely stationary/unattackable Mars reward is not established by these fixtures. The reward replication corrections already in the base revision and the new exact target identity/hit fixes need a matched client/server device test. Check Mars and a second arena, two living rewards, repeated Hot Zones, known hero/soldier kill totals, and return to the online lobby.

Cloud profile revisions prevent a stale device from silently replacing a newer save; a conflicting offline branch is retained for recovery rather than automatically merged. Paid currency is server-ledger-backed. Earned/offline progression remains part of the client-submitted profile model; this change does not convert offline rewards into server-verified match receipts.

Run the new regressions with:

```sh
python3 Tools/Combat/AuditRegression/run.py --dotnet-root /path/to/dotnet
python3 Tools/Shop/AccountRegression/run.py --dotnet-root /path/to/dotnet
```
