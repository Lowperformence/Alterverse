# Offline engagement baseline

**Model scores, not live win rates or a balance verdict.**

540 duel scenarios and 21 controlled team substitutions; 210 complete team compositions against three reference opponents.

| Hero | Basic engagement index (mean 100) | HP | Base shield | Basic DPS |
|---|---:|---:|---:|---:|
| Void Knight | 72.2 | 350.2 | 60 | 15.00 |
| Zhaev Null | 133.3 | 241.5 | 70 | 25.88 |
| Andromeda Timmy | 111.1 | 223.4 | 50 | 28.21 |
| Auren Kest | 146.6 | 481.8 | 70 | 16.19 |
| Sivra Quell | 50.0 | 185.0 | 30 | 25.81 |
| Ione Vaska | 11.1 | 200.0 | 50 | 13.64 |
| Tahla Venn | 36.7 | 277.7 | 50 | 17.00 |
| Brakk Orun | 200.0 | 362.2 | 80 | 26.67 |
| THE TRAVELER | 61.1 | 259.9 | 50 | 18.48 |
| Nerok Abyss | 177.8 | 344.1 | 90 | 24.49 |

## Composition baseline sample

Basic attacks only, against the same three reference teams. These extremes measure basic-attack composition strength in this lane model; support/AOE kits are excluded.

| Composition | Mean scenario value |
|---|---:|
| Void Knight, Zhaev Null, Sivra Quell, Brakk Orun | 1.000 |
| Void Knight, Andromeda Timmy, Sivra Quell, Brakk Orun | 1.000 |
| Void Knight, Andromeda Timmy, Sivra Quell, Nerok Abyss | 1.000 |
| Andromeda Timmy, Ione Vaska, Tahla Venn, THE TRAVELER | 0.000 |
| Auren Kest, Ione Vaska, Tahla Venn, THE TRAVELER | 0.000 |
| Ione Vaska, Tahla Venn, THE TRAVELER, Nerok Abyss | 0.000 |

## Numerical timestep audit

All 180 basic scenarios repeated at dt=0.003125 versus default 0.00625: 0 outcome-direction flips; maximum index change 0.00 points; maximum individual scenario-value difference 0.000. Team scenarios were not repeated at the finer step. Values are coarse scenario indices, not a gameplay ranking.

## Sensitivity to incomplete ability coverage

| Hero | No passives | Supported passives | Experimental limited kit |
|---|---:|---:|---:|
| Void Knight | 72.2 | 72.2 | 22.2 |
| Zhaev Null | 133.3 | 133.3 | 177.8 |
| Andromeda Timmy | 111.1 | 111.1 | 55.6 |
| Auren Kest | 146.6 | 147.1 | 127.0 |
| Sivra Quell | 50.0 | 50.0 | 94.4 |
| Ione Vaska | 11.1 | 11.1 | 7.3 |
| Tahla Venn | 36.7 | 36.2 | 96.6 |
| Brakk Orun | 200.0 | 200.0 | 200.0 |
| THE TRAVELER | 61.1 | 61.1 | 108.0 |
| Nerok Abyss | 177.8 | 177.8 | 111.1 |

These are model-variant sensitivity values, not statistical confidence intervals. Role utility stays separate: effective healing received, ally healing generated, shield absorbed, damage and range uptime appear per scenario, not as arbitrary designer weights.

The primary index uses ONLY basic attacks, with no passives or abilities; Brakk intrinsic 5% damage modifier remains part of basic damage. Contact/ranged starts and hold/retreat policies are weighted equally. It is a basic-engagement comparison, NOT an overall hero score, and MUST NOT be used to recommend buffs/nerfs. Review individual scenarios and covered abilities first.

Team swap results are in results.json; each core is held fixed. The --all-teams option covers 210 compositions against three disclosed reference opponents; this is not an all-versus-all tournament. Cross-team same heroes are allowed; team-internal duplicates are excluded.

## Coverage and limits

Covered: source-extracted HP, base shields, attack range/damage/interval/windup/speed; 0.88 attack movement multiplier (Timmy exempt); basic targeting lowest HP; expiring nonadditive shields capped at 40% HP; base-shield recharge after 15 seconds; Auren 2 HP/s regeneration and Brakk conditional 12% HP lifesteal; W shield/heal for Auren/Brakk/Tahla; uncomplicated Q damage for Zhaev/Sivra/Tahla/Traveler; mobility distance/cooldown and entry shields for five heroes.

Unsupported: other damaging skills and all ultimates, full passive kits (including Void Resonance trigger on SKILL hit, Timmy movement passive, Sivra dash speed), roots/slows, acceleration/deceleration, turning/facing, cover, arena walls, projectile visuals, bot policies, collision, animation cancellation, dash duration (modeled instantaneous), ranged facing/cone-angle changes during windup, objective damage buffs, networking. One-dimensional open lane; no simultaneous cast locks; fixed policy, not optimized human skill. This creates substantial asymmetric kit bias, particularly against Void Knight/Timmy and area heroes. Support contribution includes healing, but only part of support kit.

Time integration uses dt=0.00625 seconds. Near threshold outcomes can depend on discrete attack/movement ordering; this is numerical uncertainty, not a confidence interval. Auren/Sivra and Ione/Tahla kite regressions are checked at dt=0.00625 and 0.003125, but all matchups have not been proven timestep-converged. Treat marginal outcomes cautiously.

No gameplay changes. Source hashes in JSON allow checking exactly which snapshot was modeled. Next phase: implement remaining skills/status timelines against Unity regression fixtures before interpreting aggregate balance indices.
