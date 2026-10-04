# User-selected hero recordings — 2026-10-04

The 15 exact user-supplied MP3 filenames are preserved in `Originals/`. `manifest.json` records original SHA-256 hashes and all 17 ability mappings. The selected dash recording is shared by Gapfold, Rift Step and Phase Fold. Wreckstorm is Rook E, Chrono Snare is Traveler E, Breaker Wave is Nerok E (verified against `HeroAbilityKit`).

Rebuild: `python3 Tools/Audio/import_hero_selections.py /path/to/source_mp3s` (requires ffmpeg).

Game clips are mono 44.1 kHz Vorbis at the existing `_Cast.ogg` paths; Unity asset GUIDs remain unchanged. Leading digital silence is removed with 1 ms preroll. Full recordings/reverb tails are retained, with a 2 ms fade-in and 10 ms fade-out. PCM peak is normalized to 0.63; decoded Vorbis files were checked for finite samples and peaks below 0.95 (actual maximum 0.656).

The three filenames `yodguard-short-energy-beam-shot-4-482500.mp3`, `...482500(1).mp3`, and `...482500 (1).mp3` are byte-identical in the supplied files. They are still explicitly mapped to the requested abilities; no alternative version was substituted.

Each selected cue plays once at the existing ability-release event. Old synthesized Windup/Impact layers for those slots are disabled in `HeroCombatAudio` and their unused resources removed. This also prevents an area attack playing the entire recording separately for every victim. Basic attacks, Nerok's quiet basic hit, ultimates, death/hitmarker sounds, network cue indices, cooldowns and damage remain unchanged.

Validation: every source decoded, exact filename/hash mapping recorded, all 17 output clips decoded again, clipping headroom verified, C# syntax parsed. Unity Editor unavailable: run `DIMENSIONS FRONT > Checks > Selected hero sound recordings` and listen in Play Mode for final mix approval. No Unity playback test is claimed.

Nerok's Abyssal Maelstrom (R) uses the separately supplied `Originals/dragon-studio-elemental-spell-impact-water-478377.mp3`. Its exact bytes replace `Assets/Alterverse/Resources/Audio/Heroes/Nerok/Ultimate_Selected.mp3`, retaining that Unity asset's `.meta` GUID and its existing single playback at the vortex release. Nerok R's former Windup/Impact cues are disabled and their unused assets removed. This brings selected ability mappings to 18; the original 15-source manifest and its 17 mappings remain the record for the earlier batch.

Nerok Anchor Recall (Q) now uses the exact supplied `Originals/freesound_community-steel-chain-dragged-shower-reverb-106252 (1).mp3` at `Assets/Alterverse/Resources/Audio/Heroes/Nerok/Q_Cast.mp3`. The previous Q cast WAV is replaced while preserving its importer GUID; Q Windup/Impact layers are removed and disabled. The chain plays once at hook release, including multiplayer playback. There are now 19 selected ability mappings. Source SHA-256: `dca9119831eab25855d5796eed845f381875ddcb90d52d389467243010ce650a`. Full MP3 decode verified; Unity playback remains to be checked in Play Mode.

Shared recordings: Lumen Wall (Kaelon W), Armor Grip (Rook W), and Pressure Hull (Nerok W) now use Venn Shelter (Mira E). Vector Shift (Nyra W) and, by user request, Temporal Beam (Traveler Q) use Gapfold (Veyr E), matching Rift Step and Phase Fold. Temporal Beam gameplay remains its existing beam; this change assigns its requested audio only. All five slots suppress legacy windup/impact layers. There are now 24 complete-cue selected mappings.

Latest Brakk overrides: `brakk-recordings.json` maps the two supplied MP3s to Rook E (Wreckstorm: Cinematic Hit Shot) and Q (Wreck Swing: Boom Swoosh). This supersedes the original batch Wreckstorm mapping; retain these overrides after a batch rebuild. Runtime OGG clips use mono 44.1 kHz, PCM peak 0.63 and short endpoint fades, and were decoded again to verify nonempty, unclipped audio. All 32 ability slots now have selected recordings; seven non-Nerok basic attacks retain legacy audio.
