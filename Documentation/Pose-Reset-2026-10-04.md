# Pose reset and reference-pose diagnosis

The Unity screenshot shows the v4 code running, so this is not just a missing update.
Reset now restores the imported skin bind pose before appearance construction and
before any rig calibration. FBX default transforms are not a reliable rest pose:
Veyr imports with bent arms even with animation import disabled. Its left wrist is
(-0.2397, -0.2068, 1.1220) in Blender default space versus
(-0.3751, -0.0573, 0.9700) in bind space.

At rest, captured bind-local positions and rotations replace the Mixamo static
sample, outward shoulder correction and forced straight elbows. Idle weapon
clearance no longer rotates wrists after this reset. Attacks and abilities retain
their action paths; locomotion retains the moving body and leg pose. Sivra is
excluded completely. Auren's left arm subtree is excluded from bind restoration
and continues using its existing shield guard. Nerok icon and material changes
are untouched.

This fixes a demonstrable reference-pose/reset error. It does not prove that this
was the sole cause of the visible screenshot: offline retargeting could still
produce hanging skeleton axes from the bent FBX defaults. Actual Unity skin,
cosmetic placement and action playback still require Play Mode verification.

The new console marker is `imported rest pose RESET v5 active`.
`DIMENSIONS FRONT/Checks/Live Mixamo pose` now reports final arm deviation from the
reset guard (the existing menu path is retained). Nonzero deviation while idle
indicates a later pose writer. After updating, exit and re-enter Play Mode so the
model is reconstructed and bind calibration is captured afresh.

Validation: C# syntax parsed successfully. Bind-target capture, parent-first restore
and local TRS decomposition were reproduced on all seven FBX models (Rook also
supplies Nerok), including the runtime nonuniform ancestor scales and a rotated
actor. Maximum world-matrix error was 9.54e-7; excluded local transforms changed
by zero. No local reflection or shear ambiguity was found. Unity is not installed
in the execution environment, so these are offline checks, not a Unity compile
or rendered acceptance test.

