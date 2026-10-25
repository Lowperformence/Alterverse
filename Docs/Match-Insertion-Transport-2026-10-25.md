# R35 match insertion and loading transition

The common match-start presentation now uses the owner-supplied **CGPitbull DropShip R35** (Unity Asset Store product 121438) instead of the earlier procedural ATLAS model. It approaches once, hovers over each team's actual squad spawn centroid, releases the heroes underneath, and flies away. It does not land on the ground or replay after a hero respawn. There is no 3/2/1, instruction line, REALITY COLLIDE or GO text.

## Shared coverage

`PrototypeBootstrap.BuildPrototype` prepares the same insertion after the offline or co-op roster branch and outside the arena selection logic. There is no map, game mode, human/bot or local-team filter on insertion creation. All existing maps and match modes using that common bootstrap receive it. Both Zero and Lumen get one ship, oriented toward the battlefield, at their actual hero positions. The normal solo/bot, private co-op, online host and online client paths share this presentation. A dedicated server runs only the authoritative opening clock; client builds render the ships. A client that joins after opening completion skips it.

Only hero renderer visibility is temporarily suppressed. Actor transforms, colliders, spawn/respawn rules and replicated gameplay state are not moved. Renderer state is restored on release, cancellation or teardown. The existing authoritative opening phases govern when gameplay resumes. The camera widens during approach and returns continuously during the final phase.

## R35 appearance and assets

- The original `Assets/R35` import is not edited. The new runtime prefab references its existing root FBX GUID and mesh fileIDs, avoiding another FBX copy.
- Separate URP/Lit game materials use the original white/gray **Diffuse5** paint scheme, a mild cool metal tint and Zero violet / Lumen cyan emission. The glow map is made neutral before team tinting so the original blue pixels do not block a violet glow. Hull form, UV layout and surface detail are preserved.
- Derived maps are 2048px albedo/normal, 1024px metallic/smoothness and 512px emission, with mipmaps, CPU readback disabled and Android ASTC 6x6. The roughness source becomes inverse roughness in the metallic map's alpha channel.
- The original engine pivots tilt into downward thrust while hovering and rotate forward again for departure. The original rear cargo hatch opens during delivery and closes during departure. Guns and deployed landing legs are disabled in the separate runtime prefab.
- Preparation, shared material assignment and resource loads happen behind the loading curtain. Flight updates only transform poses, visibility, camera framing and audio gain. Resources remain owned by the match until teardown. The old procedural model has been removed; a missing R35 resource explicitly fails preparation.

`python Tools/Insertion/prepare_r35.py /path/to/R35_komplett.unitypackage` reproduces the separate runtime assets (Pillow required). The source package is intentionally not added to the repository. `r35-report.json` records provenance and conversion choices.

## Selected audio

The only transport sound is now **SpaceShip Land.wav** by **thepodcastdoctor**, supplied by the owner and selected for this sequence:
https://freesound.org/people/thepodcastdoctor/sounds/534692/

License: **Creative Commons Zero 1.0** — https://creativecommons.org/publicdomain/zero/1.0/

The complete 14-second recording is stored as 44.1 kHz stereo PCM16. Playback preserves its original speed and pitch; the match-start window uses its opening section and fades it as the ship departs. This recording is a landing/hover effect, not a separately recorded takeoff. One local AudioSource avoids doubling the sound for the two teams, follows the existing master/SFX sliders, pauses in the background and never restarts on a replicated phase correction. A late client seeks into the recording. The four earlier synthetic clips and their generator are removed entirely.

`python Tools/Insertion/prepare_audio.py /path/to/534692__thepodcastdoctor__spaceship-land.wav` converts the selected source (ffmpeg required). `audio-report.json` records the source/runtime SHA-256 and license.

## Loading transition

`MatchEntryCurtain` continues to cover local, private-co-op and online handoffs until the arena camera is ready, retains full cover through the first arena frame and dissolves over 0.35 seconds. This covers the previously visible menu panorama. Cancellation, disconnect and failed construction clear it.

## Validation and limits

- `python Tools/Insertion/Regression/run.py --dotnet-root /path/to/dotnet`: managed tests compile the actual R35 adapter, transport, audio, camera, opening and match clock. They cover shared team materials, original-pivot poses, no resource/mesh/material construction during flight, exact renderer restoration, new matches, respawns, late join, network corrections, backgrounding, mute, recording seek and opening timing.
- `python Tools/Insertion/check_assets.py`: prefab reference closure, runtime material/texture setup, original FBX references, no scripts/colliders on the imported prefab and inactive guns/landing legs.
- `python Tools/Insertion/check_audio.py`: selected source/runtime provenance, 14-second stereo PCM16 format, unclipped finite samples and synchronous preload settings; absence of old clips.
- `python Tools/Multiplayer/LoadingRegression/run.py --dotnet-root /path/to/dotnet`: loading/reveal/cancellation regressions.

These checks do not run native Unity import/rendering/audio, every map on a device, an Android build, or a real network session. Native Unity 6 import and in-game visual/audio review remain necessary, especially for engine articulation, hull coverage and the ship's framing on narrow screens. No measured frame-rate guarantee is claimed.
