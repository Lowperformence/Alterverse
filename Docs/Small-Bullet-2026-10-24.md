# Small Bullet: Sivra basic and base guardians

The selected scope is Sivra's standard attack (`HeroId.Nyra`) and the base `HealingGuardMech` shots on all arena variants. Other hero skills and non-guardian monsters retain their existing presentation.

The focused importer resolves the purchased **3D Small Bullet** projectile plus its native muzzle and impact prefabs. It follows serialized native controller references where available, otherwise searches the same Small Bullet family. It excludes 2D Alternate variants and requires all three parts. A diagnostic lists the chosen original asset paths after preparation. The purchased sources remain untouched; generated resources are under `Assets/Alterverse/Resources/CombatVfx/SmallBullet/`.

The presenter shows the muzzle, moves the small projectile along the existing attack trajectory and presents the native impact on confirmed contact after visual arrival. It does not apply damage. Misses do not produce invented hits. Existing delivery cancellation and owner death cancel flight. The base mech retains its existing three-round cosmetic burst and one original burst sound. Non-mech guardian appearances also receive the Small Bullet chain; their existing one-shot presentation cadence remains.

Projectile/trail fragments are constrained to the original shot segment with a 0.10-unit visual radius; contacts use a compact 0.18-unit radius. The native shapes, material tints, textures, bursts, particles and sub-emitters remain; demo scripts, prefab audio and colliders are stripped. Authored colour-only materials such as `solid_AB` use a neutral white texture instead of failing import.

Preparation runs after script import and before Play Mode/player builds. Manual recovery: `DIMENSIONS FRONT > VFX > Prepare Small Bullet chain only`. If the complete chain cannot be resolved, the importer reports the actual discovered paths and runtime retains the original attack visual. No partial chain is published as a valid library.

The focused suite passes 88 source-chain, flight, confirmed-impact, cancellation and guardian/audio contracts using controlled engine/editor fixtures. Existing combat/VFX tests cover the unimported fallback. These checks do not establish that the actual purchased prefabs have been resolved or rendered on the user's computer; native import and Play Mode acceptance are still required.

The Nuke importer also reports `NukeConeExplosionFire PREPARED` and the first actual playback reports `NukeConeExplosionFire ACTIVE` with its measured radius/scale. These development-console messages distinguish successful native-prefab playback from the original visual fallback. No player-facing debug UI is added.

The dedicated VFX Android agent reviewed this implementation: renderer/property-block caches, preserved native loop flags, endpoint-aware concurrent-hit assignment, and held flight parts avoid repeated spawning/early destruction. The URP adapter supports texture, vertex colour, tint and blend; custom native shader-driven dissolve/distortion still requires inspection of the actual paid asset before claiming parity with its preview.
