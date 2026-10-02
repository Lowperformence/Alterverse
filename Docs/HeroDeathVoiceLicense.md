# Hero death sound — Special Box

The active HeroDeath cue now uses the user-selected Mixkit "Open special box" sound (asset 3190), replacing the voiced HA.

Private project source: ArtSource/Audio/Combat/HeroDeathSpecialBox.wav.
Original upload: mixkit-open-special-box-3190.wav.
SHA256: ed09d1f36af5f74734afffe5e6797db4b251b139e30c47c8e6945af265f5af89.
Source listing: https://mixkit.co/free-sound-effects/hit/?page=4
License: Mixkit Sound Effects Free License.
License page checked 2026-10-02: https://mixkit.co/license/modal/sfxFree/

The license permits incorporation into commercial video games and modification. It prohibits standalone redistribution and redistribution with source files. Keep this source in the private project; do not include it in a public source release or sound library. The game runtime uses the processed clip as part of the finished game.

Tools/build_combat_audio.py rebuilds Assets/Alterverse/Resources/Audio/SFX/HeroDeath.ogg from that source. Processing removes leading silence, keeps 0.65 seconds of the opening sound, removes sub-70Hz rumble, applies a short ending fade and preserves the existing -22dBFS peak budget. No voiced exclamation, pitch change or new synthesized layer is used. The existing death and multiplayer transition playback remains unchanged.

Validation: generated Ogg decoded successfully at 32kHz mono; decoded duration 0.646s and peak -22.10dBFS, no clipping and faded ending. Live Unity playback remains unverified.

## Archived voice source

ArtSource/Audio/Combat/HeroDeathHa.wav is the previous CC0 source trimmed from attack2.wav in Brandon Song (wolfwoot)'s RPG Male Adventurer pack:
https://opengameart.org/content/voice-clip-pack-male-adventurer-rpg

It is no longer used to generate the active HeroDeath cue.
