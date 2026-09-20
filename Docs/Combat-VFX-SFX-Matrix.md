# DIMENSIONS FRONT — Combat presentation audit and attack cards

Audit: 2026-09-20. Source: `dev/vertical-slice`, commit `ccadf6aaad971eedc0118a90d4462b04fc527844`.
This is a source-based design specification, not a claim of visual or audio acceptance.

## User-authorized timing amendment

During implementation the user explicitly authorized changing immediate hits: “das die angriffe sofort treffen finde ich nicht gut das kannst du verändern”. This supersedes the original presentation-only constraint **for attack delivery timing**. Damage, HP, attack intervals, ability cooldowns, ranges, area radii, heal/shield amounts, CC durations, movement and dash distances remain unchanged.

New targeted attacks commit to their selected target, prepare, release, then resolve at contact. Ranged shots visually track that target (not free-aim skillshots). Death, disable or stun of the caster cancels pending delivery; invalid targets and targets outside the original range at release/contact receive no hit. No retargeting, collision system, cooldown refund or added input lock. These are explicit consequences of replacing instant hits and require gameplay testing.

| Hero | Basic windup | Formerly instant offensive Q/W/E windup | Ranged flight |
|---|---:|---:|---|
| Zhaev | .10 s | .14 s (Q/W) | None |
| Auren | .20 s | .24 s (Q) | None |
| Sivra | .08 s | .12 s (Q/E) | distance / 38, clamped .07–.32 s |
| Ione | .14 s | Existing area windups retained | basic distance / 23, clamped .07–.32 s |
| Tahla | .12 s | .16 s (Q) | distance / 27, clamped .07–.32 s |
| Brakk | .16 s | .20 s (Q) | None |

Sivra R: .22 s initial preparation, four releases .18 s apart, each with its own distance-based flight. Zhaev R stays .4 s; all existing AoE windups stay unchanged. Healing, shielding and dashes retain their existing activation times. Times are resolved on Unity frames, not sub-frame guarantees. Menu attack cards show the new windup values.

## Authoritative execution and invariants

- `HeroIdentity`, `HeroCombatProfile`, `HeroAbilityKit`, `HeroAbilityExecution`, `HeroCombatRules`, `Health` and `HeroCombatStatus` define the real roster, numbers, damage and statuses.
- `BasicAttack` and `MobaBotCombat` both call `HeroCombatRules.BasicHit`. `PlayerHeroAbilities` and `HeroBotAbilities` share `HeroAbilityExecution`.
- `HeroAttackCatalog` agrees with the execution branches. Internal IDs remain Veyr/Kaelon/Nyra/Elyra/Mira/Rook; displayed names are below. No new gameplay attacks are specified.
- Requested `HeroPresentation` is not a file in this snapshot. Actual presentation is `StylizedHeroVisual` (including Production/Finish partials), `HeroVisualMotion`, `HeroSkillVfx`, `HeroVfxPool`, `Combat.CombatFeedback`, `CombatImpactPool` and `HeroCombatAudio`/`DimensionAudio`.
- `PrototypeBootstrap` uses `Combat.CombatFeedback`, not the legacy `Visuals.CombatFeedback`. The latter still uses material instances and primitive bursts. `VeyrAbilities` is also legacy; current player input uses `HeroAbilityExecution`. No legacy gameplay is substituted.
- `PrototypeVfx`, founder effects, minion/tower/planet/mech effects, `MatchAudioDirector` and the audio budgets were inspected for adjacent presentation paths. Global replacement would have a wider regression surface and is outside round one.
- Baseline basics and most targeted skills were immediate. The timing amendment above replaces that behavior through shared `HeroAttackDelivery`. Ione Q still travels during its existing .45-second windup. No new projectile collision, gameplay random draw or timescale manipulation.
- Sphere overlaps retain their original radius and collider-intersection semantics; a projected warning circle describes the query sphere, not an invented center-distance hit rule. Collider edges may overlap a sphere while their pivots are outside it.
- Shield amounts below are requested values. `Health.AddShield` retains its cap (40% of target max HP), max-stacking and expiry behavior.
- Startup follows the timing amendment. Pure mobility has no enemy impact sound or hit flash.

