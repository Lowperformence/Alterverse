# DIMENSIONS FRONT — spell readability, softer audio and lunar density

## Scope and validation boundary

Audited the actual four active specs in HeroAbilityKit for all six heroes (24 abilities), not the older abbreviated UI content list. No ability data, cooldown, damage, range, windup, hitbox, movement, targeting, collider or navigation changes. No music changes.

These are implementation and source-review results. No Unity editor/runtime is available here. Unity compilation, a sustained listening session, gameplay-camera inspection and 5v5 Android performance/readability acceptance remain NOT VERIFIED.

## Ability matrix

Every accepted cast now has a short hero-shaped caster glyph and the existing cast sound. Every applied offensive hit uses a target spark, impact sound and the existing Health.Damaged material/impact feedback. A hit blocked by damage gating does not produce a false confirmed-hit spark. Area boundaries use the actual spec radius and existing windup. Width progression communicates impending resolution; warnings cancel on caster death/disable.

| Hero | Ability | Source / path | Telegraph / area | Impact / target |
|---|---|---|---|---|
| Zhaev Null (Veyr) | Null Cut | Violet fractured cut + melee arc | Instant; no invented delay | Crossed impact |
| Zhaev Null | Rift Scar | Fractured cut + arc | Instant | Impact + brief slow diamond |
| Zhaev Null | Gapfold | Dash route + following head diamond | Existing movement only | No damage implied |
| Zhaev Null | Vacant Echo | Stronger fracture arc | Following target diamond during actual 0.4 s windup | Strong impact ring |
| Auren Kest (Kaelon) | Anchor Strike | Heavy blue arc | Instant | Impact + CC diamond |
| Auren Kest | Lumen Wall | Hexagonal caster glyph | Following shield hexagon, up to 4 s | Ends when actual shield is depleted |
| Auren Kest | Gravity Anchor | Caster glyph + directed strike | 2.6 radius, 0.5 s warning | Area pulse + hit/slow feedback |
| Auren Kest | Kest Citadel | Larger hexagonal caster glyph | 3.8 radius, 0.65 s warning | Strong target impacts, ally shield hexagons |
| Sivra Quell (Nyra) | Prism Shot | Precise gold beam + directional tip | Instant hitscan | Impact |
| Sivra Quell | Vector Shift | Dash route + following head | Existing movement only | No damage implied |
| Sivra Quell | Light Snare | Gold beam + directional tip | Instant hitscan | Impact + slow diamond |
| Sivra Quell | Sightline Break | Four stronger beams at actual volley intervals | No artificial projectile delay | Each applied volley hit has impact + audio |
| Ione Vaska (Elyra) | Orbit Fracture | Triangle cast + energy line to fixed center | 2.0 radius, 0.45 s warning | Area pulse + target impacts |
| Ione Vaska | Invert Field | Triangle cast + energy line | 2.6 radius, 0.55 s warning | Impacts + slow diamonds |
| Ione Vaska | Phase Fold | Dash route + following head | Existing movement only | No damage implied |
| Ione Vaska | Impossible Center | Larger triangle + directed energy | 3.5 radius, 0.85 s warning | Strong impacts + slow diamonds |
| Tahla Venn (Mira) | Resonant Seed | Green energy beam + tip | Instant hitscan | Impact + slow diamond |
| Tahla Venn | Growth Veil | Explicit friendly energy link | Following heal circle and plus | 0.45 s pulse |
| Tahla Venn | Venn Shelter | Friendly energy link | Following blue shield hexagon | Tracks actual shield depletion, max 4 s |
| Tahla Venn | Wildsurge | Larger support glyph | Friendly-colored 5.0 radius, 0.4 s preparation | Heal pulses + shield hexagons on affected allies |
| Brakk Orun (Rook) | Wreck Swing | Wide heavy orange arc | Instant | Impact + slow diamond |
| Brakk Orun | Armor Grip | Heavy caster glyph | Self heal pulse + shield hexagon | Tracks actual 3 s shield |
| Brakk Orun | Wreckstorm | Heavy cast glyph | 2.8 radius, 0.35 s warning | Area pulse + impacts |
| Brakk Orun | Orun Overdrive | Larger heavy glyph | 3.2 radius, 0.6 s warning | Strong impacts + actual self shield |

