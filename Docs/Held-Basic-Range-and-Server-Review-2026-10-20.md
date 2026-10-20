# Held basic range fix and server review — 2026-10-20

## Cause and change

The client treated a sent basic command as an accepted attack: it immediately began body/arm windup and attack movement state. The host can reject the same target using its authoritative position/range. Once that processed command is acknowledged with zero cooldown, held input can send and animate again. Local soft-target acquisition already checks exact range; it cannot guarantee acceptance against a different server position.

Basic commands now predict only cooldown. Explicit client targets must pass the same enemy and exact-distance rules before sending. A separate retry deadline, at most 120 ms and never longer than the normal interval, prevents zero-cooldown acknowledgements from generating frame-rate retries. It does not alter authoritative cooldown or damage.

After all target, range and capacity checks pass, the host queues the attack and emits an appended BasicWindup semantic event over the existing reliable sequenced VFX transport. Only the local owner uses that event to start presentation and attack movement state. Remote rigs retain their existing snapshot presentation. The owner seeks HeroVisualMotion, melee clips and Void Knight clips to packet age, including short recovery; expired events cannot restart a full windup. Offline/host execution remains immediate. Accepted melee commitment and exact cast ranges are unchanged.

Tradeoff: owner windup now depends on host confirmation and its round-trip delay. Late confirmation seeks into the action rather than displaying a rejected speculative strike. Device latency/feel must be checked in Unity and Android. Client and server require matching builds for the new confirmation cue.

## Validation

- 1,332 skill-entry/combat assertions, including held input with processed zero-cooldown rejection, bounded retries, explicit distant client target, valid authoritative queue, cooldown rejection and absence of client damage.
- 12,954 focus/AOE edge assertions at 30/60/120 FPS with production BasicAttack, ManualAiming and HeroMotor.
- 699 ranged geometry, timing and server-command assertions at 30/60/120 FPS.
- 4,779 online VFX/combat assertions using production semantic relay/receiver. New coverage checks all ten heroes, host-only confirmation, local-owner-only replay, packet age, expiry and absence of event echo.
- Negative control: --negative-held-basic restores send-time animation in a temporary compiled copy; the held rejection regression fails with “rejected held basic never predicts phantom windups”. Production source is not changed by this control.

These are deterministic engine/network/rendering fixtures, not native Unity, NGO transport, Android or live deployment tests. Before release, check moving targets just inside/outside range while holding attack, moving attacker, melee and ranged heroes, high latency, stun/death, minions/objectives and an uninterrupted accepted strike.

## Server evidence and limits

Edgegap account inspection found deployment d0c1eeb861d7, dimensions-front v0.1.15-server, Milan, 1.00 vCPU / 2.00 GiB. It was created at 18:25:18 UTC, ready at 18:25:20 UTC (1.62 s), and the deployment log records a container-requested deletion at 18:37:54 UTC. Status is Terminated. That event alone does not establish why the game container requested shutdown. Container logs have expired; no crash or active-match performance conclusion can be drawn for this deployment. No deployment or resource settings were changed.

The retained log for 7852e18c71d5 explicitly reports disconnecting client 1 because of a game version mismatch, asking to update Android and dedicated server together. This explains that recorded connection rejection, not necessarily any later disconnect. It contains 14 performance windows: initial 58.1 FPS / 203.5 ms maximum frame, then 60.0 FPS / p95 16.7–16.8 ms and maxima up to 16.9 ms. There are no combat telemetry windows and no exception entries. These are idle/startup measurements after the rejected connection and cannot validate capacity for a full match or prove an attack rejection rate.

Earlier logs contained a missing-script / serialization-layout warning. A later retained startup no longer shows it; source commit 745b5c5 excludes Android-only Play Games Resources from dedicated server builds. Native rebuild verification remains necessary before calling that build issue resolved in production.

The clearest additional defect in the retained evidence is client/server version mismatch. For the next release, build Android and the dedicated server from the same revision/version. A fresh full-match log is needed to evaluate accepted/received attacks, human player damage and loaded server frame times. Log retention would be needed to investigate container shutdowns after termination; no storage endpoint has been configured by this change.