## Audit findings and first safe round

1. Auren and Brakk basics both call the same heavy slash. Replace with geometric hammer compression versus linear fist piston, keeping Zhaev's blade arc.
2. Tahla offensive projectiles and hit petals resemble healing petals. Give damage a narrow pointed seed silhouette and inward splinter hit; keep broad petals/plus for healing.
3. Baseline damage preceded a 65 ms projectile animation. Under the user amendment, damage now waits for preparation and actual presentation flight; a shared fixed-capacity scheduler handles player and bot delivery.
4. Auren's warning uses an inscribed hexagon: its sides sit at only 86.6% of the circle radius. Use a circumscribed 48-segment circular warning for every spherical area; keep hex identity inside the warning.
5. Hero audio is viewport-gated but all voices are centered 2D sounds of constant gain. Add bounded screen-relative pan and ground-plane attenuation within the existing eight-voice pool. No additional sources.
6. Shield absorption is already supplied by `Health.Damaged`, but generic impact geometry does not distinguish it. Add a short shield facet response from that event, even if the hit breaks the shield; do not infer absorption from remaining shield.
7. Baseline melee keyframes drew back after immediate damage. Melee now prepares before damage, reaches its contact pose at delivery, then follows through. Existing windup skills retain their windup.
8. Existing area/delayed-single coroutines check stun at resolution, not continuously. Keep their warnings through temporary stun so an attack that becomes valid again never resolves without warning. Newly queued targeted attacks cancel on stun; existing area timing/semantics remain unchanged.

Round one implements those focused presentation changes plus the explicitly authorized timing amendment. All 30 cards below are the target design; Q/W/E/R motifs not mentioned in the implementation report remain existing effects or follow-up work. No claim that the complete overhaul is visually finished.

## Shared card conventions

Intensity V/A: 1 = basic, 2 = utility, 3 = strong ability, 4 = ultimate. These are art/mix targets, not damage multipliers or literal volume settings. Impact always requires an actual damage/absorption event; a miss may have a ground release but no victim flash. Shield hit: thin glass facet and tight resonance layered with the attacking hero signature. HP hit: hero-specific contact plus brief material flash. CC indicators are small and do not imply additional control.

Sound assets: existing `Resources/Audio/Heroes/<internal ID>/<Basic|Q|W|E|R>_Cast.ogg` and supported `_Impact.ogg`. Names such as “Void Cut” below describe intended timbre, not new unverified files. Actual perceived distinctness, clipping and phone-speaker quality still need listening tests. W shield/heal and mobility use their cast cue alone; no fake impact resource is introduced.

## Zhaev Null — Veyr — violet / pale lavender edge

Signature: **Void Cut** — dry metal, short reality tear, restrained low pulse. Narrow broken lines, not circular magic. Color applies to all five cards; team accent belongs in a small separate marker, not a recoloring of the attack.

| Slot / real name | Type, unchanged values and revised timing | Startup / action shape / travel | Impact / aftermath | Sound start → impact | V/A |
|---|---|---|---|---|---|
| Basic Attack | Prepared melee (see timing table); 24 damage, 2.25 range, .85 s interval | Short alternating blade draw; crescent from active weapon at contact | Narrow cut at victim, pale pin spark; .16 s sweep/fade | Dry swish → sharp metal/void snap | 1/1 |
| Q — Null Cut | Prepared single target; 38 damage, 3.1 range, 7 s CD | Short preparation then longer forward-cut silhouette; hand flare at release | Single clean incision; split edge contracts, no ground blast | Taut blade flick → hard void cut | 3/3 |
| W — Rift Scar | Prepared single target; 28 damage, 3.3 range, 10 s CD; 20% slow 1.2 s | Short blade preparation then jagged vertical seam at target | Broken rift edge and small slow marker; dark seam seals | Dry fabric/space rip → brittle tear | 2/2 |
| E — Gapfold | Movement only; up to 4.2 range, 12 s CD | Compressed silhouette; three short torn afterimages on actual dash path | Arrival seam closes; no victim impact | Short suction/snap → none | 2/2 |
| R — Vacant Echo | .4 s delayed single target; 58 damage, up to +35% vs wounded heroes; 3.5 range, 48 s CD | Existing target warning and crossed blade preparation; never depict AoE | Two crossing incisions on confirmed victim; broken seam contracts .32 s | Rising fracture tension → paired void cuts with low body | 4/4 |

