# Timmy attack and movement regression

Run `python Tools/Combat/TimmyMovementRegression/run.py --mono-root <mono-root> --roslyn <csc.exe>`.

This compiles the production attacks, input/command routing, `HeroMotor` and `HeroLocomotion` together. Dashes advance through the real movement code and the real attack movement multiplier; physics, rendering, health storage and NGO transport remain deterministic substitutes. It is not a Unity editor or device test.

The original fixed-end Scratch and attack slowdown failed nine of the initial 15 checks at 30/60/120 FPS. Giving claws a contact margin still lost all three four-attack chase sequences; the final fix preserves strict ranges and removes Timmy's attack-state slowdown. A 300 ms landing frame also reproduced a missed valid Scratch victim and is checked explicitly.

The final 112 checks cover all five attacks, chasing basic/Q hits, four successive held basics, forward/sideways Scratch pursuit within the original release field, permanent victim revocation after field exit/re-entry, free caster movement and full ticks, untargeted jumps, wall rejection, actual Jet Pounce distance, exactly-once Stardust damage and unchanged attack slowdown for the other eight heroes. Run the common `Tools/Aiming/Regression/run.py` as well for input, selected-victim command round trips, interruptions, damage gates, VFX and the other heroes.
