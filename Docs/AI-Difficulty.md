# DIMENSIONS FRONT — Bot difficulty

Updated 2 October 2026: EASY → MEDIUM → HARD → EXPERT → NIGHTMARE.

## Profiles

| Tier | Saved value | Reaction delay | Decision interval scale | Objective interval | Ability mistake | Incoming damage |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Easy | 0 | .75 s | 1.80 | 2.40 s | 38% | 100% |
| Medium (internal Normal) | 1 | .38 s | 1.00 | 1.10 s | 18% | 100% |
| Hard | 2 | .20 s | .72 | .50 s | 5.5% | 100% |
| Expert | 3 | .11 s | .50 | .28 s | 1.5% | 100% |
| Nightmare | 4 | .08 s | .42 | .22 s | .5% | 96% |

The former Expert 4% damage reduction now belongs exclusively to Nightmare. `Health.TakeDamage` applies this difficulty multiplier only to enemy bots on team 1. It is not a player-wide damage reduction. Other independent buffs and shields still apply normally.

Nightmare improves decision quality, accuracy, positioning and discipline. Team tactics update every .28 s versus Expert's .35 s. It uses the same visibility and legal observations as the other tiers. The existing minimum decision interval and personality factors still apply; these values are not measured end-to-end reaction times. Movement speed, outgoing damage and cooldowns are not increased by this profile.

## Selection, progression and persistence

`DF_BotAdaptiveTier` stores the personal solo tier. Existing values 0–3 retain their meanings. Missing or invalid values fall back to Medium. Three consecutive solo wins increase the tier by one, now capped at Nightmare; three losses decrease it, down to Easy. Duplicate match receipts cannot advance the streak twice.

Co-op uses an authoritative match override. Lobby cycling and reading already enumerate all BotDifficulty values, so Nightmare becomes selectable and is included in the existing shared match plan and handshake. The result label is NIGHTMARE.

The existing reward rule grants 10 percentage points per tier, therefore Nightmare receives a 40% bonus, above Expert's 30%. This preserves the existing progression formula.

## Validation

Local checks confirm five stable enum values, Expert multiplier 1.0, Nightmare multiplier .96, stronger Nightmare behavior parameters and updated tier bounds. Unity editor regression checks now cover all five tiers, persistence, promotion/demotion, maximum tier, duplicate receipts and Nightmare co-op plan transfer. Old regression preference keys were corrected to the active adaptive-tier key.

Unity is unavailable in this execution environment. Compilation, editor regression execution, full matches and Android balance/performance validation remain to be run. Art changes are tracked separately in draft PR #20.
