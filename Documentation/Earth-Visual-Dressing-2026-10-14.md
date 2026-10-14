# Earth scenery pass

Earth replaces four disconnected water cylinders and the extra sphere/cone trees with
an authored scenery kit. Two mirrored streams each feed a lake; smooth centrelines,
irregular banks, reeds and shore stones tie the water together. Small settlements have
terracotta/slate cottages, sheds, doors, windows, steps, chimneys, gutters, tile courses,
fences, gardens, village roads and short driveways. Mixed oak, birch and pine models use
tapered roots/trunks, branches, irregular foliage masses and bent leaf sprays. Low shrubs
and ferns blend woodland edges into the existing terrain.

The kit is constructed in metres with ground pivots and separate material parts, ready
for Blender editing. See `ArtSource/EarthMap/README.md` for authoring/export commands.
The shipped JSON geometry is used immediately; players do not need Blender installed.

## Gameplay boundary

Only Earth presentation changes. `PlanetArenaThemeRuntime.BuildEarth` attaches the new
dressing component; its other planet themes and monster appearance paths are unchanged.
`EarthForestPolish` keeps its existing tower-soil creation and refresh routines, with no
soil overlays added to spawn cores. `PlanetLandscapeDetail` and the underlying terrain
remain unchanged. No gameplay, input, navigation, match, spawn, tower, bunker, trench,
monster, server, shop or combat files are modified.

New scenery never adds Collider, Rigidbody, NavMeshObstacle, Light or gameplay components.
The existing building-clearance helper checks complete prop bounds, including its
two-metre apron. Surface triangles also respect those clearances. Props avoid the central
combat corridor and centre. Positions are paired through the origin without using
Unity's random state. Regional material batches allow normal renderer frustum culling.
Water is a visual plane over the existing ground and does not change terrain or movement.

The water shader gains an opt-in `_AuthoredBanks` UV channel for these meshes. Its default
is zero, preserving the previous cylinder coordinate path for existing materials.

## Verification

`python Tools/EarthMap/validate_earth_map.py` passes finite position/UV, index, winding,
nondegenerate triangle, symmetry, structure-apron, layout, water-level and geometry-budget
checks. With fallback building footprints, 484 props are accepted, including 128 trees,
six cottages and two sheds. Props contain 222,460 triangles and 253,688 vertices in
68 regional material batches; the largest prop batch has 22,768 vertices. Surface strips
are additional and share those batches where possible. These are geometry counts, not
Android GPU measurements.

Changed C# parses with tree-sitter-c-sharp; all four Python tools parse successfully.
`render_earth_preview.py` renders the shipped geometry with EGL and provides the layout
and close-up images in `ArtSource/EarthMap/GeometryQA`. Those are geometry QA previews on
flat sample ground with substitute materials, not screenshots of the game. The real game
retains its existing terrain and scenery/water shaders.

Native Unity compilation, URP rendering, Blender execution and Android performance were
not available in this environment. In Unity Play Mode, run **DIMENSIONS FRONT > Checks >
Earth scenery kit (Play Mode)** to check actual generated batches, material support,
duplicate builds, finite geometry, structure clearance, flat water and absence of physics.
Then inspect Earth from the game camera and switch to another map to confirm cleanup.
