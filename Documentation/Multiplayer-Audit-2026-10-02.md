# Multiplayer code audit — 2026-10-02

Corrected findings:
- Dedicated-server player bindings were missing from movement, combat and round-ready validation. Both online transport paths now use shared identity validation.
- Gameplay, hero snapshots/audio, world state and world events lacked round isolation. Their V2 messages now carry and validate the current plan fingerprint before applying data.
- Repeated valid online plan acknowledgements could invalidate the same client; acknowledgements are now idempotent while duplicate identity claims remain rejected.
- Dedicated handshake state survived disconnected idle sessions. Idle teardown now resets roster/ack/launch state; pre-launch departures invalidate the published plan.
- Online client transport loss lacked the cooperative lobby fallback. It now triggers the existing menu/session cleanup flow.
- Non-finite movement/ability values and invalid snapshot counts are rejected. Server combat commands are rejected for dead actors or while paused.

Validation: inspected changes against the immutable GitHub source; checked matching writer round headers and reader guards for all 19 writer sites, message version changes, and balanced source braces. Unity is unavailable in this environment: compilation, transport behavior and multi-client play tests have NOT been run. All peers and the dedicated server must use the updated build.

Required live follow-up: host plus two clients and dedicated server; move/cast on each player; complete at least two consecutive rounds; disconnect before launch, during play and on results; reconnect; test delayed packets and repeated acknowledgements. Relay host migration is not implemented; host departure still closes that session.
