# Brakk: restore intact chest and thigh armor

The screenshot shows Brakk Orun, not Void Knight. The previous contour fitter
projected the outline vertices of each plate layer independently onto the body.
Large flat faces still spanned that outline with long triangles. On curved
regions those triangles crossed the smaller graphite inset and hid portions of
it. Correct bone weights and a small mounting gap did not detect this defect.

The repaired runtime export rebuilds the original 126 closed authored solids.
Each module uses one shared 4 cm contour triangulation for every layer. All
faces, bevels and side walls are clipped against those same cells before the
height deformation. This keeps the layers parallel within each cell and retains
their thickness, footprint, winding and manufactured edges. Interpolated surface
gradients supply smooth normals across the contour cells while keeping the
authored bevel boundaries. Overhanging corners retain their tangential position.

`surface-profiles.json` preserves the support contour recovered from the
previous shipped resource. It is calibration data for the current anatomy and
plate footprints, not a second skeleton or a runtime dependency. The editable
planar Blender concept and individual part models remain the design source;
`diagram-geometry.json` supplies their original closed faces to the repair.
The game loads only the regenerated `BrakkArmor/Model.bytes`. Its loader revision
is updated together with the resource. The shared Rook FBX, Nerok, fists, belt,
body cleanup and gameplay are unchanged.

## Verification

`validate_layers.py --compare /path/to/previous/Model.bytes` tests the actual
shipped geometry at 882 points across all 18 modules. The previous resource has
27 missing-face samples and 119 buried-inset samples. The repair has zero of
either; the minimum inset-to-gold clearance is approximately 4 mm.

The test also checks finite, nondegenerate geometry, original solid volumes,
rigid bone assignments, and Blender armature deformation in 27 poses with 378
sample vertices per pose. Maximum attachment error is below 0.000001 m. Materials,
part IDs, bones, bind-space pivot and reference units retain their original
contracts. `layer-validation.json` records the measured result. The studio PNG
renders the actual exported armor with its supplied normals, without the body.

Runtime armor: 68,964 vertices and 53,437 triangles. There are still five material
slots and one armor renderer. The higher triangle count repairs the curved
surfaces; on-device performance has not been measured.

Native Unity Editor import/rendering, body clearance and Android checks were
not run here. The old body-distance measurements belong to the previous contour
revision and are not evidence for this new triangulation. After pulling, exit
Play Mode and start it again to recreate the hero and reload the static cache.
Check the chest and both thighs in the hero preview, walking and attacks.

## Reproduction

Install `Tools/BrakkArmor/requirements.txt` in the Blender/bpy Python environment.
`repair_layered_surfaces.py` migrates the v1 contour resource once and is
idempotent afterward. Normal `export_runtime.py` calls `fit_surface.conform`,
which reuses the shared profile and closed authoring geometry. `validate_runtime.py`
recognizes the repaired contour resource as well. Run `validate_layers.py` and
`render_runtime.py` after export. A changed anatomy or plate footprint requires
fresh support calibration; set aside the old profile, export the initial contour,
then run the repair to generate the new profile.
