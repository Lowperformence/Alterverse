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

Only five used clips are shipped. Contact indices: jab 7, cross 10, slash 8, heavy 15, spell 17 (1-based). Spell is composed from Enter + Shoot + Exit, with release at the end of Enter. Clips are retimed to `HeroAttackDelivery.Windup`, not vice versa. Damage, cooldowns, projectile simulation and movement speed rules are unchanged. The original source heavy combo is trimmed to its first swing; body rotation is limited and the actual hammer/anchor trajectory is authored separately to prevent a full-body spin and underhand swings.

Character locomotion remains in control of legs. Combat preserves foot positions **and orientations** during torso adjustments. Death/disabled components release pose ownership. Ability poses blend back into guard. Multiplayer uses the existing timestamped attack event and Seek.

Validation performed outside Unity: Blender import/sampling, numerical frame/quaternion checks, C# parsing, source pose review and code review. Unity is unavailable in this environment. Run `DIMENSIONS FRONT > Checks > All authored hero combat clips` and inspect all eight heroes in Play Mode for final weapon/shield clearance, ability interruption, moving attacks and remote timing. These Unity checks are supplied but have **not** been executed here.
