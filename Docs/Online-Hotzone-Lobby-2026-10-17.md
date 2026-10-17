# Online Hotzone, match completion and short frame spikes

## Corrected behavior

- Dedicated servers send Hotzone open/capture events using server authority. A dedicated
  server is not a local player-host; the old `LocalIsHost` gate prevented both events.
  Clients now receive the zone position and spawn the awarded monster replica. A small
  progress message updates the capture clock, scores and participant counts twice a second.
  Old open/resolve events cannot reopen a finished zone or duplicate its reward monster.
- Online Casual and Ranked never use the same-server next-round protocol, including queued
  plan/countdown/launch callbacks. After the local result reward transaction commits, the
  result screen offers **Back to lobby** and returns there automatically after 12 seconds.
- Returning closes the dedicated connection and clears Edgegap membership/assignment while
  retaining the Unity friend session, invite code, players and loadouts. Ready resets for a
  deliberate new search. An explicit leave action still leaves/deletes the party.
- Repeated friend searches distribute the fresh Edgegap group ID and exact per-player join
  receipts through member-visible Unity session/player properties. This works after the
  dedicated connection has replaced the old Relay transport. The leader becomes Edgegap-ready
  only after every friend acknowledges joining the current group. Cancellation clears the
  shared group, and guests observe it through the surviving session.

## Performance change

World-state packets are chunked. Previously each received chunk rebuilt the complete
non-hero entity lookup. The lookup now refreshes once per frame, with an immediate refresh
if the number of active Health owners changes. HP/shield/position are still applied per
packet; the server send interval, simulation speed and damage rules are unchanged. Packets
for an old round are rejected before scanning. The dedicated server also skips the two
terrain-conforming Hotzone render meshes/materials while continuing all objective logic.

Controlled fixture with 110 world objects and 19 chunks received in one frame:
**4,180 → 220 Health component lookups** for registry discovery. This measures that operation,
not a percentage improvement in device FPS or live server latency. The reported intermittent
lag still needs native frame-time/network measurements to determine any remaining causes.

## Verification and deployment

Run `Tools/Ranked/Regression/run.py` and `Tools/Multiplayer/Regression/run.py` with .NET 8.
The ranked suite compiles the actual service, exit, round-loop, auto-return and world-state
classes; Hotzone transitions and message handlers are extracted verbatim. Terrain physics,
rendering, Unity Services, HTTP and NGO are controlled substitutes. Counterexamples restoring
the old Hotzone host gate, repeated world scan or queued online launch are rejected.

No native Unity/Android build or real Edgegap match was performed in this environment.
Build **both the Android client and Linux dedicated server from the same updated commit**,
publish a new server image/version, then switch matchmaking to that version. The existing
server image and already uploaded AAB do not contain these source changes.

Required device test: host + guest finish a match, verify saved XP/shards/drop, return with
the same invite code and members, ready manually and find another match. Repeat for 2–4
friends, test cancellation, and confirm Hotzone at 60 seconds and capture/monster after
30 active seconds. Verify local frame-time spikes, RTT/jitter and server CPU/frame time in
a real full 4v4 match. Reward formulas have not been changed by this update.
