# Online movement after arena enlargement

The owner motor allowed authored half-extents 41/35, while server pose validation, dash/forced movement and ability endpoints still used 40/33. With the existing 1.21 length scale this meant a server X boundary of 48.40 versus the owner's 49.61, and Z boundaries of 33 versus 35. A valid owner movement in those strips was clamped and acknowledged at a different position, producing corrections that could feel like an invisible wall or delayed movement.

`ArenaLayout.MovementHalfWidth/MovementHalfHeight` now derive the existing owner limits from the floor dimensions, retaining the one-unit authored margin. Owner defaults, pose validation, locomotion, ability endpoints and aim prediction use these shared values. LengthScale is still applied once by the existing rounded clamp. Speed validation, authorization and rounded corners remain in force.

Validation with deterministic engine/network substitutes:

- 225 transport/history/status/scoreboard checks, including accepted movement in all four previously rejected edge strips and preservation of movement made while an acknowledgement is in flight.
- Negative control: restoring the old 40/33 server limits fails the new enlarged-area acceptance check.
- 14,017 production arena/spawn/healing/geometry checks.
- 4,306 production VFX/combat/shader checks and 1,310 skill-entry/combat assertions.

This identifies a source-level boundary mismatch. It does not establish which image is deployed in the submitted video, explain every blockage near the healing platform, or measure network latency. Native Unity physics, Android and live Edgegap tests remain necessary. Separate differences in local/server collider setup were observed but are outside this bounds change.

Build Android and the Linux dedicated server from the same corrected revision. Push a new server image, register its matching version and image tag, select that version in matchmaking, and start a new match. Updating GitHub alone does not update a deployed image or an installed Android app. Record the actual deployment version, server frame reports, RTT/jitter and device frame times when investigating remaining delays.
