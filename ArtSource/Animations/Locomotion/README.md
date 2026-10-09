# Free authored directional locomotion

Animation source: **KayKit Character Animations 1.1**, by Kay Lousberg.

- Author's package: https://kaylousberg.itch.io/kaykit-character-animations
- Author's website: https://kaylousberg.com/game-assets/character-animations
- License: CC0; original package notice is preserved in `License-KayKit.txt`.
- The free GLB download supplies every action below. No purchase or paid Blender source is required.

| Bank clip | Free source action | Rig_Medium GLB |
|---|---|---|
| `walk` | `Walking_A` | `Rig_Medium_MovementBasic.glb` |
| `run` | `Running_A` | `Rig_Medium_MovementBasic.glb` |
| `left` | `Running_Strafe_Left` | `Rig_Medium_MovementAdvanced.glb` |
| `right` | `Running_Strafe_Right` | `Rig_Medium_MovementAdvanced.glb` |
| `back` | `Walking_Backwards` | `Rig_Medium_MovementAdvanced.glb` |

`KayKit_MovementBasic.blend` and `KayKit_MovementAdvanced.blend` are derived locally
from those free GLBs. They contain the imported skeleton and selected actions,
with mannequin meshes removed. These are not the author's paid source files.

## Reproduce the runtime bank

Download/extract the free 1.1 archive. Its root must contain `Animations/`.
Using Blender 4.5.3, from the repository root:

```sh
blender -b --python Tools/Movement/bake_locomotion_library.py -- /path/to/KayKit_Character_Animations_1.1
python Tools/Movement/GroundedClipRegression/validate_bank.py --source-root /path/to/KayKit_Character_Animations_1.1
```

The baker samples the actual actions at approximately 30 FPS, closes every loop,
aligns the left support phase, and exports anatomical rotations, joint positions,
sole clearance, supporting contacts and distance-based cycle lengths to
`Assets/Alterverse/Resources/LocomotionAnimation/Library.bytes` (gzip JSON).
`sampling-report.json` records action names, source hashes, durations and contacts.

Contacts use evaluated skinned foot/toe vertices against the source rest-pose
floor, plus a stanceward ankle-velocity check to exclude a scraping swing foot.
Foot orientation uses the same `(0, -0.3, 1)` anatomical calibration as the target
runtime. Target steps remove source hip sway because the target pelvis retains
its existing pose. The source forward axis is converted from Blender `-Y` to
game `+Z`, and anatomical left from source `+X` to game `-X`.

The source contains some sole penetration and broad running flight phases.
Runtime terrain/sole correction remains necessary; the data is not proof of
native Unity skinning or in-game visual quality.
