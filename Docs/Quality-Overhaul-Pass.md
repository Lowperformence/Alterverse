# DIMENSIONS FRONT — gameplay and presentation quality pass

Baseline: `d4fd49bfee80f84fffcd1964d2ac695e9586ca55`, branch `dev/vertical-slice`.

This is a source audit and implemented quality pass, not a certification of final visual quality. No Unity executable, C# compiler or Android device is available here. The previous release-readiness audit remains applicable.

## Audit and priorities

| Priority | Area / source | Finding and disposition |
| --- | --- | --- |
| CRITICAL | First minute / PlayerHeroAbilities / onboarding | Successful casts never notified onboarding; restored the existing player-only signal. |
| HIGH IMPACT | Touch / PrototypeHUD | Two-step skill dialog, tiny text actions, fixed cooldown denominator and mismatched safe-area drawing. Replaced interaction/drawing while retaining bootstrap API and early input execution order. |
| HIGH IMPACT | Combat / Health / CombatFeedback | Hits created spheres, materials, coroutines and property-block arrays repeatedly; shields did not produce damage feedback. Added explicit HP/absorbed damage event, bounded reusable impact marks and cached flash state. |
| HIGH IMPACT | Minions / bots / SoftTargeting | Idle minions bypassed search interval; repeated global target arrays in common combat paths. Active Health registry plus indexed scans; empty-target scans and lane-route checks limited to the existing 0.2-second cadence. Existing minion-opponent priority, bot scoring/hysteresis and siege priority retained. |
| HIGH IMPACT | HUD / objective events / onboarding | Independent overlays used incompatible coordinates and overlapping top panels. Shared safe-area layout, bottom-center onboarding and one priority slot for siege/planet notices. |
| HIGH IMPACT | Heroes / locomotion / animation | Prior six-role, material/silhouette and distance-gait passes preserved. Procedural articulation still needs in-engine review; it is not authored animation production approval. |
| HIGH IMPACT | AI / navigation / respawn | Previous shared locomotion, respawn, detour and hazard changes preserved. Existing editor bot/movement checks and actual consecutive matches are still required. No claim that bots behave correctly from source inspection alone. |
| HIGH IMPACT | VFX / camera / combat readability | Current pass removes generic per-hit spheres and adds shield-hit distinction and dash direction preview. Skill strokes, telegraphs and camera require runtime review under ten-hero combat. No extra screen shake added. |
| HIGH IMPACT | Main menu / hero selection / profile / cosmetics | Existing frontend, hero showcase, profile journal and shop logic preserved. Avatar art and hero assets still need visual acceptance; no blanket claim that current assets are final. |
| HIGH IMPACT | Android / performance | Earlier signing/ARM64/SDK gate preserved. Selected repeated allocations removed; render/input/thermal budgets remain unmeasured. |
| MEDIUM IMPACT | Towers / cores / Front / match flow | Existing damage, protected-core rules and win conditions unchanged. Match-result and repeated-match tests remain required. |
| MEDIUM IMPACT | Planets / meteor shower / siege | Existing 2:00, 5:30, 8:30 schedule, capture influence and buff values unchanged. Warning hierarchy improved. Telegraph fairness, meteor visuals, mech entrance and sound need actual play review. |
| MEDIUM IMPACT | Map / lunar ground / environment / lighting | Parallel lunar art deliberately preserved. No evidence from this environment justifies replacing that work. Camera-view screenshots/device capture required for lighting and composition approval. |
| MEDIUM IMPACT | Audio / music | Existing synthesized tones and runtime sound routing remain. **FINAL ASSET REQUIRED**: approved impact/cast/objective/victory sound palette and music/ambience where no authored asset exists. Runtime routing is not final audio quality. |
| MEDIUM IMPACT | Progression / replayability | XP 1–1000, no stat advantage, existing shard economy and persistence retained. Hero/team variation and objective decisions are existing replay hooks; match pacing, snowballing and retention need repeated-play evidence, not speculative numerical changes. |
| POLISH | Typography / icons / accents | Neutral surfaces, functional original glyphs, actual skill names, hero accents and gold ultimate distinction. No copied franchise assets. Small-display typography still requires device review. |

## Implemented interaction contract

