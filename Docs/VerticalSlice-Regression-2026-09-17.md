# DIMENSIONS FRONT — regression pass

Base commit: 1542e254d916ff9cd12cf4709dd56c871b066bf6
Scope: respawn state preservation, bot recovery, planet attacks, 8:30 objective, menu layout, visible hit feedback.

## Source and mathematical checks executed
- PASS: Third spawn at 510 seconds
- PASS: Warning and spawn each guarded once
- PASS: 8:15 warning and exactly one 8:30 spawn in boundary simulation
- PASS: Bot recovery cannot be overridden by planet rotation
- PASS: Team 0 recovery lies inside healing radius
- PASS: Team 1 recovery lies inside healing radius
- PASS: Player respawn restores captured visibility
- PASS: Bot respawn preserves decorative collider state
- PASS: Telegraph radius is world-space damage radius
- PASS: Preview clears purchase row
- PASS: Shop footer fits panel
- PASS: Menu fits 1920x1080
- PASS: Menu fits 2400x1080
- PASS: Menu fits 1280x720
- PASS: Menu fits 800x600
- PASS: Characters/HeroRespawn.cs balanced braces
- PASS: AI/MobaBotRespawn.cs balanced braces
- PASS: AI/BotLanePlan.cs balanced braces
- PASS: AI/MobaBotMover.cs balanced braces
- PASS: Match/PlanetPulseAttack.cs balanced braces
- PASS: Match/DimensionPlanetDirector.cs balanced braces
- PASS: UI/MainMenuController.cs balanced braces
- PASS: Combat/CombatFeedback.cs balanced braces
- PASS: Match/DimensionPlanet.cs balanced braces

These checks execute source assertions and numerical boundary/layout simulations. They do not compile C# or run Unity.

## Required Unity / Android checks (not executed in this environment)
- Enter a match with every hero; confirm selected identity, ability kit, and visible model agree.
- Kill and respawn the player twice. Root capsule must stay hidden; visible hero parts must return.
- Kill and respawn bots twice. Confirm movement resumes and decorative colliders remain disabled.
- Damage a bot below retreat threshold; it must reach its team's healing zone, heal above 80%, then return to its lane.
- Observe opposing minion waves through first contact and at towers; both teams must damage each other without ignoring nearby targets.
- Stand just inside and outside the planet warning boundary. Damage must apply only inside, after 1.15 seconds; pause during warning and destroy the planet during warning.
- Verify objective announcements at 1:45, 4:45, 8:15 and spawns at 2:00, 5:00, 8:30. Third spawn is Flux, 460 HP, existing 60-second team buff.
- Third planet appears at (0, 1.25, 10), separate from earlier uncaptured planets.
- Check shop preview, price and purchase buttons at 16:9 and wide Android aspect ratios, including safe-area insets.
- Repeatedly damage a hero; visible armor should flash and restore its previous colors without changing the gameplay transform scale.
- Build Android and test touch controls, frame time, readability, suspend/resume, and thermal behavior.

Known limitations: menu currently uses scaled IMGUI, not a completed production Android UI. No Unity editor, C# compiler, device runner, or shell is available through this session's exposed tools. Playability and release readiness remain unverified.

## Arena presentation changes
- Replace noisy lane markings with segmented, shaded paving and subdued team borders (ten shared-material strip meshes).
- Remove static luminous fracture stripes that competed with the animated front and attack telegraphs.
- Remove center-lane decorations from the two-lane arena.
- Raise camera from (0,8,-6) to (0,12,-9) for more encounter visibility.
- Lighten hero armor so its silhouette separates from the floor.
- These graphics changes are implemented but have not been visually inspected in Unity. Art quality, mobile GPU cost, and camera framing require a playtest.

## Follow-up changes from player feedback
- Hero geometry now uses generated tapered and chamfered faceted meshes instead of capsule/sphere primitives. This is a procedural art iteration, not final character art.
- Minions explicitly select opposing minions before structures or heroes and keep that opponent while it remains valid. Hits now produce a visible tracer.
- HUD buttons name their implemented action (target hit, heal, dash, evade), show keyboard bindings, and report failed casts rather than silently doing nothing.
- The combat HUD now restores GUI.matrix after drawing so other HUD panels are not scaled twice.
- A Unity-only live regression probe is available at DIMENSIONS FRONT > Validation > Run Minion Combat Probe. It creates two isolated opposing minions, waits four game seconds, and reports whether both lost health. It has NOT been executed here.
- Ability aiming, accurate shape previews, individualized skill icons and full character art remain unfinished. Current skills still use the existing auto-target implementation.
