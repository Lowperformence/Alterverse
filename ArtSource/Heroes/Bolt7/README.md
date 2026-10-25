# BOLT-7 original authoring assets

Original illustrated PNGs and the animated FBX are retained here. The native Blender project is part of the previously delivered BOLT-7 model package. Runtime rendering uses the same geometry and bone poses in `Assets/Alterverse/Resources/Bolt7/Model.bytes`, without FBX import conversions. Runtime PNGs are byte-identical copies under `Bolt7/Raw/*.bytes`.

Export from the native project using Blender:

```
blender -b BOLT7.blend --python Tools/Bolt7/export_runtime.py -- --output Assets/Alterverse/Resources/Bolt7/Model.bytes
```

The exporter also accepts `--source-fbx ArtSource/Heroes/Bolt7/BOLT7_animated.fbx` when invoked with a clean Blender scene. The native project is preferred for matching the shipped file exactly.

Validate native skinning against the shipped data:

```
blender -b BOLT7.blend --python Tools/Bolt7/validate_runtime.py -- --model Assets/Alterverse/Resources/Bolt7/Model.bytes
```

The static model is 2.419m tall before the existing runtime 0.85 visual scale. Mapping `(x,y,z) -> (x,z,-y)` rotates Blender Z-up into Unity Y-up, with the weapons facing positive Z. Local bone poses, UVs, six material assignments and rigid vertex weights are preserved.
