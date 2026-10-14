# Earth scenery: Blender authoring kit

The runtime source is `Assets/Alterverse/Resources/EarthMap/Dressing.json`. All props share
a metre scale, Y-up Unity coordinates, a ground pivot and material-separated mesh parts.
The Blender workflow converts to X/-Z/Y coordinates and uses linked mesh instances in
the layout, so one edited kit part updates every preview instance.

| Module | Construction | Use |
| --- | --- | --- |
| Oak / birch / pine, two variants each | Tapered seven-sided trunk, root flares, branch tubes, irregular smooth foliage masses, closed bent leaves | Mixed woodland with varied scale and yaw |
| Shrub / fern / reeds | Low radial stems, bent closed leaf sprays, reed seed heads | Forest edges, gardens and shoreline |
| Cottage / slate cottage | Wall shell, fieldstone plinth, gable roof, tile courses, framed doors/windows, chimney, gutter/downpipes | Two small settlements |
| Shed | Timber shell, pitched roof and plank seams | Peripheral settlement detail |
| Fence / stone | Posts and two rails; irregular closed stone mesh | Gardens and shoreline |
| Stream / lake / road strips | Smoothed spline ribbons and irregular lake rings; UV bank distance | Connected scenery conforming to the original surface |

## Build or edit

From the repository root:

```sh
python Tools/EarthMap/build_earth_map.py
python Tools/EarthMap/validate_earth_map.py
blender --background --python Tools/EarthMap/author_in_blender.py -- --render
```

The Blender command creates `ArtSource/EarthMap/EarthDressing.blend`, plus two Blender
renders. Open that file and unhide **KIT - edit these meshes**. Edit mesh vertices in Edit
Mode or add modifiers. Keep kit object names, `earth_asset` / `earth_material` properties,
ground pivots and metre scale. Apply any object transforms before exporting. No asset
depends on transparency, rigging, physics, booleans or complex material node setups.

```sh
blender --background ArtSource/EarthMap/EarthDressing.blend --python Tools/EarthMap/author_in_blender.py -- --export
python Tools/EarthMap/validate_earth_map.py
```

The exporter evaluates modifiers and writes triangulated kit parts back to the existing
JSON, preserving its layout and materials. The game loads that JSON directly. The LAYOUT
collection is a linked preview on flat sample ground; placement edits there are not
exported. To change placement, edit `placements` or the builder's layout, keeping structure
clearance and paired positions. Re-running `build_earth_map.py` replaces manual mesh edits
with the deterministic base kit, so commit edited JSON before regenerating it.

Keep the cheap closed leaf volumes and palette reuse when refining vegetation. Shared
normals soften foliage; separate faces preserve roof/wall corners. Build additional detail
into the existing parts instead of creating per-tile/per-leaf GameObjects in Unity.

## Geometry previews

```sh
python Tools/EarthMap/render_earth_preview.py
```

Requires NumPy, Pillow and ModernGL/EGL. `GeometryQA/EarthLayout.png` and
`GeometryQA/EarthVillage.png` show the shipped mesh layout using substitute preview
lighting/materials and flat terrain. They are not Unity screenshots. Blender was not
installed in the implementation environment; the authoring script is supplied, and
the runtime JSON and EGL previews have been generated and checked.
