# DIMENSIONS FRONT — production audio integration

FINAL MUSIC ASSET REQUIRED. FINAL SFX ASSETS REQUIRED.
No final recordings are present in this repository. Synthesized prototype tones have been removed; missing cues remain silent and produce one startup summary warning. This is an asset integration pipeline, not delivered final sound design.

## Music delivery
Place authored, original licensed clips under Assets/Alterverse/Resources/Audio/Music:
Menu, Match, Pressure, Climax, Victory, Defeat (WAV/OGG import names without extensions).
Menu/Match/Pressure/Climax must loop seamlessly; align tempo and tonal center because transitions overlap for 0.9 seconds. Result tracks play once. Missing pressure/climax falls back to Match, not generated tones.

Direction: cosmic electronic, quiet textured percussion and a sparse recurring motif; ZERO broken resonances, LUMEN clean harmonics. No copied musical themes. Menu spacious and contemplative; Match steady restrained momentum; Pressure adds rhythmic tension; Climax resolves the same motif urgently. Avoid loudness jumps, excessive sub-bass on phone speakers, generic synthwave and trailer impacts.

Two music sources, equal-power crossfade, combat ducking, live master/music volume, background/game-pause handling. Actual planet arrival and siege events raise pressure; combat, remaining time, control and results select intensity. No fake timer for objective events.
Use streaming Vorbis for long music, preload disabled where verified safe; review loop seams and loading on Android before release. Final loudness and mix require listening on target speakers/headphones.

## Sound delivery
Put authored short clips in Resources/Audio/SFX with names matching DimensionSound:
HitLight, HitHeavy, RecallStart, RecallComplete, RecallCancel, RealityShatter, FrontShift,
Overtime, SuddenDeath, Victory, Defeat, UiConfirm, UiBack, Attack, AbilityCast, Shield,
Heal, HeroDeath, Kill, TowerTarget, TowerShot, TowerImpact, CoreHit, PlanetArrival,
MeteorImpact, SiegeArrival.
Use short crisp transients, controlled mechanical/body layers, short dimensional tails; no test beeps.
TowerTarget must be an unmistakable warning. Kill must differ from hit; HeroDeath from minor damage.
SFX: short mono samples where suitable, compressed-in-memory or decompressed according to measured CPU/memory. No runtime synthesis. Eight pooled voices and per-cue throttles; offscreen positional cues are culled. UI/results remain audible independently of camera framing.

## Verification boundary
Not compiled or played in Unity. No final clips supplied, no listening test, no Android audio latency/memory test. Faction/hero-specific recorded variants and frontend/frontline ambient beds remain external sound-design work. Current Attack/AbilityCast slots are shared hooks, not six finished hero sound identities.
