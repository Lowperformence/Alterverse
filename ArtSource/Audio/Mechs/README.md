# Mech Gatling sound

Source: user-provided `freesound_community-heathers-gunshot-effect2-100653.mp3` (10 October 2026). This replaces the previously proposed rifle-and-shell-ping sample.

`Assets/Alterverse/Resources/Audio/SFX/MechGun.ogg` is a 0.62-second, 24 kHz mono burst made from three copies of this shot, separated by 50 ms. The initial silence is removed; each copy keeps 520 ms of the report with a 100 ms tail fade. The combined burst is normalized to -5 dBFS before Vorbis encoding. The original upload is preserved here.

Runtime: `DimensionSound.MechGun`, gain 0.35, spatial presentation volume 0.5, ordinary combat priority, 120 ms cooldown, maximum two overlapping bursts within the existing eight-voice audio pool. At full distance gain its decoded peak is approximately -20 dBFS, matching the reduced Nerok basic attack reference. Existing master/SFX volume, attenuation, panning and warning ducking still apply.

One burst is played per attack on the presenting peer. Replicated attacks seek forward by event age rather than replaying a late full burst. The three visible bullets are cosmetic; existing authoritative damage, range and cooldown stay unchanged.
