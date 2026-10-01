# Hot Zone monster art

All seven visual families now use a Blender-authored part and surface library: lunar machine guard, Mars robot skeleton, Xeno titan, Noxara spider, Kryon frost bear, GJ504b sandstone beast and PSR diamond guardian. The default guard covers the remaining map variants.

The Blender source contains 35 UV-unwrapped, normalized parts, seven procedural surface atlases and seven normal maps. Mechanical parts have beveled edges, panel seams and fasteners; organic shells have overlapping panzer/scales; mineral parts have hard facets and fracture lines. `PartsPreview.png` is a Blender library preview, not a Unity screenshot. The FBX is a reference asset; the JSON library is used at runtime.

`AuthoredHotZoneArt` fits each supported rigid part to its exact original local mesh bounds. Custom plates, claws, rings and diamonds retain all original triangle edges and extrema, with a shallow Blender-authored inset fan. Existing transform hierarchy, muzzle pivots, animation controllers, colliders, health, damage, speed and abilities remain unchanged. Original controllers continue to drive cloned material colors and emission. Transparent crystal materials and bright energy materials retain their existing effects. All added runtime meshes and materials are released with the monster.

Rebuild: `blender -b --python Tools/HotZoneArt/build_monsters.py`.
Validate source data: `python Tools/HotZoneArt/validate_monsters.py`.
Unity integration check: **Alterverse / Validation / Hot Zone Blender Art**. It exercises all seven families against primitive and custom parts, checking mesh bounds, transforms, collider bounds, UVs, animated material tint and repeated initialization.

Blender generation, source-data checks and C# syntax checks ran in the build workspace. Unity compilation, the editor integration check and Android visual/performance checks require Unity and have not run in this workspace.
