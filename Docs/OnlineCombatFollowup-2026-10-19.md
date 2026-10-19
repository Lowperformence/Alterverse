# Online focus, Hot Zone replicas and result K/D/A — 2026-10-19

Base: `bdd8f951adc286f8ec53226f204bb966ffc4597e`, `dev/vertical-slice`.

## Findings and changes

The supplied 4.47-second Android recording accompanies reports of held-focus jitter and a motionless/unattackable Hot Zone reward. It does not contain the final scoreboard or identify deployed binary revisions. These changes close reproduced code failure paths; device reproduction and acceptance are still required.

- `BasicAttack.TryAttack` reacquired the ranged target every held-input frame. Ranged selection ranks by hero class, HP and distance, so changing target HP/distance or another manual preview could alternate the chosen focus. `AcquireBasic` now keeps a separate held-basic commitment with a 200 ms input heartbeat. New hero targets still preempt nonheroes. Dead/invalid/distant targets are released. A 0.2-unit outer band retains focus only and cannot start an out-of-range attack. Manual/cone selection and initial HP ranking are unchanged.
- Client `ApplyNetworkResolve` spawned a reward independently of whether the server's two-living-reward cap accepted one. `CoopWorldStateSync.OnState` permanently discarded unknown world keys. Together these paths could create client-only rewards or fail to recover a missing replica. Resolve events now update zone presentation only. Host snapshots create/recover reward replicas using an event-and-team key (`HOTZONE:{occurrence}:T{team}`); the server alone enforces the spawn cap. Repeated packets are idempotent. Reliable tombstones prevent late movement packets from resurrecting removed entities, and the tombstone set resets for a new plan. Key presentation caches are configured after monster initialization to cover the complete visual hierarchy on death.
- `CoopMatchResultSync.CaptureHostResult` used `MatchTelemetry.HeroStats.Kills` for final K/D/A, but `RecordDeath` increments that counter for soldiers and other nonhero victims too. Final K/D/A now comes from the authoritative hero-only `MatchScoreboard.GetNetworkScore`, as live score snapshots do. Existing quest totals remain separate. Result hero/team presentation now resolves the actual local co-op member.

## Validation

- Aiming/facing: **12,954** assertions at 30/60/120 FPS, including alternating target HP/distance, manual preview interference, focus release, death, outer-band range and hero priority.
- Reward lifecycle/results: **34** checks of production spawn decisions, snapshot receiver, stable key parser, tombstone rejection and result capture; includes 2 hero kills versus 19 quest kills.
- Existing ranged-cone suite: **699** assertions.
- Existing combat/skill-entry suite: **1,310** assertions plus both editor delivery suites under fixtures.
- Existing world interpolation suite: **862** checks.
- Existing movement/transport/scoreboard suite: **267** checks.
- Three independent negative controls restore per-frame basic selection, unknown-snapshot dropping and telemetry K/D/A respectively; each fails its matching new regression.

These are C# tests with deterministic engine/network fixtures. Reward tests extract production spawn/receive/capture methods; art/AI initialization and native NGO/physics are substitutes. No Unity player build, Android rendering or live latency test was run here.

Run:

```sh
python3 Tools/Aiming/Regression/run_entry.py --dotnet-root /path/to/dotnet --aim-jitter
python3 Tools/Multiplayer/HotZoneResultRegression/run.py --dotnet-root /path/to/dotnet
python3 Tools/Aiming/Regression/run_entry.py --dotnet-root /path/to/dotnet --ranged-cones
python3 Tools/Aiming/Regression/run_entry.py --dotnet-root /path/to/dotnet
python3 Tools/Multiplayer/WorldMotionRegression/run.py --dotnet-root /path/to/dotnet
python3 Tools/Multiplayer/Regression/run.py --dotnet-root /path/to/dotnet
```

## Delivery and device acceptance

Build and roll out both the Android client and dedicated server from the same new revision. Old clients use a different reward key and old servers do not send the self-describing reward key; mixed revisions do not provide this reward lifecycle fix. No hosted server or store release was changed by this commit.

In Unity and on Android, hold a ranged basic while strafing among multiple enemies and at maximum range, use a manual skill during a hold, release, and kill/move away from the focused target. Verify no alternating focus, correct hero priority and no extra range. Resolve repeated Hot Zones with two living rewards, then after killing one, and join/test consecutive online rounds. Verify each visible reward has the server's pose/team/HP and can receive damage; repeat on Mars and another arena. Finish a match with separately known hero and soldier kills and compare live K/D/A with the result and quest counters.
