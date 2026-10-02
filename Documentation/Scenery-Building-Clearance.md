# Building clearance and small foundations on all maps

SceneryBuildingClearance excludes decorative meshes from building interiors and a rounded 2 m apron around their horizontal footprint. It uses complete transformed mesh bounds, including rotation, tilt and scale, rather than object pivots. Building height is deliberately ignored.

Deterministic fallback footprints reserve all four bunkers, four trenches, both base decks and spawn pads before visual construction. Actual bunker, trench and core render bounds extend these reservations when available. Queries occur at build time; there is no per-frame physics scan.

Integrated before batching in GJ sandstone/geodes, lunar-ground decoration, planetary landscape pockets, and Space Kit scenery; Pyraxis basalt, lava pools and volcanoes also obey the exclusion. Rejected objects are disabled immediately before deferred destruction. Ground, structural details and the continuous cliff shell remain intact. GJ mesa groups and their obstacle colliders share the same exclusion envelope so omitted groups leave no invisible collision.

The rule applies to every arena variant, including both frontier layouts. It separates scenery from buildings; deliberately assembled parts within a rock formation, building or lava pool are still allowed to overlap.

Validation: C# syntax parse passed; 10,000 numerical horizontal-distance cases matched an independent corner-to-rectangle distance reference. Unity regression menu: Alterverse > Checks > Scenery Building Clearance (interiors, large objects, rounded apron, rotated/scaled bounds, mirrored layouts, all eleven map variants). Unity compilation and the editor regression require a Unity installation and were not run in the remote workspace. Visually check the maps from the gameplay camera after rebuilding the match.

UniversalBuildingFoundation adds a neutral dark concrete plinth below each bunker, trench and core/base building. A 0.40 m lip and 0.35 m clipped corners keep it small; the center sits 0.015 m above the structure origin and the outer lip follows the terrain with 0.035 m clearance and the skirt reaches at least 0.65 m down to hide gaps. Foundation objects live outside building hierarchies so they cannot inflate subsequent scenery-clearance snapshots. Terrain materials/textures and all building gameplay colliders are unchanged. The foundation owns and releases its meshes/material on world replacement.

Additional systems covered: Earth trees, Dark 00 floodlight props, satellite debris, and PSR crystal terraces/perimeter scenery. PSR terrace collision and visual groups share a conservative exclusion envelope. Underfloor cosmos, terrain/crater surfaces and the continuous map-edge shell are preserved.