## Auren Kest — Kaelon — amber / ivory metal edge

Signature: **Metal Thunk** — solid armor body and shield resonance. Geometric compression, heavy hammer and upright panels. No sword arc for basic.

| Slot / real name | Type, unchanged values and revised timing | Startup / action shape / travel | Impact / aftermath | Sound start → impact | V/A |
|---|---|---|---|---|---|
| Basic Attack | Prepared melee (see timing table); 14 damage, 2.3 range, 1.05 s interval | Heavy hammer draw then contact; short hexagonal compression at contact | Broad angular amber facet, two heavy sparks; .20 s fade | Short armor movement → low metal thunk with audible mid crack | 1/1 |
| Q — Anchor Strike | Prepared single target; 24 damage, 3.4 range, 9 s CD; 35% slow 1.5 s, stun .55 s | Heavy hammer preparation then downward accent | Small ground fracture and compact overhead control marker | Hammer release → dense metal/stone knock | 3/3 |
| W — Lumen Wall | Self 75 shield / 4 s, 14 s CD | Shield arm braces; panels form around actual recipient | Stable shell until actual shield ends, no damage explosion | Metallic panel lock / shield resonance → none | 2/2 |
| E — Gravity Anchor | .5 s warning; 20 damage; target within 5, radius 2.6; 12 s CD; 35% slow 1.5 s | Accurate circular outer boundary, hex and falling anchor inside | Inward geometric pressure; central shard settles, .42 s fade | Heavy anchor tension → low grounded pressure hit | 3/3 |
| R — Kest Citadel | .65 s warning; self-centered radius 3.8; 38 damage; enemy 50% slow 2 s; allies 60 shield / 3 s; 55 s CD | Circular boundary plus rising inner fortress panels; planted pose | Heavy geometric slam and independent ally shield shells; .4 s field fade | Energy rise + metal resonance → broad citadel thump | 4/4 |

## Sivra Quell — Nyra — cyan / white core

Signature: **Rifle Crack** — dry technical transient with a small energy tail. Narrow straight traces, never magic circles or a sustained laser.

| Slot / real name | Type, unchanged values and revised timing | Startup / action shape / travel | Impact / aftermath | Sound start → impact | V/A |
|---|---|---|---|---|---|
| Basic Attack | Prepared ranged + flight; 22 damage, 6.8 range, .62 s interval | Short aim preparation; muzzle flash + kick; narrow visible flying bolt | Pinpoint directional spark; trace fades within .09 s | Tight rifle crack → small armor/electric tick | 1/1 |
| Q — Prism Shot | Prepared target + delivery; 32 damage, 7.8 range, 7 s CD | Stronger narrow prism core; twin bevels, not larger AoE | Sharp split-point contact; .12 s tail | Harder precision crack → glass-edged ping | 3/3 |
| W — Vector Shift | Movement only; up to 3 range, 14 s CD | Backstep body/recoil with parallel cyan path ticks | Fading directional ticks; no enemy hit | Compressed thruster hiss → none | 2/2 |
| E — Light Snare | Prepared target + delivery; 20 damage, 6.8 range, 12 s CD; 30% slow 1.8 s | Narrow forked shot motif | Two local crossing snare bands; keep victim readable | Technical catch snap → tight energy clamp | 2/2 |
| R — Sightline Break | Four flying shots, .22 s preparation, .18 s release spacing; total 64 damage, 7.8 range, 50 s CD; stops when target/caster invalid | Short aim/charge then four discrete muzzle kicks and flying shots | Four point impacts, no beam loop; clean recovery after last shot | Four stronger rifle pulses → matching distinct contact ticks | 4/4 |

