# Sole calibration regression

Run `python Tools/Movement/SoleCalibrationRegression/run.py` from the repository.
The runner uses `mono` and `mcs` from PATH. Alternatively use `--mono-root PATH`
for a bundled Mono runtime.

The runner extracts the **unchanged current production** `Leg`, `CaptureSoles`,
`FootWeighted`, `SkinVertex`, `CaptureSolePoint`, `SoleExtent`, and `PrepareFoot`.
It compiles those against an isolated transform/skin substitute and Mono.
No shared test runner, product code, or shared stub is modified.

The geometry tests use three known boot points on each foot. Import scales .01,
1 and 100 cancel against the skeleton's inverse import scale. Renderer
translations, rotations and nonuniform scales are included. Both bone orders are
tested. Bind matrices are immutable snapshots, so mixed-weight animated-bone
checks cannot accidentally recalculate their expected rest pose.

Tests cover the full current `CaptureSoles` loop, correct world/foot-local
coordinates, normalized mixed weights, the fourth influence slot, missing or
invalid influences, rejected distant and nonfinite points, and bounded extent
after corrupted points or scale changes.

`BeforeMethods.cs` preserves exact relevant methods from the pre-hotfix source.
The unchanged old `SoleExtent` and `PrepareFoot` produce a **2.380 m** ankle target
from a deliberately injected 2.4 m downward sole point. Current methods produce
**0.400 m** and reject such a point during calibration. This reproduces the
unsafe height amplification, **not proof that the injected point matches a real
Unity import failure**.

These tests do not run Unity, FBX importing, BakeMesh, actual scene physics,
SkinnedMeshRenderer deformation, blend shapes or the final rendered character.
In-engine evidence remains necessary to identify the original coordinate error
and validate the appearance after this bounded calibration fix.

The runner also extracts unchanged current/old `PlanAuthored` and
`StabilizeAuthoredTarget`, plus current `FitAuthoredTarget`, with flat-ground
substitution at Y=-.02. An initial previous ankle at 2 m leaves the old support
anchor at **1.880 m**; the next unchanged authored contact retains it. The
corrected plant is **.060 m** (floor plus .080 m sole) immediately, while the
visible ankle retains bounded temporary lift and descends to ground within ten
frames. Descent stays within the frame movement budget. Reach fitting excludes
transient lift when updating the support plant. Corrupt swing heights are
bounded for walk/blend/run, planar motion respects the movement budget, and
below-ground support is raised to the floor.

An unreadable-mesh substitute throws on all vertex, weight and bind-matrix reads.
Full calibration skips it without reading those arrays, while valid skins still
supply the expected sole geometry. These checks protect the runtime fallback;
Unity reimport/readability configuration must also be verified separately.
