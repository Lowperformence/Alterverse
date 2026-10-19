# Online packet jitter diagnosis — 2026-10-19

Base: `16130a7b400c51e1c1d7a0e8506e9744fb756b05` on `dev/vertical-slice`.

## Confirmed code failure

Hero and moving-world presentation buffers timestamped snapshots with local receipt time. Consequently, changing network delay changed the apparent interval between two evenly sampled server positions. The renderer interpolated these positions at different speeds despite steady server movement.

Reproduction compiles the production `HeroPresentationBuffer` and `CoopWorldEntityKey`. Server sampling is fixed at 20 Hz and constant movement is 4.77 units/s. Alternating one-way delay of 20/60 ms changes only arrival spacing. After startup:

| Render rate | Receipt-time speed minimum/maximum | Receipt-time speed standard deviation | Corrected speed standard deviation |
| --- | --- | --- | --- |
| 30 FPS | 2.65 / 9.01 units/s | 3.00 units/s | 0.0039 units/s |
| 60 FPS | 2.65 / 11.13 units/s | 3.24 units/s | 0.0051 units/s |
| 120 FPS | 2.65 / 19.61 units/s | 5.05 units/s | 0.0175 units/s |

These are controlled C# results, not measurements of the supplied Android video or a live network. Residual error reflects single-precision timestamp arithmetic in this test. A negative control restoring receipt-time sampling fails the new steady-speed assertion.

## Correction

Hero and world records include the server's unscaled sample time. The render buffer preserves server sample intervals and estimates the clock offset from the earliest observed delivery. It continues holding the last known pose when packets stop and resets on explicit relocations. Hero rendering now pushes and evaluates against the same unscaled local clock as world presentation. Native gameplay position, collision, owner reconciliation, damage and cooldown logic are unchanged.

Snapshot message versions are now `DF_COOP_HERO_SNAPSHOT_V7` and `DF_COOP_WORLD_STATE_V3`. Build and deploy the Android client and dedicated server together. Mixed old/new versions will not exchange these snapshot message types. No binary was built or deployed by this code change.

Application payload for eight hero records is 944 bytes; ten records project to 1178 bytes before transport framing. World records remain bounded to six per packet and fit the existing 1024-byte writer budget. Ten-player admission was not changed.

## Edgegap observations and requested resource change

The latest visible deployment, `a043e93d8be0`, ran `v0.1.13-server` in Paris, with **2 vCPU / 4 GiB** and **one user**. It started at 21:33:47 Europe/Berlin on 19 October and its container requested deletion at 21:44:17. Container logs had expired; all three historical resource/network panels returned retrieval errors. Current CPU utilization, RTT and packet loss could not be established from that match.

At the user's request, the existing `v0.1.13-server` version was reduced to **1 vCPU / 2 GiB** and the dashboard confirmed `Application version updated successfully`. This changes new deployments, requires no image/AAB rebuild and does not resize already running containers. It is independent of this source-code correction.

## Remaining uncertainty

The code failure establishes one mechanism for online movement stutter. It does not prove the full cause of the user's live lag. Packet jitter, packet loss, device frame stalls and CPU stall correlation still require a live comparison after both binaries are updated. Existing 100 ms hero and 150 ms world presentation delays are retained, so this change does not remove baseline network/presentation latency.

A separate probe of existing owner movement validation with four valid 20 Hz packets delivered together after 200 ms shows worst positional rejection of 0.0000 units at 4.77 units/s, 0.0255 at 6.5, and 0.1560 at 8.0. That is a possible secondary source of correction at faster/buffed speeds; it was not changed speculatively or attributed to this recorded match.

## Checks

- 2,960 world-motion checks including both the actual buffer and actual world-entity caller under 40 ms arrival jitter, source clock offset, packet gaps, relocations, invalid time and local/offline resets.
- 274 existing/extended movement, transport, scoreboard, healing and status checks, including hero source-time forwarding and payload bounds.
- 34 Hot Zone reward/result checks with the updated world record, preserving the preceding replica and K/D/A fix.

Run `Tools/Multiplayer/WorldMotionRegression/run.py`, `Tools/Multiplayer/Regression/run.py` and `Tools/Multiplayer/HotZoneResultRegression/run.py` with `--dotnet-root /path/to/dotnet`. All use deterministic Unity/network substitutes. Unity/Android/native latency acceptance remains outstanding.
