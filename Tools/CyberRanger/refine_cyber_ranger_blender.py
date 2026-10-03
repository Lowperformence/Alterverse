"""Shape Sivra Quell's Cyber Ranger model in Blender and export Unity's runtime mesh.

Run after build_cyber_ranger.py with:
  blender --background --python refine_cyber_ranger_blender.py
"""
import bpy
import bmesh
import gzip
import json
import math
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "Assets/Alterverse").is_dir())
MODEL = ROOT / "Assets/Alterverse/Resources/CyberRanger/Model.bytes"
ART = ROOT / "ArtSource/Heroes/CyberRanger"
ART.mkdir(parents=True, exist_ok=True)

with gzip.open(MODEL, "rt", encoding="utf-8") as f:
    source = json.load(f)
bone_names = source["boneNames"]
anchors = source["anchors"]
vertices = [tuple(source["vertices"][i:i+3]) for i in range(0, len(source["vertices"]), 3)]
bone_indices = source["boneIndices"]
colors = [
    (.075, .085, .10, 1),  # anthracite undersuit
    (.080, .115, .120, 1),
    (.46, .57, .57, 1),
    (.025, .65, .36, 1),
    (.007, .018, .016, 1),
]

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
material_data = []
for i, color in enumerate(colors):
    mat = bpy.data.materials.new(f"Nyra_Surface_{i}")
    mat.diffuse_color = color
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = color
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .78 if i == 0 else .38
    material_data.append(mat)

objects = []
original_bounds = None
leg_measure_before = {}
for material_index, submesh in enumerate(source["submeshes"]):
    tris = [submesh["triangles"][i:i+3] for i in range(0, len(submesh["triangles"]), 3)]
    by_bone = {}
    for tri in tris:
        indices = [bone_indices[i] for i in tri]
        if len(set(indices)) != 1:
            raise ValueError(f"Triangle crosses skeleton bones: {tri}")
        by_bone.setdefault(indices[0], []).append(tri)

    for bone_index, bone_tris in by_bone.items():
        used = sorted({v for tri in bone_tris for v in tri})
        remap = {old: new for new, old in enumerate(used)}
        mesh = bpy.data.meshes.new(f"Nyra_{bone_names[bone_index]}_Mat{material_index}")
        mesh.from_pydata([vertices[i] for i in used], [], [[remap[v] for v in tri] for tri in bone_tris])
        mesh.materials.append(material_data[material_index])
        mesh.update()
        obj = bpy.data.objects.new(mesh.name, mesh)
        bpy.context.collection.objects.link(obj)
        obj["boneIndex"] = bone_index
        obj["materialIndex"] = material_index
        obj.data.polygons.foreach_set("use_smooth", [True] * len(obj.data.polygons))
        objects.append(obj)
        if bone_names[bone_index].startswith(("Thigh", "Shin")):
            a = anchors[bone_index]
            head, tail = Vector(a["head"]), Vector(a["tail"])
            axis = (tail - head).normalized()
            radius = []
            for p in mesh.vertices:
                offset = p.co - head
                t = offset.dot(axis) / max((tail-head).length, 1e-8)
                radial = offset - axis * offset.dot(axis)
                if .2 < t < .8:
                    radius.append(radial.length)
            if radius:
                leg_measure_before[bone_names[bone_index]] = sum(radius) / len(radius)

# Stretch each thigh and shin radially in Blender while preserving its bone length.
for obj in objects:
    name = bone_names[obj["boneIndex"]]
    if not name.startswith(("Thigh", "Shin")):
        continue
    a = anchors[obj["boneIndex"]]
    head, tail = Vector(a["head"]), Vector(a["tail"])
    axis_vector = tail - head
    length_sq = axis_vector.length_squared
    axis = axis_vector.normalized()
    for vertex in obj.data.vertices:
        p = vertex.co.copy()
        offset = p - head
        t = offset.dot(axis_vector) / length_sq
        if 0 < t < 1:
            envelope = min(1.0, t / .12, (1-t) / .12)
            center = head + axis_vector * t
            vertex.co = center + (p - center) * (1 + .20 * envelope)
    obj.data.update()

