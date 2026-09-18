# DIMENSIONS FRONT — six-role baseline

## Audit at 3e5a52e

Player ranged basics had a hidden 0.78 multiplier; bots did not. Player attack intervals differed by hero while bots used 1.05 seconds for everyone. Abilities duplicated player/bot implementations, mostly applied single-target damage irrespective of the visual effect, and lacked actual shields/stuns/slows. Several sustain effects made offensive classes also self-heal. Host mesh removal also removed bot hitboxes, breaking physics-based targeting and planet damage detection. Meteor code referenced the absent PrototypeVfx.RadialBurst method.

This pass preserves the 45-second initial ultimate lock, respawn resets, tower safety, lane/minion logic, 8:30 objective timing, planet buffs including +30% Flux minion damage, meteor behavior, saved player progression and rewards. Hitboxes return as invisible CapsuleCollider components, not primitive renderers.

## Numerical baseline (no planet buffs)

| Hero | HP | Speed m/s | Basic damage | Interval s | Basic DPS | Range m |
|---|---:|---:|---:|---:|---:|---:|
| Zhaev Null | 200 | 7.10 | 24 | .85 | 28.24 | 2.25 |
| Auren Kest | 380 | 5.60 | 14 | 1.05 | 13.33 | 2.30 |
| Sivra Quell | 180 | 6.20 | 22 | .62 | 35.48 | 6.80 |
| Ione Vaska | 190 | 5.90 | 15 | 1.10 | 13.64 | 6.10 |
| Tahla Venn | 230 | 6.10 | 12 | 1.00 | 12.00 | 6.00 |
| Brakk Orun | 300 | 6.15 | 24 | .90 | 26.67 | 2.50 |

Both control paths use the same numbers and the same damage/ability implementation. Flux modifies cooldowns and minion damage equally. AbilitySpec.Power is the actual baseline value; no hidden AbilityPower multiplier. No account-level combat advantages are introduced.

## Kits, strengths and costs

| Hero | Q | W | E | Ultimate |
|---|---|---|---|---|
| Zhaev | 38 single / 7s CD | 28 single, 20% slow for 1.2s / 10s | 4.2m dash / 12s | 58 + up to 35% missing-health bonus against heroes, 0.4s windup / 48s |
| Auren | 24 single, .55s stun / 9s | 75 shield for 4s / 14s | 20 in 2.6m, 35% slow / 12s | 38 in 3.8m, 50% slow for 2s, 60 allied hero shield for 3s / 55s |
| Sivra | 32 single / 7s | 3m evade / 14s | 20 single, 30% slow for 1.8s / 12s | Four 16-damage shots over .54s; leaving range/stun interrupts / 50s |
| Ione | 45 in 2m after .45s / 8s | 26 in 2.6m after .55s, 35% slow for 2s / 12s | 1.8m evade / 16s | 72 in 3.5m after .85s, 45% slow for 2s / 58s |
| Tahla | 18 single, 25% slow for 1.5s / 7s | Heal wounded nearby hero 42 / 9s | Shield wounded nearby hero 50 for 4s / 13s | Allied heroes within 5m gain 58 HP + 25 shield after .4s / 58s |
| Brakk | 34 single, 20% slow for 1s / 7s | Heal 30 + shield 20 for 3s / 13s | 28 in 2.8m after .35s / 10s | 48 in 3.2m after .6s + self shield 65 for 4s / 52s |

- **Assassin:** gap-close and finish isolated fragile targets. Q+W+ultimate totals at most 144.3 before basics, well below the tank's 380 HP and shield. No passive lifesteal. Short offensive ranges; losing the dash leaves him exposed. His advantage is concentrated damage, not a guaranteed instant kill.
- **Tank:** highest HP, real shields and brief stun, lowest offensive pressure apart from support. Valuable at a choke and while protecting allies; cannot chase ranged enemies reliably. Q also applies a 35% slow for 1.5s. Tanks/objectives cannot be chain-stunned: structures ignore CC; heroes/minions get 1.5s immunity after stun.
- **Marksman:** highest basic DPS and range, lowest HP. Best sustained objective damage when protected; one long-cooldown escape. Vulnerable when the assassin closes the gap. Ultimate is sustained single-target pressure, not a large AoE.
- **Mage:** strongest telegraphed group damage. Fixed impact position is sampled at cast time, so targets can leave. Poor basic DPS, low HP and weakest/slowest-returning escape. Excellent wave clear and clustered fights, weak during cooldowns.
- **Support:** shields prevent damage rather than only increasing health. Heals/shields are range-limited, target heroes, and cannot be farmed on structures. Low solo pressure; highest payoff while grouped. No damaging ultimate.
- **Bruiser:** sturdy melee attrition. Basics restore 12% of actual HP damage against enemy heroes only while below 60% HP; no sustain farming from towers or shields. Low range and no dash, less burst than assassin and less protection than tank.

All area casts snapshot the point, telegraph their exact radius, apply one hit per eligible target, respect team/core/planet gates and cancel when the caster dies or is disabled. A stun still active at resolution interrupts. Slows use the strongest active factor; durations do not add. Shields do not stack additively and cap at 40% of target max HP; death/respawn clears shields and CC. A later weaker shield can refresh duration without adding strength.

## Power spikes / evaluation

First major shared spike: ultimate at 45 match-play seconds after actor initialization (ordinary respawn does not reset it). During early fights the assassin needs basics after his two-hit burst; the tank/support contribute without waiting for ultimates. Planet buffs are the next existing spike. No hero-level stat growth was added; saved player levels remain experience indicators.

DPS is theoretical while continuously in range, not a measured win rate. Bots retain reaction delays and defensive heuristics, so equal kits do not imply equal perfect DPS. The balance still needs full matches and human positioning/aim tests. In particular: minion wave survival after marksman DPS changes, tank shield uptime, support group healing, mage multi-target damage and ranged tower safety.

## Validation

- C# syntax parse and source baseline checks run locally; see commit notes. No Unity editor/compiler is available here.
- New Play-mode menu command: `DIMENSIONS FRONT > Checks > Hero overhaul regression (Play mode)`. Covers all-six ultimate locks and basic parity, collider-free targeting, shield overflow, status reset, real delayed multi-target AoE, escape/friendly exclusion, death cancellation and ranged support healing/shields.
- Existing bot regression updated for shared cooldown ownership and telegraphed E attacks.
- These Unity commands have NOT been executed in this environment. Android performance, actual feel and visual quality require in-engine/device verification.
