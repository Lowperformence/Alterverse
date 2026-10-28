# Void Knight

Black armored, purple emissive swordsman from an unknown galaxy. Helmet reference: existing shop avatar `Resources/Avatars/Avatar21.jpg`. Preview-only unreleased hero, appended ID 9. Launch price: 1,600 shards, SKU `hero_void_knight`. Purchase and deployment remain blocked by `VoidKnightReleased=false` and the server release gate.

| Attack | Damage | Reach | Cooldown | Behavior |
|---|---:|---:|---:|---|
| Greatsword basic | 20 | 3.1 m | 1.2 s interval | 0.28 s preparation; three alternating two-handed strikes |
| Q — Rift Sweep | 24 | 3.4 m | 7 s | 0.28 s preparation; one acquired enemy in the melee cone |
| W — Null Guard | — | Self | 12 s | Anchored 0.65 s; 80% reduction in a fixed frontal 120° sector; successful block triggers one 14-damage counter at 3.2 m |
| E — Rift Lunge | 22 | 4 m | 9 s | 0.18 s preparation, 0.22 s lunge; first enemy stops travel and is slowed 50% for 2 s; native movement collision; no invulnerability |
| R — Eventide Cleave | 42 + 20 drain | 3.8 m | 40 s | 0.65 s telegraph, fixed 80° cone; once per enemy; up to 20 HP restored per target from actual health lost; 25% slow for 1.2 s |

Passive: three confirmed basic hits prime a 30-point temporary shield for 2 s on the next damaging skill. Whiffs and rejected damage do not build or consume charges. Multi-target hits consume it once.

Health: 290 raw, **390.175 final HP** after the existing universal multiplier and the additional 40 final HP, versus Auren's 481.7925 HP. Existing regenerating hero shield capacity: 60. Sustained basic DPS: 16.67 (20 damage / 1.2 s). These values include the October 25 basic, guard, lunge and health buffs. The normal 45-second opening ultimate lock is retained.

## Assets

- `ArtSource/Heroes/VoidKnight/VoidKnight.blend`: 281 individually named editable mesh parts, five materials, 19-bone rig and twelve actions. Each mesh's `bone` custom property identifies its skin binding.
- `Construction/`: current front/side/back body, armor and sword views, five attack poses and complete part manifest. `Concept-v1/` archives the initial sketches; the current model supersedes their earlier weapon/helmet details.
- `Resources/AuthoredHeroes/VoidKnight.fbx`: runtime copy merged into `DF_Anatomy` and `DF_WeaponR`, 34,406 triangles. Re-export merges only an in-memory copy and preserves separate source geometry.
- `Icons/0.png`–`4.png`: original built-in image-generator artwork, basic / Q / W / E / R. Runtime copies: `Resources/VoidKnight/Icons/`, imported with 512-pixel limits. Each motif depicts the corresponding sword cut, sweep, parry, thrust or downward cleave.
- `SoundSource/`: eleven original procedural WAVs and NumPy/SciPy generator. Sixteen mono 48 kHz PCM16 runtime cues under `Resources/Audio/Heroes/VoidKnight/`; three basic cast variants. W has no separate preparation cue.

Actions: Idle, Run, Attack1, Attack2, Attack3, Skill0, Skill1, Skill2, Ultimate, Counter, Hit, Death. The final upper-body pose layer samples original clips after shared grounded locomotion, while preserving leg transforms. Imported Animators are disabled so they cannot compete for bone ownership.

Gameplay remains host-authoritative. Stuns, death and disabling cancel preparation and release owned roots. Cone/thrust warnings and their cancellation have a dedicated reliable, server-validated NGO presentation message; existing cue IDs remain stable.

## Validation

Re-executed after restoration:

- 103 deterministic C# combat, weapon shop and release-lock checks: exact damage, extended melee range, timing, passive charges/whiffs, parry sectors/counter, first-contact lunge, cone/slow, interruption and client authority, ownership bypass prevention, purchase rejection and unchanged existing prices. Fourteen further checks use a temporary release-enabled source copy to exercise the future 1,600-shard local/server purchase, insufficient funds, duplicate protection, persisted ownership and deployment.
- FBX reimport: two meshes, 19 bones, twelve named clips, normalized weights and finite geometry/bone poses across 384 frames.
- Five square icon files and sixteen nonempty mono PCM16 48 kHz runtime WAVs.

**Not run here:** native Unity Editor import/build/play mode, actual NGO host/client session, Android build or on-device performance. Fixture tests do not establish those results.

In the full Unity project run **DIMENSIONS FRONT → Checks → Void Knight integration (non-destructive)**. Then test all attacks, E against walls, stun interruption, two-hand grip during movement/attacks, emission/bloom, icon slots and volume controls, and two actual connected clients. No scene replacement or separate prefab wiring is needed: the existing roster creates the hero and loads its authored model by HeroId.