# Find the fabric-covered pelvis on the Hips bone. Blender builds two round,
# integrated suit volumes at the back of the hips, then remeshes them into one form.
hip_index = bone_names.index("Hips")
pelvis = next(o for o in objects if o["boneIndex"] == hip_index and o["materialIndex"] == 0)
pelvis_original = [v.co.copy() for v in pelvis.data.vertices]
old_back = min(v.co.z for v in pelvis.data.vertices)
for sign in (-1, 1):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=64, ring_count=40,
        location=(sign * .066, .928, -.087))
    cheek = bpy.context.object
    cheek.name = "Blender_Sculpted_Anthracite_Glute_L" if sign < 0 else "Blender_Sculpted_Anthracite_Glute_R"
    cheek.scale = (.080, .082, .065)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    cheek.data.materials.append(material_data[0])
    cheek["boneIndex"] = hip_index
    cheek["materialIndex"] = 0
    cheek.data.polygons.foreach_set("use_smooth", [True] * len(cheek.data.polygons))
    bpy.context.view_layer.objects.active = pelvis
    pelvis.select_set(True)
    cheek.select_set(True)
    bpy.ops.object.join()

pelvis.data.remesh_voxel_size = .005
bpy.context.view_layer.objects.active = pelvis
bpy.ops.object.voxel_remesh()
bm = bmesh.new()
bm.from_mesh(pelvis.data)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=.12, use_axis_x=True, use_axis_y=True, use_axis_z=True)
bm.to_mesh(pelvis.data)
bm.free()
pelvis.data.update()
for poly in pelvis.data.polygons:
    poly.use_smooth = True
glute_back = min(v.co.z for v in pelvis.data.vertices)
glute_width = max(v.co.x for v in pelvis.data.vertices) - min(v.co.x for v in pelvis.data.vertices)
if glute_back > old_back - .045 or glute_width < .29:
    raise ValueError("Blender glute sculpt did not create the intended visible shape.")

# Preserve the existing bone anchors and assign the sculpt to Hips.
arm_data = bpy.data.armatures.new("DF_Skeleton")
armature = bpy.data.objects.new("DF_Skeleton", arm_data)
bpy.context.collection.objects.link(armature)
bpy.context.view_layer.objects.active = armature
armature.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
edit_bones = {}
for name, anchor in zip(bone_names, anchors):
    bone = arm_data.edit_bones.new(name)
    bone.head = anchor["head"]
    bone.tail = anchor["tail"]
    edit_bones[name] = bone
for name, parent in {
    "Spine": "Hips", "Chest": "Spine", "Head": "Chest",
    "UpperArmL": "Chest", "UpperArmR": "Chest",
    "ForearmL": "UpperArmL", "ForearmR": "UpperArmR",
    "HandL": "ForearmL", "HandR": "ForearmR",
    "ThighL": "Hips", "ThighR": "Hips",
    "ShinL": "ThighL", "ShinR": "ThighR",
    "FootL": "ShinL", "FootR": "ShinR",
}.items():
    edit_bones[name].parent = edit_bones[parent]
bpy.ops.object.mode_set(mode="OBJECT")

