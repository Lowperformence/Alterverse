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
| Zhaev / Veyr | Blades rest beside the thighs; Regular A plus recovery, mirrored on alternate attacks; free arm stays to the side |
| Auren / Kaelon | Shield left, hammer right; limited Heavy Combo torso, authored horizontal hand/shaft path |
| Brakk / Rook | Hands and elbows relaxed beside the body; fists turn and extend only for alternating Punch Jab / Cross |
| Nerok | Low anchor guard; horizontal hand/shaft path, heavy recovery and free left arm |
| Sivra / Nyra | Rifle parked at right hip, support hand relaxed; both hands take actual grip/muzzle/support sockets for firing |
| Ione / Elyra | Right-hand mirrored Spell Enter/Shoot/Exit, left hand supporting |
| Tahla / Mira | Left-hand Spell Enter/Shoot/Exit, lower relaxed guard |
| Traveler | Lower shoulders/hands; right-hand mirrored casting, original orb grip retained |

`MeleeBasicAttackAnimation` keeps its historical component name for serialized/network compatibility but owns guard/basic arm poses for all eight heroes. There is one final bone writer. Source world orientation is baked into anatomical frames; runtime offsets account for each imported rig's axes and bind pose. No FBX Euler-angle constants are transferred.

Six Quaternius clips are shipped, plus the user-supplied Mixamo standing baseline described below. `idle_relaxed` remains the previous fallback. The fitted weapon guard is captured before blending into an attack, so resting grip correction and attack recovery share the same baseline. Contact indices: jab 7, cross 10, slash 8, heavy 15, spell 17 (1-based). Spell is composed from Enter + Shoot + Exit, with release at the end of Enter. Clips are retimed to `HeroAttackDelivery.Windup`, not vice versa. Damage, cooldowns, projectile simulation and movement speed rules are unchanged. The original source heavy combo is trimmed to its first swing; body rotation is limited and the actual hammer/anchor trajectory is authored separately to prevent a full-body spin and underhand swings.

Character locomotion remains in control of legs. Combat preserves foot positions **and orientations** during torso adjustments. Death/disabled components release pose ownership. Ability poses blend back into guard. Multiplayer uses the existing timestamped attack event and Seek.

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
