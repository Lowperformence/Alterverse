# Hot Zone monster art

The seven reference creatures had replaced the earlier rigid Blender parts with skinned meshes. Those meshes had no UV channel, and the old AuthoredHotZoneArt scan only visited MeshFilter/MeshRenderer objects. Therefore the old claim that all current monsters already used the new textures was incomplete.

The new Blender pass unwraps the exact current meshes and exports SkinUV.bytes. UV seams duplicate only existing vertices. Model.bytes is unchanged: source coordinates, normals, triangles, material slots, weights, named joints and combat data remain authoritative. The loader verifies SHA-256 against the original model and checks every remapped triangle against its original index order. Rebuilding a model requires rebuilding its UV data.

| Map | Current monster | Surface completion |
| --- | --- | --- |
| Moon | Mechanical guard | Existing Blender parts and Machine textures |
| Mars | MarsSkeleton | Added Blender UVs, Skeleton surface and normal maps |
| GreenPlanet | GreenStalker | Added Blender UVs, Xeno surface and normal maps |
| Noxara | Clockwork spider | Added Blender UVs, Spider surface and normal maps |
| Kryon | Frost bear | Existing Blender geometry; added UVs, Ice surface and normal maps |
| Earth | Mechanical guard | Existing Blender parts and Machine textures |
| Pyraxis | Mechanical scorpion | Added Blender UVs, Machine surface and normal maps |
| Neptune | Oktopus | Added Blender parts and Xeno textures after final body creation; detail materials included |
| Dark00 | Rolling orb | Added Blender parts and Machine textures after final body creation |
| DiamondCore | Crystal titan | Added Blender UVs, Crystal surface and normal maps |
| GJ504b | Magenta golem | Added Blender UVs, Sandstone surface and normal maps |

The seven atlas/normal pairs already existed. They are now connected to the current skinned models through ResourceCreatureMesh. Energy slot 3 retains its original material and emission. The runtime shader now distinguishes Unity-imported normal-map channel packing from the existing raw RGB terrain normals. Runtime meshes recalculate tangents for the normal maps, and retain existing rigid bone bindings and animation controllers. All seven UV meshes remain below 65,536 vertices; no triangles are added. Additional seam vertices still increase runtime skinning memory and need Android profiling.

Editable Blender source: ArtSource/HotZoneMonsters/HotZoneSkinnedSurfaces.blend. This is a UV authoring library in the existing bone-local Unity coordinates, not a posed in-game screenshot. It includes the existing surface and normal atlases. The original seven-family rigid-part library remains in HotZoneParts.blend.

Rebuild current skinned UVs: blender -b --python Tools/HotZoneArt/build_skinned_surfaces.py.
Validate exact geometry/rig preservation: python Tools/HotZoneArt/validate_skinned_surfaces.py.
Unity integration check: DIMENSIONS FRONT / Checks / Hot Zone skinned Blender surfaces. This also runs before builds, checking the real loader, remapped weights, UVs, tangents, shaders, surface/normal bindings and untextured energy slots.
Original rigid-part integration check: Alterverse / Validation / Hot Zone Blender Art.

Blender 4.5.9 generated all seven UV assets. Independent numeric checks passed for source hashes, finite UVs, original triangle order, rig preservation and vertex budgets. C# syntax checks passed. Unity compilation, the editor integration checks, animation review and Android GPU/performance checks have not run in this workspace.
