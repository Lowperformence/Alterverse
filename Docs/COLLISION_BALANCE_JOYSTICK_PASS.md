# Collision minimization, delayed attacks and mobile joystick

## Status

Implemented from 2becf10286d17b41c00623ed5f67d7734ea1db30 on dev/vertical-slice. Unity Editor, C# compiler and Android runtime are unavailable here.

**Compile successful: NO — not executed (not a reported compiler failure). Unity runtime tested: NO.** C# syntax parsing and whitespace checks passed; that is not Unity compilation. No measured FPS, hit-rate, collider-count or recovery-rate improvement is claimed.

## Collision audit and changes

- Audited the shared Moon/Mars/Green ground builders and all repository Visuals C# files. Production ground/rocks/craters/slabs/minerals use generated meshes without colliders. Tower/core shells likewise use render meshes. The repository SampleScene has no serialized colliders; no prefab files were present in the tree. A user's uncommitted scene is outside this audit.
- Many primitive visual builders already disabled colliders. These components are now disabled immediately AND destroyed: atmosphere, sky/background, legacy tower shells, architecture, landmarks, green/mars mech parts, guardian/tank/wave-unit parts, presence motes, prototype effects and recall fragments. Wing nodes and lane cleanup now disable before deferred destruction. Art remains intact.
- Hero production geometry, armour, weapons and wings stay collider-free. All gameplay units retain their single root query hitbox; player retains CharacterController.
- Added DF_Units (8), DF_Structures (9), DF_Ground (10), DF_Decorative (11). Units↔Units contacts are disabled for BOTH teams: heroes, bots, minions, siege minions, healing guardians and Hot Zone/Mars/Xeno tanks. This deliberately removes enemy as well as allied body blocking. Root colliders remain enabled for targeting, AoE and hits. Existing bot separation stays active.
- Decorative layer contacts are disabled against every layer; authored visual builders remove their colliders rather than relying on this layer alone. Movement/dash casts query Default + Structures + Ground, excluding units, decoration and previews. Default remains for explicitly authored walls/legacy obstacles. Sight queries exclude moving crowds; static walls/structures still occlude bots.
- Towers remain solid, radius .70 and height 2.3 unchanged; no platform/ring collider. Guardians keep .72 local radius at scale 1.02, siege minions .72 at scale 1.05; Hot Zone titan retains its root primitive size (max planar radius .675), now all nonblocking to units. No broad invisible replacement volumes.
- Reality cores previously had NO root collider in bootstrap, which excludes them from collider-based AoE queries. Added one central capsule per core, radius .90, height 2.6, centre y=1.3. Cores are intentionally solid and queryable; surrounding art remains walkable.
- Two adjacent floor boxes became one equivalent 84×.2×72 floor box. Existing movement bounds x±40/z±33 and dash clamps remain. The invisible floor corresponds to the rendered playable ground and prevents falling.
- No projectile collision model rewrite: HeroAttackDelivery is timed target delivery, not a physical projectile. Removing decorative colliders does not change those trajectories; movement/sight and AoE masks now expressly exclude decoration/floor as appropriate.
- Important count distinction: removing ALREADY DISABLED components reduces stored components, not active colliders. The floor saves one active shape; core correctness adds two. Mobile-unit collision PAIRS are strongly reduced; a net reduction of active shape count is not claimed. Use the new inventory in a live match for actual totals and largest shapes.

## Attack diagnosis and limits

A moving hero travels 2.66 units at 5.9 m/s during Ione Q's old .45 s warning; its radius is only 2.0. Ione ultimate's old .85 s permits 5.02 units versus radius 3.5. Targeted area casts used a frozen unled current position. Melee attacks required exact range again after every windup, even for tiny boundary crossings. These explain reliability problems without requiring damage inflation.

Targeted AoE now freezes a modest predicted point when the warning begins. Prediction uses current observed motion, clamps speed to 8, lead time to .24 s and displacement to 1.2 units, then clamps to cast range/arena bounds. No tracking of the warning after cast; a reversal/dash can still evade it. Self-centred areas remain fixed at cast position.

