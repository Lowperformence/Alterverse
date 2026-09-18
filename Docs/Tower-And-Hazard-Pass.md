# DIMENSIONS FRONT — tower pressure and hazard decisions

## Changes

- Bastions prioritize enemy minions, then heroes, with target retention within each group. This gives a pushing wave a meaningful purpose. This slice does not yet have hero-provocation aggro rules.
- Tower target acquisition uses exact planar distance and unit health components, not a fixed 32-collider buffer. Armor colliders no longer change target availability. Scans are throttled to 0.2 seconds, including when no targets are present.
- Bots respect an enemy tower's range plus a 0.75 m safety margin. They enter while that tower is targeting a living allied minion, withdraw when cover disappears, and may still attack from safe range. This conservative policy intentionally avoids unsupported melee dives.
- Entire movement segments and offensive dashes are checked against tower danger circles. A long frame cannot skip the boundary check.
- Bot separation includes an explicit opposite-direction solution for exact overlaps; it is bounded by movement speed. It currently separates bots from bots, not minions or the human player.
- Planets expose their existing warning state and exact blast radius. Bots select an escape point beyond an active warning; dashes cannot move deeper into that warning.
- The existing modest 7-damage pulse, 1.15-second warning, and third objective spawn at 510 seconds (8:30) are retained.

## Verification

- C# syntax parse: passed for all 14 local inspected C# files.
- Numerical segment/circle geometry check: 10,000 deterministic random cases; no sampled unsafe crossing was accepted. This is a formula check, not execution of the Unity component.
- Extended the existing Unity Play-mode regression command to check tower minion priority, dead-cover removal, planar range, long-step/dash restrictions, cover-enabled pushes, pulse escape, and separation of exact overlaps. Existing movers are temporarily disabled to isolate these synchronous checks, then restored. Use a disposable match; movement state and telemetry may change.
- Unity compilation, the Play-mode regression command, full matches, and Android profiling have **not** run here. The editor/compiler is unavailable. No device FPS or visual-quality claims are made.

## Remaining limits

This is local steering, not navigation around solid obstacles. A retreat from overlapping dangers selects a local escape and may still fail; survival is not guaranteed. Bots only react after cached hazard discovery. Scene scans still allocate arrays and require profiling on device. Tower safety must be tested for stalemates, ranged-hero advantages, and minion-cover flicker during actual waves. Full tactical pathfinding, player/minion avoidance and shared skill execution remain future repair work.
