# Christmas: Snowmen on Map

Replaces the Halloween promotion with collectible snowmen, red scarves and Santa hats, a matching snowman news icon, and icy blue menu/pickup accents.

Enabled in the shared match bootstrap, so the event runs in all procedural arenas and in offline, Relay-hosted and dedicated-server matches.

- The first snowman appears after 120 seconds of active match time, then one more every 120 seconds.
- Pauses, the opening sequence and result screens do not advance the timer. Match end stops spawning and collecting.
- A free random position is chosen inside the existing rounded map bounds, outside healing spawns, buildings and steep relief. A failed placement is retried after one second.
- Unclaimed snowmen remain until collected or the match world is destroyed. There is no inventory item or paid reward involved.
- Walking within 1.35 world units horizontally and 2 units vertically collects a snowman automatically.
- Each snowman is consumed once and grants exactly **1 earned Shard**. Difficulty, wing and match-result bonuses do not multiply this reward.
- Offline: only the living input-controlled hero can collect. Online: only a living, connected, authenticated human roster actor can collect. Bots, minions and dead heroes cannot consume a snowman.

## Ownership and persistence

Online spawning and proximity checks run only on the listening NGO server, including headless dedicated servers. Clients do not send pickup claims. Spawn/claim messages use the existing round fingerprint and reliable ordered delivery. The recipient credits only its own roster slot; a dedicated server never writes a player's wallet.

Clients request a small chunked state snapshot every five seconds. It restores missing snowmen, removes stale ones and reconciles the collecting player's cumulative reward count. Requests require an authenticated human sender and are throttled. Duplicate/reordered cumulative reward notifications do not pay twice.

The cumulative receipt and `DF_Shards` earned balance are written together through the existing `ProfileTransaction` journal and account-scoped `AccountProfilePrefs`. This uses the same account/cloud-sync path as existing earned match rewards. Switching the attached account during a match cannot redirect that match's rewards into another account.

The Christmas event starts immediately with the updated build and has no automatic calendar expiry; remove/disable its bootstrap component when the promotion ends. A new match creates a fresh timer and match receipt namespace.

The existing `DF_HALLOWEEN_*_V1` transport names and `DF_HalloweenPumpkins_` receipt prefix are intentionally retained for rolling-update compatibility and replay protection. Presentation and runtime class names use Christmas/Snowman. Unity script `.meta` GUIDs are preserved across renames.

## Build and validation

Rebuild the player and dedicated server for a complete Christmas rollout. A Git push does not update an already deployed server binary or an installed client. The wire protocol remains compatible with servers running the previous collectible event; servers predating that event do not spawn collectibles, and clients deliberately cannot replace server authority with a local spawn loop.

Run the production-source regression suite:

```sh
python3 Tools/Christmas/Regression/run.py --dotnet-root /path/to/dotnet-sdk
```

This compiles the production event, visual, news UI, rules, wallet journal, wallet and protocol sources with C# 9 against engine/transport substitutes. It checks cadence, paused/ended matches, blocked terrain, bot/dead/disconnected rejection, one winner per snowman, exact rewards, crash recovery, account isolation, sender/round validation, chunked state recovery, offline/host/dedicated/client behavior, mesh winding, shared meshes, collider-free decorations and the generated menu icon. All 95 checks passed on November 5, 2026. It does not replace a Unity Editor/Android build or a real two-client NGO test.

Before release, verify in Unity on the playable maps, then a host plus client and a headless server plus two clients: both clients see the same spawn; the first living human to reach it gets one Shard; a bot touching it does nothing; reconnecting does not pay again.

