# Violet armor and closed helmet

This revision replaces the crowned helmet with a closed, segmented helmet and a continuous violet V visor. Shoulder chevrons and long forearm/shin inserts follow the approved concept. The dark chest diamond, layered torso, hands, weapon, original skeleton and all existing actions are retained.

All plates, seals, luminous inserts, brow rails, ear rims and vents are separate editable objects in `VoidKnight.blend`. The existing module exporter also writes 17 independently openable armor assemblies in `Modules/`, including `Helmet.blend`.

The build starts from the current production source, saves a temporary `_violet_baseline.blend`, and compares every bone matrix in all 384 animation frames against that exact source. Validation also protects hand and sword geometry, five shared materials, normalized skin weights, UVs, finite vertices, and the existing 35,000-triangle ceiling. The runtime exporter merges only its in-memory copy into the established two skinned meshes. Unity continues loading the same FBX path and GUID through the existing URP importer.

Rebuild with Blender 5.0.1, in order:

1. `build_violet_armor.py`
2. `validate_violet_armor.py`
3. `export_void_knight.py`
4. `validate_fbx.py`
5. `export_modules.py`
6. `validate_modules.py`
7. `validate_refinement.py`
8. `render_reference.py`

The GitHub build creates the native files, FBX, validation reports and studio renders, then commits only those generated outputs to the feature branch. `_violet_baseline.blend` and Blender backup files are temporary and are not staged. A new revision of the generator should start from a clean production source rather than repeatedly apply to V4.

Studio renders are previews of actual Blender geometry. They are not Unity or Android screenshots. Native Unity import, mobile bloom/readability, animation clipping, shop/gameplay previews, and an actual Android performance check remain separate checks in the game editor and on device.
