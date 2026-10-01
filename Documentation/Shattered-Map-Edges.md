# Shattered borders across all eleven planets

The shared surface and the independent GJ 504 b / Diamond Core floors now use one deterministic, outward-only angular outline. The complete original play rectangle remains covered. The interior below 88% of the half extents is unchanged. Gameplay colliders and navigation code are untouched.

Four shared-boundary cliff bands form chipped ledges and a recessed underside. The border remains attached to the map; no loose fragments are generated outside it. Existing planet materials are reused, including magenta sandstone and Diamond Core's transparent/dark crystal materials. The Diamond Core overlay samples the same one-unit boundary grid as its base floor so a straight overlay cannot hide the new silhouette.

`Tools/PlanetSurfaces/validate_edge_geometry.py` passed numeric checks for all eleven grids: no inverted top triangles, unchanged interior, outward-only coverage, exact perimeter counts. `DIMENSIONS FRONT/Visuals/Validate all shattered map outlines` runs the corresponding check against the actual C# implementation in Unity. C# syntax parsing passes; Unity compilation and in-game camera review remain pending because Unity is unavailable here.