Use Blender 5.0.1 Python to run `refine_void_knight.py`, `export_modules.py`, `export_void_knight.py`, `validate_refinement.py`, `validate_fbx.py`, `validate_modules.py` and `render_reference.py` from the source folder. Native `.blend` source remains modular and is never overwritten by the merged export.

## Armor and sword revision

Version 2 replaces the armor and sword geometry with curved layered black metal, separate pectoral and abdominal plates, tapered limbs, smaller wrist joints, shaped boots and a broad diamond-section greatsword with swept quillons. Five materials separate dark metal, machined edges, fabric and two violet emission strengths. The importer sets matching URP metallic/smoothness/emission values only for this hero.

See `ArtSource/Heroes/VoidKnight/BLENDER-REDESIGN.md` for the build plan. `redesign_void_knight.py` builds from the frozen `VoidKnight-v1-reference.blend`, preserving original bones, twelve actions and both hand contacts. `validate_redesign.py` compares every original bone matrix over all 384 action frames and checks the unchanged 0.38 m grip span. Gameplay values, icon IDs and audio IDs remain unchanged.

## Weapon shop and combat additions

The weapon shop lists AMETHYST RIFT and GLACIAL RIFT as two colorways of the original Rift Edge Greatsword. They use the existing mesh-sharing energy-overlay pipeline and standard 500-shard weapon price. The normal variant selector and preview are enabled for Void Knight. Hero release, local purchase and the server catalog remain locked; `weapon_9_0` and `weapon_9_1` are marked unavailable, so they cannot debit funds or grant ownership before release. Existing products/prices are unchanged.

Rift Lunge now slows only the first damaged enemy by 50% for two seconds. The cleave now applies 42 base damage plus a 20-point drain component as one authoritative hit and heals the caster by min(20, actual enemy HP lost). The heal is capped by maximum health and occurs once per target. Shields and rejected damage do not generate healing.

## Hand proportions and hero shop price

Both complete hands are 30% larger in all three dimensions, including palm, fingers, thumb, wrist cover and armor. The rebuild scales 31 components per hand around the original palm center. The unchanged skeleton, sword and hand-contact centers preserve the original animations. Source, runtime FBX, construction views and attack previews are regenerated together.

Void Knight is registered as a premium hero at **1,600 shards**, SKU `hero_void_knight`, with persisted ownership key `DF_HeroOwned_VoidKnight`. The hero shop has its own Void Knight tab, description, price and normal purchase/selection flow. The shared commerce catalog grants the same ownership key; existing transaction handling supplies earned/paid shard debit, retries, duplicate protection and cloud ownership restoration. The local key registry includes the new entitlement for account migration.

The earlier release hold remains: `VoidKnightReleased=false`, server product `available=false`, so previewing cannot debit currency, grant ownership or permit deployment. Future release requires enabling both client and server flags; this commit does not deploy the commerce service. Twenty-six SQLite/Firestore commerce checks pass, including blocked purchase paths and a test-only future release with mixed earned/paid shards, retries and ownership restoration. Native Unity and live service/device testing remain outstanding.

## V3 armor and shoulder carry (2026-10-16)

The current model adds rimmed pectoral shells, articulated three-layer shoulder armor,
cheek and jaw panels, tassets, vambrace lames, knee/greave trim and shaped gauntlet palms. Rear helmet shells, lumbar lames, rear hip plates and calf vents finish the back view.
The greatsword has a forged cross section, steel cutting facets, a recessed violet fuller
and a framed guard crystal. Four repeating 512px albedo/normal maps distinguish brushed
metal and woven undersuit without adding material slots. Unity import version 21 assigns
those same maps to the existing black metal, edge metal and fabric materials.

Idle and Run now carry the sword diagonally backward over the right shoulder. Both hands
follow the same handle; the face is clear. Preparation blends out of that stance and recovery
returns from a fixed follow-through pose. Strike contacts, guard/thrust holds, root, torso,
head and leg motion are retained. Native checks cover 384 frames, unchanged rest bones,
protected contact matrices, unit arm scales and 180 eye-visibility rays from front/±45°.

`ArtSource/Heroes/VoidKnight/Modules/` contains 17 standalone Blender scene files, including
separate left/right pauldrons, vambraces, gauntlets, cuisses, greaves and sabatons, plus helmet,
cuirass, waist, tassets and sword. Each opens directly with its own editable assembly, packed
textures and the original rest binding skeleton. `Modules/manifest.json` lists every part.
Append at the original world origin to preserve its fit to the character.

Runtime remains two skinned meshes, five materials and 34,406 triangles. The resource path
and FBX GUID stay fixed. The 130% hand envelope and 1,600-shard release-locked shop mapping
remain. The `Void Knight model regression` CI opens the source and every module, reimports
the runtime FBX and runs the combat/shop checks. Blender studio renders are construction
previews; actual Unity URP import, Android appearance/performance and native play mode still
need the editor/device pass.

