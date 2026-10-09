# Source clip contact regression

Run with Python 3 and Mono installed:

```sh
python Tools/Movement/GroundedClipRegression/run.py
```

Pass `--mono-root /path/to/extracted/mono` to use an extracted distribution.

The runner extracts the exact production `PlanAuthored`, `Plan`, and `PrepareFoot`
methods and their leg state. It compiles those methods with the full unchanged
`CombatMotion`, `HeroUpperBodyGait`, and `MeleeBasicAttackAnimation` sources.
Quaternion and Transform substitutes reuse the existing System.Numerics harness.
Ground sampling is replaced by a deterministic level floor.

Both legs are checked for a fixed world contact across actor translation and phase
wrap, source swing elevation, preserved source foot rotation, a fresh touchdown,
and smooth low-delta-time stopping followed by settling. Planner state and targets
are inspected directly.

This does not load animation resources, imported hero models or Unity physics. Sole
mesh extents, sloped floors, the complete presentation pipeline, skinning, visual
quality and Android performance require engine testing. The substitute nominal
stance and cycle stride intentionally isolate the new source-clip contact path.

## Baked source and complete sampling components

```sh
python Tools/Movement/GroundedClipRegression/validate_bank.py
python Tools/Movement/GroundedClipRegression/run_runtime.py
```

The asset validator uses only Python's standard library. Supply `--source-root`
pointing to the extracted KayKit folder containing `Animations/` to also verify
recorded source-file SHA-256 hashes. It checks finite arrays, normalized rotations,
anatomical thigh/shin axes against child joints, loop closure, both-leg stance and
swing, world sole clearance, and source duration.

The runtime runner compiles unchanged `LocomotionAnimationLibrary.cs` and
`HeroLocomotionAnimation.cs` with the actual baked bank's field values. Unity JSON
parsing is substituted by fixture field assignment; production loading and
validation still execute on those values. It uses the native 19-bone Nerok source
rest under the real imported 1.342/1.32 model scale. Eight hero identity profiles
are exercised on this fixture; this does not import eight separate hero models.

It covers six directions, walk/run blend endpoints and midpoint, 64 source phases,
continuous 360-degree steering, phase seams, fresh/stale foot samples, finite
outputs, reachable planar targets, source lift, root/joint position ownership,
stop/pause/death/suspension/disable, and the exact production distance-phase
expression at 30/60/120 FPS. ApplyLegs is preceded by a rest rebase matching the
authored final-pose writer. Full control rig retargeting, real Unity JSON APIs,
ground contact skin geometry, rendering and performance remain engine checks.

The same runner also extracts the exact production `FinalizeFeet`,
`FitAuthoredTarget`, `RequiredDrop`, `PrepareFoot`, `Solve` and supporting state.
It executes 4,800 moving frames through that combined path, followed by stopping,
using the real bank, native Nerok rig scale and `nerok-foot-sole.csv` (150
foot-weighted rest vertices per foot from the native source Blender file).
The fixture includes original boot rotation/sole compensation, bounded pelvis
drop and source target fitting; deterministic level ground replaces raycasts.
Both visible legs must change pose and both feet must settle into contact.
Endpoint error above 35 mm fails the run. Actual mesh deformation, slopes and
the complete Unity presentation pipeline still require engine testing.

The integrated contact test additionally runs the exact production `FinalizeFeet`,
`RequiredDrop`, `PrepareFoot`, and `Solve` methods on the native chain after source
sampling. It advances actual actor distance for 400 frames per direction/run case,
then verifies stopped feet settle. A flat deterministic floor replaces raycasts.
The 300 source-rest sole points in `nerok-foot-sole.csv` were exported from
`ArtSource/Heroes/CyberShark/NerokCyberShark.blend` using foot weights above 0.35,
matching production sole capture. Their canonical model positions match the
native rest-bone fixture. Pitched sole compensation uses the full substitute
Transform hierarchy and the original production vertex loop. Unity's mesh import,
actual skinned renderer baking, terrain and the entire authored body writer are
still outside this test.
