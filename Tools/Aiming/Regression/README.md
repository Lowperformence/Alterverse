# Manual aiming C# regression harness

This harness compiles the production aiming, attack delivery, ability execution, HUD gesture and NGO command sources against deterministic engine fixtures. It also executes `BasicAttackDeliveryRegression.Run()` and `AttackDeliveryRegression.Run()` from the existing Unity editor checks.

Requirements: Python 3, a Mono runtime laid out under an extracted Linux package root, and Roslyn `csc.exe` with C# 9 support (tested with Microsoft.Net.Compilers.Toolset 4.8). Dependencies are supplied by the developer; the runner does not download or install them.

From the repository root:

```bash
python3 Tools/Aiming/Regression/run.py \
  --mono-root /path/to/extracted-mono \
  --roslyn /path/to/roslyn/tasks/net472/csc.exe
```

The Mono root must contain `usr/bin/mono-sgen`, `usr/lib/mono/4.5/System.dll`, `System.Core.dll`, the runtime/config and required framework facades. The executable is built in a temporary directory and removed automatically. Nonzero compilation or assertion results fail the run.

Latest result: **1228 assertions passed**, plus both existing editor regression suites. Cases include all 8 basic attacks and 32 skills, real catalog/profile values, directional misses, windup/flight dodges, first contact, walls, moving-target sweeps, AOE timing, Nerok hook/vortex, three-shot volley, ally eligibility, explicit homing, cooldowns, queue capacity, interruptions, skill gestures, HUD multitouch, display coordinate scaling and V4 serialized aiming intent. Basic HUD cases verify immediate press, cooldown-bound hold repetition, holding during an existing cooldown, no manual aim pose, no extra release cast, continued movement, button exit/return, lost pointer, stun interruption, transition back to skill aiming and the equivalent mouse path. Restored basic soft-target cases verify all eight heroes can acquire sideways/behind targets, prefer heroes over closer minions, retain eligible sticky targets, reject allies and out-of-range targets, reacquire after death, wait without spending cooldown when no target exists, start a held attack when an enemy enters range, allow windup/flight dodges and transmit frozen direction without homing in V4 client basic commands.

`ConnectionTiming.cs` additionally measures every attack at 30/60/120 FPS, checks first-effect bounds against windup plus geometric flight, samples ranged basics at 4 m and projectiles near maximum range, and drives rapid HUD gestures with only `Began` and `Ended`. Holds of 0/50/150/750/1000 ms acquire an enemy that enters the assist window only on release, without an intervening aim update. Diagnostic assertions reproduce the current wide-guide/narrow-projectile mismatch and a moving caster shifting the launch origin during windup; they do not claim these behaviors have been fixed.

Add `--connection-timing` to print `TIMING`/`GESTURE` CSV records. Tests run with or without that output flag. These are gameplay-clock measurements, not CPU performance, real hardware touch/renderer latency or real network round trips. Dash zero values mean an accepted dash start, not completed native movement. Results and the proposed next change are documented in `Docs/AttackConnectionTiming.md`.

Fixtures model time, coroutines, planar movement bodies, obstacle queries, Health and network buffers. Graphics/audio/VFX and Unity native physics are substituted. This is not a Unity compile, shader test, real network session or Android device test. The production visual source files are not type-checked by this runner. Run the real Unity editor regressions and the device checklist in `Docs/ManualAiming.md` before accepting visual feel or shipping a client/server build.
