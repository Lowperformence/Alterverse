# Direct ranged attacks: cone delivery

Direct hero attacks now resolve damage through the visible aiming wedge. Projectile
and homing enum values remain presentation styles; they no longer run a damage
flight simulation. Direct beams use the same wedge. Damage is eligible after the
existing windup, with a fixed active interval (0.20 seconds, or 0.12 for a beam).
Body overlap and swept movement use the existing AimGeometry queries.

One direct attack damages one victim. Selection is lexicographic: enemy heroes
before other eligible enemies, lowest remaining HP within that class, then nearest
distance. Cone preview and authoritative damage share that ordering. Automatic
ranged acquisition uses it too; automatic melee commitment is unchanged. Nyra's
three-shot non-AOE volley shares one victim, including when that victim dies.
The next independent attack can reacquire another enemy.

## Audited direct ranged attack classes

| Hero ID | Direct attacks |
| --- | --- |
| Nyra | Basic, Prism Shot, Light Snare, Sightline Break |
| Elyra | Basic |
| Mira | Basic, Resonant Seed |
| Traveler | Basic, Temporal Beam |
| Veyr | Rift Scar |
| Kaelon | Anchor Strike |
| Nerok | Anchor Recall |

Area skills keep their existing circles/sectors, multi-target damage and timings.
Mobility, ally support, melee basic commitment, damage amounts, catalog ranges and
cooldowns are preserved. Existing wall visibility checks still protect covered
bodies. A wall on the cosmetic centre line cannot discard a clear edge of the wedge.
Legacy aiming overrides are normalized so they cannot restore ranged projectile
collision.

The inspected NPC attack paths (HotZoneTank, HealingGuardMech, DefenseBunker,
LaneTower, LaneMinion, TrenchSoldier and PlanetPulseAttack) already apply direct
range/area damage; their projectile visuals do not determine collision.

## Verification

Run:

`python Tools/Aiming/Regression/run_entry.py --dotnet-root /path/to/dotnet --ranged-cones`

The targeted suite compiles production combat sources against deterministic engine
fixtures. It covers all 12 direct ranged classes at 30/60/120 FPS, cone edges,
cosmetic speed independence, late entry, cover, range, ally exclusion, lowest-HP
and class priority, single-victim damage, volley commitment, AOE multi-target
behavior and V6 server command execution for every ranged class.

The existing skill-entry suite is also run without the optional flag. Editor checks
now assert cone contact at release and range-limited ranged damage rather than the
removed projectile-arrival behavior.

These are not native Unity compilation, renderer, Android or live multiplayer
tests. Build matching clients and the dedicated server before device acceptance.
