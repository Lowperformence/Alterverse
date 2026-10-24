# Sivra: blue Nova Small and Nova

Only Light Snare (E, internal slot 2) and Sightline Break (R, slot 3) change. The accepted standard attack, base guardian shots, bomber Nuke and their sizes remain intact.

| Ability | Selected source | Presentation | Existing gameplay |
| --- | --- | --- | --- |
| Light Snare | Blue Nova Small | Native muzzle, compact travelling Nova Small, confirmed native impact, two compact following status planes | 20 damage, 6.8 range, 30% slow for 1.8 seconds |
| Sightline Break | Blue Nova | One cast charge; one native release/flight/contact per existing shot | Three shots at the existing 0.18-second spacing, 64 total damage, 7.8 range |

The importer resolves the actual blue Nova Small and Nova projectile families separately. Native GameObject/Component references take priority for their muzzle, impact and optional status effects. Shared blue Nova muzzle/impact stages may serve both sizes. The previously verified `MagicMuzzleBlue` prefab is an explicitly selected shared muzzle if no native Nova-specific muzzle is supplied. It never replaces a missing Nova Small projectile with an unrelated effect. No 2D Alternate variant is selected. The imported sources are not overwritten.

All selected stages are generated under `Resources/CombatVfx/SivraNova`. The existing URP particle adapter retains source textures, vertex colours, material tints and blend factors. Motion and fragment clipping stay on the existing shot segment; the ultimate does not gain a damage area. Snare status starts from the actual debuff callback, follows the affected unit, lasts at most the existing 1.8 seconds and disappears when the slow ends or the victim dies. The visual presenter never applies damage or changes targeting.

For these two actions, audio also comes from the purchased package. Native AudioSource/controller clip references are preferred, then actual Nova-family clips. When a separate cast recording is not provided, the previously verified purchased `etfx_shoot_magic.wav` / `etfx_shoot_mystic.wav` is a deliberate shot-sound selection. Actual available package Nova/magic/mystic impact clips may provide the corresponding contact sound. The generated source log records each selected file; absent phases stay explicit, never fabricated. Clips are referenced in full, not trimmed. Prefab AudioSources are removed to avoid doubled playback.

Hero audio cue IDs and network messages stay unchanged. Native hit sounds are deferred to the matching confirmed visual arrival. Each of the three ultimate releases can play its own package shot cue instead of being suppressed by the former selected-voice cooldown. Other hero actions retain their original recordings and routing.

Preparation runs after script import and before Play Mode/player builds. Manual menu: `DIMENSIONS FRONT > VFX > Prepare Sivra blue Nova chains only`. Missing mandatory visual roles identify the discovered package paths and retain the prior skill visual at runtime; they are not reported as a completed native import.

Validation: 80 focused importer/presentation/status/audio-scheduling checks and 276 production audio-bank/routing checks pass with controlled fixtures. Existing 4,779 combat/VFX and 1,332 skill-entry assertions pass; accepted Small Bullet and Nuke checks also pass. These checks do not render or listen to the real paid assets. Native import, appearance, complete package sound and Android crowded-combat acceptance remain to be reviewed in Unity.
