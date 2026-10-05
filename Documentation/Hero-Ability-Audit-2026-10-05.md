# Hero ability audit — 2026-10-05

Source reviewed at `1b5484e18520fc4150f33bb206fec8fe3ca4c541`.

The audit follows all 32 active skills through the shared player/bot execution,
catalog, delayed delivery, target/team validation, cooldowns, death cancellation,
status and movement interfaces. Source review is not a Unity runtime pass.

| Hero | Skill 1 | Skill 2 | Skill 3 | Ultimate | Mechanical passive |
|---|---|---|---|---|---|
| Zhaev / Veyr | Targeted slash | Targeted damage + 20% slow / 1.2s | 4.2m dash + 30 shield / 2.25s | Delayed execute, +0–35% vs wounded heroes | Catalog has no nonzero passive effect |
| Auren / Kaelon | Damage, 35% slow / 1.5s, .55s stun | 75 shield / 4s | Delayed 2.6m enemy area + slow | 3.8m area; 50% slow / 2s, allied hero 60 shield / 3s | Catalog has no nonzero passive effect |
| Sivra / Nyra | Ranged targeted shot | 3m dash | Targeted damage + 30% slow / 1.8s | Four .18s-spaced shots, target/range/stun validation | Catalog has no nonzero passive effect |
| Ione / Elyra | Delayed 2m area | Delayed 2.6m area + 35% slow / 2s | 1.8m dash | Delayed 3.5m area + 45% slow / 2s | Catalog has no nonzero passive effect |
| Tahla / Mira | Targeted damage + 25% slow / 1.5s | Lowest-health nearby hero heal | Nearby hero 50 shield / 4s | Allied hero heal + 25 shield / 3s in 5m | Catalog has no nonzero passive effect |
| Brakk / Rook | Melee damage + 20% slow / 1s | 30 heal + 20 shield / 3s | Delayed 2.8m area | Delayed 3.2m area + self 65 shield / 4s | Hero basic hits heal 12% of HP damage below 60% HP; basic audit covers shared resolution |
| Traveler | Ranged targeted beam | 3.6m dash | Delayed 2.2m area + 28% slow / 1.5s | Delayed 3.2m area + 38% slow / 1.8s | Catalog has no nonzero passive effect |
| Nerok | LOS hook, range revalidation, damage, mobile pull up to 4.2m | 45 shield / 3s + 15% speed / 2s | 2.8m enemy area + 25% slow / 1.2s | Fixed 3.2m field; six half-second damage ticks over 3s and continuous run-speed pull | Successful hero hook adds 12 shield / 2s |

Concrete fixes:

- Permanent hero base shields no longer prevent Auren/Nerok defensive skills or
  Tahla's shield target selection. Only an existing temporary shield blocks those casts.
- Disabled execution and non-authoritative co-op clients cannot execute a skill directly.
  Normal client command forwarding remains in `PlayerHeroAbilities`.
- All shared area checks use the same planar distance as target ranges and ground warnings.
- A brief stun now cancels delayed skills when it begins, even if it expires before the
  windup finishes. Death/disable still cancel; cooldowns remain spent.
- Rejected damage cannot apply a slow or stun in the shared hit resolver.
- Nerok's descriptions now match run-speed pulling, without claiming an obsolete slow.
  Zhaev's delayed hit text reads the configured windup; Tahla's heal text avoids an
  outdated hardcoded amount.

`HeroAbilityExecutionRegression` adds a disposable Play-mode check for all eight
heroes and all four slots, plus cooldown rejection, ally exclusion, intact base shield
utility casts, vertical offsets, short-stun interruption, death cancellation,
damage-gate control suppression and disabled execution. Run via
`DIMENSIONS FRONT/Checks/All hero abilities (Play mode)` in a disposable offline or
host match. These runtime checks were authored but not run here: Unity and a C#
compiler are unavailable in the execution environment. Animation/VFX contact and
online reconciliation still require a real game session.
