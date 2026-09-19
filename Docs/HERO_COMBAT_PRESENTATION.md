# Hero and combat presentation overhaul

Implementation for `dev/vertical-slice`, based on the supplied six-hero concept board. Internal IDs remain unchanged: Veyr/Zhaev, Kaelon/Auren, Nyra/Sivra, Elyra/Ione, Mira/Tahla, Rook/Brakk.

## Six hero models

All six now use cached, contoured mesh geometry for bodies, armor and weapons. The runtime builders use lofted sections, authored ribbons and rings, rather than primitive GameObjects as final body art. Human heroes have modeled facial features and hair; Zhaev and Auren have masked heads. This is a procedural interpretation of the concept, not imported sculpted character artwork.

- **Zhaev:** narrow dark silhouette, violet armor accents, cloth tails and two shaped blades attached to articulated arms.
- **Auren:** broad armored body, masked helmet, amber shield and hammer. Corrected the former cyan accent assignment.
- **Sivra:** human face and hair, compact armor, cyan rifle with magazine, scope, muzzle socket and separate recoil pivot. Corrected the former gold accent assignment.
- **Ione:** violet caster armor, human head, floating orb, independently moving rings and shards, cloth tails.
- **Tahla:** mint accents, human head, long staff with shaped crown and energy seed, cloth tails.
- **Brakk:** heavy silhouette, face and beard, two articulated orange reactor gauntlets with modeled fingers.

Weapon geometry remains attached to arm pivots. Mesh batching preserves independently moving weapons and orbit elements. Existing skins, overdrive scaling, founder presentation and menu showcase integration are retained; these combinations still require visual inspection in Unity.

## Thirty attack presentations

Slots Q/W/E/Ultimate correspond to indices 0/1/2/3. Existing skill values and behavior are unchanged.

| Hero | Attack | Implemented presentation |
|---|---|---|
| Zhaev | Basic | Alternating blade pose, paired violet slash ribbons, small shards and confirmed-hit flash |
| Zhaev | Q | Stronger blade sweep and release gesture |
| Zhaev | W | Jagged violet rift with dark core and debuff ring |
| Zhaev | E | Phase compression, bounded rift trail, departure/arrival rings |
| Zhaev | Ultimate | Existing target warning and windup, crossed slashes, collapsing rift |
| Auren | Basic | Heavy arm stroke, amber arc and hexagonal ground pulse |
| Auren | Q | Hammer strike, amber hex pulse, cracks and impact shards |
| Auren | W | Amber segmented shield tied to real shield health |
| Auren | E | Fixed-radius warning, central shard, inward hex aftermath |
| Auren | Ultimate | Warning and heavy release, expanding amber barrier and actual ally shields |
| Sivra | Basic | Rifle recoil, muzzle core, short cyan tracer and confirmed-hit flash |
| Sivra | Q | Stronger rifle shot and cyan projectile |
| Sivra | W | Existing dash with cyan trail streaks |
| Sivra | E | Rifle projectile and crossed contracting snare bands |
| Sivra | Ultimate | Four distinct recoil/projectile/impact events at the existing 0.18-second interval; cast audio per shot |
| Ione | Basic | Orb gesture, violet projectile and muzzle ring |
| Ione | Q | Projectile over existing windup, area warning, expanding release and shards |
| Ione | W | Contracting field, dark core and tilted violet ring |
| Ione | E | Phase compression, rift trail and departure/arrival rings |
| Ione | Ultimate | Warning-centered dark core, contracting rings and short release pulse |
| Tahla | Basic | Staff gesture, mint orb/seed release and hit flash |
| Tahla | Q | Stronger staff projectile and confirmed-hit/debuff feedback |
| Tahla | W | Traveling seed link, green healing plus and rising ring |
| Tahla | E | Seed link and mint segmented shield, distinct from the healing plus |
| Tahla | Ultimate | Beneficial area warning, green release ring, healing plus and actual ally shields |
| Brakk | Basic | Alternating heavy gauntlet pose, orange arc and ground ring |
| Brakk | Q | Strong gauntlet strike, heavy arc and impact |
| Brakk | W | Gauntlet energy pulse, actual self shield and separate healing cue |
| Brakk | E | Existing windup warning, ground shockwave, cracks and shards |
| Brakk | Ultimate | Heavy preparation/release, larger shockwave/cracks and actual self shield |

## Synchronization and gameplay boundary

Presentation receives the existing authoritative windups, area radii, successful casts, confirmed damage events, dash positions and shield state. Death cancels pending warnings; destroyed follow targets release their slots. Cast interruption is reflected in the visual pose. Removing the duplicate BasicAttack VFX call prevents double basic effects.

