# Authored hero combat movement

Source: Quaternius Universal Animation Library 1 and 2, **free Standard** packages, CC0 1.0.
https://quaternius.itch.io/universal-animation-library
https://quaternius.itch.io/universal-animation-library-2

The `.blend` files contain the selected editable armatures/actions, with unused meshes and finger curves removed. No paid/Source-package animations are included. Rebuild with Blender 4.5:

```
blender -b --python Tools/CombatAnimation/bake_combat_library.py -- /path/to/extracted/packages
```

| Hero | Guard and attack |
| --- | --- |
| Zhaev / Veyr | Blades rest beside the thighs; alternating side-to-front horizontal cuts with a fitted wrist arc; free arm stays to the side |
| Andromeda Timmy | Alternating side-to-front claw cuts using the same horizontal hand/wrist path; scratch channel timing remains owned by gameplay |
| Auren / Kaelon | Shield left, hammer right; limited Heavy Combo torso, authored horizontal hand/shaft path |
| Brakk / Rook | Hands relaxed beside the body; alternating punches chamber beside the chest and extend nearly to full natural arm reach |
| Nerok | Low anchor guard; horizontal hand/shaft path, heavy recovery and free left arm |
| Sivra / Nyra | Rifle parked at right hip, support hand relaxed; both hands take actual grip/muzzle/support sockets for firing |
| Ione / Elyra | Right-hand mirrored Spell Enter/Shoot/Exit, left hand supporting |
| Tahla / Mira | Left-hand Spell Enter/Shoot/Exit, lower relaxed guard |
| Traveler | Lower shoulders/hands; right-hand mirrored casting, original orb grip retained |

`MeleeBasicAttackAnimation` keeps its historical component name for serialized/network compatibility but owns guard/basic arm poses for all eight heroes. There is one final bone writer. Source world orientation is baked into anatomical frames; runtime offsets account for each imported rig's axes and bind pose. No FBX Euler-angle constants are transferred.

Six Quaternius clips are shipped, plus the user-supplied Mixamo standing baseline described below. `idle_relaxed` remains the previous fallback. The fitted weapon guard is captured before blending into an attack, so resting grip correction and attack recovery share the same baseline. Contact indices: jab 7, cross 10, slash 8, heavy 15, spell 17 (1-based). Spell is composed from Enter + Shoot + Exit, with release at the end of Enter. Clips are retimed to `HeroAttackDelivery.Windup`, not vice versa. Damage, cooldowns, projectile simulation and movement speed rules are unchanged. The original source heavy combo is trimmed to its first swing; body rotation is limited and the actual hammer/anchor trajectory is authored separately to prevent a full-body spin and underhand swings.

Character locomotion remains in control of legs. Combat preserves foot positions **and orientations** during torso adjustments. Death/disabled components release pose ownership. Ability poses blend back into guard. Multiplayer uses the existing timestamped attack event and Seek.

Zhaev and Timmy use `slash_a` for limited body motion only. Their attacking hand loads at the side, travels forward on a level plane through the existing contact time, follows across the body and returns to the captured guard. The opposite hand stays at its guard. Wrist direction is calibrated from the imported anatomical frame, so the fitted blade/claws follow the forward cut instead of inheriting the source clip's upward pull. Both hands alternate through the existing mirror flag. Damage, attack intervals, ability poses and network event timing are unchanged.

2026-10-17 validation: the production combat pose writer, IK solver and actual compressed clip bank compile and pass numerical forward-cut sampling with native Veyr source-rest and rotated synthetic rigs, both hands, standing/moving input and short/normal windups. The previous code reproduces the rejected strike direction. The existing upper-body/guard/lifecycle suite also passes. These substitute-engine checks do not render the fitted meshes or load Timmy's FBX; inspect final blade/claw clearance and playback in Unity/Android.

2026-10-17 arm-motion clarification: the requested larger strike radius is visual. Zhaev/Timmy have a wider side windup and follow-through with more forward arm extension; Auren/Nerok use a wider heavy sweep; Brakk uses explicit extended jab/cross hand paths. Void Knight expands the imported basic swing while moving both hands together within both arms' natural reach. Arm/weapon lengths, contact times and damage volumes stay fixed. The accidental +0.8m gameplay-range change was reverted to the preceding balance values.

Validation of the wider motion: before/after samples from the production clip bank show increased forward hand extension and hand-path span for all five shared-rig melee profiles. The existing guard/lifecycle suite and forward-cut checks pass. Void Knight's extension, shared hand contact, grip rotations, unchanged local bone positions/scales, recovery and ability exclusion pass using synthetic clips on rotated/native-rest fixtures. These checks do not load Void Knight's FBX or render final meshes; the visual Unity/Android check remains outstanding.

Validation performed outside Unity: Blender import/sampling, numerical frame/quaternion checks, C# parsing, source pose review and code review. Unity is unavailable in this environment. Run `DIMENSIONS FRONT > Checks > All authored hero combat clips` and inspect all eight heroes in Play Mode for final weapon/shield clearance, ability interruption, moving attacks and remote timing. These Unity checks are supplied but have **not** been executed here.

2026-10-04 idle validation: inspected front/side projections of the downloaded source skeleton; both hands remain below their elbows throughout all 76 frames. This validates the source, not rendered Unity hero clearance. The existing Standard FBX download is reused; no paid Source assets are used.

## User-supplied Mixamo starting pose — 2026-10-04

`StandingIdle.fbx.gz` is a losslessly compressed copy of the user's `Standing Idle (1).fbx`.
It is a Mixamo asset supplied by the user, **not** part of the CC0 Quaternius packages above.
The first animated frame (frame 1, not the skeleton's T-pose) is the stationary starting
position for Zhaev, Auren, Ione, Tahla, Brakk, Traveler and Nerok. Sivra/Nyra is excluded.
Auren/Kaelon's left upper arm, elbow and hand retain the previous shield guard and grip.

Rebuild the separate runtime asset without changing the attack library:

```
blender -b --python Tools/CombatAnimation/bake_mixamo_standing_idle.py -- ArtSource/Animations/StandingIdle.fbx.gz
```

`StandingIdle.json` contains the same pose twice for the existing interpolating sampler.
`mixamo-standing-idle-report.json` records the original SHA-256, source frame and anatomical
mapping. Runtime offsets retarget this pose to each hero; no local FBX Euler angles are copied.
The body blends out as the hero starts moving. Foot IK preserves ground height and sole
orientation, with a small visual pelvis adjustment that is reset before every upstream pose
update and on disable. The gameplay root and collider do not move. Attacks return to the
captured new guard, including heavy-weapon hand paths. Abilities keep their existing priority.

Validation: Blender 4.5.9 imported the uploaded file and all seven source hero rigs; anatomical
arm/leg direction and quaternion checks passed. Every affected source rig retains lowered
arms; Nerok uses Rook's rig. Ground targets are reachable after the pelvis correction.
C# syntax was parsed successfully. Unity is not installed here, so the editor regression and
live gameplay/menu rendering still need to run in Unity, especially Auren's final shield
clearance with the runtime replacement mesh.
