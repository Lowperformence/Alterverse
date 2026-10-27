# Station Kit 02

Original hard-surface structure art for Dimensions Front, authored with Blender 4.2.

Models: Reality Core and walkable deck, bunker, trench with integrated decorative tower,
flush healing pad, and flush central healing pickup. There is no standalone tower.
The Blender scene and authoring script are source assets; the Unity player loads only
the compressed mesh library in Resources/StationKit/StationBuildings.bytes.

Run from the repository root:

```sh
blender -b --factory-startup --python Tools/StationKit/author_station.py -- --render
python Tools/StationKit/Regression/run.py --dotnet-root /path/to/dotnet
```

The adapter hides the old renderers after new geometry/material creation succeeds.
It retains original gameplay components, colliders, transforms, damage, healing,
collection/respawn and network ownership. Bunker turret meshes are attached to the
original aiming transform; the original muzzle is not moved. The trench tower is
decorative and adds no attack, targeting or collision behavior. The core reuses its
original emission material so vulnerability and damage flashes keep their writer.
The pickup's new flat symbol follows the existing availability parent, cancelling
only the inherited visual bob/scale on its own child.

Geometry bounds are checked by the regression. Healing pad height: 0.0512 m.
Healing node height: 0.0348 m. Both are placed at sampled ground + 0.005 m with no
model collider. Per-asset triangle counts and bounds are in geometry-report.json.
Combined HQ art is 4,508 triangles; other structures are below 3,000 triangles.
Meshes are cached across matches; repeated parts are grouped by material and mount.

Validation: actual Blender modeling, triangulation, rendering and compressed export;
managed client/UNITY_SERVER adapter compilation and lifecycle checks. Native Unity
import/rendering, Android performance and a live multiplayer visual check remain
outstanding. Preview PNGs show the actual meshes, not the generated concept art.
