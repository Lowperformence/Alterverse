# Online combat performance — 2026-10-31

Reported symptom: stutters when pressing basic attacks and casting online.
Based on `57c6519a3964b6c38396328876ba65f181010f13` on `codex/epic-toon-replacement-20261024`.

## Changes

- `CoopGameplayCommands`: resolve command actors and hero targets from the existing live Health registry instead of allocating global scene-search arrays per command. Removed/disabled health components cannot execute stale intentions. Preserve round, player identity, sender authorization, sequence, alive-state, range and gameplay checks. For nonhero targets, check the exact world key before classifying kind/team; never substitute another entity.
- `SelectedAbilityVfx`: reuse completed selected particle compositions (smoke, blood, strike, circle, fire, stars), including cached renderer/particle arrays and property blocks. Reset particle contents, actor references, following, movement, hold status, clipping and authored renderer visibility between uses. Retain at most 16 idle compositions per kind; excess completed compositions are destroyed. Concurrent casts are not dropped or stolen. Scene destruction removes entries. No damage, cooldown, packet format, delivery timing, VFX size or clipping-radius changes.

## Validation

Run with a .NET 8 SDK root:

```sh
python Tools/Visuals/SelectedAbilityRegression/run.py --dotnet-root /path/to/dotnet
python Tools/Multiplayer/VfxRegression/run.py --dotnet-root /path/to/dotnet
```

- Selected ability suite: 521 checks pass, including 200 sequential casts without new effect hierarchies after warm-up, independent concurrent effects, bounded idle retention, renderer restoration after held-star cancellation, original follow/clip/timing behavior.
- Production online VFX/combat suite: 5,241 checks pass, including basic/cast parity at simulated 30/60/120 FPS, zero scene searches in command resolution, exact live target resolution, removed/dead actors, replacement roster actors, duplicate commands and pending cooldown acknowledgements.
- Negative controls: restoring the original command code fails the no-scene-discovery assertion; restoring original selected-effect code fails composition reuse. Both controls compile before failing.

These tests use controlled Unity/GPU/network substitutes; they are not native Unity or Android builds and do not establish a measured handset FPS improvement. One existing unused-field compiler warning in the network-health fixture remains.

## Device acceptance still required

Build and install a new Android player, and rebuild/deploy the dedicated server for its command-path improvement. Test repeated and simultaneous attacks/casts with multiple online players, especially Timmy hold/leap, moving strikes and area effects. Check scene exit/re-entry, interrupted casts and target death. Compare frame-time spikes and GC allocation before/after on the same handset and server; compare server combat telemetry as well.

First use of each selected effect and bursts exceeding the available idle pool can still allocate. This patch removes demonstrated recurring work; it does not prove every reported stutter was caused by it. Device GPU cost, first-load/shader stalls, network latency and movement correction still require a native capture if symptoms persist. No Android artifact or live server deployment was produced here.
