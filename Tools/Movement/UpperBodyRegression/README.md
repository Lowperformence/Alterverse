# Isolated upper-body regression

Run with Python 3 and Mono installed:

```sh
python Tools/Movement/UpperBodyRegression/run.py
```

An extracted Mono distribution can be supplied with `--mono-root`. Use `--amplitudes /path/to/output.csv` to keep measured bone angles. Temporary binaries are removed automatically.

This directly compiles the unmodified production `HeroUpperBodyGait.cs`, full `MeleeBasicAttackAnimation.cs` and full `CombatMotion.cs`. Substitute Unity math/Transform/component implementations in `EngineStubs.cs` use System.Numerics. The authored writer's fresh arm-rest rotations are reproduced before its synchronous final pose evaluation. Actual shoulder quaternions and captured GuardWorld are inspected, rather than testing only a sine formula.

Coverage: eight profiles, two rest fixtures, forward/back/strafe, walk/run endpoints, sixteen phases per cycle; continuous 360-degree steering; no direct actor/model/moving-leg writes; fixed grip local transforms; preserved idle body input; rifle/left-shield exclusions; zero gait weight; 720 repeated and 50 paused authored frames per profile/fixture; 100 paused direct evaluations of gait-owned arm chains; ability early return; action fade/recovery; original two-bone solver wrist preservation. The fixture CSV contains 19 native Nerok source-rest bones exported from the repository's Blender source. The second fixture uses deliberately rotated synthetic bone axes and rotated/scaled parents. All eight **profiles** run on these fixtures; this does not load all eight imported FBX models.

Auren's hammer guard is checked through a full turn on both fixtures, with original and half-length forearms: the hand stays forward and below the shoulder with a bent elbow, unchanged limb lengths and unchanged grip contact. Right-arm gait amplitude is measured from the zero-gait guard, which includes Auren's forward hammer pose.

The clip library, rifle sockets/aim and `FinalizeCombatGrip` are stubs. Real attack timing/clip poses, final weapon intersections, Unity skinning/rendering, terrain contact, full HeroVisualMotion/retarget execution and Android performance require engine testing. The preceding authored arm rebase is emulated, not the entire authored presentation component. Raw repeated Kaelon shield IK without that rebase is an existing separate pose path; the new gait does not write his left arm.

For actual visible bones, use the Unity editor checks `Upper body gait curves` and `Record selected hero upper body for 10 seconds` under `DIMENSIONS FRONT/Checks`. Select a gameplay hero in Play Mode and run continuously without attacking for the recording. It checks chest/arm motion across a full cycle on the final imported skeleton and detects later writers suppressing that motion.
