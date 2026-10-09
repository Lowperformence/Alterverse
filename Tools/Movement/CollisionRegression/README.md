# Saturated collision query regression

Run `python3 Tools/Movement/CollisionRegression/run.py` with Mono installed, or pass
`--mono-root /path/to/extracted/mono`.

The runner compiles the unchanged `HeroLocomotion.Sweep` method extracted from production
against deterministic substitutes for the physics query. It checks that more than 32
ignored unit/self/ground hits do not block movement, that a solid nearest blocker outside
the initial buffer is still respected, that ordinary queries avoid the allocating fallback,
and that contact normals pointing away from movement remain ignored.

This verifies query completeness and filtering, not Unity's physical collision detection
or scene/controller setup. The saturation bug is a possible movement blockage; it was
not established as the cause of the supplied video's animation problem.
