# Moon healing guard: rotating shoulder armor

The reported unit is the small healing-zone mech on Moon, not the Hot Zone reward robot.
Its broad shoulder plate and shoulder cap were grouped into `DF_GuardArmMotionL/R` along
with the joint and upper arm. The attack IK therefore swept the rigid armor around the
shoulder hinge. The production transform regression measured 69.515 degrees of plate
deviation and a 59.582 degree frame jump before the fix.

`BuildGuardPivots` now leaves these four rigid armor pieces on `DF_LocomotionPose`, where
they follow the torso. The shoulder joint, upper arm, elbow, forearm armor and hand retain
their articulated hierarchy and existing animation. Geometry, authored offsets, materials,
gameplay root, colliders, movement rules, damage and audio are unchanged. The fallback guard
rig shared by Moon and Kryon receives this correction; dedicated planet rigs are untouched.

## Validation

Run the portable production-method test with Python's `tree_sitter` and `tree_sitter_c_sharp`
packages and a Mono installation containing `usr/bin/mono-sgen` and `usr/lib/mono/4.5/mcs.exe`:

```sh
python Tools/Movement/GuardArmRegression/run.py --mono-root /path/to/mono-root
```

The test uses the actual `HealingGuardMech.BuildArm`, guard binding, attack-chain construction,
rest capture, pose update, relocation reset, attack path and IK solver methods. Rendering and
Unity transforms are represented by test doubles. It covers idle, walking, turning attacks
and moving attacks at 24, 30, 60 and 120 FPS, plus relocation and pause. Checks include rigid
plate mounts, preserved initial authored poses, bone lengths and unchanged gameplay roots.
The unfixed code fails the rigid-mount regression. With the fix, shoulder deviation and
frame jumps are zero; upper-arm and elbow articulation remain present.

The Unity editor's existing `DIMENSIONS FRONT/Checks/Attack paths and rig-independent IK`
check now also constructs the actual guard and verifies shoulder and forearm mounts with
Unity transforms. That editor check and visual playback on Unity/Android were not run in
this environment and remain necessary to confirm the user's exact visual symptom.