Bots estimate velocity from successive local visible observations of the SAME selected target; loss/reacquisition resets it. No input reading, no hidden live velocity. Easy quality .05, Medium .35, Hard .65, Expert .80 (fraction of windup before the .24 s cap). Player auto-targeted AoE uses .55 and a fresh obstruction check before reading existing movement velocity. Missing samples use zero lead. Hard/Expert heavy AoE ultimates additionally prefer steady observed motion, visible control, grouped enemies or a finishing opportunity; existing reaction/mistake gates remain.

Melee basics and short targeted non-AoE attacks gain .25 units of resolution grace ONLY after an in-range start. Zhaev's separately telegraphed ultimate gets .30 units after its valid cast. Large escapes still fail. Ranged attacks keep their original range checks; projectile speeds and travel caps are unchanged. No radius, damage, HP, movement speed, attack interval, cooldown, heal, shield or CC-number buffs.

## Per-hero base stats (unchanged)

| Hero | HP | Basic damage | Interval s | Range | Move speed |
|---|---:|---:|---:|---:|---:|
| Zhaev Null | 200 | 24 | 0.85 | 2.25 | 7.1 |
| Auren Kest | 380 | 14 | 1.05 | 2.3 | 5.6 |
| Sivra Quell | 160 | 22 | 0.62 | 6.8 | 6.2 |
| Ione Vaska | 190 | 15 | 1.1 | 6.1 | 5.9 |
| Tahla Venn | 230 | 16 | 1 | 6 | 6.1 |
| Brakk Orun | 300 | 24 | 0.9 | 2.5 | 6.15 |

## Every attack: old → new

Power means damage except support/heal/shield skills. All listed power, cooldown, range and radius values are unchanged. Timing is code-derived, not measured in Unity. A delivery windup and AbilitySpec.Windup are alternative execution paths, not additive delays.

