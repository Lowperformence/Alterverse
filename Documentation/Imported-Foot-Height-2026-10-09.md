# Imported foot height repair — 2026-10-09

## Trigger and behavior

After the v2 locomotion update, the user reported feet fixed at the shoulders.
The portable native-rig checks passed but substituted the two relevant systems:
imported sole calibration and real ground raycasts. Those results did not establish
correct in-engine movement.

This patch removes two ways to generate a high foot target and one way to retain it:

- Sole points now come from explicit weighted skin bind matrices, not a BakeMesh
  snapshot whose scale/coordinate interpretation is reused as foot geometry.
- Nonfinite or implausibly distant foot points are rejected. Rotated sole extent
  remains bounded even if a model's scale changes after calibration.
- Ground queries only the project's Ground layer (10). Nearby units, guards,
  buildings and default-layer colliders cannot become the foot's floor.
- A supporting plant stores terrain height. Interpolation can briefly retain a
  bounded landing height, but cannot turn it into a permanent airborne anchor.
  Reach fitting subtracts that transient lift before storing the plant again.

The importer now enables Read/Write for all seven native hero FBXs, rather than
only Mira/Rook/Veyr. Its version changes from 11 to 12 so already imported models
refresh. Native calibration reads mesh vertices once; unreadable skins are skipped
without accessing their arrays and the initial log reports their count.

## Evidence and limits

The focused sole test compiles the exact complete current CaptureSoles loop and
its helpers, plus actual planning/stabilization/reach-fitting methods. It covers
known boot points under .01/1/100 import factors, both bone orderings, renderer
translation/rotation/nonuniform scale, animated mixed weights, invalid/nonfinite
influences, corrupted point/scale data, unreadable meshes and settling contacts.

The unmodified earlier SoleExtent/PrepareFoot produce a 2.380 m ankle target from
an injected invalid point; the bounded current path produces 0.400 m. The earlier
stabilization also stores an elevated 1.880 m contact anchor and reuses it. The
current anchor remains on terrain and its visible target settles within ten frames.
These are reproductions of unsafe code paths, not proof that the injected data
matches the user's actual Unity import.

Focused results: 209 sole/skin/contact checks and 80 ground-query checks pass.
The ground negative control using the earlier broad layer mask fails by selecting
an unrelated surface: 0.89 m ankle height instead of the correct 0.06 m.

The original directional/contact suite still passes: 71,983 native-fixture
evaluations, 741,461 assertions and 24,000 continuity frames across eight identities
with seven distinct generator-derived rigs (Nerok aliases runtime Rook).
The twelve isolated planner transitions also pass.

The live Unity foot check now rejects a foot or target above the native hip.
It no longer accepts a small IK error as sufficient when both foot and target
are in the wrong place.

All local regression physics, API and matrix behavior remains substituted.
No native Unity compile, actual imported FBX deformation, Android run or rendered
gameplay acceptance has been performed here. A current screenshot/video and
recorder trace remain the way to confirm the actual failure source and appearance.

## Run the focused checks

```sh
python Tools/Movement/SoleCalibrationRegression/run.py --mono-root MONO_ROOT
python Tools/Movement/GroundRaycastRegression/run.py --mono-root MONO_ROOT
python Tools/Movement/GroundedClipRegression/run.py --mono-root MONO_ROOT
python Tools/Movement/GroundedClipRegression/run_runtime.py --native-fixtures --mono-root MONO_ROOT
```

`GroundRaycastRegression/run.py --old-mask` is a negative control and must fail.

## Unity verification

Exit Play Mode, pull the latest dev/vertical-slice, wait for script/model imports,
then start a fresh Play session. The new grounding log begins:

```text
DF: grounded locomotion v3 bind-matrix soles on ...
```

It includes valid point counts, left/right sole heights, rejected points and
unreadable meshes. An unreadable count above zero warrants inspecting which model
was not reimported or was made unreadable by a later mesh operation.

Use `DIMENSIONS FRONT > Checks > Movement live foot contacts` for an immediate
native joint/target check. The existing 15-second all-hero recorder captures
end-of-frame imported joints, pelvis, targets and contacts; its CSV path is logged.

## Primary API references

- Skin vertices and bind matrices:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh-bindposes.html
- BakeMesh coordinates and scale semantics:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SkinnedMeshRenderer.BakeMesh.html
- Runtime readability:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh-isReadable.html
- Model importer readability:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ModelImporter-isReadable.html