The existing ranged skills resolve as instantaneous hits or fixed-center delayed areas, not physical projectiles. Beams therefore show the actual event immediately; cosmetic flight after already-applied damage would misrepresent the mechanic. The dash head follows actual locomotion. No persistent damaging zone was invented for one-shot AoEs.

## Own-hero feedback and overload controls

The existing short material reaction and impact pool remain. Actual damage/absorption additionally draws a small ground-facing directional chevron on heroes with HeroRespawn; no fullscreen flash or camera shake was added. Support effects use different geometry from damage. Debuff pulses are limited to hero targets. Basic attacks retain their existing shape and size.

Skill strokes now reuse a bounded pool: 128 maximum LineRenderers, of which the last 32 slots are unavailable to ordinary impacts/basic attacks and remain reserved for warnings. At saturation optional strokes are dropped. There is one shared material, no lights, no added particles, no screen-filling translucent surfaces. Geometry buffers are small (maximum 40 points per current call); following shields check the actual Health shield and release on death/depletion. Pooling reduces repeated GameObject construction; it is not an allocation-free implementation (short point arrays are still constructed). Actual frame time and draw calls require device profiling.

## Audio

12 cues were rebuilt: Attack, AbilityCast, AbilityImpact, HitLight, HitHeavy, TowerShot, TowerImpact, TowerTarget, UiConfirm, UiBack, Heal, Shield. Bright laser/computer sources were replaced with short impact, thruster and low-frequency energy textures from the existing licensed pack. Controlled 2.2–2.8 kHz low-pass, broad presence reduction and soft onset/tail. Final ordinary combat RMS is approximately -26 dBFS; warning target cue -23 dBFS; quiet UI/heal around -29 dBFS, before runtime volume controls. Full provenance and parameters: ThirdPartyAudioLicenses.md. The prior voice priorities/cooldowns remain.

All 12 final clips decode; measured peaks remain below -5 dBFS after Vorbis encoding and signals are non-silent. These measurements do not establish subjective comfort; listen on a phone through sustained combat before accepting the mix.

## Lunar density

Eight authored pockets add 48 low, partially buried rock/slab fragments and eight dust scars. Interior pockets at (-24,-8), (-16,7), (24,8), (17,-7) stay low; outer pockets at (-29,26), (-15,-27), (28,-26), (16,28) carry more relief. Asymmetric offsets and rotated cluster compositions retain negative space around lane, core and center-objective corridors.

Twelve additional impact bowls, including small overlapping impacts, join the existing baked regolith field with existing rim/ejecta erosion variation. Existing terrain tessellation stays unchanged. No Earth grass or extra vegetation was added; neutral rock/crater detail supplies the requested density without another material system.

Additional static geometry: 48 existing 54-triangle rocks plus eight two-triangle scars = 2,608 triangles before batching. Existing shared materials and spatial batching reused. No added per-frame map logic, lights, particles, colliders or textures. Craters add work to initial surface baking, not an Update loop.

## Checks and remaining acceptance

- 205 C# files parsed without syntax errors; not a Unity compiler result.
- All 24 ability specs and gameplay-critical invocation arguments checked against the preceding commit.
- Modified audio decoded and measured; music unchanged.
- Diff/whitespace and remote hash checks performed at publish time.
- Still required in Unity: all 24 abilities from the gameplay camera, moving-target warning, interrupted cast, shield depletion, dash obstruction, crowded 5v5, pool saturation, scene restart, sustained phone audio and performance capture.
