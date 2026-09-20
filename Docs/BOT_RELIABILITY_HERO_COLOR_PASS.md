# Bot reliability and hero colour pass

## Code diagnosis

- Base exit had no phase memory: crossing the one-unit lane tolerance switched back to tactical/objective routing. Tactical formation could steer back over that boundary. The screenshot coordinates around x +/-28, z +/-15 match that boundary; reproducing the exact screenshot in Unity remains required.
- HealingGuardMech has a solid capsule (radius .72, scale 1.02), can move within its base leash and directly change position. The mover only skirted tower bodies. The guardian therefore blocked a base route without getting its own local bypass. Its collider, damage, health and behaviour remain intact.
- Recovery selected similar short directions, erased perception and reset the same route repeatedly. Attempts grew without a limit, while .65 seconds of free sideways movement could prematurely clear failure history.
- Big solid targets required a collider stand-off, but basic attack start, release and impact all checked distance to their centre. Melee heroes could stand outside the body yet fail every attack check.
- Formation offsets could park combatants beyond attack range. Backline/regroup decisions could leave a visible close opponent selected while Lane state disabled basic attacks.
- Candidate score changes could continually restart initial target reaction. Fight without target was already bounded by recent memory; the extra empty-target commitment timer was unnecessary.

## Changes

- BotLanePlan.cs: latched alignment then forward exit; reset with the existing respawn route reset. MobaBotMover uses that exit until past the base gate, preserving explicit defence/hazard priorities.
- HeroLocomotion.cs: allocation-free bounds-corner waypoint probes using the movement capsule; strict full-path clearance and escape angles through 180 degrees. Existing hero/minion crowd exclusion remains; walls, towers and guardians stay solid. No teleport or per-frame scene search.
- MobaBotMover.cs: local waypoints, alternating sides, wider clearance and periodic backward/local escape; stage capped at six. Failed probes wait rather than repeatedly drive into a known blocked direction. Goal progress plus net displacement distinguish oscillation from bypass movement; navigation recovery no longer clears a visible target. Stand-off clears tower steering margin; combat formation stays within range including stop tolerance. Dev-only logs include goal, distance, blocker, stage and mode and suppress repeated identical stages.
- BotTargetSensor.cs: stable visible candidate during reaction; existing visibility, local radius, memory and disengage rules retained.
- MobaBotBrain.cs: no empty-target commitment after observation memory expires. Existing respawn clears perception, chase, movement, recovery, tactical reset and route; new recovery fields reset there too.
- MobaBotCombat.cs: visible in-range basic attacks also work while Lane movement regroups; retreat/recovery still suppress attacks. Reaction, cooldown and line-of-sight gates retained.
- HeroCombatRules.cs / HeroAttackDelivery.cs: shared surface-distance rule for basic attacks against solid non-hero/non-minion targets, including release and impact. Hero/minion centre ranges and ability ranges remain unchanged. This is a deliberate basic-attack geometry fix, not a stat increase.

## Hero visuals

Production armour was mostly neutral grey, non-tank trim blended 65% toward dark grey and coat colour 72% toward dark cloth. HeroSurface added an unbounded scene-light term and a white specular component. These are code-level contributors to low identity/washed highlights; exact appearance has not been measured on device. Showcase has isolated point lights, but this custom shader reads main light and SH, not those additional lights; their intensity alone cannot explain the difference.

- StylizedHeroVisual.Production.cs: coloured breastplate and shoulder surfaces, clearer trim and cloth; skin, dark joints, metal weapons and energy retain separate colours.
- Resources/HeroArt/HeroSurface.shader: bounded hero-only illumination, lower neutral specular contribution and a shadow-side colour floor. Existing energy and hit-flash controls retained; no new shader or lights.
- Both menu and match use the same model/material pipeline. Whether they now visually match more closely requires Unity rendering.
- Voidfall recolouring, wings, weapon overdrive and loadout selection paths remain unchanged. Cosmetic runtime rendering is NOT verified.
- No UI, joystick, global lighting, map, damage, health, movement speed, cooldown or hitbox edits.

## Validation and limits

- C# syntax parse: all 20 inspected C# files passed. git diff --check passed.
- MovementRegression now includes both teams/both lanes, lane-edge exit latch, respawn alignment reset, a guardian-sized capsule bypass checked against actual swept movement, solid basic range and unchanged hero centre range. Run DIMENSIONS FRONT > Checks > Movement collision and dash (Play mode).
- These Unity checks were added but NOT executed. Unity Editor/C# compiler/shader compiler are unavailable in this workspace. No Unity compile, runtime, visual, FPS or Android validation is claimed.
- Matchstart, all bots exiting, moving guardian, bastions, minions, heroes, melee/ranged fights, crowding, Hot Zone, planets, retreat, respawn, base defence and teamfight still require an actual match run. The geometry fixture does not substitute for those scenarios.
- All six heroes on Moon/Mars/Green Planet, 5v5 readability and Original/Voidfall/Plague/Beta/weapon cosmetics still require visual checks at gameplay camera distance.
- Arbitrarily enclosed geometry or a moving collider overlapping a hero is not guaranteed solvable by local steering; no positional teleport fallback was introduced. Capture the new diagnostic if a persistent block remains.
