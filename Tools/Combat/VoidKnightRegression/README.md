# Void Knight regression

Run `python Tools/Combat/VoidKnightRegression/run.py --dotnet-root /path/to/dotnet8`.

Compiles production combat, character state, aiming, input, audio and attack-catalog sources with existing deterministic Unity fixtures and small additional fixtures. Mock Health invokes production VoidKnightState reduction at its simulated damage boundary. Production Health and NGO transport are reviewed separately, not compiled here.

67 checks cover roster/cue stability, balance, audio paths, guard sectors, root/action locking, passive consumption, client authority, cleave timing/damage/slow/interruption, lunge contact, sweep, counter, actual basic delivery and whiffs. Native Unity/physics/network/Android testing remains separate.
