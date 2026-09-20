# Bot Difficulty Overhaul 2.0

## Status / validation

Implementation ready for Unity validation; gameplay acceptance is NOT established.
Unity was not executed. No Unity Editor, dotnet, csc or mcs is available in this environment. C# syntax parsing is not a semantic compile check. No Play-mode assertions, full 5v5 matches, Android profiling or 60 FPS acceptance have been run.

## Findings and changes

1. **Why Expert could feel easy:** code inspection found largely independent decisions despite high profile values, distance-led target scores that could favor nearby minions, HP-led retreat, and no shared plan to turn a fight advantage into objectives. These are architectural findings, not a measured explanation of the reported match loss.
2. **Decisions:** Hard/Expert assess local strength from own living heroes, roles, HP, own Q/ultimate readiness, observed opponents, nearby waves and tower exposure. Full-health 1v3 can disengage; a 35%-HP bot with allies can finish an isolated sub-30% enemy.
3. **Coordination:** one fixed-buffer coordinator per team, ticking every 0.60 s on Hard / 0.35 s on Expert. Group, push, defend, contest/take objective, teamfight, disengage, siege and collapse plans hold about 2–2.5 s. Emergency defense and retreat override holds. Each bot reacts after its own delayed call gate.
4. **Focus:** visible sensor observations are merged; isolated, wounded and backline-threatening heroes receive priority. Calls last 1.5 s on Hard / 2.2 s on Expert before reevaluation. Local range, sight, reaction, chase suppression and tower protection still apply. Hidden movement and enemy cooldowns are not queried by the coordinator.
5. **Regroup:** two or more accumulated allied deaths trigger a 5 s regroup window; two survivors with multiple dead allies stay defensive. Respawn clears stale movement/target state and rejoins the current plan. Retreat may route toward safer allies; low-health healing retains the existing recovery path.
6. **Roles:** tank leads, bruiser offsets beside it, ranged roles stay behind nearby frontline, support moves toward wounded allies, assassin approaches from the side and briefly resets after burst. Mage ground casts prefer visible clusters and delay follow-up on observed CC. Existing support execution still chooses wounded allies; it was not rewritten or given stronger healing.
7. **Macro:** observed kills open a 7 s opportunity to take core/tower objectives. Losing sight alone is not a victory. Vulnerable own core takes priority; visible pressure calls all available defenders, otherwise the two nearest. Threatened towers similarly assign two defenders. Public Hot Zone events and locally observed planets are considered when higher-priority work is absent.
8. **Difficulty:** Easy and Medium keep existing slower, individual decisions and mistakes. Hard adds team planning and ordinarily assigns four of five bots; Expert assigns all five with faster assessment and shorter reactions. Easy/Medium numeric profiles are unchanged. Actual difficulty separation still needs human playtests.
9. **Stat cheats:** none enabled or added. HP, damage, range, speed, cooldowns, player controls and economy are unchanged.
10. **Stat fallback:** no stat assist prepared, because the prerequisite post-overhaul playtests have not happened. Any future adjustment requires their evidence.
11. **Regression:** the existing `DIMENSIONS FRONT/Checks/Bot and minion regression (Play mode)` now includes the team suite. A standalone `Team difficulty regression (Play mode)` menu is also available. Fixtures check all four difficulty gates, healthy 1v3 retreat, wounded 3v1 focus, tank/ranged/support formation, non-stacking, favorable low-HP finish, intent hold, delayed calls, Hard/Expert participation, regroup, respawn, kill-to-tower tempo, core defense, chase reacquisition suppression and wall-hidden targets. Existing watchdog, skirting, abilities, minion, hazard and memory checks remain. These assertions were authored and statically reviewed, NOT executed here. Chase suppression is not a substitute for measuring chase duration in real matches.
12. **Performance:** bounded arrays and existing Health.Active registry; no LINQ in added runtime paths. Team scans run on the tactical interval, event discovery every 2 s. Formation/retreat uses bounded local loops. Editor/development counters expose tactical ticks, focus calls, regroup calls, observed kills and tempo calls through `Print team AI telemetry`; no release log spam. Actual CPU/GC/frame-time impact is unmeasured.
13. **Compile check:** all 22 C# files in the targeted local snapshot parse without tree-sitter syntax errors. `git diff --check` passes. This does not establish Unity API/type compatibility or successful compilation.
14. **Unity not run:** explicitly unverified. Run the regression menu and several full 5v5 games per difficulty before accepting this as balanced or shipping it.

## Required in-editor acceptance

- Compile in Unity 6000.6, enter a disposable Play-mode match and run the combined regression menu.
- Play Easy/Medium/Hard/Expert with the same roster and record early game, teamfights, objective conversion, towers, core defense, respawn and overtime.
- On Hard/Expert repeatedly try unsupported engages, chasing toward allies, ignoring objectives and tower diving without minions. Record whether mistakes are punished without permanent aggression or hidden tracking.
- Observe ranged attack uptime behind tanks, support healing range, assassin reset, multi-target ultimates and CC follow-up delays; tune only from observed failures.
- Verify no base/stuck loops, oscillating group calls or endless pursuit. Check calls when only two heroes remain and when the core is vulnerable without current attackers.
- Profile a full 5v5 Android build for CPU, GC and frame time. The implementation alone does not demonstrate stable 60 FPS.
