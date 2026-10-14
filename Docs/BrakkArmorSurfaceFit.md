# Brakk armor surface fit

Brakk's replacement plates were positioned as flat solids. The authoring pass
used the maximum depth of every body triangle intersecting each plate's bounding
rectangle, including triangle vertices outside the plate footprint. Large gaps
remained over curved anatomy and made the rigidly attached plates look detached.

The runtime export now conforms the mounting plates, shells and fasteners to the
connected anatomical skin in bind space. The nominal mounting gap is 8 mm;
layer thickness and a small silhouette overhang remain. Normals are recalculated
for the fitted faces. Bone mapping, material slots and triangle topology are
unchanged. The editable planar concept remains the source design.

Regenerate with `export_runtime.py`; it automatically calls `fit_surface.py`.
The standalone fitter is idempotent and can repair the previous runtime export.

Validation on the shipped resource:

- `validate_surface_fit.py`: all 18 mounting plates pass; maximum surface
  distance is 0.016856 m; all triangles have nonzero area and data is finite.
- The previous resource fails the same test (A01 alone exceeds 0.05 m).
- `validate_runtime.py`: 27 animation poses pass with maximum attachment error
  of 0.000000257 m. Original torso cleanup still preserves protected anatomy.

The regression now tests the actual fitted resource through Blender's armature
modifier. A Unity Editor is not available in the repair environment; confirm
walking, turning and attacks in Play Mode after pulling `dev/vertical-slice`.
Exit Play Mode before updating so the static geometry cache is reloaded.
