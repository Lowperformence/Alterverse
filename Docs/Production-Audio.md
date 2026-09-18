# DIMENSIONS FRONT — licensed audio integration

The repository now contains two CC0 music recordings by yd and 29 edited cues from Kenney Sci-fi Sounds. The imported OGG payload totals approximately 3.4 MB. Exact source filenames, source URLs, licenses, hashes, modifications and intended uses are recorded in [ThirdPartyAudioLicenses.md](ThirdPartyAudioLicenses.md). The original Kenney license is retained in AudioLicenses.

Music/Menu is Observing the Star; Music/Match is Out There. Pressure and climax reuse Match with intensity changes rather than restarting it or loading duplicate recordings. Victory/defeat fade the music bed and use their SFX cues. This is a two-track integration, not six separately composed scores.

Long music uses streaming Vorbis, 32 kHz stereo, no preload. Short SFX use mono 32 kHz Vorbis and decompress on load. Two music sources crossfade; eight pooled SFX voices and cue throttles bound overlapping sound. Camera-relative culling limits offscreen effects. Live settings and existing pause/focus controls are retained. Purchase, minion death and ability impact now have dedicated hooks.

Licenses permit commercial redistribution without required attribution; credits are retained voluntarily. No synthetic placeholder test tones are included. Some source recordings serve several related events with distinct edits; six fully bespoke hero sound identities remain future sound-design work.

## Verification boundary

All 31 OGGs are decode-checked using FFmpeg. Import settings and cue/resource names are statically checked. Listening, loop seams, subjective mix quality, Unity importer behavior, Android audio latency, memory and performance: **NOT VERIFIED**. Final speaker/headphone audition remains necessary before release; asset presence does not establish final sound quality.
