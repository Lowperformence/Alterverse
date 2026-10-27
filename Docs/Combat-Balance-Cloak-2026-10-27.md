# Combat balance and Rift Sweep cloak — 27 October 2026

Branch: `codex/epic-toon-replacement-20261024`.

## Player-facing changes

- BOLT-7 Frag Arc and Impulse Grenade: 2 m AoE radius and 35 damage throughout the circle. Frag Arc no longer loses damage toward the edge. Throw range, timing, cover rules and Impulse knockback are unchanged.
- Andromeda Timmy: basic attack damage is 25.
- Sivra Quell Sightline Break: range increases from 7.8 m to 15.6 m; its full cone angle is 3 degrees. The three-shot volley still deals 64 total damage to one victim. Preview, active hit region and authoritative command execution share the same values, including with legacy aiming overrides.
- Void Knight Rift Sweep: its actual swing activates a two-second cloak, including when the swing misses. Interrupted or rejected windups grant no cloak. Late cone contact does not restart it.
- The cloaked model has 20% opacity for its owner and allies (80% transparency), and 2% opacity for enemies (98% transparency). Temporary ghost materials include body, weapon and attached headwear, suppress opaque shadows and attached particle/line/trail renderers, and restore original materials and settings afterward.
- A short, dissipating smoke cloud stays at the swing position. It does not follow the escaping Knight. Enemy hero target acquisition and health/target markers suppress the cloaked Knight. Cloak does not grant damage immunity: directed and area attacks can still hit.
- Combat menu descriptions read the same balance constants as gameplay.

## Network behavior

The server owns cloak start and expiry. The reliable Void Knight effect starts presentation; ordinary snapshots repair missed effects. Clients subtract elapsed server time, reject stale/duplicate samples and never restart an expired cloak from a delayed event. Death and disable clear the state.

Hero snapshots use `DF_COOP_HERO_SNAPSHOT_V8`. Both server and Android client must be rebuilt together before online acceptance. Shared packet timestamps keep the complete eight-player snapshot at 956 application bytes (transport framing is additional).

## Verification

Controlled C# fixtures compile and execute production combat, presentation material swaps, semantic VFX and actual snapshot/effect readers:

```sh
python3 Tools/Combat/Bolt7Regression/run.py --dotnet-root <dotnet8>
python3 Tools/Combat/VoidKnightRegression/run.py --dotnet-root <dotnet8>
python3 Tools/Aiming/Regression/run_entry.py --dotnet-root <dotnet8>
python3 Tools/Aiming/Regression/run_entry.py --dotnet-root <dotnet8> --ranged-cones
python3 Tools/Multiplayer/Regression/run.py --dotnet-root <dotnet8>
python3 Tools/Multiplayer/VfxRegression/run.py --dotnet-root <dotnet8>
```

Native Unity shader import, GPU composition, Android appearance and a live two-client match are not covered by these fixtures. No Unity Editor is installed in the editing environment. Validate body/weapon/headwear fading from both clients, smoke fixed at the cast location, restoration after two seconds/death, the 3-degree preview and targets at the new 15.6 m range when building the server and AAB.
