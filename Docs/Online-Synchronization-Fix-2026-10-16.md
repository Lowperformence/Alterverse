# Online movement, overhead bars, live scores and repeat searches

## Evidence

The v0.1.3 dedicated server log confirms an authoritative match with one human and
bot fill. The user reports jittering overhead health bars, a mismatch between visible
position and healing pickup interaction, missing kills in the live scoreboard, and
`COULD NOT CLEAN UP PREVIOUS SEARCH` after the match.

The observed live CPU window showed approximately 55–80% of the allocated 0.5 vCPU,
with memory near 27% of 1 GiB. This short sample does not establish frame times or
rule out earlier spikes. No hosting resource change was made as part of this fix.

Source inspection identified three replication gaps:

- The owner reconciled its current position against an older server position, without
  knowing which submitted movement the server had processed. Ordinary network delay
  could therefore rewind movement that was still in flight.
- Remote hero meshes already used `HeroVisualMotion.PresentedPosition`, but overhead
  bars projected the gameplay root, which jumps at the snapshot interval.
- K/D/A existed on the server scoreboard but reached clients only in end-of-match
  result snapshots, leaving the in-match scoreboard without those counters.

The matchmaker inspection endpoint returned zero active tickets. The exact HTTP
response from the phone's failed cleanup was not captured; server container logs do
not contain the phone's outgoing cleanup requests.

## Changes

- Owner movement now includes a sequence number. The server snapshots the accepted
  position at the time it processes that sequence and acknowledges both together.
  The client corrects that historical prediction and retains newer movement.
- Outstanding predictions are rebased after a correction, preventing it from being
  applied twice. History is bounded, unsigned sequence wrap is supported, old packets
  are ignored, and round changes reset prediction/acknowledgement state.
- Server speed/collision checks and authenticated ownership remain authoritative.
  Forced movement, respawning and death still apply authoritative relocations.
- The transport methods are separated into a partial class so the actual production
  writer/readers can be compiled and exercised without unrelated visual-effect code.
- Hero snapshots carry absolute live kills, deaths and assists. Applying them updates
  client scoreboard entries without replaying death events or granting rewards.
- Overhead health bars use the same presented position as the rendered hero.
- Cancel/leave releases an already assigned group's local handle without DELETE.
  Assigned groups are immutable and expire automatically in Edgegap. Unassigned
  queued groups still require successful cleanup; permission/network failures remain
  failures. Existing terminal 404/409/410 handling for group deletion is retained.

## Protocol and rollout

This changes the gameplay wire format. Dedicated handshake protocol is now **5**;
Relay/co-op handshake protocols are **3**. Owner pose messages are
`DF_COOP_OWNER_POSE_V4` and hero snapshots `DF_COOP_HERO_SNAPSHOT_V5`.
Old peers are rejected by the handshake rather than entering an incompatible match.

Build **both** the Linux dedicated server and Android app from this same commit.
Use a new server image tag, `v0.1.4-server`, and a higher unused Android version code.
Coordinate the Edgegap version switch with installation of the matching client.
The currently running v0.1.3 server and installed app do not receive source changes.

```powershell
# Run in Builds/DedicatedServer/Linux after the Unity dedicated build succeeds.
docker build -t registry.edgegap.com/dimensions-front-yf4x25e6aj2i/dimensions-front-server:v0.1.4-server .
docker push registry.edgegap.com/dimensions-front-yf4x25e6aj2i/dimensions-front-server:v0.1.4-server
```

After the server build, explicitly switch the Unity build profile to Android and
wait for import/compilation before preparing/building the signed Android bundle.
The Unity title must show Android, not Windows/Mac/Linux.

## Verification

```sh
python Tools/Ranked/Regression/run.py --dotnet-root /path/to/dotnet
python Tools/Multiplayer/Regression/run.py --dotnet-root /path/to/dotnet
```

Results: **1,923** lobby/identity/ranked checks and **53** movement acknowledgement,
transport and live-score checks passed. The new transport suite compiles production
transport/history classes and the scoreboard accessors against controlled fixtures;
it exercises latency/in-flight movement, corrections, repeated/out-of-order messages,
sequence wrap, bounded history, round transitions, owner/sender validation, speed
limits, forced movement, complete eight-slot snapshots, and live K/D/A replication.
The previous movement and assigned-cleanup implementations fail the new regressions.

These checks are not a native Unity player build, device rendering/physics test, or
live network performance measurement. Required on-device checks remain: move and
stop at the healing pickup, confirm bars track moving heroes, score kills and verify
live team/player K/D/A, test death/respawn and movement abilities, then return to the
menu and search again. Verify both offline and updated multi-device play as well.
