# Combat audio balance — 10 October 2026

The basic-attack reference is Nerok's existing `Basic_Impact.wav` with its playback gain reduced by 30% (multiplier 0.70). All seven other heroes' basic recordings are adjusted to that reference. The ability reference is the current Paradox Collapse recording, `Traveler/Ultimate_Selected.ogg`, with its existing playback gain unchanged. All other ability and ultimate main cues are adjusted to that reference.

## Measurement and scope

Measurements use decoded audio plus the local player's code gain, with default master/SFX settings, without ducking. These are digital levels, not acoustic decibels at the player's speakers. Basic attacks use the loudest 100 ms RMS window per hero because their short cues often cannot be measured reliably with integrated LUFS. One common adjustment per hero preserves the relative levels of basic cast, impact, preparation and variants. Ability main cues use integrated LUFS; quiet ultimate preparation and impact layers remain unchanged.

| Reference | Target at local playback | Result across heroes/cues |
| --- | --- | --- |
| Reduced Nerok basic | −31.46 dBFS, maximum 100 ms RMS | Hero maxima within 0.43 dB |
| Current Paradox Collapse | −22.88 LUFS | All 32 main ability cues within 0.51 LU |

79 audio assets were changed. All 98 active recordings were measured after encoding. The reference audio files remain byte-identical; Nerok's reduction is applied in `HeroCombatAudio.cs`. Existing Unity `.meta` files and GUIDs are unchanged. File formats, channel counts and sample rates are preserved, and decoded duration changes are below 30 ms. Existing silent preparation placeholders remain silent.

The offline pipeline uses a limiter without automatic makeup gain, compensates its latency, preserves the full cue, and checks reconstructed peaks after encoding. Changed files remain below −1.4 dBTP at source and within the selected reference's peak at estimated local playback. This does not guarantee that simultaneous live sounds cannot overload the combined game mix.

The previous Traveler and Nerok ability gain modifiers, remote-player attenuation, distance attenuation and ducking remain in place. The new recordings compensate for the existing per-ability gain when matching the reference.

## Validation and reproduction

`CombatVolumeBalance-2026-10-10.json` contains all post-encoding measurements and SHA-256 hashes of the changed audio. `CombatVolumeAuditBaseline-2026-10-10.json` contains the original measurements from commit `b7fb899d08b621df298b7e95f0244bbb7ef646a9`.

To reproduce, use an original checkout of that commit as the source and a separate output directory. Python, numpy, ffmpeg and ffprobe are required:

```sh
python3 Tools/Audio/balance_combat_volume.py \
  --source /path/to/original-checkout \
  --audit Docs/CombatVolumeAuditBaseline-2026-10-10.json \
  --output /path/to/separate-output
```

Do not run the pipeline on recordings that have already been balanced. Copy only generated assets into the project and retain the original Unity metadata. The Nerok basic gain change must also be present in `HeroCombatAudio.cs`.

Unity import, listening tests and an Android build were not available in this environment and still need verification in the editor/device.
