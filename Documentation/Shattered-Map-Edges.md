# Universal Blender-authored map border

All eleven maps share one connected fractured cliff design. The silhouette uses the same deterministic outline across planets; only the existing map materials and upper-floor relief vary. There are no detached splinters.

`Tools/PlanetSurfaces/build_universal_border.py` creates `UniversalBorder.blend`, a static reference FBX, a rendered preview and the numerical ring profile in `Assets/Alterverse/Resources/PlanetSurfaces/UniversalBorder.json`. Blender authors six stepped, faceted geological bands and a connected underside. Edit and regenerate through the source pipeline to keep the exported adaptive profile synchronized.

`ShatteredArenaEdge` reads that baked profile once and adapts its ring samples to the exact live boundary of each map. The first ring begins at the actual floor vertices, avoiding gaps at different map resolutions. Terrain relief fades into the cliff; the underside closes at -18 m. Existing planet materials provide the surface details. Diamond Core retains its crystal material.

The shared budget is at most 9,152 triangles for the densest boundary, seven static meshes without colliders or cast shadows. Desktop and Android use the same border shape. The gameplay rectangle, navigation and interior remain unchanged.

Blender topology validation and the Python adaptation checks cover all eleven grids, continuous attachment, nondegenerate faces, outward coverage and zero detached fragments. Unity editor outline validation also loads and validates the profile. Unity compilation and in-game camera/device checks remain pending because Unity is unavailable in this environment.

The revised border has much deeper missing sections between its outer rock shelves. Its top remains over the original gameplay rectangle. Blender now also authors branched ground cracks and 39 small rocks attached to the upper rim. `UniversalBorderDecor.json` transfers this geometry to all eleven maps; fissures and rocks follow the actual floor relief. No floating external debris is added.
