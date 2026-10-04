# Kryon Frost Bear

Original Blender geometry designed from the supplied mechanical-quadruped direction, with a broad bear head, short ears and tail, broad four-toed paws, glacier armor, exposed hydraulic struts and cyan rotary-joint rings.

Rebuild: `blender --background --python Tools/KryonBear/build_bear.py`.

The editable `.blend` includes the rigid armature and named groups. The game reads `Resources/KryonBear/Model.bytes` through ResourceCreatureMesh; KryonBearBlenderModel creates the named sockets used by HotZoneTank and CreatureAttackMotion. One skinned renderer, five materials, 14,976 triangles, 15 bones. No collision or combat values changed. The old procedural build is only a missing-resource fallback and is not created when the new asset loads.

Validated: finite geometry and normals, complete ordered joint hierarchy, skin indices, triangle indices, model bounds and C# syntax. Blender preview inspected. Unity play-mode and Android rendering still require an engine-side test.