| Hero / attack | Power | CD / interval s | Range | Radius | Windup old → new s | Flight / effective hit time | Reason / unchanged behavior |
|---|---:|---:|---:|---:|---|---|---|
| Zhaev Null — Basic | 24 | 0.85 | 2.25 | 0 | 0.10 → 0.09 | 0; hit 0.09 s | Shorter melee preparation; .25 range grace |
| Zhaev Null — Skill 1: Null Cut | 38 | 7 | 3.1 | 0 | 0.14 → 0.13 | No separate flight; hit 0.13 s | Shorter warning; .25 range grace |
| Zhaev Null — Skill 2: Rift Scar | 28 | 10 | 3.3 | 0 | 0.14 → 0.13 | No separate flight; hit 0.13 s | Shorter warning; .25 range grace |
| Zhaev Null — Skill 3: Gapfold | 0 | 12 | 4.2 | 0 | 0.00 → 0.00 | Instant utility; dash traversal .16 s | Unchanged |
| Zhaev Null — Ultimate: Vacant Echo | 58 | 48 | 3.5 | 0 | 0.40 → 0.32 | No separate flight; hit 0.32 s | Shorter warning; .30 range grace |
| Auren Kest — Basic | 14 | 1.05 | 2.3 | 0 | 0.20 → 0.16 | 0; hit 0.16 s | Shorter melee preparation; .25 range grace |
| Auren Kest — Skill 1: Anchor Strike | 24 | 9 | 3.4 | 0 | 0.24 → 0.20 | No separate flight; hit 0.20 s | Shorter warning; .25 range grace |
| Auren Kest — Skill 2: Lumen Wall | 75 | 14 | 0 | 0 | 0.00 → 0.00 | Instant utility | Unchanged |
| Auren Kest — Skill 3: Gravity Anchor | 20 | 12 | 5 | 2.6 | 0.50 → 0.45 | No separate flight; hit 0.45 s | Shorter warning; capped lead |
| Auren Kest — Ultimate: Kest Citadel | 38 | 55 | 0 | 3.8 | 0.65 → 0.55 | No separate flight; hit 0.55 s | Shorter warning |
| Sivra Quell — Basic | 22 | 0.62 | 6.8 | 0 | 0.08 → 0.08 | .07–.32 s; hit 0.15–0.40 s | Unchanged |
| Sivra Quell — Skill 1: Prism Shot | 32 | 7 | 7.8 | 0 | 0.12 → 0.12 | Flight .07–.32 s; hit 0.19–0.44 s | Unchanged |
| Sivra Quell — Skill 2: Vector Shift | 0 | 14 | 3 | 0 | 0.00 → 0.00 | Instant utility; dash traversal .16 s | Unchanged |
| Sivra Quell — Skill 3: Light Snare | 20 | 12 | 6.8 | 0 | 0.12 → 0.12 | Flight .07–.32 s; hit 0.19–0.44 s | Unchanged |
| Sivra Quell — Ultimate: Sightline Break | 64 | 50 | 7.8 | 0 | 0.22 → 0.22 | 4 shots at .18 s cadence; first .29–.54 s, last .83–1.08 s | Unchanged |
| Ione Vaska — Basic | 15 | 1.1 | 6.1 | 0 | 0.14 → 0.14 | .07–.32 s; hit 0.21–0.46 s | Unchanged |
| Ione Vaska — Skill 1: Orbit Fracture | 45 | 8 | 7.3 | 2 | 0.45 → 0.38 | No separate flight; hit 0.38 s | Shorter warning; capped lead |
| Ione Vaska — Skill 2: Invert Field | 26 | 12 | 6.5 | 2.6 | 0.55 → 0.46 | No separate flight; hit 0.46 s | Shorter warning; capped lead |
| Ione Vaska — Skill 3: Phase Fold | 0 | 16 | 1.8 | 0 | 0.00 → 0.00 | Instant utility; dash traversal .16 s | Unchanged |
| Ione Vaska — Ultimate: Impossible Center | 72 | 58 | 7 | 3.5 | 0.85 → 0.68 | No separate flight; hit 0.68 s | Shorter warning; capped lead |
| Tahla Venn — Basic | 16 | 1 | 6 | 0 | 0.12 → 0.12 | .07–.32 s; hit 0.19–0.44 s | Unchanged |
| Tahla Venn — Skill 1: Resonant Seed | 18 | 7 | 6.5 | 0 | 0.16 → 0.16 | Flight .07–.32 s; hit 0.23–0.48 s | Unchanged |
| Tahla Venn — Skill 2: Growth Veil | 42 | 9 | 6.5 | 0 | 0.00 → 0.00 | Instant utility | Unchanged |
| Tahla Venn — Skill 3: Venn Shelter | 50 | 13 | 6.5 | 0 | 0.00 → 0.00 | Instant utility | Unchanged |
| Tahla Venn — Ultimate: Wildsurge | 58 | 58 | 0 | 5 | 0.40 → 0.40 | No separate flight; hit 0.40 s | Unchanged |
| Brakk Orun — Basic | 24 | 0.9 | 2.5 | 0 | 0.16 → 0.13 | 0; hit 0.13 s | Shorter melee preparation; .25 range grace |
| Brakk Orun — Skill 1: Wreck Swing | 34 | 7 | 2.9 | 0 | 0.20 → 0.17 | No separate flight; hit 0.17 s | Shorter warning; .25 range grace |
| Brakk Orun — Skill 2: Armor Grip | 30 | 13 | 0 | 0 | 0.00 → 0.00 | Instant utility | Unchanged |
| Brakk Orun — Skill 3: Wreckstorm | 28 | 10 | 0 | 2.8 | 0.35 → 0.30 | No separate flight; hit 0.30 s | Shorter warning |
| Brakk Orun — Ultimate: Orun Overdrive | 48 | 52 | 0 | 3.2 | 0.60 → 0.50 | No separate flight; hit 0.50 s | Shorter warning |

