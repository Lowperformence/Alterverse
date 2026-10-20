# Premium weapon finish and white Deluxe Wings — 2026-10-20

The weapon-shop colorways previously used a flat translucent color layer and the same small floral pattern regardless of the selected hero style. The existing overlay now combines a restrained brushed-alloy surface response with larger fluorescent runes. Its metal layer uses the project's shared URP PBR lighting; the rune and edge color remains independent of scene illumination.

Four actual patterns follow the existing hero grouping: circuit chevrons (Nyra/Traveler), arcane wards (Elyra/Mira), forge marks (Kaelon/Rook), and split-rift sigils (Veyr/Nerok/VoidKnight). Both purchased colorways retain their catalog colors. The base finish opacity is 0.24; rune cores can reach full opacity. Dark borders suggest an inset around the luminous strokes. Analytic brush detail fades with pixel footprint to avoid distant stripes.

Coordinates and projection normals are stored on an owned overlay mesh in UV channels 2/3 before skinning. Projection therefore does not switch when the weapon rotates or bends. Source meshes/materials remain intact; switching back removes the owned overlay. The overlay now inherits the source renderer's light/reflection probes. It adds no new renderers beyond the pre-existing overlay, textures, lights or particles. PBR changes fragment work, so Android GPU cost still needs measurement.

Deluxe Wings retain their silver/crystal surfaces and existing sparse energy facets. Those facets now use neutral white base/emission with the same Resources-backed unlit shader on desktop and Android. The existing emission animation stays neutral white. The shop/inventory icon was recaptured from the actual exported production geometry, using the new white material intent. This is a controlled studio render, not a Unity screenshot. The updated LegacyShopGeometry.json can also rebuild the Blender shop scenes. ArtSource/Wings/render_deluxe_gl.py reproduces the current icon without Blender.

No previously downloaded surface add-on was identified. This change uses existing project shading and original procedural patterns, with no third-party art asset or new runtime package.

## Verification

- Existing production-wing fixture suite plus Deluxe cases: 761,560 assertions pass across all ten heroes in game and preview modes. Deluxe checks cover paired construction, shared meshes/materials, nonempty silver/crystal/white submeshes, repeated builds, white animated emission and owned-resource cleanup. These are engine fixtures, not native Unity.
- OriginalWeaponColorwayOverride.cs parses without C# syntax errors under Roslyn. This is not a Unity assembly compilation.
- Extracted rune/brush math, mechanically translated to GLSL and executed with Mesa/EGL: 114 assertions over 24 fixtures (four styles, two offsets, three resolutions) pass for finite values, opacity, luminous coverage, visible non-rune surface and distinct styles. This checks the pattern math only, not the complete HLSL shader or URP lighting.
- The 384×240 Deluxe icon was rendered from the production presenter's 896 triangles, with finite HDR output and no geometry edits, and visually inspected.

Native Unity 6000.6.1f1 shader/C# import, gameplay/shop visuals on actual heroes, Android Vulkan/GLES rendering, GPU time and a new client build remain pending. There is no Unity executable or Android device in this workspace.

## Reproduce the icon

Export production geometry using the existing fixture exporter, then run the icon studio renderer:

```sh
python Tools/Shop/PremiumWingsRegression/run_visuals.py --dotnet-root /path/to/dotnet8 --export-icons ArtSource/Wings/LegacyShopGeometry.json
python ArtSource/Wings/render_deluxe_gl.py
```

The icon studio requires NumPy, Pillow, ModernGL, SciPy and an EGL implementation. It is an offline art tool only.

