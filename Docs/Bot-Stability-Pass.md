# DIMENSIONS FRONT — bot stability pass

## Repaired causes

- Lane movement used a time-based PingPong destination. Routes now advance toward the surviving enemy bastion on the assigned lane, then the other bastion, then the vulnerable core. Returning from the healing zone first rejoins the assigned lane.
- A separate combat-health threshold made moderately wounded bots advance into enemies while refusing to attack. Bots now either fight or enter retreat; retreat remains latched until 80% HP.
- Target scans discarded their previous decision every 0.22 seconds. The current valid target now receives a 35% score discount; switching still occurs for a substantially better enemy, death or range exit. Dead, disabled and invulnerable objectives are rejected.
- Respawn refreshes late-added dependencies and clears the target, route and movement velocity. Arrival stops velocity; direction reversal removes residual backwards movement and displacement is capped to the stopping distance.
- Same-lane bots no longer spawn at identical coordinates.
- Bot utilities now dispatch by hero AND slot. Rook's zero-range W can heal without an enemy; Kaelon/Rook/Nyra E and Elyra W deal damage. Support healing requires a wounded ally within range; its ultimate heals rather than attacking an enemy. Defensive utilities can run during retreat. Movement abilities clear stale movement and produce visible cast effects.
- Offensive bot skill power and cooldown buffs now follow the current player kit values (removes the old hidden 0.82 damage multiplier). This needs match-balance validation.
- Minions now reject cached friendly, disabled or invulnerable targets before attacking.

## Verification status

All locally inspected C# files passed a tree-sitter C# syntax parse. This checks syntax only, not Unity API compatibility or compilation.

Added `DIMENSIONS FRONT > Checks > Bot and minion regression (Play mode)` to the Unity Editor. Run in a disposable offline match; it creates temporary units, asserts state transitions, target retention, zero-range healing, offensive ability slots, ranged support healing, reciprocal minion damage and pause handling, then removes its units. Temporary skill VFX expire normally. It can affect match telemetry and is not intended for a recorded playtest. The checker throws on a failure and logs success only after every assertion passes.

**The Unity regression runner, actual respawn coroutine, Android build and device playtests have NOT been executed here.** No Unity editor or C# compiler is installed in the execution environment. This pass must not be presented as a verified bug-free build.

## Required in-engine follow-up

1. Run the Editor regression command in Play mode; inspect console for compilation/runtime failures.
2. Observe both teams after at least three deaths each; confirm movement resumes, placeholder renderers stay hidden and damaged bots return to the healing zone, then leave it.
3. Observe both minion lanes for three waves. Check reciprocal damage and tower/core progression.
4. Check that target retention and dash decisions do not cause bad pursuit; compare skill effects for all six heroes.
5. Check full match balance after restoring player-equivalent bot skill power.

Still outstanding: obstacle-aware movement and crowd separation, tactical tower-risk evaluation, shared player/bot ability execution, allocation reduction in scene scans, readable directional skill aiming, production art and Android frame-time profiling. This is a focused repair of existing systems, not release readiness.
