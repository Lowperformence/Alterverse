# Traveler thigh armor — 2026-10-26

The two cyan chronometer phone assemblies have been removed, including their
casings, telemetry rows and hanging cables. Two slim, tapered carbon plates now
cover the front of the thighs. Their 64px repeating twill uses a separate,
non-emissive material; both plates remain rigidly skinned to their existing thigh
bones. The knees and existing cyan leg stripes remain clear.

The shipped resource is `Assets/Alterverse/Resources/ChronoTraveler/Model.bytes`.
All original body vertices, normals, bone assignments, anchors and non-phone
triangles are preserved exactly. The plates add 44 vertices and 80 triangles while
the removed accessories account for 576 triangles. The orb, weapons, head, chest,
shoulders, animation assets and combat code are unchanged.

`Tools/ChronoTraveler/fit_carbon_thigh_armor.py` reproduces the resource edit from
the exact pre-change payload. It is idempotent for the new payload and rejects
unrecognized input rather than applying vertex ranges to a different model.
The archived concept Blender/FBX exports predate this change; do not overwrite the
shipping resource with those exports without reapplying and checking this edit.

Validation: exact geometry preservation checks and 59 checks executing the actual
Traveler loader/build/material code with controlled Unity substitutes passed.
These include UV/material-slot agreement, thigh skinning, legacy phone suppression,
carbon texture generation and mesh/material/texture cleanup. Authored geometry was
rendered for comparison. Native Unity menu and in-match appearance still require
visual acceptance after pulling the update.
