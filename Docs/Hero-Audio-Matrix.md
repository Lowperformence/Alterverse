# DIMENSIONS FRONT — individual hero audio

30 unique cast/release cues and 23 separate impact cues (53 OGG files, 278,743 bytes). Source recordings are layered, trimmed, filtered and rhythmically shaped offline; no oscillator/test-tone synthesis and no pitch-only duplicate identities. Original generic Attack/AbilityCast/AbilityImpact clips are no longer routed for hero actions. Generic non-hero hit/support effects remain for other systems. Music is unchanged.

## Audit matrix

All rows: new authored recording layers; no prototype tone; CC0 licensed. **Production listening approval pending** for every row. Distinct hashes prove distinct data, not perceptually successful identities. Travel loops are not applicable to the current instant-hit and one-shot-area mechanics. Support/dash activation cues play on accepted execution; Wildsurge has a distinct delayed resolution cue.

| Hero | Action | Signature | Cast/release | Impact |
|---|---|---|---|---|
| Zhaev Null | Basic | cut | Veyr/Basic_Cast | Veyr/Basic_Impact |
| Zhaev Null | Null Cut | double | Veyr/Q_Cast | Veyr/Q_Impact |
| Zhaev Null | Rift Scar | tear | Veyr/W_Cast | Veyr/W_Impact |
| Zhaev Null | Gapfold | reverse | Veyr/E_Cast | Activation only; no separate impact |
| Zhaev Null | Vacant Echo | collapse | Veyr/R_Cast | Veyr/R_Impact |
| Auren Kest | Basic | weight | Kaelon/Basic_Cast | Kaelon/Basic_Impact |
| Auren Kest | Anchor Strike | slam | Kaelon/Q_Cast | Kaelon/Q_Impact |
| Auren Kest | Lumen Wall | swell | Kaelon/W_Cast | Activation only; no separate impact |
| Auren Kest | Gravity Anchor | anchor | Kaelon/E_Cast | Kaelon/E_Impact |
| Auren Kest | Kest Citadel | citadel | Kaelon/R_Cast | Kaelon/R_Impact |
| Sivra Quell | Basic | snap | Nyra/Basic_Cast | Nyra/Basic_Impact |
| Sivra Quell | Prism Shot | prism | Nyra/Q_Cast | Nyra/Q_Impact |
| Sivra Quell | Vector Shift | shift | Nyra/W_Cast | Activation only; no separate impact |
| Sivra Quell | Light Snare | snare | Nyra/E_Cast | Nyra/E_Impact |
| Sivra Quell | Sightline Break | volley | Nyra/R_Cast | Nyra/R_Impact |
| Ione Vaska | Basic | pulse | Elyra/Basic_Cast | Elyra/Basic_Impact |
| Ione Vaska | Orbit Fracture | orbit | Elyra/Q_Cast | Elyra/Q_Impact |
| Ione Vaska | Invert Field | invert | Elyra/W_Cast | Elyra/W_Impact |
| Ione Vaska | Phase Fold | fold | Elyra/E_Cast | Activation only; no separate impact |
| Ione Vaska | Impossible Center | singularity | Elyra/R_Cast | Elyra/R_Impact |
| Tahla Venn | Basic | seed | Mira/Basic_Cast | Mira/Basic_Impact |
| Tahla Venn | Resonant Seed | resonate | Mira/Q_Cast | Mira/Q_Impact |
| Tahla Venn | Growth Veil | heal | Mira/W_Cast | Activation only; no separate impact |
| Tahla Venn | Venn Shelter | shelter | Mira/E_Cast | Activation only; no separate impact |
| Tahla Venn | Wildsurge | surge | Mira/R_Cast | Mira/R_Impact |
| Brakk Orun | Basic | crush | Rook/Basic_Cast | Rook/Basic_Impact |
| Brakk Orun | Wreck Swing | swing | Rook/Q_Cast | Rook/Q_Impact |
| Brakk Orun | Armor Grip | grip | Rook/W_Cast | Activation only; no separate impact |
| Brakk Orun | Wreckstorm | storm | Rook/E_Cast | Rook/E_Impact |
| Brakk Orun | Orun Overdrive | overdrive | Rook/R_Cast | Rook/R_Impact |

## Routing and budget

All clips load once with the scene audio service and use the existing eight-voice stress-limited pool. No extra per-hero AudioSources or runtime audio synthesis. Unique cue IDs retain per-action/phase cooldowns; area impacts play once at the actual center, not once per victim. Basic impacts require real damage/absorption; blocked hits do not confirm. Generic hit sounds are suppressed for hero sources to avoid doubling authored impacts. Ability-only support calls suppress generic heal/shield sounds; existing public Health overloads remain compatible. Cast sounds follow validation, before execution; delayed impacts and volley hits use their existing resolution timing.

High-pass 75 Hz; broad -6 dB presence cut at 2.8 kHz; hero-family low-pass 2.1–2.9 kHz; soft onset/tail; controlled PCM RMS (-29 basic, -26 skill, -25 ultimate dBFS), pre-encode -7 dBFS peak ceiling. Runtime gain keeps basics below abilities and warning voices retain higher priority. Files use mono 32 kHz Vorbis and short durations; decoded float PCM totals approximately 1.78 MiB, excluding Unity overhead.

## Verification

206 C# files passed syntax parsing. 30/30 action identities covered; 53 distinct decoded PCM hashes, all files non-silent, finite and decodable; highest measured peak approximately -7 dBFS. Existing ability specs unchanged. No Unity compilation, hearing test or 5v5/device playback performed. Subjective production quality and perceptual differentiation still require actual listening. Missing clips explicitly log FINAL PRODUCTION SFX REQUIRED, with no generic beep fallback.

Full recipe/source hashes: HeroAudioAssets.json. Rebuild: `python Tools/build_hero_audio.py /path/to/Kenney/Audio` (ffmpeg + numpy). The tool processes recorded assets only.
