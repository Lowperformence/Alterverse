# ATLAS match insertion and loading transition

The video `15414.mp4` exposes the menu's star panorama for roughly 0.1 seconds between the loading screen and arena reveal. Local entry used to deactivate the menu-owned loading UI before the bootstrap finished initializing the battlefield.

## Resulting sequence

A persistent `MatchEntryCurtain` covers local, private-co-op and online handoffs until the initialized arena camera is revealed. It retains full cover through the first arena frame, then dissolves over 0.35 seconds. Failure, disconnect, cancelled plans and menu recovery clear it.

Each team gets an original ATLAS VTOL cargo transport at its squad's actual spawn centroid. It approaches, hovers over the spawn with a solid cargo roof, opens its **bottom** hatches, reveals the heroes underneath, and climbs away. There is no ground touchdown or ramp sequence. Only hero renderers are temporarily suppressed: gameplay transforms, colliders, respawn code and replicated state are untouched.

The existing authoritative opening phases still govern release of gameplay. `MatchOpening` draws no text: the numbers, rules, bomber warning, “REALITY COLLIDE” and “GO” are absent. The camera starts with 45% more distance for the aircraft and returns continuously during the final opening phase.

The transport is created once per match world and cannot replay on respawns. Late clients that already received completion skip the introduction. Interrupted introductions restore each renderer's original `forceRenderingOff` state. The dedicated server creates no aircraft or audio.

## Model and sound

`InsertionTransportModel` builds 1,172 triangles in eight mesh parts per aircraft: tapered cockpit, broad armored cargo roof, side wing supports, four VTOL nacelles, luminous nozzles, caution bars and moving belly hatches. Model/material creation and mesh upload happen during loading. The small hidden models remain owned by the match world until teardown, avoiding resource disposal at gameplay release.

Four original, offline-authored mono PCM assets provide a three-second seamless turbine loop, bottom-bay air pressure release, hydraulic mechanisms and rising departure wash. No third-party samples or runtime synthesis are used. Two local audio sources provide one mix instead of doubling the sound for both teams. The mix follows the existing master/SFX controls, pauses in the background, and does not repeat old one-shots after network corrections. All clips are preloaded and decompressed during loading (449,108 WAV bytes in the repository).

Rebuild the sound assets with `python Tools/Insertion/build_audio.py` (NumPy/SciPy). `audio-report.json` records durations, peak/RMS levels and loop edge delta.

## Validation

- `python Tools/Multiplayer/LoadingRegression/run.py --dotnet-root /path/to/dotnet`: 45 assertions for entry, cancellation, recovery, camera reveal and persistent curtain lifecycle.
- `python Tools/Insertion/Regression/run.py --dotnet-root /path/to/dotnet`: 1,220 assertions compiling the real transport, model, audio, camera, opening and match-clock sources against controlled managed Unity substitutes. This includes 1,172 triangle degeneracy checks and lifecycle/network/audio/camera checks; it is not 1,220 independent gameplay scenarios.
- `python Tools/Insertion/check_audio.py`: PCM format, finite peaks, DC offset, loop seam, one-shot fades and synchronous preload settings.

The managed tests do not run native Unity, the GPU, Unity audio playback, an Android build or a real dedicated-server session. Final acceptance still needs native import/compilation, an in-game view of the hovering spawn cover, listening on device/headphones, and a cold-start frame-time capture on the target phone. No measured frame-rate guarantee is claimed.
