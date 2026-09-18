# DIMENSIONS FRONT — lunar environment pass

## Implemented
Continuous deterministic regolith field with 22 irregular crater sites, low relief, eroded rims and ejecta, quieter compacted lane corridors and a world-space albedo/normal atlas. The surface shares its exact perimeter with three descending rock strata and a closed undercut. Static details combine by material and spatial cell; decorative geometry adds no colliders.

One owned lighting pass restores scene lighting on teardown. Duplicate legacy ground/backdrop builders yield to the production surface. Six external fragments use fractured rock meshes. The space backdrop plane moves below the lunar slab. Bastions gain load-bearing supports; cores gain silhouette scale and damage/vulnerability emission without changing combat rules. Planets use open orbit meshes and borrowed lunar surface texture. Meteor warning and damage share the 2.1m radius constant; the previous cylinder only displayed half that radius.

## Preserved
Hero balance, AI, movement, navigation, lane/collider positions, tower/core positions, planet schedules (120/330/510 seconds), siege schedule and damage values. Environment remains visual-only. Existing combat fixes are retained.

## Budget and limitations
Ground: 6,205 vertices / 12,096 triangles. Two 512-square RGBA32 mipmapped textures: approximately 2.67 MiB GPU memory. Texture baking runs once at match construction, not per frame; startup CPU cost is NOT VERIFIED and needs device profiling. Cliff and detail mesh batching preserves spatial culling. New rocks share geometry. Owned materials, textures and meshes are released on teardown.

## Verification
Tree-sitter C# syntax and git diff whitespace checks only. Unity compilation, shader inclusion, visual appearance, Android memory/FPS and gameplay-camera review are NOT VERIFIED. These changes are not proof of release quality.

Unity review tools: DIMENSIONS FRONT / Checks / Lunar geometry and corridor bounds; Capture lunar gameplay views (Play mode). The latter saves six camera-derived views under Temp/DimensionsFront-LunarReview. Run both in Unity, inspect both lanes, bases, center and broken rim, then review planet/meteor/siege events and repeated match teardown on device.
