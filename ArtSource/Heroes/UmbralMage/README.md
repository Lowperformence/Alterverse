# Ione Vaska / Umbral Mage

Reference-inspired original hooded dark mage: obscured face, pointed pauldrons, silver armor borders, split folded robe, amethyst jewelry and a new crystal staff. No source photograph or stock asset is included.

Build with `python Tools/UmbralMage/build_umbral_mage.py` (NumPy, Pillow). Separate anatomy/staff meshes retain the existing renderer identities and animation rig. Upper mantle follows the chest, lower split panels follow the thighs; this is skinned geometry, not a cloth simulation. Staff contact markers stabilize the idle guard and place the spell release at its crystal.

Validated mesh coordinates/normals, indices, groups and budgets; reviewed exact-geometry previews. Unity compilation, movement intersections, shading and device performance require an editor/device check.
