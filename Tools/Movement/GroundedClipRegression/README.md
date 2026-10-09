# Locomotion regression and native geometry fixtures

Run from the repository root with Python 3 and Mono:

```sh
python Tools/Movement/GroundedClipRegression/validate_bank.py
python Tools/Movement/GroundedClipRegression/run.py
python Tools/Movement/GroundedClipRegression/run_runtime.py --native-fixtures
python Tools/Movement/SoleCalibrationRegression/run.py
python Tools/Movement/GroundRaycastRegression/run.py
```

Supply `--mono-root /path/to/extracted/mono` to either compiled runner.
The bank validator also accepts `--source-root KAYKIT_ROOT --ual-source-root UAL_ROOT`
to verify the recorded SHA-256 hashes of both free packages.

## What is executed

The runtime runner compiles unchanged production
`LocomotionAnimationLibrary.cs` and `HeroLocomotionAnimation.cs`.
Actual baked bank values are assigned to their serialized fields; production
loading/validation then runs. It extracts exact production grounding methods,
including pelvis preparation/restoration, source-foot planning, reach fitting,
sole correction, contact stabilization, and final leg solving.

`--native-fixtures` loads seven distinct generator-derived native armatures and
boot samples. Eight hero identities are covered; Nerok deliberately uses Rook's
armature, matching the imported model used by runtime. Actor Y=1, physical floor
Y=-.02, and the real authored visual/model translations and nonuniform scales
are represented. Without the flag the historical single CyberShark source fixture
is used for compatibility; it must not be described as eight distinct models.

Checks cover six cardinal/diagonal directions, walk/run blending, loop seams,
contact hysteresis, fresh/stale samples, movement fade, pause/death/suspension/
disable, native FK consistency, root and joint-position ownership, native stride,
and distance-driven phase at 30/60/120 FPS.

The integrated final pose advances each identity for 400 frames per direction/
run pair. The continuity test adds 24,000 frames at 60 FPS across eight identities:

- Slow forward diagonal with small steering jitter.
- Fast backward diagonal with small steering jitter.
- Continuous full-circle steering.
- Smoothed abrupt direction reversals and sideways turns.
- Accelerated starts and decelerated stops through the full weight fade.

It measures both final feet, both knees, all six joint rotations, body displacement
and actual boot-vertex ground clearance. It rejects feet above hips, large foot/
knee/body jumps, boot orientation snaps, significant sole penetration and failure
to settle after stopping. A small final IK error alone is insufficient.

The isolated planner runner checks 12 contact/swing/stop transitions on both legs,
including bounded foot rotation that converges to the source orientation.

## Fixture generation

```sh
blender --background --python Tools/Movement/GroundedClipRegression/export_native_rest.py
```

The exporter derives native rest chains and rounded boot geometry from the existing
hero generator's `rig`, `arm_points`, `PROFILE` and `proportion` definitions.
It includes slim-boot and per-hero proportions, but omits full body/details meshes.
Canonical model coordinates are `(x,z,-y)`.
`fixtures/native-baseline-76f3938.json` records the earlier FK-target mismatch.

## Limits

Unity APIs, JSON field assignment, Transform/quaternion math and level-ground
raycasts are substituted. The tests do not run Unity's FBX importer, Animator,
SkinnedMeshRenderer.BakeMesh, full combat/presentation pipeline, real scene
colliders or Android. Native imported/default poses and custom hero boot details
can differ from generator-derived fixtures. Run the in-engine recorder and inspect
gameplay to establish those behaviors.

See `Documentation/KayKit-Locomotion-2026-10-09.md` for the repair and current results.
The separate sole/ground runners exercise the calibration and layer-query paths
that this integration harness replaces. Their old-code negative controls reproduce
unsafe height paths; they still do not establish rendered Unity acceptance.
