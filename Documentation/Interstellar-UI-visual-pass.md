# Interstellar menu and first figure pass

## Implemented

- Home is the entry/return page. It uses a full-screen orbital painting, live selected hero, left navigation, profile/XP badge and linked deployment, battle-pass and mission cards.
- All existing menu pages use the same left navigation and dark glass/champagne theme. Existing match, inventory and purchase handlers remain in place.
- Bundled Rajdhani Regular/SemiBold fonts cover the shared IMGUI skin, frontend styles and uGUI ability labels, including German characters. Latin/symbol subsets ship with the original SIL OFL license.
- Settings overlays use the new safe-area layout. Voice volume and quit confirmation have separate vertical slots.
- All seven heroes receive plate-mounted rail/status details before existing mesh batching, with baked coating variation and warmer portrait lighting. Assassin/healer accents follow the red/green reference. Ranger retains the rifle as requested.
- Shared monster armor meshes use crisp bevel normals and smoother head cross-sections. Hotzone materials retain separate roughness for organic surfaces, joints, metal and energy. The Mars skeleton receives steel/servo materials, orange reactor hardware, limb-mounted servo covers and a separate team beacon.

## Validation and remaining work

Passed C# tree-sitter syntax parsing, git whitespace checks, font parsing and required glyph coverage. Verified the generated background visually and checked navigation/overlay coordinates in source.

Unity is not installed in the execution environment. Compilation, shader import, runtime layout, hero attachment placement, mobile performance, purchases and gameplay have NOT been tested in Unity. This branch should receive those checks before merging.

The figure changes are a first visual pass on existing procedural models. They are NOT full high-detail 3D reproductions of the seven-character concept image. Detailed replacement meshes, textures and visual acceptance remain outstanding. Existing movement controllers were not replaced in this change.

Suggested Unity checks: enter Home; open all eight navigation destinations and return; click all three home cards; inspect all seven original heroes and owned cosmetics; start/finish a match; verify ability labels and settings; inspect Hotzone creatures on each arena; check Console and Android build. Include 16:9, ultrawide and notched landscape safe areas.

## Artwork provenance

`Assets/Alterverse/Resources/FrontEnd/InterstellarBackdrop.png` was created with the built-in image-generation tool for this project. Prompt: cinematic science-fiction orbital city, giant blue-gray planet and pale gold rings, navy starfield/violet nebula, ships at right, dark negative space at left for live UI, empty platform for a separately rendered hero; no text, UI, characters or watermark. A second edit removed unwanted marks at the bottom right while retaining the scene.

Fonts: Indian Type Foundry Rajdhani, fetched from google/fonts ofl/rajdhani. Original copyright/license is included as `Resources/FrontEnd/OFL.txt`.

## Latest creature reference

Target directions: Neptune octopus with emissive tentacle detail; Pyraxis lava scorpion; industrial ROBOT SKELETON on Mars (the reference label “Robot Bear” was corrected by the user); crystalline frost beast on Kryon; organic verdant horror on Green Planet; lunar armored warden. Keep the existing Mars skeleton and Kryon creature as distinct enemies. This commit does not create new enemy types.
