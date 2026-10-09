# Free directional locomotion sources

The runtime v2 bank combines a human walk/sprint with free directional steps.
No purchased assets or paid Blender source files are required.

| Bank clip | Provider | Free action | Source GLB |
|---|---|---|---|
| `walk` | Quaternius Universal Animation Library Standard | `Walk_Loop` | `Godot/AnimationLibrary_Godot_Standard.glb` |
| `run` | Quaternius Universal Animation Library Standard | `Sprint_Loop` | Same Standard GLB |
| `left` | KayKit Character Animations 1.1 | `Running_Strafe_Left` | `Animations/gltf/Rig_Medium/Rig_Medium_MovementAdvanced.glb` |
| `right` | KayKit Character Animations 1.1 | `Running_Strafe_Right` | Same MovementAdvanced GLB |
| `back` | KayKit Character Animations 1.1 | `Walking_Backwards` | Same MovementAdvanced GLB |

Both packages use CC0. Their original notices are preserved in
`License-Quaternius.txt` and `License-KayKit.txt`.

- https://quaternius.com/packs/universalanimationlibrary.html
- https://quaternius.itch.io/universal-animation-library
- https://kaylousberg.itch.io/kaykit-character-animations

`Quaternius_Movement.blend` and `KayKit_MovementAdvanced.blend` are locally
derived skeleton/action files from these free GLBs with mannequin meshes removed.
The earlier `KayKit_MovementBasic.blend` is retained as a historical source;
its forward walk/run are no longer used by the runtime bank.

## Reproduce

Use Blender 4.5.3 from the repository root. KAYKIT_ROOT contains `Animations/`;
UAL_ROOT contains `Godot/AnimationLibrary_Godot_Standard.glb`.

```sh
blender -b --python Tools/Movement/bake_locomotion_library.py -- KAYKIT_ROOT UAL_ROOT
python Tools/Movement/GroundedClipRegression/validate_bank.py --source-root KAYKIT_ROOT --ual-source-root UAL_ROOT
```

The deterministic gzip JSON output is
`Assets/Alterverse/Resources/LocomotionAnimation/Library.bytes`.
Five clips contain 145 loop-closed samples. `sampling-report.json` records
source hashes, action names, per-provider leg lengths, duration, pelvis range,
contacts and cycle-distance metadata.

The baker exports anatomical world rotations, reference joint positions,
source sole clearance, contact probabilities, and per-clip pelvis displacement.
Contacts use evaluated foot/toe-weighted vertices plus stanceward ankle velocity.
The coordinate conversion is source left +X, forward -Y, up +Z to game left -X,
forward +Z, up +Y.

Different provider leg lengths are kept per clip. Runtime first retargets rotations
onto the native hero, then samples the resulting FK feet and knees. It uses native
stride calibration, actual boot sole extents and bounded source pelvis motion.
Reference source joint positions/clearance are provenance data; they are not a
second independently scaled target trajectory.

These source files and portable checks do not establish Unity import, real mesh
skinning, terrain handling, Android performance or observed gameplay quality.
