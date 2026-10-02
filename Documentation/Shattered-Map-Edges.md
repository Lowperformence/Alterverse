# Subtly fractured map edges — Blender model

All eleven maps use the same connected broken-rock border with short cracks, small upper-rim chips and irregular attached cliff blocks. The top contour has only shallow weathering: 0.18–0.80 m outward displacement, replacing the former deep, repeating bays. The original gameplay rectangle and interior remain covered.

## Authored assets and live integration

`Tools/PlanetSurfaces/build_universal_border.py` runs in Blender to create:
- `ArtSource/PlanetSurfaces/UniversalBorder.blend`: editable model and studio setup.
- `ArtSource/PlanetSurfaces/UniversalBorder.fbx`: actual cliff geometry and attached rocks.
- `ArtSource/PlanetSurfaces/UniversalBorder.png`: Blender geometry preview, not a Unity screenshot.
- `Assets/Alterverse/Resources/PlanetSurfaces/UniversalBorder.json`: six geological rings.
- `Assets/Alterverse/Resources/PlanetSurfaces/UniversalBorderDecor.json`: 52 short fissures, 39 small rim chips, 416 staggered cliff-block placements and their closed rock mesh.

The game reads the Blender-exported profile and geometry through `ShatteredArenaEdge`, adapting the upper edge to the exact live floor vertices. This avoids seams on differently sampled terrain. The static FBX is the complete reference model; the runtime profile preserves its design while conforming to each planet's terrain relief. No extra Animator, collider, navigation obstacle, light or particle system is introduced.

Four irregular courses of faceted cliff blocks overlap the backing shell; small differences in width, depth, rotation and placement avoid a regular brick pattern. Existing oversized edge formations are no longer layered over the authored border. The lower shell closes at -10 m, with no detached giant core.

## Planet materials

Moon uses grey regolith; Mars uses red stone; GreenPlanet/Moor uses moss-colored rock; Noxara uses blue-black stone; Kryon uses icy rock. Earth, Pyraxis, Neptune and Dark00 now have explicit cliff tints matching their soil, basalt, abyssal stone and dark geology. DiamondCore/PSR retains its crystal materials; GJ504b retains rose quartz stone. Existing per-planet world-space surface textures provide fine detail.

## Validation

Blender backing shell: 2,185 vertices and 4,056 triangles. Each closed cliff block uses 44 triangles; the densest adapted map uses 27,456 triangles for shell plus cliff blocks, with small rim/crack detail in additional batches. The rock blocks are combined into three material meshes at runtime, rather than 416 GameObjects.

Python checks pass for all eleven map grids and three relief heights: outward-only coverage, unchanged interior, positive surface triangles, nondegenerate cliff facets, monotonically descending rings, valid closed rock topology and bounded crack/rock sizes. A numerical intersection check confirmed all 416 block placements penetrate the backing shell and remain visible at zero relief. Changed C# files passed syntax parsing. Unity compilation, final camera appearance and device performance still require a Unity play-mode check.

Rebuild: `blender -b -P Tools/PlanetSurfaces/build_universal_border.py`
Validate: `python Tools/PlanetSurfaces/validate_universal_border.py`
