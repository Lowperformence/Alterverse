# Online pacing and unnecessary work

The owner and server pose senders previously scheduled each deadline relative to the current frame. In the controlled 30 FPS fixture this reduced the intended 20 updates/second to 15. SnapshotCadence keeps the previous deadline, skips missed backlog after a stall, and sends at most one current snapshot per frame. World discovery now shares the movement clock without resetting it; moving hotzone creatures and minions retain their 20 Hz samples. Match-state publication uses the same deadline helper while retaining its opening-countdown guard.

Unchanged stationary world entities now receive a one-second recovery heartbeat instead of being serialized in every full scan. Position, rotation, health, shield and death changes bypass the suppression on the next full scan. Moving creatures are never suppressed. Reliable removal messages, packet layout and authority rules are unchanged. History resets on detach or a new match plan. Tradeoff: a lost final stationary-state packet or a late join may wait approximately one second plus a scheduling frame for repair. In the synthetic 110-static-object case, packets fell from 152 to 19 per second (87.5%); this is not an estimate of total match bandwidth savings.

Scoreboard roster enrollment uses the existing Health.Active registry, removing the twice-per-second global scene query and temporary array. Existing scores and death subscriptions remain intact across inactivity and respawn.

Dedicated servers emit `DF SERVER PERF` every ten seconds, including while unfocused or in a paused countdown. Fields are average FPS, p95 and maximum frame milliseconds, generation-zero collection count, and time scale. The collection count does not measure GC pause duration or establish the cause of a frame spike. Samples reuse bounded storage; above 2048 frames/window, p95 uses the most recent 2048 samples while FPS/max cover the whole window.

## Validation and rollout

- 267 multiplayer checks: production sender Update methods at 30/60/90/120 FPS, world cadence, state changes, recovery heartbeat, removals, packet sizing, movement/authority/combat regressions, diagnostics and allocation checks.
- 25 respawn/HUD checks: 1200 stable roster refreshes and paints allocate zero managed bytes in the fixture and issue no global scene queries.
- 1223 roster-cache and 862 world-motion checks pass unchanged. These suites and the HUD suite now run in ranked-regression CI.
- Mutation checks restore the old hero sender, world sender or scoreboard method individually; the new tests fail for the corresponding old behavior.

These are controlled C# tests with Unity/network substitutes, not a native Unity build, Android frame-time measurement or live 4v4 load test. Both Android/AAB and Linux dedicated server must be rebuilt from this commit to receive all changes. Wire compatibility is preserved. No paid resource or matchmaker tier changes are included.

After deploying the rebuilt server and client, capture an active match's server FPS/p95/max, CPU/RAM, RTT/jitter and phone frame times. Exercise hotzone monsters, crowded fights, death/spectating and the scoreboard. Compare the same conditions before considering more compute. A completed source change does not establish that every reported live stutter is fixed.
