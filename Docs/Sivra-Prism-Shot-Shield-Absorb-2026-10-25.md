# Sivra: Prism Shot shield absorption and Light Snare damage

Prism Shot (Nyra/Sivra slot 0) retains its 32 normal damage and transfers up to
15 existing shield points from the confirmed enemy target to Sivra before that
damage is applied. A target with five shield transfers five; an unshielded target
transfers none. The transfer cannot take health.

Sivra refills missing rechargeable base shield first. Any excess adds to her
temporary shield for three seconds, within the existing 40%-of-max-health
temporary-shield cap. A larger existing temporary shield is increased rather
than replaced with 15; a longer existing duration is preserved. If only two
points fit, exactly two are removed from the target and awarded to Sivra.

Transfer runs at confirmed contact through Health's authoritative mutation guard.
Friendly, inactive, dead and damage-gated targets do not donate shield; dead or
inactive recipients cannot receive it. Clients do not predict resource changes.
The existing combined shield snapshot already supports the capped result, so
there is no new transport field. The enemy's ordinary recharge and ability to
activate a new shield remain available.

Light Snare (slot 2) increases from 20 to 25 damage. Its 30% movement slow for
1.8 seconds, cooldown and range remain unchanged. Ability cards read the same
runtime numbers and describe Prism Shot's absorption and overflow.

## Validation

```sh
python Tools/Combat/PrismShotRegression/run.py --dotnet-root /path/to/dotnet
python Tools/Combat/ShieldRegression/run.py --dotnet-root /path/to/dotnet
python Tools/Combat/Bolt7Regression/run.py --dotnet-root /path/to/dotnet
```

The new regression compiles production Health, combat rules, ability data and
cards, plus verbatim Hit/ResolvePresentedHit methods. It checks exact/partial/zero
transfer, damage order, base refill, overflow, expiry, caps, friendly/invulnerable/
dead/client rejection, snapshots, recharge, slot isolation and Light Snare.
Engine, command acknowledgement and VFX boundaries are controlled substitutes.
The shared aiming fixture includes a no-op API boundary; siphon behavior is
exercised with real Health rather than duplicated in that fixture.

Native Unity playtesting and multiplayer transport are not tested here. Rebuild
both the client and authoritative match server for online matches.
