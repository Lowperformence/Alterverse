# Standard attack targeting and retreat audit — 2026-10-11

The requested behavior is implemented for all nine heroes: Veyr, Kaelon, Nyra,
Elyra, Mira, Rook, Traveler, Nerok and Timmy. An automatic standard attack selects
an eligible enemy inside its existing range at acceptance and retains that target
through contact. Holding the button keeps the actor facing the eligible enemy
while running away, including the intervals between attacks.

## Findings and corrections

- Most melee basics discarded the selected body and retained a fixed world point.
  Moving during preparation could turn an accepted strike into a miss or hit a
  different enemy. All default basic AimSpecs now use AutoBasic, retaining the
  target reference. The range check occurs when queueing; melee contact resolves
  once against that target after windup and still checks actual obstacles.
- Automatic ranged basics refreshed/predicted the enemy once at launch and then
  traveled straight. Direction changes, caster retreat near the range boundary,
  or another enemy crossing the path could lose the intended hit. Targeted shots
  now share a live endpoint with the existing target-following VFX. Launch
  distance divided by catalog projectile speed determines the nominal flight
  time; the path interpolates toward the live endpoint over that time. This is
  target-bound flight rather than constant-speed free projectile physics.
- During cooldown, TryAttack previously returned before refreshing facing. A
  separate motor target heartbeat now gives held basic facing priority without
  restarting animation, cooldown or attack movement slowdown. The shared late
  facing owner receives the desired combat direction. Rotation is completed on
  strike/release, including targets initially behind the actor.
- Local attack animation receives the target body instead of an obsolete point.
  Space-key hold now repeats through the same cooldown-bound path as touch/mouse.
- V5 commands already carry target identity. All nine basics now use those fields;
  the host validates enemy eligibility and initial range before committing. The
  wire format and channel names remain unchanged.

The requested commitment changes escape behavior deliberately: movement after a
valid attack starts does not cancel its hit. It does not authorize a new attack
against an initially out-of-range enemy. The captured identity cannot silently
turn into a free shot when its target is destroyed. Dead/disabled/allied targets,
real walls, pre-release stun, caster death/disable and existing damage gates still
prevent damage as applicable. Released projectiles retain the existing caster-stun
behavior. A different enemy cannot intercept a targeted standard attack.

Damage amounts, attack intervals, catalog ranges, windups and movement speeds are
preserved. Rook damage rules remain in the shared hit resolver. Manual aimed casts,
skills, cone capture, areas, support, dashes, hooks, vortex, volley and Timmy's
Scratch release-field rules retain their existing geometry. The serialized legacy
basicLeadQuality value remains compatible but no longer controls bound basics.

## Validation

- 5,252 production motor/locomotion fixture checks pass at 30/60/120 FPS. The new
  cases cover each hero retreating from targets ahead, beside and behind it near
  the initial range edge; strike/release facing; three held backpedaling attacks;
  cooldown-facing priority; exact single damage; a changing target direction and
  nearer decoy; initial range/allied host rejection; and space-key hold.
- 2,609 common aiming/input/command assertions pass, plus both editor delivery
  suites compiled in the fixture runner. Existing touch/mouse hold, concurrent
  movement, moving-target command receipt, saturated sight queries with walls,
  skills, shields, damage gates, cancellation and fixed-point casts remain covered.
- Earlier automatic dodge expectations were replaced with explicit committed-hit
  expectations. Explicit free-aim dodge tests remain, distinguishing the two paths.
- The aiming runner no longer compiles the unrelated shop integration editor check;
  its new shop dependencies are outside this combat fixture. That production check
  remains available in Unity.

These are C# production-code checks with deterministic substitutes for Unity
physics/rendering/health and NGO transport. No Unity Play Mode, Android build,
visual animation inspection or device test was available in this environment.
