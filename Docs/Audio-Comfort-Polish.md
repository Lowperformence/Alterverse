# DIMENSIONS FRONT — audio comfort pass

Nine existing CC0 SFX edited: Attack, HitLight, HitHeavy, AbilityImpact, TowerShot, TowerImpact, TowerTarget, UiConfirm, UiBack. Sources/rights unchanged; see ThirdPartyAudioLicenses.md. Music files are unchanged.

Offline processing: broad 3.2 kHz EQ cut (Q 0.8, -3 to -5 dB), two-pole low-pass at 3.8–5 kHz, 8 ms onset fade, continuous sample timestamps, Vorbis quality 3. No loudness normalization afterwards, so the reduced high-frequency energy is not gained back. Measured integrated energy above 3 kHz fell 5.1–9.7 dB relative to the preceding imports. This measurement does not prove subjective comfort.

Runtime: common SFX gains reduced to 72–90%; tower-target warning gain retained. Repeated attack/impact/UI cues use 100–200 ms minimum intervals; target warning uses 650 ms. Low-priority cues allow at most two overlapping instances of the same cue. Objective/warning voices cannot be replaced by ordinary combat cues. Total pool remains eight voices, subject to the existing stress budget. Subtle +/-1.8% pitch and +/-3.5% gain variation uses a local deterministic sequence rather than consuming gameplay random state.

Validation: all nine edited clips decode without FFmpeg errors; 205 C# files pass syntax parsing. No Unity compile or listening test performed. Remaining acceptance: listen to a sustained fight with frequent attacks/tower fire on the target phone; verify pleasant repeated feedback and clearly audible warnings at normal volume. Do not retune the unchanged music as part of this pass.