## Ione Vaska — Elyra — arcane violet / cool lilac rim

Signature: **Arcane Pulse** — resonant orbital tone, soft attack envelope, controlled implosion. Distinguish from Zhaev by circular orbital topology, not hue alone. Orb flight uses its own bounded speed from the timing table.

| Slot / real name | Type, unchanged values and revised timing | Startup / action shape / travel | Impact / aftermath | Sound start → impact | V/A |
|---|---|---|---|---|---|
| Basic Attack | Prepared ranged + flight; 15 damage, 6.1 range, 1.10 s interval | Brief orb preparation then compact orb flight from its actual focus | Small inward rune/orbit response; .18–.24 s contraction | Rounded arcane pulse → soft resonant compression | 1/1 |
| Q — Orbit Fracture | .45 s warning; 45 damage, 7.3 range, radius 2; 8 s CD | Orb travels during existing windup; circular warning at fixed center | Outward fracture with four fragments; runes fade .30 s | Orbital rise → controlled arcane crack | 3/3 |
| W — Invert Field | .55 s warning; 26 damage, 6.5 range, radius 2.6; 12 s CD; 35% slow 2 s | Tilted orbital circle compresses inside fixed warning | Inward field and compact dark center; no implied pull movement | Descending resonance → hollow inward pulse | 3/3 |
| E — Phase Fold | Movement only; up to 1.8 range, 16 s CD | Paired orbital gates and compressed body silhouette along actual dash | Arrival orbit contracts; no enemy impact | Soft phase fold → none | 2/2 |
| R — Impossible Center | .85 s warning; 72 damage, 7 range, radius 3.5; 58 s CD; 45% slow 2 s | Small dark center plus tilted rings; exact outer warning throughout | Controlled implosion, short outward ring; rune residue .48 s | Layered orbital tension → deep mid-rich implosion | 4/4 |

## Tahla Venn — Mira — green-turquoise / pale mint (damage), warm mint (healing)

Signature: damage **Energy Tick**, healing **harmonic bloom**, shield **crystal shelter**. Pointed seeds are offensive; broad petals and upward plus forms are beneficial.

| Slot / real name | Type, unchanged values and revised timing | Startup / action shape / travel | Impact / aftermath | Sound start → impact | V/A |
|---|---|---|---|---|---|
| Basic Attack | Prepared ranged + flight; 16 damage, 6 range, 1 s interval | Brief emitter preparation then pointed seed flight | Small inward seed splinter; no petal/plus damage flash; .14 s fade | Short organic tick → sharp energy contact | 1/1 |
| Q — Resonant Seed | Prepared target + delivery; 18 damage, 6.5 range, 7 s CD; 25% slow 1.5 s | Larger pointed seed, compact thorn edge | Tight seed splinter and subtle foot-level slow mark | Seed pluck → crisp thorn tick | 2/2 |
| W — Growth Veil | Immediate most-wounded ally heal 42, 6.5 range, 9 s CD | Broad petal link from emitter; recipient pulse starts with real heal | Warm plus/upward ring, .45–.48 s soft release | Warm harmonic impulse → none (single cast cue) | 2/2 |
| E — Venn Shelter | Immediate wounded-ally shield 50 / 4 s, 6.5 range, 13 s CD | Faceted protective shell closes around recipient | Stable transparent shell; disappears when shield actually ends | Crystal lock with gentle resonance → none | 2/2 |
| R — Wildsurge | .4 s warning; self-centered radius 5; allies heal 58 and shield 25 / 3 s; 58 s CD | Beneficial circle and spreading open petals; no threat burst | Upward heal flows and individual shells; soft .55–.6 s wave | Warm ensemble swell → broad harmonic resolution | 4/4 |

