# Attack VFX upgrade – 20 October 2026

New CC0 textures and flipbooks now run inside the existing combat effects facade,
not as a separate damage or network system. All ten heroes' basic attacks and
four abilities receive the imported art through their real release, cast,
support, dash, area and confirmed-impact paths.

| Hero | Selected visual treatment |
| --- | --- |
| Zhaev Null / Veyr | Animated light/cross slashes, tinted flying rift detail, short shadow departure |
| Auren Kest / Kaelon | Purple punch/blue rune travel, shield badge, yellow ultimate contact accent |
| Sivra Quell / Nyra | Compact radial muzzle flashes, red/white projectile glow and coloured contact sparks; blue laser stays a beam |
| Ione Vaska / Elyra | Tinted animated energy core, runic field contact and soft phase departure |
| Tahla Venn / Mira | Golden offensive stars, green healing stars and a protective shield badge |
| Brakk Orun / Rook | Compact animated hot fist contacts, dirt/dust slam accent and healing/shield feedback |
| THE TRAVELER | Chronolight/rune accents matching existing yellow, purple and white skill colours; soft portal departure |
| Nerok Abyss | Cyan lunge/anchor detail, a rotating inner vortex crest and water contacts instead of Brakk's gauntlet fractures |
| Andromeda Timmy | Animated claw accent, confirmed scratch mark, inner dust burst and two direction-aligned nozzle flames |
| Void Knight | Heavy animated sword swings, guard badge and cleave accent; basic now uses a melee slash rather than the generic ranged orb fallback |

The twelve planet-monster attack styles also receive matching melee sweeps,
projectile accents and confirmed-impact art, including compact laser eye/contact
glows. Soldiers and shared legacy effects now use pooled shots, sparks, rings,
slashes, support and ground pulses. Bomb/meteor plumes reuse the same atlas and
pool instead of creating a noise texture, material, five particle systems and
their objects on every impact.

## Runtime limits

- One 1024×1024 atlas with 64 cells, one shared material and one cached two-triangle quad.
- Android ASTC 6×6 override; no mip chain or readable CPU copy. Source PNG is about 0.54 MB.
- Slots, MaterialPropertyBlocks, age, delivery cancellation, network replay age, target/presentation following and death cleanup reuse the existing scheduler.
- New layers are optional under the existing battery/overload budget. The 32 warning and 32 core reserves remain protected.
- Missing/unsupported atlas material omits only the imported layer, avoiding magenta/opaque fallback quads.
- Exact warning geometry, beam endpoints, damage, healing, shields, cooldowns, movement and event protocol retain their gameplay sources.
- Sprite rows are converted to Unity UV origin, frame indices clamp inside each clip, and translucent padded cells use clamp/bilinear sampling.
- Jet/anchor texture axes follow their presented heading. Timmy cancellation removes the new attached flames too. Shield accents end with the shield.

## Validation

`Tools/Multiplayer/VfxRegression/run.py` exercises the complete production facade,
pool and transport against controlled Unity/GPU/network boundaries. It also
executes every one of the 50 actual combat actions with and without graphics,
requires the imported material to appear, and compares combat outcomes and
semantic events. Additional checks cover 30/60/120 FPS animation age, paused
animation, delayed replay, cancellation, absent resources and saturated pools.

`Tools/Visuals/AttackAtlasRegression/run.py` validates asset identity, masks,
frame partitions, padding, resource links and Android import settings.
The aiming and all-hero delivery fixture suites cover targeting and attack
application separately.

Completed in this workspace:

- 4,699 production VFX/combat/transport fixture checks, including all 50 actual hero actions, all twelve monster styles and 500 simultaneous shared contact bursts without consuming player-shot reserves.
- 12,954 held-attack/facing/area-edge assertions and 699 ranged-cone/delivery assertions at 30/60/120 FPS.
- 111 atlas integrity, padding, provenance, resource-link and mobile-import checks.
- Void Knight combat and temporary future-release purchase regression suites.
- Negative control: disabling the imported layer in a temporary compile is rejected by the real-action test, while the production sources remain intact.

Earlier in the same implementation, the 1,319 skill-entry/general combat checks
also passed. These counts refer to deterministic fixtures, not device frames or
native Unity rendering.

The existing Unity preflight now also verifies the atlas material, actual texture
import and Android settings. Run **DIMENSIONS FRONT → Validation → Validate and
precompile project shaders** in Unity. This executes automatically before Play
Mode and build through `ProjectShaderValidation`.

**Outstanding acceptance:** native Unity 6000.6.1f1 / URP 17.6 import and shader
compilation, visual inspection at the real gameplay camera, Android frame-time
and memory measurements in a ten-player fight, and an updated Android build.
No Unity editor or device GPU is available in this workspace, so deterministic
fixture results must not be reported as completed device/render tests.

Original archives, authors and CC0 provenance are documented under
`ArtSource/CombatVfx`. Only the selected atlas enters the game; the 52 MB download
bundle and the packages' sample projects have not been imported.
