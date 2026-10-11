# Blender bomber art

`ArtSource/SpaceBomber/SpaceBomber.blend` is the editable Blender source. It has
90 separate model objects: a tapered fuselage, faceted cockpit, swept wings,
control surfaces, service panels, flush fasteners, intakes/louvres, engine
reinforcing rings, nozzle vanes, twin tail fins and closed bomb-bay doors.
The FBX is an additional interchange export; Unity uses the exact evaluated
geometry in `Resources/AuthoredBomber/Model.bytes`, not a separate FBX importer.

Rebuild with Blender 4.3 or newer:

```sh
blender -b --python Tools/SpaceBomber/build_bomber.py
python Tools/SpaceBomber/validate_bomber.py
```

The runtime export contains 7,504 triangles in five immutable shared meshes:
three material batches and the two existing pulsating thruster meshes. It keeps
the original `.78` visual scale, +Z forward direction, arena-dependent armor,
dark and energy materials, and exact thruster positions/scales. Geometry remains
within the compact aircraft envelope. The source objects remain individually
editable. No colliders, Rigidbody, gameplay controller or additional audio are
introduced. Meshes are loaded once, uploaded without CPU mesh copies and reused
by subsequent flyovers. Parsed geometry arrays are released after upload.

The only combat-source edit is the early geometry-selection hook inside
`SpaceBomberFlyover.BuildVisual`. Flight routes, event cadence, speed, altitude,
banking, thruster pulse, sound, networking, bomb targets, drop spacing, telegraph,
damage, impact radius and cleanup retain their previous code. The original
primitive visual remains the fallback for absent/invalid art data.

Validation performed: Blender source render and a render reconstructed from the
exact runtime mesh export; C# syntax and production-loader compilation; 52 fixture
assertions covering shared meshes, original sockets/scales, material identity,
visual-root preservation, no physics/behavior additions, cache cleanup and
missing/invalid-art fallback. The fixture uses Unity stubs and a JSON deserializer
shim; Unity's native serializer, GPU upload and rendering have not run here.
Export checks cover UV/normal counts, finite values, outward winding, nonzero
triangle area, referenced vertices, envelope and the mobile triangle budget.
A byte comparison with the prior combat source proves that removing the art
hook restores the original file exactly.

For that comparison, pass an original `SpaceBomberEvent.cs` to the validator:

```sh
python Tools/SpaceBomber/validate_bomber.py --baseline /tmp/OriginalSpaceBomberEvent.cs
```

Unity compilation and a flyover on PC/Android remain to be checked in Unity.
Pull outside Play mode and restart Play mode after import to recreate the bomber.
