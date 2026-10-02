# Siege Tank / Kaelon

Original olive armor inspired by the supplied armored-mech reference. Compact helmet with amber optics, bronze panel borders, rivets, joint pistons, rear exhausts, oval shield and industrial hammer. No stock artwork or texture is shipped.

Run `python Tools/SiegeTank/build_siege_tank.py` with NumPy and Pillow. The compressed resource contains independent body/shield/hammer groups and rig-space anchors. Existing renderer identities and weapon grip bones remain in use; all owned resources are released. Contact markers stabilize the guard and reposition the hammer effect origin. Attack poses keep the established action controller.

Generator checks: finite vertices and normals, triangle indices, bone indices, renderer group integrity and triangle budget. Exact-geometry front/back previews checked offline; these are not Unity screenshots. Unity compilation, animated collisions and device performance still need an editor/device run.
