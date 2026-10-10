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

Latest result: **406 assertions passed**, plus both existing editor regression suites. Cases include all 8 basic attacks and 32 skills, real catalog/profile values, directional misses, windup/flight dodges, first contact, walls, moving-target sweeps, AOE timing, Nerok hook/vortex, three-shot volley, ally eligibility, explicit homing, cooldowns, queue capacity, interruptions, gestures, HUD multitouch, display coordinate scaling and V4 serialized aiming intent.

Fixtures model time, coroutines, planar movement bodies, obstacle queries, Health and network buffers. Graphics/audio/VFX and Unity native physics are substituted. This is not a Unity compile, shader test, real network session or Android device test. The production visual source files are not type-checked by this runner. Run the real Unity editor regressions and the device checklist in `Docs/ManualAiming.md` before accepting visual feel or shipping a client/server build.
