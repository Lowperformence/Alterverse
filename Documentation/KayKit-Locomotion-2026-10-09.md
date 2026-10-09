# Directional authored hero locomotion — 2026-10-09

## Result

Moving imported heroes now use five real free KayKit animation clips on their
visible thigh, shin and foot bones: forward walk/run, strafe left/right and
backward walk. Directional weights blend continuously for diagonal movement.
Cycle phase follows actual presented distance through the existing
`HeroVisualMotion.GaitPhase`; it is not advanced by an independent timer.

The new `HeroLocomotionAnimation` is initialized automatically when
`AuthoredHeroPresentation` binds an imported model. The resource bank and its
Unity metadata are committed. No Animator controller setup or manual component
attachment is required.

## Final pose ownership

Previously, earlier lower-body work could be overwritten by the authored and
combat pose layers, and procedural grounding reconstructed its own gait.
The final synchronous order is now:

1. Existing authored guard, upper body and weapon grip corrections.
2. Existing `MeleeBasicAttackAnimation.EvaluatePose()`.
3. New `HeroLocomotionAnimation.ApplyLegs()` on the actual imported bones.
4. `HeroGroundedLocomotion.FinalizeFeet()` using that frame's source trajectories,
   contact flags, knee hints and foot rotations.

Grounding anchors supporting feet in world space. Swing feet follow the source
trajectory and actual source sole clearance. Boot sole correction and the
existing leg IK then adapt the pose to terrain. Stopping uses the existing smooth
settle. Paused, dead or suspended movement cannot reuse stale clip samples.

Source hip sway is subtracted from foot trajectories before target rest placement
is added, avoiding unreachable strides caused by transferring source pelvis
motion without transferring the pelvis itself. Source and target foot rest axes
match, avoiding an artificial extra boot pitch.

After sole compensation, authored targets are fitted to the actual three-
dimensional leg reach within the existing pelvis-drop limit. A reachable stance
anchor remains fixed; an unreachable one is replanted from the current source
pose. Fitting preserves source swing lift and resamples terrain/sole compensation.
This corrects overextension caused by keeping the target pelvis higher than the
source clip's pelvis.

The new layer writes only six leg rotations; actor motion, collider dimensions,
bone local positions, weapon grips, upper-body attacks and audio remain owned by
their existing systems. A missing/invalid resource reports an explicit error and
retains the prior pose path.

## Source and checks

KayKit 1.1 free GLBs are CC0, including commercial use. Provenance, the original
license, derived editable skeleton/action sources, sampling metadata and the
reproducible baker are under `ArtSource/Animations/Locomotion/`.

The regression tools under `Tools/Movement/GroundedClipRegression/` check the
real baked data, exact production sampling components and extracted production
foot-planning methods. They cover cardinal/diagonal directions, walk/run blends,
cycle seams, stop/contact transitions, continuous steering, foot clearance,
finite rotations, planar target reach and distance-phase stability at
30/60/120 FPS. Source hashes and both-leg support/swing are checked separately.

Final portable results: 208,272 assertions over 14,384 fixture evaluations;
12 isolated foot-planner transition tests; all five source-bank validations pass.
An integrated 4,800-frame run uses exact production finalization, pelvis-drop,
foot-fit and leg-solve methods with actual Nerok foot-weighted sole points.
Across 3,153 supporting-foot samples, maximum ankle-to-target error is 6.1 mm;
no endpoint exceeds 35 mm. Before the reach fit, the same native sole fixture
exposed up to 110 mm swing and 54 mm supporting-foot error. These numbers describe
the substitute-runtime fixture, not observed Unity gameplay.

The C# runtime harness uses one native Nerok rest skeleton with its imported
nonuniform model scale and eight hero identity profiles. It does **not** import
eight independent hero meshes. Unity APIs are substituted; this is not a native
Unity compile, physics, mesh-skinning, Android-performance or visual acceptance
test. Unity is unavailable in this environment.

## In-engine acceptance

Pull the newest `dev/vertical-slice`, let Unity import the committed scripts and
`Library.bytes`, and play the game normally. The console should report
`KayKit CC0 directional locomotion active` and, while moving,
`authored locomotion foot trajectories and contacts active` for imported heroes.

Check visible alternating strides for all available heroes, forward/backward and
sideways movement while aiming, diagonal direction changes, start/stop, attack
while moving, slopes and unit crowds. Supporting soles should remain planted
until lift, with no persistent straight-legged glide or floor penetration. Real
Unity compilation and this gameplay review remain the final acceptance checks.
