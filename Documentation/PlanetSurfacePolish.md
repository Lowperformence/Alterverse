# Planet surface material pass — 2026-09-26

## Integration

Based on dev/vertical-slice f412cea. PrototypeBootstrap.BuildArena creates ProductionLunarGround for Moon, Mars, Green Planet, Noxara and Kryon. This pass modifies that runtime builder and PlanetSurfaceDetailRuntime, not a demonstration scene.

The existing 1536-square unique albedo and RGB normal fields remain unchanged. The original palette constants, relief sampling, crater anchors, geometry, map extents, spawn positions, colliders and gameplay scripts are unchanged. No extra props or physics objects are added.

A dedicated Resources-backed PlanetSurface shader is assigned to the live ground, dark/rim rock materials, perimeter fracture faces, cliff strata and both crater materials. Other runtime shaders, heroes, UI, metal props and emissive accent materials are untouched.

## Surface work

- Reference-inspired warped sand ripples, fine grain and stone fractures, with different detail strength for each existing planet.
- Packed linear slope/roughness/value fields generated once per arena. Two 256-square RGBA8 mipmapped textures: 699,048 GPU bytes, approximately 0.67 MiB. CPU texture copies are released. No TextureImporter or Android import compression applies to these runtime-created maps; no claim of ASTC compression is made.
- Trilinear filtering and 4x anisotropy on the new data. Existing unique atlas resolution/filtering retained. Shader variations are scalar; the planet hue is not recolored.
- World-space detail for the ground and triplanar stone detail for UV-less rock/cliff meshes; macro warp and a rotated second frequency reduce obvious repetition.
- Quieter detail around the central court and base approach; this is a shading mask only, not a new lane layout.
- Crater skirt vertex weights blend back into the existing unique ground colour, with no change to crater vertex positions.
- Actual URP main-light direction and shadow attenuation, ambient fill, restrained roughness response and cavity shading. Includes shadow caster/depth passes, SRP-compatible material buffer and instancing variants. No SSAO, parallax, transparency or extra light is introduced.
- Existing spatial batching retained; the main ground is now excluded by material for all variants (previous name-based exclusion only matched the Moon).

## Verification status

Source checks performed: live builder/camera path traced; original colour constants and surface field retained; edits restricted to visual assets/editor validation; crater colour array counts checked against the existing topology; packed height data periodicity/bounds checked; git diff whitespace checked; upstream changes compared before committing.

NOT VERIFIED: Unity C# compilation, shader compilation on Vulkan/GLES, actual game-camera appearance, each planet's hero/ability readability, draw-call measurements, Android FPS/GPU time, thermal stability. This execution environment has no Unity Editor or attached Android device. Do not describe this pass as visually approved or 60-FPS verified.

## Required in-project review

1. Open with the project's Unity 6000.6.1f1 / URP 17.6.0 and let shaders compile. Build preprocessing fails if the surface shader reports compile errors.
2. Launch a real match for each of Moon, Mars, Green Planet, Noxara and Kryon. Wait for terrain/crater initialization. Use DIMENSIONS FRONT > Visuals > Validate active planet surface to check live shader/data bindings and absence of presentation colliders.
3. At the actual follow-camera position, inspect centre, bases, crater interiors/rims and perimeter rock groups. Use Visuals > Capture gameplay surface to write actual camera frames to Temp/PlanetSurfaceReview. These are captures of the running game, not synthetic previews.
4. Compare with the previous revision using the same camera, hero and quality setting. Check retained palette, no repeating bands, no shimmering while moving, readable spells/minions, no dark crater seams and no pink materials.
5. On target Android devices compare Balanced/Battery/Performance frame timings, batches, texture memory and a sustained match. New detail is ~0.67 MiB; the existing runtime atlas and its initialization cost were not redesigned. Texture samples excluding shadows: ground 4, crater 3, stone 3. Actual extra shadow-pass cost must be profiled.
6. If needed reduce SandStrength/RockStrength before increasing texture size. Do not alter colours, colliders or paths to fix readability.