## Brakk Orun — Rook — burnt orange / warm steel spark

Signature: **Heavy Punch** — hydraulic movement, metal knuckle crack, dry ground body. Linear piston and physical debris distinguish him from Auren's geometric shield architecture.

| Slot / real name | Type, unchanged values and revised timing | Startup / action shape / travel | Impact / aftermath | Sound start → impact | V/A |
|---|---|---|---|---|---|
| Basic Attack | Prepared melee (see timing table); 24 damage, 2.5 range, .9 s interval; below 60% HP heals 12% HP damage vs heroes | Short alternating fist draw; compact linear compression at contact, no slash | Short shock ring, two steel fragments; .18–.22 s follow-through | Hydraulic punch → dry metal knuckle crack | 1/1 |
| Q — Wreck Swing | Prepared target + delivery; 34 damage, 2.9 range, 7 s CD; 20% slow 1 s | Larger piston punch with shoulder rotation | Concentrated physical shock, short sparks, no area damage suggestion | Heavy piston release → hard mechanical crunch | 3/3 |
| W — Armor Grip | Self heal 30 plus shield 20 / 3 s, 13 s CD | Both gauntlets brace; armor segments lock | Compact shell plus upward recovery mark | Hydraulic armor lock → none | 2/2 |
| E — Wreckstorm | .35 s warning; self-centered radius 2.8; 28 damage, 10 s CD | Double-fist lift during real windup; exact circle | Dry cracks and short low shockwave; .38 s ground fade | Piston load → short ground slam | 3/3 |
| R — Orun Overdrive | .6 s warning; self-centered radius 3.2; 48 damage plus self shield 65 / 4 s; 52 s CD | Double gauntlet raise, mechanical pressure, accurate warning | Largest physical slam motif; low debris dome, shell on Brakk; .4 s fade | Hydraulic overdrive → deep physical slam with metal mid crack | 4/4 |

## Budgets and verification gates

- Existing shared mesh pool: 192 render slots, 32 reserved for warnings. One shared material, per-slot MaterialPropertyBlock. No new per-hit GameObjects, materials, lights, colliders, particle systems or basic-attack coroutines. `HeroAttackDelivery` is added once per attacking hero and reuses eight pending slots; existing ability coroutines remain. New seed shape is built once at pool initialization.
- Mesh submissions are not measured draw calls: transparent passes and visible renderer count must be profiled. Basic release budgets (excluding confirmed-hit response): Zhaev 4 slots, Auren 4, Sivra 3, Ione 4, Tahla 4, Brakk 4. Hero impact normally adds 2; shield response adds 1. Existing generic impact pool/rig flash also costs rendering work.
- Utility shells persist for real shield lifetime, not arbitrary minute-long particles. Area effects remain bounded; many simultaneous shields can exhaust optional slots. Warnings retain dedicated capacity, but pathological overlapping casts can still exhaust 32 slots; stress test required.
- Audio retains 8 pooled voices and existing per-cue cooldowns. Screen-relative pan/ground-plane gain is a mobile mix, not a claim of full world-space 3D acoustics. Same-hero cues can still share a limiter; own-hero priority requires a later mix pass. Sivra R uses an .08-second limiter so its four .18-second releases are not suppressed by the generic .3-second gate.
- Real Android 60 FPS, monochrome readability, sound-only identification and aesthetic quality are acceptance gates, not proven by source inspection.
- Unity Play Mode checklist: six basics against HP and shields; each Q/W/E/R; lethal and blocked hits; caster death/CC/disable during windup and flight; no stale hits after respawn; target leaves range; moving and multiple targets; both teams; 10-hero fight; repeated pause/respawn/restart; phone speaker and headphones; profiler after warmup.
