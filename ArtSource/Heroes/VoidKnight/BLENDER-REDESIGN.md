# Void Knight — armor and greatsword redesign

## Visual target

Modern sci-fi knight with a deliberate black silhouette and controlled violet emission. The tank/assassin midpoint reads through a strong chest and shoulders above a narrow waist, articulated limbs and a long two-handed sword. Helmet crown, closed black face and purple eye identity stay recognizable from the existing shop avatar. All combat numbers and timings remain unchanged.

## Blender construction plan

| Assembly | Construction | Finish and detail |
|---|---|---|
| Torso | Tapered elliptical chassis, two curved pectoral shells, separate collar, overlapping abdominal lamellae | Broad quiet black surfaces, narrow machined bevels, recessed central violet diamond |
| Shoulders | Low rounded multi-ring caps with layered edge plates | Small asymmetric secondary plate on guard shoulder; thin violet insert |
| Arms and gloves | Tapered armor sleeves, floating-free wrist clearances, smaller joint covers, separate backhand/knuckle plates | Fabric at flexion zones, metal face plates, small inset lights |
| Hips and legs | Segmented tassets, split thigh plates, shaped knees and fluted greaves | Consistent V-shaped cuts, parallel narrow light channels rather than large glowing rectangles |
| Boots | Chamfered wedge soles, overlapping toe plates, sculpted instep | Preserve original zero-height sole; ankle remains free to flex |
| Greatsword | Broad tapered cross-section with actual cutting facets, central ridge/channel, swept quillons, inset rhombus core | Dark metal flats, contrasting steel bevels, long wrapped hilt, saturated violet fuller on both sides |

Use closed low-poly meshes with baked two-segment bevels and consistent normals. Keep all source components separately named and rigidly weighted to the existing 19-bone skeleton. Preserve action data, both wrist contact targets, hilt center, hilt axis and 0.38 m hand span. No detached ornamental pieces that could drift away from a joint.

## Technical acceptance

- Twelve existing actions retained; finite bone matrices throughout every frame.
- Both hands remain aligned to the original grip targets.
- Source parts remain editable; runtime exports as two skinned meshes with at most five materials.
- Target under 35,000 runtime triangles; inspect actual result before accepting.
- Update current construction renders and inspect idle, guard, thrust and cleave poses.
- Preserve serialized asset GUIDs, resource paths, collider, reach, damage and ability behavior.
- FBX material importer must retain the distinct cloth, black metal, edge metal and purple energy appearance.

## Release state

Void Knight remains visible as a preview but unreleased. Purchase and match selection are blocked until `VoidKnightReleased` is deliberately enabled later. Do not invent a launch price or SKU.

## Implemented result

214 separate source meshes, 26,832 runtime triangles, five materials and twelve original actions. Every original bone matrix is identical across all 384 action frames; the two-hand grip span remains 0.38 m. 76 deterministic gameplay/release checks pass. Unity/NGO/Android validation is still a separate native step.
