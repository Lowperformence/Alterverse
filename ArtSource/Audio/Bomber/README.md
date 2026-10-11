# Bomb jet flyby

The recording `mightuser-fighter-jet-powerful-low-flyby-sfx-337205.mp3`
was supplied by the user on 2026-10-11 to replace the previous bomber engine.
This folder retains the original upload without modification.

Original SHA-256: `d82a4c7963ffb560eb0fe8b499f272795694423173f61df8fbb8ab5abc4fa1d7`

The in-game asset remains `Resources/Audio/BombJetFlyby.ogg` and retains
Unity GUID `83bb334f17b44c2cad4c0a0a369ed02f`. Its 8.8-second window keeps
the approach, main flyby and receding engine, fitting the existing roughly
7.7–8.9-second flight routes. Pitch stays at 1; the recording contains its
own flyby sweep. The sound plays once per aircraft.

Rebuild from the repository root:

```sh
ffmpeg -v error -y \
  -i ArtSource/Audio/Bomber/mightuser-fighter-jet-powerful-low-flyby-sfx-337205.mp3 \
  -af 'atrim=start=1.2:end=10,asetpts=PTS-STARTPTS,afade=t=in:st=0:d=0.15,afade=t=out:st=8.15:d=0.65,aresample=192000,alimiter=limit=0.707945784:attack=2:release=35:level=false:latency=true,aresample=48000,apad,atrim=duration=8.8' \
  -ac 1 -ar 48000 -c:a libvorbis -q:a 5 -map_metadata -1 \
  Assets/Alterverse/Resources/Audio/BombJetFlyby.ogg
```

The previous game's source gain of 0.168 is retained, as requested in
the user's follow-up. There is no additional fixed file-gain reduction.
A limiter without makeup gain controls only reconstructed recording peaks
before Vorbis encoding. `SpaceBomberFlyover.FlybyVolume` caps
source gain at 0.168, multiplied by player-distance attenuation, route
fades, and the existing Master/SFX sliders. Playback starts silent until
the first attenuation update, preventing a loud spawn transient.
Import normalization remains disabled.

Output: 48 kHz mono, 8.8 seconds. The complete live audio mix is not
included in offline peak estimates. Decode, finite-sample, duration,
fade endpoint and GUID checks are required after rebuilding;
Unity/Android listening verification remains pending.