Damage, ranges, cooldowns, status effects, targeting, AI and locomotion logic were not changed. Existing instant attacks remain instant. Their cosmetic ranged tracers last 65 ms, so they do not establish a new projectile collision or damage delay. Impact feedback and impact audio remain attached to the authoritative damage instant. Consequently these attacks cannot have a new pre-damage anticipation or physically timed flight without changing gameplay. Windup attacks use their existing anticipation interval. Instant attacks instead use release and recovery poses.

## Geometry, materials and pooling

`HeroMeshLibrary` caches source and vertex-colored mesh variants. Vertex RGB stores the palette; alpha marks energy surfaces. `HeroSurface` is a shared URP material with lighting, rim/specular response, energy pulse and property-block hit flash. Mesh combination reduces renderer count while keeping articulation. Source renderer/filter components are removed after batching, but sockets and transform pivots remain.

`HeroVfxPool` prewarms 192 slots and ten reusable effect meshes. Thirty-two slots are reserved for warnings. Active warnings are never replaced; new effects can be dropped when their partition is full. Slots reuse renderers, mesh references, shared material and property blocks. The attack effect paths do not construct meshes, materials, textures, GameObjects or per-effect coroutines. Pool initialization and hero construction do allocate and must be profiled separately. This does not claim that the entire game allocates zero memory per attack.

Transparent effects use alpha blending, depth testing and no depth writes. Shield faces have low vertex opacity. Short effect lifetimes and bounded shard counts limit overdraw, but Android performance has not been measured. There are no newly generated textures.

## Files

Modified:
- `Scripts/Characters/HeroAbilityExecution.cs`: presentation timing/slot forwarding and volley audio.
- `Scripts/Combat/BasicAttack.cs`: remove duplicate basic effect.
- `Scripts/Combat/HeroCombatRules.cs`: confirmed basic impact feedback.
- `Scripts/UI/MenuHeroShowcase.cs`: updated orbit positions.
- `Scripts/Visuals/HeroSkillVfx.cs`: semantic effect routing for all six kits.
- `Scripts/Visuals/HeroVisualMotion.cs`: attack, recoil, windup, dash and energy poses.
- `Scripts/Visuals/StylizedArtDirection.cs`: Auren/Sivra palette correction.
- `Scripts/Visuals/StylizedHeroVisual.cs`: production builder/material/batching integration.
- `Scripts/Visuals/StylizedHeroVisual.Finish.cs`: moving-part batches and vertex palette support.

Added (paths relative to `Assets/Alterverse`, with Unity metadata):
- `Scripts/Visuals/HeroMeshLibrary.cs`
- `Scripts/Visuals/StylizedHeroVisual.Production.cs`
- `Scripts/Visuals/HeroVfxPool.cs`
- `Resources/HeroArt/HeroSurface.shader` and `.mat`
- `Resources/CombatVfx/HeroEnergy.shader` and `.mat`
- `Editor/HeroPresentationChecks.cs`

This report is `Docs/HERO_COMBAT_PRESENTATION.md`.

## Validation and remaining acceptance checks

Completed outside Unity:
- C# syntax parsing of the local implementation with tree-sitter: passed.
- Normalized gameplay AST comparison against the original `HeroAbilityExecution`, `BasicAttack` and `HeroCombatRules`, excluding presentation/audio statements: passed.
- `HeroLocomotion` unchanged.
- Static checks of resource material/shader GUID references and bounded effect-pool structure: passed.
- Whitespace/error-marker check using `git diff --check`: passed.
- Remote branch comparison before publication: unrelated concurrent changes preserved.

**Not executed:** Unity C# compilation, shader compilation, rendered comparison against the reference, all 30 attacks in Play mode, skin/founder combinations, device profiling or a 60 FPS Android measurement. No Unity editor or C# compiler is available in the implementation environment. Static parsing is not a compilation result.

After pulling the branch, let Unity import the resources, start a disposable match and run **DIMENSIONS FRONT > Checks > Hero presentation assets and live meshes**. The added editor check inspects shader errors, vertex palettes, visual colliders, weapon parenting and the existing base-hero budgets of 24 renderers/6 materials; it does not enforce those budgets for all cosmetic combinations. Run the existing HeroOverhaulRegression checks as well. Visually exercise Basic/Q/W/E/Ultimate for every hero, including interrupts, death, shield depletion, paused time and repeated match reloads. Profile a crowded fight on the target Android device before claiming the 60 FPS goal is met.
