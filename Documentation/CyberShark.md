# Nerok Abyss — cyber-shark bruiser

Nerok is appended as HeroId 7. Existing hero/save identifiers, Traveler's 1,500-shard price and the original 105 audio cue addresses remain unchanged. Nerok uses normal hero selection, progression, universal cosmetics, respawn and server-authoritative ability execution. Like the original six heroes, he is available without a separate hero purchase.

## Baseline combat budget

285 raw HP becomes 344.1375 HP under the existing global health multiplier. Basic attack: 22 damage, 2.6m reach, 0.98s interval. Movement uses 6.5 times the established global movement multiplier. His HP and basic DPS are below Brakk's; range/control compensate for lower sustained damage. These are starting balance values, not a claim that duel or multiplayer playtesting has been completed.

| Ability | Behavior | Cooldown |
|---|---|---:|
| Passive — Salvage Tide | A damaging hook against a hero grants 12 shield for 2s. | Tied to hook |
| Anchor Recall | 28 damage, 6.2m target range, 0.30s windup plus visible flight. Pull mobile enemies up to 4.2m, stopping 1.3m from Nerok. | 12s |
| Pressure Hull | 45 shield for 3s; 15% speed boost for 2s. | 14s |
| Breaker Wave | 26 damage in 2.8m after 0.30s; 25% slow for 1.2s. | 9s |
| Abyssal Maelstrom | Fixed 3.2m radius at up to 6m range, 0.65s warning, six ticks totaling 60 damage over 3s; 25% slow and small inward nudges. | 58s |

The existing 45-second initial ultimate lock applies. Leaving the hook's range, breaking sight, or interrupting its windup avoids the hook. Buildings block the throw and swept movement. Buildings/cores/towers are not displaced. Leaving the vortex prevents remaining ticks. Pending casts/fields end on death or disable; no new colliders change hero hitboxes.

## Blender source and runtime assets

`Tools/CyberShark/build_cyber_shark.py` generates the original shark, standard anchor, armature, three effect meshes and five HUD images using Blender 4.5. Runtime rigid skin data is under `Assets/Alterverse/Resources/CyberShark`; the editable scene is `ArtSource/Heroes/CyberShark/NerokCyberShark.blend`. Current exported hero/accessory mesh: 26,742 triangles (9,614 head-bound triangles). The game's established skeleton/pose adapter is shared with the existing hero template; visible geometry is replaced by Nerok's mesh. No reference photograph/watermark is embedded.

The VFX reuse the existing pooled HeroEnergy shader and lifetime scheduler. The water field has feathered rotating spiral ribbons, low wave crests, bubbles and a readable boundary. Its warning matches the actual radius. Multiplayer relays confirmed shark VFX from the server and synchronizes forced movement with hero snapshots. All peers must use this build (hero snapshot message V3).

## Icons, Epic shop and audio

Blender-rendered icons cover Q/W/E/ultimate/basic and use the established HUD cooldown material. The Epic TIDAL CIRCUIT and DEEP CORAL anchors cost the standard 500 shards each and use the existing purchase/equip/save flow. Both retain the standard anchor geometry, 50% transparent glow and visible ornaments.

The user-supplied `universfield-water-splash-199583.mp3` is preserved unchanged as `Audio/Heroes/Nerok/Ultimate_Selected.mp3` (SHA256 `6d6b9a8f4c8e801dd2c11eb236c21bdd76e288642230e7b089b308dcb2959f52`). It plays once when the vortex starts, not once per damage tick. Original synthesized anchor/hydraulic preparation and impact cues are reproducible with `Tools/CyberShark/build_audio.py`; that script never overwrites the uploaded ultimate recording.

## Verification

Blender generation/rendering, exported triangle/normal/bone/channel validation, distinct audio addresses, icon/resource presence and MP3 byte equality were checked outside Unity. `DIMENSIONS FRONT/Checks/Cyber shark integration (non-destructive)` adds a Unity resource/catalog/balance-bound check. Unity compilation, hook collision playtests, mobile appearance/FPS and a two-peer multiplayer test remain to be run in Unity; Unity is unavailable in the editing environment.

## Pirate art revision
Light-blue shark skin, tapered snout, visible teeth and gills, naval vest with brass trim, leather straps, split coat, curved tail, lantern and detailed anchor authored in Blender. The hook draws a pooled interlocked-chain VFX between live actor and victim transforms for the 0.35-second pull; target identity is relayed for online presentation. Nerok has 90 base shield (standard 50 plus requested 40), including existing recharge, respawn and network shield limits.

Latest art adjustment: lantern and its hanging chain removed; standard anchor mesh doubled around the grip, with the projectile export and anchor HUD framing updated accordingly.

Small healed scars added to exposed head, chest, arms and hands as bone-bound geometry.

Free-hand chain hook: seven interlocked oval metal links, a forged eye and a long pointed hook are bound to `WeaponGripL` in independent mesh group 1. `CyberSharkAppearance` renders this group with its own rust-brown metal/oxidation colors and a runtime swing bone parented to the left grip. The forged hook uses a darker brown material slot than the chain. The held link remains fixed while the accessory swings with `HeroVisualMotion.PlanarSpeed` and `GaitPhase` (up to 10 degrees forward/back and 4 degrees sideways), easing back to rest when stopped. Right-hand anchor shrinking, Epic anchor skins and the thrown anchor export continue to affect group 2 only. No collider or ability behavior changes.



## Reference head revision — 2026-10-08

The supplied front-view reference guided a broader, shorter muzzle, rounded forehead, sloped inset eyes with subtle bronze irises, compressed brow folds, inlaid nostrils, a raised-corner grin, upper/lower primary and secondary tooth rows, modeled gums and mouth interior, curved gill cuts, a flush healed scar and swept cranial fin. The unified skull/muzzle/jaw/cavity has 1,666 shared source vertices and is a closed manifold with consistent winding. The cranial fin retains the previous maximum height of 2.075 source units.

`CyberShark/SharkSkin` adds slate dorsal pigmentation, an ivory lower jaw, filtered fine denticle relief and a restrained wet surface response. The face uses unposed source coordinates in UV channel 2, keeping its skin detail fixed during animations. Eyes, iris, ivory enamel and dark gums receive independent runtime material assignments. Existing body and accessory rest coordinates retain their prior behavior.

`build_reference_head.py` contains the reproducible head geometry and Blender materials. For a head-only runtime revision, run `build_cyber_shark.py` with `--skip-icons --preserve-body-from /path/to/previous/Model.bytes`; `merge_head_export.py` preserves all non-head triangle positions, normals, winding, skin weights and materials exactly. This revision preserves 17,128 body/weapon triangles, the rig anchors, hook grip, tail pivot and accessory groups. Each runtime renderer selects a 16-bit or 32-bit index buffer according to its own vertex count.

`head-front.png`, `head-three-quarter.png` and `head-side.png` were rendered and inspected with Blender 4.5.9. `head-validation.json` records export integrity, finite unit normals, rigid head binding, mouth indices, body preservation and source topology. Re-run the checks with `python Tools/CyberShark/verify_reference_head.py --original /path/to/previous/Model.bytes`; add `--source` when invoking that script through Blender. The existing manual Unity menu **DIMENSIONS FRONT / Validate / Nerok skin and anchor poses** now also checks the head shader, materials and rest coordinates. Unity compilation, Android shader compilation, in-game appearance and mobile performance remain unverified in this environment.