# Pack Blender meshes back into the exact skinned format Unity already loads.
out_vertices, out_normals, out_bones = [], [], []
out_submeshes = [[] for _ in source["submeshes"]]
leg_measure_after = {}
for obj in objects:
    if obj.type != "MESH":
        continue
    bone_index = int(obj["boneIndex"])
    material_index = int(obj["materialIndex"])
    mesh = obj.data
    mesh.calc_loop_triangles()
    # Triangle indices address vertices, while out_vertices is flattened xyz data.
    offset = len(out_vertices) // 3
    local = [v.co.copy() for v in mesh.vertices]
    if bone_names[bone_index].startswith(("Thigh", "Shin")):
        a = anchors[bone_index]
        head, tail = Vector(a["head"]), Vector(a["tail"])
        axis = (tail- head).normalized()
        radius = []
        for p in local:
            offset_to_point = p - head
            t = offset_to_point.dot(axis) / max((tail-head).length, 1e-8)
            if .2 < t < .8:
                radius.append((offset_to_point-axis*offset_to_point.dot(axis)).length)
        if radius:
            leg_measure_after[bone_names[bone_index]] = sum(radius)/len(radius)
    out_vertices.extend([float(x) for p in local for x in p])
    out_normals.extend([float(x) for v in mesh.vertices for x in v.normal])
    out_bones.extend([bone_index] * len(local))
    for tri in mesh.loop_triangles:
        out_submeshes[material_index].extend(offset + i for i in tri.vertices)

print(f"Blender mesh vertex count before budget check: {len(out_vertices)//3}")
if len(out_vertices) // 3 > 45000:
    raise ValueError("Blender result exceeds the mobile mesh vertex budget.")
for name, before in leg_measure_before.items():
    after = leg_measure_after.get(name, 0)
    if after < before * 1.12:
        raise ValueError(f"{name} failed the 20% cross-section edit: {before} -> {after}")
source["vertices"] = [round(v, 6) for v in out_vertices]
source["normals"] = [round(v, 6) for v in out_normals]
source["boneIndices"] = out_bones
source["submeshes"] = [{"triangles": values} for values in out_submeshes]
MODEL.write_bytes(gzip.compress(json.dumps(source, separators=(",", ":")).encode("utf-8"), compresslevel=9, mtime=0))

# Make the complete sculpt editable in Blender and save its native project file.
for obj in objects:
    vg = obj.vertex_groups.new(name=bone_names[obj["boneIndex"]])
    vg.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    modifier = obj.modifiers.new("DF_Skin", "ARMATURE")
    modifier.object = armature
    obj.parent = armature
    obj["boneName"] = bone_names[obj["boneIndex"]]
bpy.ops.object.select_all(action="DESELECT")
for obj in objects:
    obj.select_set(True)
armature.select_set(True)
bpy.context.view_layer.objects.active = armature

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 720
scene.render.resolution_y = 960
scene.render.resolution_percentage = 100
scene.world.color = (.025, .035, .05)
for name, location, power, size in [
    ("Key", (2, -4, 4), 650, 3.5),
    ("Fill", (-3, -1, 2), 420, 3),
    ("Rim", (0, 4, 3), 700, 2.5),
]:
    lamp_data = bpy.data.lights.new(name, "AREA")
    lamp_data.energy = power
    lamp_data.shape = "DISK"
    lamp_data.size = size
    lamp = bpy.data.objects.new(name, lamp_data)
    bpy.context.collection.objects.link(lamp)
    lamp.location = location
    lamp.rotation_euler = (Vector((0, .95, 0)) - lamp.location).to_track_quat("-Z", "Y").to_euler()
camera_data = bpy.data.cameras.new("Sivra_Sculpt_Review")
camera = bpy.data.objects.new("Sivra_Sculpt_Review", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (.7, 1.0, -3.5)
camera.rotation_mode = "QUATERNION"
camera.rotation_quaternion = (Vector((0, .96, -.01)) - camera.location).to_track_quat("-Z", "Y") @ Quaternion((0, 0, 1), -math.pi / 2)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 2.12
scene.camera = camera
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(ART / "Sivra_Blender_Preview.png")
bpy.ops.wm.save_as_mainfile(filepath=str(ART / "Sivra_Quell.blend"), compress=True)
bpy.ops.render.render(write_still=True)
print(json.dumps({
    "vertices": len(out_vertices)//3,
    "triangles": sum(len(s["triangles"])//3 for s in source["submeshes"]),
    "gluteDepthIncrease": round(glute_back-old_back, 4),
    "legCrossSectionsBefore": leg_measure_before,
    "legCrossSectionsAfter": leg_measure_after,
    "boneAnchorsPreserved": len(bone_names),
}, indent=2))
