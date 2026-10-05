# Combat, shop and ranking audit — 2026-10-05

Reviewed all eight hero basics and 32 active abilities. See `Hero-Ability-Audit-2026-10-05.md` for the individual ability mapping.

Fixed authority checks, bot keyboard input, attack presentation timings, temporary-shield checks, planar area targeting, brief-stun cancellation, crowd control after rejected damage, expired speed boosts and invulnerable targets in Nerok's vortex. Added disposable Unity Play-mode regressions for all hero basics and abilities.

Planet Guard retains 630 HP (900 × 0.70). Its buff now gives **only 20% damage reduction**; the minion damage and movement bonuses were removed centrally and from HUD text. Flux remains separate.

The Shards shop now retries billing connection/product recovery failures and reports confirmation failures correctly. Nerok's two weapon skins were added to the server catalog and tested for purchase, equip and persistence. Google Play ranking now uses the plugin's direct Android APIs, manual authentication on a user retry, guarded asynchronous reporting/UI requests, and immediate reporting after co-op wins.

Validation: 35 commerce backend tests passed. Local C# sources passed a syntax parse. Blender inspection confirmed both Zhaev blades are rigidly weighted to their respective WeaponGrip bones, and simulated split FBX vertices identify the marked gold terminal contact on both sides. This environment has no Unity editor or C# compiler: Play-mode, shader compilation, Android billing, animation contact and online integration checks remain unexecuted.

## Required service configuration

- `Assets/Alterverse/Resources/Commerce/AccountServerConfig.json`: `httpsEndpoint` is empty. Set the real deployed account/commerce HTTPS endpoint; shard purchases require verified server responses.
- `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset`: `mAppId` is empty. Run the Google Play Games Android Setup with the real Play Console Android resources. The generated Android manifest also has no valid game App ID. The existing Most Wins leaderboard ID alone does not configure sign-in. Verify published resources, tester access and the build's signing certificate on Android.

These service settings were not invented or bypassed. The UI now identifies incomplete configuration explicitly.
