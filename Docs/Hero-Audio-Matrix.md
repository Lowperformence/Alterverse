# Combat audio — current seven-class matrix

Replaces the older 53-file documentation and all 62 pre-overhaul hero audio files. There are now **121 hero clips**: 35 releases/activations, 27 confirmed impacts/support resolutions, 27 preparations, and 32 additional recorded variants. Resource names and all existing .meta GUIDs are retained.

| Hero | Sound character | Q / W / E / Ultimate | Audition start |
|---|---|---|---:|
| Zhaev Null / Veyr | Dry blades, knife draw, short cut contact | Null Cut / Rift Scar / Gapfold / Vacant Echo | 0:00 |
| Auren Kest / Kaelon | Heavy metal and plate, low impact body | Anchor Strike / Lumen Wall / Gravity Anchor / Kest Citadel | 0:05.10 |
| Sivra Quell / Nyra | Recorded bolt-action rifle shots, short outdoor tail, subtle mechanism | Prism Shot / Vector Shift / Light Snare / Sightline Break | 0:11.06 |
| Ione Vaska / Elyra | Slowed creaking buildup, mineral fracture and dark discharge | Orbit Fracture / Invert Field / Phase Fold / Impossible Center | 0:17.29 |
| Tahla Venn / Mira | Soft glass/bell layers, delicate movement, clear attack transient | Resonant Seed / Growth Veil / Venn Shelter / Wildsurge | 0:23.86 |
| Brakk Orun / Rook | Cloth air movement, heavy punch and soft body contact | Wreck Swing / Armor Grip / Wreckstorm / Orun Overdrive | 0:29.56 |
| The Traveler | Reversed medium glass, wood fracture, 47 ms temporal echoes | Temporal Beam / Rift Step / Chrono Snare / Paradox Collapse | 0:35.10 |

`Combat-Audio-Audition.mp3` / `.ogg`: approximately 41 seconds. Each class: three basic takes, Q, W, E, R. Nyra R includes the four-shot .18-second cadence. Representative ranged flight is .14 seconds; actual game flight remains distance-dependent. The common audition gain is for browser playback, not a change to runtime master volume. Listening approval on real devices is still pending.

## Trigger routing

- `HeroAttackDelivery`: preparation on accepted basic/targeted ability queue; release on the existing launch timestamp; confirmed contact via `HeroCombatRules.ResolveBasicHit` or `HeroAbilityExecution`.
- Delayed single/area abilities: preparation at acceptance, release at the existing resolution timestamp. Area contact plays once only if damage/shield absorption occurred. Tahla's resolution requires actual healing/shield addition.
- Dash, healing and shield activations stay on their accepted execution path. No fake impact bank for utility-only actions. Nyra R has one preparation and four individual releases.
- Generic target-hit audio is suppressed for hero, tower and bunker sources that already emit authored contact audio. Tower/bunker contact checks actual HP/shield reduction. Reapplying an unchanged generic shield no longer produces a new shield sound.
- Hero, tower, healing, shield, death and explosion recordings load once. The old runtime-synthesized hero death sound is removed. Shared warning/UI identities remain, with summing headroom.
- Existing scene service uses 5/7/8 pooled voices by mobile quality. Per-actor cue cooldowns prevent another copy of a hero from silencing the player. Three separately recorded takes rotate for every basic phase and Nyra R; no gameplay Random consumption.
- Local hero cues have priority 2, distant combat 0/1, important warnings 3. Warnings cannot be stolen by hero ultimates and briefly duck combat. World cues attenuate with ground-plane distance and use restrained stereo pan. Local cues remain centered.
- Co-op presentation messages relay authoritative phases to clients before host camera culling. They carry only slot/action/phase/position, accept only the server, and never modify combat state. Client death snapshots trigger a single presentation sound. Networking and latency still require a real session test.

## Validation boundary

`CombatAudioBefore.json`: decoded audit of all original 62 files. `CombatAudioValidation.json`: current output checks. Offline checks cover C# syntax, all 35 action resource mappings, every decoded file, non-silence, finite PCM, distinct frequent variants, GUID uniqueness/preservation, conservative eight-voice peak summing and repeated .18-second rifle volleys. Balance catalog and hero identity values are unchanged.

**Not performed:** Unity compilation, Unity Console inspection, actual normal-attack/ability gameplay, 5v5 runtime profiling, multiplayer session, phone speaker/headphone listening. Unity is unavailable in this execution environment. Offline summing is not a runtime or subjective quality test and does not cover simultaneous music/bomber sources outside the combat pool.

In a disposable Unity Play Mode match, verify all seven basics plus Q/W/E/R against a valid target, damage-gated target, empty area, shielded target and moving/out-of-range target; cancel windups by stun/death. Then use duplicate hero classes in 5v5 and test warning audibility, four-shot volleys, client/host perspectives and the 5-voice battery setting. Confirm a clean Console and audition on phone and headphones before a production release.

Sources and licenses: `ThirdPartyAudioLicenses.md`, `ArtSource/Audio/Combat/sources.json`, and `CombatAudioAssets.json`. Rebuild: `python Tools/build_combat_audio.py`; offline checks: `python Tools/validate_combat_audio.py`.
