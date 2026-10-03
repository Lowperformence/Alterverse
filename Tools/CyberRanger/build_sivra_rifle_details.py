"""Blender-authored hard-surface overlay for Sivra's imported rifle.

The origin is DF_SupportGrip. +Z in Unity points toward DF_Muzzle, +Y is up.
Run with Blender 4.5: blender --background --python build_sivra_rifle_details.py
"""
from pathlib import Path
from math import radians
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ASSET = ROOT / "Assets/Alterverse/Resources/CyberRanger/SivraRifleDetails.fbx"
PREVIEW = ROOT / "ArtSource/Heroes/CyberRanger/Sivra_Rifle_Details.png"
ASSET.parent.mkdir(parents=True, exist_ok=True)
PREVIEW.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)

def material(name, color, metal, rough, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = (*color, 1)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    if emission:
        node.inputs["Emission Color"].default_value = (*color, 1)
        node.inputs["Emission Strength"].default_value = emission
    return mat

titanium = material("Sivra_Titanium", (.33, .45, .48), .82, .28)
graphite = material("Sivra_Graphite", (.026, .043, .052), .52, .48)
cyan = material("Sivra_CyanEmission", (.025, .72, .94), .24, .18, 2.2)
parts = []

def cube(name, pos, size, mat, bevel=.004, tilt=0):
    # Map Unity (x, up, forward) into Blender (x, -forward, up).
    bpy.ops.mesh.primitive_cube_add(size=1, location=(pos[0], -pos[2], pos[1]))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler[2] = radians(tilt)
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new("Machined edge", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in obj.data.polygons:
        p.use_smooth = True
    parts.append(obj)
    return obj

def bolt(name, side, up, forward):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8,
                                        location=(side, -forward, up))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (.006, .006, .006)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(graphite)
    for p in obj.data.polygons:
        p.use_smooth = True
    parts.append(obj)

# The two cheeks leave the original rifle receiver exposed between them.
for side in (-1, 1):
    cube("Stepped receiver armor", (side*.068, .020, .045),
         (.018, .073, .19), titanium, .005)
    cube("Inset carbon panel", (side*.079, .024, .040),
         (.006, .045, .135), graphite, .002)
    cube("Long luminous conduit", (side*.082, -.005, .214),
         (.008, .012, .29), cyan, .003)
    cube("Stock hinge plate", (side*.058, .005, -.115),
         (.014, .050, .064), titanium, .003)
    for i in range(4):
        z = -.008 + i*.038
        cube("Recessed heat vent", (side*.084, .039, z),
             (.006, .012, .025), graphite, .001)
    for z in (-.029, .097):
        bolt("Receiver captive bolt", side*.086, .055, z)

# Clearly legible, low profile optic with front and rear glowing lenses.
cube("Optic bridge", (0, .100, .054), (.068, .022, .139), titanium, .005)
cube("Optic main housing", (0, .136, .075), (.075, .055, .125), graphite, .010)
cube("Optic top ridge", (0, .168, .070), (.055, .009, .079), titanium, .002)
cube("Optic front aperture", (0, .137, .141), (.054, .039, .009), titanium, .003)
cube("Optic front lens", (0, .137, .147), (.043, .028, .004), cyan, .002)
cube("Optic rear lens", (0, .137, .008), (.034, .023, .004), cyan, .002)

# Front rail and muzzle hardware follow the full barrel instead of ending at the receiver.
cube("Rail bridge", (0, .071, .302), (.047, .017, .31), graphite, .003)
for i in range(6):
    cube("Rail tooth", (0, .082, .181+i*.043),
         (.055, .012, .016), titanium, .002)
for z in (.386, .547, .685):
    cube("Barrel cooling collar", (0, .000, z),
         (.063, .060, .026), titanium, .004)
    cube("Collar top insert", (0, .034, z),
         (.031, .005, .018), cyan, .001)
cube("Split muzzle brake", (0, 0, .726), (.073, .062, .043), graphite, .005)
for side in (-1, 1):
    cube("Muzzle side port", (side*.038, .007, .728),
         (.006, .024, .023), titanium, .001)

# Three renderers in Unity (one per material) keep the mobile renderer cost low.
groups = {mat.name: [obj for obj in parts if obj.data.materials[0] == mat]
          for mat in (titanium, graphite, cyan)}
merged = []
for mat in (titanium, graphite, cyan):
    group = groups[mat.name]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in group:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = group[0]
    bpy.ops.object.join()
    group[0].name = "DF_SivraRifle_" + mat.name
    merged.append(group[0])

bpy.ops.object.select_all(action="DESELECT")
for obj in merged:
    obj.select_set(True)
bpy.context.view_layer.objects.active = merged[0]
bpy.ops.export_scene.fbx(filepath=str(ASSET), use_selection=True,
                         object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                         apply_unit_scale=True, bake_space_transform=False,
                         path_mode="AUTO")

# Source preview; the in-game attachment uses the same three exported meshes.
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 960
scene.render.resolution_y = 600
scene.render.resolution_percentage = 100
world = bpy.data.worlds.new("Sivra rifle studio")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (.018, .025, .035, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = .55
for name, pos, power, size in [
    ("Key", (1.2, -1.1, 1.5), 170, 2.0),
    ("Fill", (-1.2, -.5, .7), 100, 1.6),
    ("Rim", (.2, 1.1, 1.0), 190, 1.0),
]:
    light = bpy.data.lights.new(name, "AREA")
    light.energy = power
    light.shape = "DISK"
    light.size = size
    obj = bpy.data.objects.new(name, light)
    bpy.context.collection.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (Vector((0, -.32, .08))-obj.location).to_track_quat("-Z", "Y").to_euler()
camera = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
bpy.context.collection.objects.link(camera)
camera.location = (1.05, -1.45, .72)
camera.rotation_euler = (Vector((0, -.33, .08))-camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.04
scene.camera = camera
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(PREVIEW)
bpy.ops.render.render(write_still=True)
print("SIVRA_RIFLE_DETAILS", ASSET.stat().st_size, len(merged), len(parts), flush=True)
