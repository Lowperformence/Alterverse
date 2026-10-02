# Cyber Ranger / Nyra

Original graphite and titanium cyborg suit inspired by the supplied visual reference: emerald targeting monocle, respirator, articulated armor and rear circuitry. No reference photograph or stock texture is included.

Run `python Tools/CyberRanger/build_cyber_ranger.py` with NumPy and Pillow to rebuild the compressed mesh and exact-geometry previews. Preview lighting is an offline rasterizer, not Unity.

CyberRangerAppearance fits individual armor pieces to the existing Nyra skeleton at instantiation and replaces the instance DF_Anatomy mesh. Existing renderer identity, weapon geometry, grip sockets and movement controls remain in use. The rifle is refinished to match. Owned meshes/materials are released on destruction.

Validation: finite coordinates/normals, triangle and bone indices, mesh budget, front/back geometry renders. Unity compilation, animation intersections, game lighting and mobile performance still require verification in the project editor.