- Move and fight with separate fingers. Captured touch IDs cannot swap roles mid-gesture.
- Hold an ability to inspect name, effect and range. Release inside its expanded hit region to cast; drag outside and release to cancel. Touch cancellation, missing touch, pause, focus loss, death and safe-area changes clear capture without casting.
- Attacks fire while held, respecting existing attack cooldowns. Skills use existing auto-targeting and cast rules; this is **not** a manual ground-aim redesign.
- Dash preview uses the same movement/auto-evade/facing selection as the cast. It displays intended direction/range, not a guaranteed unobstructed endpoint. The existing swept locomotion resolves collision.
- Actual ability cooldown replaces the old hard-coded 12-second display denominator. Initial ultimate lock remains enforced by existing execution code.
- HP/shield and temporary stunned/slowed state are legible independently of the hero name. Damage trail is visual only. No movement locks, damage, range, cooldown or role values changed.
- The technical `PrototypeHUD` name is retained for compatibility; no new visible product name is introduced.

## Implementation limits

The HUD still uses IMGUI. This pass removes the modal cast interaction and unifies coordinates without replacing working scene/bootstrap wiring. A retained-UI migration is not declared complete. Functional glyphs and opaque impact rays are implemented assets, but final art approval requires seeing the actual game. Other legacy VFX still allocate and are not described as pooled.

The Health registry includes active, enabled Health components; consumers still filter dead/friendly/ineligible targets. It rebuilds once after scene load and removes disabled actors immediately. Target tie order now follows activation order rather than a global enumeration; priorities and score formulas are unchanged. Empty-target acquisition may take up to 0.2 seconds, matching the intended scan cadence. Structure route decisions also refresh at this cadence.

The impact pool warms during hero Start, is parented to the match root and has a hard cap of 24 marks. It reuses one mesh/material and cached property blocks, culls off-screen emissions, replaces the oldest slot when full, and has no colliders, lights or shadows. Hero flash caches can be explicitly refreshed if a later system rebuilds a live hero's renderers. Shared material colors are never mutated by this feedback implementation.

## VERIFIED in this workspace

- Tree-sitter syntax parse: 189 C# files, no syntax errors; **not compilation**.
- `git diff --check` clean.
- Numerical HUD geometry model: five safe areas, 25 action bounds/coordinate round trips, 50 button-pair overlap checks passed. This models the equations; it does not execute Unity input or render text.
- Source checks: selected bot/minion/player acquisition paths no longer call FindObjects to allocate target arrays.
- Twelve protected core files byte-identical to baseline: HeroCombatProfile, HeroAbilityKit, HeroAbilityExecution, HeroLocomotion, HeroMotor, DimensionsFront, DimensionPlanetDirector, PlanetBuffReceiver, LaneWaveSpawner, ProductionLunarGround, StylizedHeroVisual and MobaBotMover.

## Added checks — NOT RUN

Unity menus:

- `DIMENSIONS FRONT/Checks/Combat HUD safe areas`
- `DIMENSIONS FRONT/Checks/Health registry and shield feedback (Play mode)`

The second creates a disposable test actor, verifies shield-only and shield-spillover events, and checks disable/re-enable registry membership without changing the profile. Run existing bot and movement checks as well.

## Runtime acceptance still required

1. Compile in the project's Unity version, launch all six heroes, compare actual skill text/preview/cast behavior. Verify no null kit/camera references during launch/respawn.
2. Android two-finger joystick+attack and joystick+ability; quick tap, long hold, drag-out, pointer cancellation, third finger, focus/background loss, death and rotation/safe-area change during capture. No ghost movement or delayed cast after resume.
3. Inspect the five aspect-ratio layouts on device: skill-name wrapping, cooldown readability, notch clearance, tutorial/planet/siege overlap, result and respawn panels.
4. Verify HP-only, shield-only, spillover and repeated hits restore materials. Restart match repeatedly: no leaked pools/materials or registry entries. Confirm no purple sphere impacts remain on the active hero feedback path.
5. Run a full 10-player match at different frame rates; inspect minion opposition, bot target transitions, structures, planets and siege. Profile allocations, draw calls, steady FPS and thermals. No 60 FPS guarantee has been established.
6. Capture first-minute and final-minute gameplay video. Judge animation, audio, visual hierarchy and pacing from that evidence before calling this production-ready.
