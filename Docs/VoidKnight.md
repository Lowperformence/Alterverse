# Void Knight

Black armored, purple emissive swordsman from an unknown galaxy. Helmet reference: existing shop avatar `Resources/Avatars/Avatar21.jpg`. Standard roster hero, appended ID 9; no premium purchase SKU or price was invented.

| Attack | Damage | Reach | Cooldown | Behavior |
|---|---:|---:|---:|---|
| Greatsword basic | 18 | 3.1 m | 1.2 s interval | 0.28 s preparation; three alternating two-handed strikes |
| Q — Rift Sweep | 24 | 3.4 m | 7 s | 0.28 s preparation; one acquired enemy in the melee cone |
| W — Null Guard | — | Self | 12 s | Anchored 0.65 s; 50% reduction in a fixed frontal 120° sector; successful block triggers one 14-damage counter at 3.2 m |
| E — Rift Lunge | 16 | 4 m | 9 s | 0.18 s preparation, 0.22 s lunge; first enemy stops travel; native movement collision; no invulnerability |
| R — Eventide Cleave | 42 | 3.8 m | 40 s | 0.65 s telegraph, fixed 80° cone; once per enemy; 25% slow for 1.2 s |

Passive: three confirmed basic hits prime a 30-point temporary shield for 2 s on the next damaging skill. Whiffs and rejected damage do not build or consume charges. Multi-target hits consume it once.

Health: 290 raw, **350.175 final HP** after the existing universal multiplier, versus Auren's 481.7925 HP. Existing regenerating hero shield capacity: 60. Sustained basic DPS: 15, below the tank's 16.19. The explicit 18-damage requirement takes precedence over the initial lower-per-hit-damage idea. The normal 45-second opening ultimate lock is retained.

## Assets

- `ArtSource/Heroes/VoidKnight/VoidKnight.blend`: 145 individually named editable mesh parts, five materials, 19-bone rig and twelve actions. Each mesh's `bone` custom property identifies its skin binding.
- `Construction/`: current front/side/back body, armor and sword views, five attack poses and complete part manifest. `Concept-v1/` archives the initial sketches; the current model supersedes their earlier weapon/helmet details.
- `Resources/AuthoredHeroes/VoidKnight.fbx`: runtime copy merged into `DF_Anatomy` and `DF_WeaponR`, 15,548 triangles. Re-export merges only an in-memory copy and preserves separate source geometry.
- `Icons/0.png`–`4.png`: original built-in image-generator artwork, basic / Q / W / E / R. Runtime copies: `Resources/VoidKnight/Icons/`, imported with 512-pixel limits. Each motif depicts the corresponding sword cut, sweep, parry, thrust or downward cleave.
- `SoundSource/`: eleven original procedural WAVs and NumPy/SciPy generator. Sixteen mono 48 kHz PCM16 runtime cues under `Resources/Audio/Heroes/VoidKnight/`; three basic cast variants. W has no separate preparation cue.

Actions: Idle, Run, Attack1, Attack2, Attack3, Skill0, Skill1, Skill2, Ultimate, Counter, Hit, Death. The final upper-body pose layer samples original clips after shared grounded locomotion, while preserving leg transforms. Imported Animators are disabled so they cannot compete for bone ownership.

Gameplay remains host-authoritative. Stuns, death and disabling cancel preparation and release owned roots. Cone/thrust warnings and their cancellation have a dedicated reliable, server-validated NGO presentation message; existing cue IDs remain stable.

## Validation

Re-executed after restoration:

- 67 deterministic C# combat checks: exact damage, extended melee range, timing, passive charges/whiffs, parry sectors/counter, first-contact lunge, cone/slow, interruption and client authority.
- FBX reimport: two meshes, 19 bones, twelve named clips, normalized weights and finite geometry/bone poses across 384 frames.
- Five square icon files and sixteen nonempty mono PCM16 48 kHz runtime WAVs.

**Not run here:** native Unity Editor import/build/play mode, actual NGO host/client session, Android build or on-device performance. Fixture tests do not establish those results.

In the full Unity project run **DIMENSIONS FRONT → Checks → Void Knight integration (non-destructive)**. Then test all attacks, E against walls, stun interruption, two-hand grip during movement/attacks, emission/bloom, icon slots and volume controls, and two actual connected clients. No scene replacement or separate prefab wiring is needed: the existing roster creates the hero and loads its authored model by HeroId.

Use Blender Python/bpy to run `export_void_knight.py`, `validate_fbx.py` and `render_reference.py` from the source folder. Native `.blend` source remains modular and is never overwritten by the merged export.