Projectile speeds: Sivra 38, Ione 23, Tahla 27 units/s. Actual flight is clamp(distance/speed, .07, .32); ranges/flight limits unchanged. Ione AoE spell animation shares the warning duration and does NOT add a second projectile delay.

Role effects remain: Zhaev W slow .8×/1.2 s and missing-health ultimate modifier; Auren Q stun .55 s, normal slow .65×/1.5 s, ultimate .5×/2 s, W shield75/4 s and ultimate allied shield60/3 s; Sivra snare .7×/1.8 s; Ione W/ultimate slows .65×/.55× for2 s; Tahla Q slow .75×/1.5 s, W heal42, E shield50/4 s, ultimate heal58+shield25/3 s; Brakk Q slow .8×/1 s, W heal30+shield20/3 s, ultimate shield65/4 s and low-HP basic sustain. Mobility distances and all passives unchanged. No claim of measured role win-rate balance.

## Joystick exact changes

| Item | Old | New |
|---|---|---|
| Idle centre in safe-area logical coordinates | (104, height−104) | (164, height−124) |
| Outer render radius | 58 (~56.8 visible) | 74 (~72.8 visible) |
| Thumb render radius | 20 (~19.2 visible) | 26 (~25.2 visible) |
| Left idle edge margin | ~47 | ~91 UI units |
| Input travel radius | 64 | 74 |
| MoveCapture | min(width×.36,300) ×220 | min(width×.38,360) ×260 |
| Cached circle textures | 192 /96 | 256 /128 |
| Idle / active alpha | .22 /.36 | .22 /.36 |
| Normalized deadzone | .08 | .08 |

Changed PrototypeHUD.cs and CombatHudLayout.cs. Floating touch origin, finger IDs, separate combat capture and two-pass movement-before-ability processing remain intact. Thumb initializes at its centre on touch begin (prevents a stale first-frame position). At travel74, normalized input reaches1; input speed/acceleration/camera-relative motor unchanged. At full input the thumb centre reaches the outer radius; the thumb overlaps the rim by design.

The outer ring is a single cached analytic anti-aliased texture, not line segments. Thumb is the second texture. No extra GUI layers, glow, shader, material, render texture or GameObject. Actual rendered smoothness on an Android display remains unverified.

## Checks provided / executed

- Source inspection: all six kits/profiles, delivery/execution/bot casting, shared movement and all Visuals builders; repository scene collider inventory.
- Executed here: C# syntax parsing; git diff --check; serialized layer-matrix symmetry/contact assertions; safe-area/layout math for16:9,18:9,19.5:9,20:9 at360/540/1080 heights with insets; input end-point/deadzone arithmetic.
- Added Unity menu `Checks/Collision inventory (Play mode)` reports active/disabled/mesh/unit-root/child collider counts and12 largest shapes.
- Added `Checks/Unit pass-through and solid wall (Play mode)` checks CharacterController crossing a unit and stopping at a structure while query hitboxes remain present.
- Added `Checks/Joystick geometry and limited prediction` checks aspect ratios, insets, capture/button separation and stationary/slow/normal/fast/reversing prediction bounds.
- Extended `Checks/Attack delivery (Play mode)` with melee grace and invalid-start rejection for melee heroes; existing six-hero death/stun/disable/range/damage gates retained.
- NONE of those Unity menu checks were executed here. Compile and runtime remain outstanding.
- Still required in Unity/device: player+all9 bots base exit, guardians/towers, crowding, decor/dash traversal, minion waves,5v5,Hot Zone,planets,respawn/base defence; each hero vs stationary/slow/normal/lateral/reversing/slowed/stunned targets;1v1/2v2/5v5 hit rates; all3 arenas; real simultaneous move+ability touches, readability and FPS.

Save/economy/shop/inventory/rewards/wing bonus code is untouched. No map art removed. No measured claim that stuck recovery is now less frequent; the known unit-blocking cause is removed, fixed structures still use existing recovery.
