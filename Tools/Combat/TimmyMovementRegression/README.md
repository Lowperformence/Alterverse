# Timmy attack and movement regression

Run `python Tools/Combat/TimmyMovementRegression/run.py --mono-root <mono-root> --roslyn <csc.exe>`.

This compiles the production attacks, input/command routing, `HeroMotor` and `HeroLocomotion` together. Dashes advance through the real movement code and the real attack movement multiplier; physics, rendering, health storage and NGO transport remain deterministic substitutes. It is not a Unity editor or device test.

The original fixed-end Scratch and attack slowdown failed nine of the initial 15 checks at 30/60/120 FPS. Giving claws a contact margin still lost all three four-attack chase sequences; that pass removed Timmy's attack-state slowdown. Basic attacks now use the range-at-acceptance commitment documented below. A 300 ms landing frame also reproduced a missed valid Scratch victim and is checked explicitly.

The original Timmy checks cover all five attacks, chasing basic/Q hits, four successive held basics, forward/sideways Scratch pursuit within the original release field, permanent victim revocation after field exit/re-entry, free caster movement and full ticks, untargeted jumps, wall rejection, actual Jet Pounce distance, exactly-once Stardust damage and unchanged attack slowdown for the other eight heroes. Run the common `Tools/Aiming/Regression/run.py` as well for input, selected-victim command round trips, interruptions, damage gates, VFX and the other heroes.

`BasicMovementRegression.cs` adds all nine heroes at 30/60/120 FPS: retreat from targets ahead, beside and behind the caster at the initial range boundary; facing on the strike/release frame; three sustained backpedaling attacks with facing checked throughout cooldown; exactly-once damage; a moving accepted target and a closer decoy; allied/out-of-range host rejection; and keyboard hold repetition. Both production movement components are compiled, including LateUpdate facing arbitration. Quaternion interpolation and CharacterController physics remain fixture substitutes. Together with the Timmy cases, **5,252 checks pass**. See `Docs/BasicAttackTargetCommitmentAudit.md`.
