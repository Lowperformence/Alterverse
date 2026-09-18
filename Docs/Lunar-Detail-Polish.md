# DIMENSIONS FRONT — small lunar landmarks

Three authored outer sites were added to the existing procedural ground construction:

- (-23, 27): seven partially buried ejecta fragments and three short scars at the existing large impact.
- (-18, -26): three suspended dark fragments over a broken scar with one short violet seam.
- (23, 26): three pale stabilization fragments with one short cyan vein.

Dark lunar ground remains dominant. No changes to lane geometry, navigation, towers, objectives, spawn points, colliders or balance. Locations are outside the lanes at z +/-16 and away from the central collision area. Actual gameplay-camera readability still needs Unity verification.

Static cost from construction: 13 instances of the existing 54-triangle rock mesh plus seven two-triangle scars = 716 additional triangles before existing batching. Materials are reused; no textures, lights, particles, colliders or Update methods were added. All new geometry joins the existing material/spatial batches (at most seven additional material/cell combinations, fewer when existing batches match). Surface sampling seats the details on the visual regolith. No measured device frame-time or draw-call claim is made.

Validation: C# syntax and diff checks pass. Unity compilation, actual gameplay-camera inspection and target-device performance capture were unavailable. Limit the remaining visual acceptance to these three sites; this is not a map rebuild.
