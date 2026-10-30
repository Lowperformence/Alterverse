# Photo panorama map backgrounds

Eleven photorealistic image assets are integrated for Moon, Mars, GreenPlanet, Noxara, Kryon, Earth, Pyraxis, Neptune, Dark00 DiamondCore and GJ504b. They are generated illustrations, not documentary photographs.

`CosmicArenaBackdrop` first loads the selected map image through `PhotoArenaPanorama`. When available, the procedural void plane, star point meshes and sphere planets are not constructed. The existing procedural backdrop remains a fallback for missing assets. Battlefield geometry, actors, combat, input and planet lighting are unchanged.

## Rendering and lifecycle
The panorama is assigned to a Skybox component only on the main camera with MobaCamera, never to portrait cameras or global RenderSettings. Rotation aligns the planet with the gameplay camera. The skybox is at infinite distance and follows camera rotation rather than player translation. The previous camera clear mode and skybox are restored on disable/destruction. The match-owned material and selected Resources texture are released at destruction; only one panorama is loaded per match.

The project-owned shader lives in Resources to provide a player-build dependency. Textures are opaque 1774x887 PNGs (2:1), imported as sRGB, uncompressed, non-readable, without mipmaps or NPOT resizing, repeat horizontally and clamp vertically. PC and Android use the same shader and import settings, with no platform brightness multiplier. One loaded RGBA image occupies approximately 6 MiB; the eleven source PNGs total approximately 25 MB. Avoiding the previous procedural backdrop removes its runtime meshes and materials in the normal path.

## Verification
Checked: all eleven enum variants resolve to an image; dimensions are 2:1; new GUIDs are unique against the existing project; C# syntax; shader projection uses full sphere longitude/latitude; default camera yaw maps to texture center. Generated outputs were visually inspected.

Unity compilation, skybox drawing and PC/Android screenshots were not available in this workspace. Check the background at both base edges, match restart/menu restoration, mobile color parity and memory release in Unity. The generated panorama seams and poles also need inspection if the camera is later allowed to rotate beyond its current fixed orientation.

## Image generation
Created with the built-in image generation tool. Assets: `Assets/Alterverse/Resources/MapPanoramas/<ArenaVariant>.png`.

Shared prompt: Photorealistic space skybox for Dimensions Front; clean 2:1 equirectangular full-sphere panorama; seamless matching horizontal edges, calm poles; prominent planet limb in lower middle, sparse stars and low-contrast nebula; designed for a floating arena with a camera pitched approximately 50 degrees downward; no HUD, text, border, collage, arena or characters.

Scene prompts by map:
- Moon: silver cratered Moon, distant Earth, violet dust.
- Mars: rust-red planet, canyons, warm dust and small moons.
- GreenPlanet: emerald planet, turquoise atmosphere and clouds.
- Noxara: indigo planet with violet nebula filaments.
- Kryon: white-blue glacial world and cyan aurora.
- Earth: oceans, white clouds and night city lights.
- Pyraxis: volcanic planet with restrained orange molten fissures.
- Neptune: blue gas giant, cyan clouds and pale moons.
- Dark00: nearly black planet, subtle violet rim and dim stars.
- DiamondCore: silver crystalline planet, iridescent facets and lilac/cyan cosmic dust.

- GJ504b: magenta sandstone planet, rocky ridges, dusty dunes and pink-violet atmosphere.
