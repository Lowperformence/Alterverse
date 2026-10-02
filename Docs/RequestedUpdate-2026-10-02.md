# Gameplay, cosmetics and shop update

- Hero respawn delay multiplied by 1.20 after the authored progression/cap, for humans and bots: 4.8, 6, 7.2, 8.4, 9.6, 10.8, 12 seconds. Remote humans use the same hero countdown.
- Short voiced HA marker plays on hero death, including a client's first dead network snapshot. Existing kill-confirmation feedback remains.
- Brakk outgoing damage multiplied by 1.05 at Health's incoming-damage boundary. Tank base HP multiplied by 1.05, preserving the existing global durability adjustments and Traveler's latest HP bonus.
- Tahla healing powers increased by 1.08 in the shared ability catalog, so both combat and menu values agree. Healing W: 45.36; healing ultimate: 62.64. Shield powers unchanged.
- Tahla's long robe islands/embroidery are removed from the authored FBX and Blender source, replaced by four light plates bound independently to thigh/shin bones. Existing newer Ione leg armor is preserved.
- Tank shield receives raised purple-emissive DR geometry, rigidly weighted to WeaponGripL. Its inscription keeps its color under cosmetic overlays.
- Epic/Legendary energy overlays use 0.50 opacity over the opaque original weapon. Original geometry/material detail remains visible.
- MOOR12C and PYRAXIS cost 800 shards each; PSR costs 1200. Shop listing, persistent ownership, affordability, selection locks and duplicate-charge protection use the existing transaction path.
- Ten additional avatars use IDs 17–26; ID 16 remains the Beta Crown. All new avatars cost 450 shards, matching existing entry-price avatars. Ownership/equip use the existing shop and profile systems.

## Avatar assets and prompts

Generated using the built-in Imagegen tool. Common prompt: premium hand-painted square sci-fi/fantasy combat-game avatar, strong silhouette readable at 64px, centered subject, vivid rim lighting, dark cosmic backdrop, crisp details, no watermark/border.

| ID | Resource path | Subject prompt |
| --- | --- | --- |
| 17 | Assets/Alterverse/Resources/Avatars/Avatar17.jpg | Smug space cat, mirrored sunglasses, purple astronaut collar |
| 18 | Assets/Alterverse/Resources/Avatars/Avatar18.jpg | Furious tiny yellow duck in oversized futuristic combat armor |
| 19 | Assets/Alterverse/Resources/Avatars/Avatar19.jpg | Sleepy friendly robot hugging a steaming coffee mug |
| 20 | Assets/Alterverse/Resources/Avatars/Avatar20.jpg | Joyful green alien in headphones with pink/violet disco highlights |
| 21 | Assets/Alterverse/Resources/Avatars/Avatar21.jpg | Solemn dark knight, narrow violet visor |
| 22 | Assets/Alterverse/Resources/Avatars/Avatar22.jpg | Noble silver frost wolf with icy blue eyes |
| 23 | Assets/Alterverse/Resources/Avatars/Avatar23.jpg | Majestic fiery phoenix with orange/gold feathers |
| 24 | Assets/Alterverse/Resources/Avatars/Avatar24.jpg | Determined astronaut; text NEVER GIVE UP |
| 25 | Assets/Alterverse/Resources/Avatars/Avatar25.jpg | Clasped robotic hands beneath a shield; text TEAM FIRST |
| 26 | Assets/Alterverse/Resources/Avatars/Avatar26.jpg | Friendly purple monster, thumbs up; text GG |

## Validation

- 17 Python commerce tests passed, including all ten new avatar purchases, retry safety, insufficient balance, persisted ownership after restart and account isolation.
- Changed/local C# files parsed without syntax errors.
- Ten avatar images decoded successfully, 512×512, unique catalog IDs and 450-shard prices.
- Tahla FBX reimport: 19 bones, nine actions, normalized skin weights, preserved separate weapon renderer.
- DR: 244 triangles in the dedicated material submesh, all weighted to WeaponGripL; source preview inspected.
- Updated Unity editor checks cover MOOR12C/PYRAXIS/PSR prices, insufficient balance, exact-price purchase, selection and duplicate-charge protection. These editor checks have not been executed here.
- Unity compilation, in-game audio audition and rendering remain to be checked in Unity; the editor is unavailable in this execution environment.
