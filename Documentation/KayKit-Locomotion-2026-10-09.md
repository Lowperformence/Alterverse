# Hero locomotion repair — 2026-10-09

The subsequent imported-foot-height fix is documented in
`Imported-Foot-Height-2026-10-09.md`. The original fixture checks below bypassed
sole capture and scene raycasts; the user's shoulder-height feet report showed
why those results were insufficient for native acceptance. The current grounding
path uses bind-matrix skin geometry, bounded sole extent, Ground-only queries
and terrain-height support anchors.

## Problem and corrected behavior

The supplied Unity video already showed the authored movement path was active.
The defect was in pose transfer and final grounding, rather than a missing
component. The old tests reused one CyberShark source skeleton under eight identity
labels and accepted small IK errors. They did not establish correct movement for
the distinct imported hero proportions.

Generator-derived native fixtures reproduced a source-target disagreement up to
32.8 cm, planted-foot jumps exceeding one metre in one frame, and knee-plane flips.
The corrected path uses the actual retargeted FK pose, holds a reachable contact,
projects an unreachable anchor to its nearest reachable position, and stabilizes
both the knee bend direction and final boot orientation.

Forward walk and sprint now use the free Quaternius Universal Animation Library
Standard human clips. Free KayKit clips supply strafing and backsteps. The cartoon
forward walk had much stronger knee lifts and a different leg/body ratio.
Both packages are CC0; source notices and editable derived action files are in
`ArtSource/Animations/Locomotion/`. No purchase is needed.

## Final pose ownership

1. Restore the prior visual pelvis correction.
2. Prepare terrain alignment from the native bind soles and bounded source pelvis motion.
3. Apply the existing authored upper-body guard, hands and weapons.
4. Evaluate the existing melee pose.
5. Apply the six authored leg rotations to the actual visible skeleton.
6. Sample those resulting FK feet/knees in the same frame and finalize contact IK.
7. Re-follow rifle details after the final body correction.

Actor travel/collision and input remain owned by their gameplay systems.
The movement layer rotates six leg bones and offsets the visual pelvis; it does
not move ankle local positions. The source pelvis replaces the old additional
procedural movement bob. Rest offsets are restored each frame rather than accumulated.

The v2 resource stores source leg length and pelvis motion separately per clip.
Native stance velocity calibrates each hero's stride. Phase advances from actual
presented distance. Contacts have a dead band for ambiguous directional blends.
Stopping retains the clip while its weight fades, then settles both feet.

Native sole clearance is measured relative to actual terrain. Boot extent uses
the displayed foot matrix, including model scale. World contacts no longer reacquire
the opposite end of a source stride during reach fitting. Foot targets have a
bounded travel rate, knees retain an anatomical forward bend, and the final boot
rotation has a persistent per-frame rotation budget.

## Verified outside Unity

- Source bank: all five clips, 145 loop-closed samples; schema, finite metadata,
  anatomical axes, normalized rotations, pelvis seams, contacts and both source hashes pass.
- Exact source sampling components plus extracted grounding methods:
  71,983 native-fixture evaluations and 741,461 assertions pass.
- Integrated checks cover all eight identities with seven distinct native rigs;
  Nerok aliases the actual runtime Rook rig.
- Additional continuity checks: 24,000 frames, 40 identity/scenario combinations,
  no threshold violations.
- In that continuity fixture: peak foot displacement 20.8 cm/frame at fast motion,
  peak knee displacement 31.17 cm/frame, peak body displacement 14.66 cm/frame
  during reversal, and peak final boot rotation 12 degrees/frame at 60 FPS.
  Feet remain at least 41.73 cm below the hips; worst endpoint error is 1.2 mm;
  worst sampled sole penetration is 0.6 mm.
- Twelve isolated planner/rotation/stop transition checks pass.
- The recorder source also passes a compile against a substitute Unity API with
  obsolete instance-ID APIs treated as errors.

These are mathematical/fixture results, not observed Unity or Android gameplay.
Unity is unavailable in the execution environment. FBX import/default poses,
SkinnedMeshRenderer deformation, custom model details, real ground raycasts,
combat while moving and Android performance require native execution.
The numerical bounds above are regression limits, not a visual quality guarantee.

## Native acceptance and useful evidence

Pull the latest `dev/vertical-slice`, allow Unity to import scripts and
`Assets/Alterverse/Resources/LocomotionAnimation/Library.bytes`, then enter Play Mode.

The new console marker is:

```text
DF: locomotion v2 coherent native FK active on ...
```

The grounding component now logs `grounded locomotion v3 active` and
`grounded locomotion v3 bind-matrix soles`.

Inspect every available hero moving forward, backward and sideways while aiming,
diagonals, sharp reversals, start/stop, moving attacks, terrain edges/slopes and crowds.
Look for alternating strides, correct knee direction, no raised/flipping feet,
no lasting ground penetration and smooth stopping.

In Play Mode use:

`DIMENSIONS FRONT > Checks > Movement record all heroes for 15 seconds`

The recorder now writes an invariant-culture CSV to
`Application.temporaryCachePath/DF-Movement-YYYYMMDD-HHMMSS.csv` and logs its exact path.
Rows capture actor/presented travel, speed/weight/phase, active clip state,
visual pelvis, all six native joint world positions/local rotations/scales,
foot targets, contact flags and errors. It records the actual imported rig rather
than inferred source skeletons. A new short gameplay video plus this CSV can
distinguish importer/skin deformation from target/contact or pose-writer problems.

## Primary references

- Unity 6: 2D locomotion blending and velocity axes:
  https://docs.unity3d.com/6000.0/Documentation/Manual/BlendTree-2DBlending.html
- Unity 6: humanoid retargeting and Avatar setup:
  https://docs.unity3d.com/6000.0/Documentation/Manual/Retargeting.html
- Unity Animation Rigging: two-bone targets and knee hints:
  https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwoBoneIKConstraint.html
- Unity 6: nonuniform Transform scale:
  https://docs.unity3d.com/6000.0/Documentation/Manual/class-Transform.html
- Quaternius free animation library:
  https://quaternius.com/packs/universalanimationlibrary.html
- KayKit character animation source:
  https://kaylousberg.itch.io/kaykit-character-animations
