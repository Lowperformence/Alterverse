# Void Knight regression

Run `python Tools/Combat/VoidKnightRegression/run.py --dotnet-root /path/to/dotnet8`.

Compiles production combat, character state, aiming, input, audio and attack-catalog sources with existing deterministic Unity fixtures and small additional fixtures. Mock Health invokes production VoidKnightState reduction at its simulated damage boundary. Production Health and NGO transport are reviewed separately, not compiled here.

101 checks cover roster/cue stability, balance, audio paths, guard sectors, root/action locking, passive consumption, client authority, cleave timing/damage/slow/interruption, lunge contact, sweep, counter, actual basic delivery and whiffs, and release lock behavior before any transaction/server request. Native Unity/physics/network/Android testing remains separate.

Additional coverage: two Void Knight shop entries at the standard price, blocked pre-release weapon transactions, dash slow strength/expiry/target scope, multi-target cleave drain, overkill, shield-only and rejected hits, and maximum-health cap. `Server/commerce/test_void_knight.py` checks both SQLite purchase paths and Firestore rejection before database access.
