"""Create an original, editable UFO test hat using Blender 5.0.

Run: blender --background --python create_ufo_hat.py -- --output /absolute/folder
The accessory uses meters. Blender front=-Y/up=+Z; origin is the head-seat plane.
Studio scenery is excluded from mesh exports. No game files are modified.
"""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parent)
parser.add_argument('--no-render', action='store_true')
options = parser.parse_args(args)
out = options.output.resolve()
out.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.render.resolution_x = 1440
scene.render.resolution_y = 1120
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.view_settings.view_transform = 'AgX'
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.045, .058, .095, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .35

model_collection = bpy.data.collections.new('UFO_HAT_ONLY__EXPORT')
scene.collection.children.link(model_collection)
studio_collection = bpy.data.collections.new('STUDIO__DO_NOT_EXPORT')
scene.collection.children.link(studio_collection)
parts = []

def move_to(obj, collection):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    collection.objects.link(obj)

def material(name, color, metallic=0, roughness=.35, emission=0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = (*color, 1)
    shader = m.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    shader.inputs['Emission Color'].default_value = (*color, 1)
    shader.inputs['Emission Strength'].default_value = emission
    return m

silver = material('DF_UFO_PearlMetal', (.38, .46, .62), .72, .29)
purple = material('DF_UFO_PlumShell', (.24, .038, .49), .32, .28)
teal = material('DF_UFO_TurquoiseLight', (.025, .85, .64), .2, .25, 1.4)
white = material('DF_UFO_EyeWhite', (.94, .97, 1), .02, .28)
ink = material('DF_UFO_Midnight', (.009, .018, .035), .24, .38)
amber = material('DF_UFO_AmberBeacon', (1, .32, .035), .15, .25, .3)

def finish(obj, name, mat, smooth=True):
    obj.name = name
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = smooth
    move_to(obj, model_collection)
    parts.append(obj)
    return obj

def lathe(name, profile, mat, segments=40):
    # Profile follows a closed cross-section; the center remains an actual head opening.
    verts = [(radius * math.cos(i * math.tau / segments),
              radius * math.sin(i * math.tau / segments) * .9, z)
             for radius, z in profile for i in range(segments)]
    faces = []
    for row in range(len(profile)):
        nxt = (row + 1) % len(profile)
        for i in range(segments):
            j = (i + 1) % segments
            faces.append((row * segments + i, row * segments + j,
                          nxt * segments + j, nxt * segments + i))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    model_collection.objects.link(obj)
    return finish(obj, name, mat)

def orb(name, position, scale, mat, segments=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=position)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat)

def ball(name, position, radius, mat, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=radius, location=position)
    return finish(bpy.context.object, name, mat)

def torus(name, position, radius, thickness, mat, major=40, minor=5, flatten=.9):
    bpy.ops.mesh.primitive_torus_add(major_segments=major, minor_segments=minor,
        major_radius=radius, minor_radius=thickness, location=position)
    obj = bpy.context.object
    obj.scale.y = flatten
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat)

def rod(name, a, b, radius, mat):
    a, b = Vector(a), Vector(b)
    delta = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=delta.length, location=(a + b) / 2)
    obj = bpy.context.object
    obj.rotation_euler = delta.to_track_quat('Z', 'Y').to_euler()
    return finish(obj, name, mat)

lathe('Saucer_body', [(.145, 0), (.145, .044), (.18, .085), (.235, .106),
    (.306, .082), (.342, .061), (.342, .041), (.307, .022), (.20, .006)], silver)
lathe('Soft_inner_headband', [(.145, -.005), (.145, .025), (.153, .025), (.153, -.005)], ink, 32)
torus('Luminous_equator', (0, 0, .051), .343, .009, teal)
torus('Upper_rim_pipe', (0, 0, .089), .275, .0045, ink)
torus('Cockpit_seal', (0, 0, .103), .208, .009, ink)

# The closed purple dome is deliberately an opaque game-friendly shell.
segments, rings = 32, 6
verts = []
for row in range(rings):
    elev = row / rings * math.pi / 2
    for i in range(segments):
        angle = i / segments * math.tau
        verts.append((.208 * math.cos(elev) * math.cos(angle),
                      .187 * math.cos(elev) * math.sin(angle), .105 + .16 * math.sin(elev)))
verts.append((0, 0, .265))
faces = []
for row in range(rings - 1):
    for i in range(segments):
        j = (i + 1) % segments
        faces.append((row * segments + i, row * segments + j,
                      (row + 1) * segments + j, (row + 1) * segments + i))
for i in range(segments):
    faces.append(((rings - 1) * segments + i, (rings - 1) * segments + (i + 1) % segments, len(verts) - 1))
mesh = bpy.data.meshes.new('Cockpit_dome')
mesh.from_pydata(verts, [], faces)
obj = bpy.data.objects.new('Cockpit_dome', mesh)
model_collection.objects.link(obj)
finish(obj, 'Cockpit_dome', purple)

# Unequal eyes and deliberately mismatched pupils make the saucer look pleasantly confused.
for name, x, z, size, pupil_x, pupil_z in [
    ('Left', -.073, .193, .050, -.061, .198),
    ('Right', .074, .201, .044, .061, .211)]:
    orb(name + '_eye', (x, -.166, z), (size, .027, size * 1.12), white)
    orb(name + '_iris', (pupil_x, -.191, pupil_z), (.027, .008, .030), teal, 14, 7)
    orb(name + '_pupil', (pupil_x, -.198, pupil_z), (.017, .006, .021), ink, 14, 7)
    orb(name + '_glint', (pupil_x - .005, -.204, pupil_z + .008), (.005, .003, .006), white, 10, 6)

# A grinning mouth, like a tiny air intake, sits under the eyes.
lathe('Lower_cockpit_ring', [(.19, .113), (.19, .123), (.196, .123), (.196, .113)], purple, 24)
for i in range(5):
    x = -.033 + i * .0165
    orb('Silly_intake_tooth_' + str(i), (x, -.183, .132 + abs(i - 2) * .002), (.005, .006, .011), ink, 8, 5)

for i in range(12):
    angle = i * math.tau / 12
    ball('Saucer_bolt_' + str(i), (.299 * math.cos(angle), .269 * math.sin(angle), .084), .007, amber, 1)

left = [(-.105, .035, .241), (-.129, .048, .325), (-.196, .044, .382)]
right = [(.104, .028, .242), (.155, .008, .318), (.141, -.014, .360)]
for name, points, color, size in [('Left', left, amber, .025), ('Right', right, teal, .030)]:
    ball(name + '_antenna_socket', points[0], .021, ink, 1)
    for i in range(len(points) - 1):
        rod(name + '_crooked_antenna_' + str(i), points[i], points[i + 1], .008, silver)
    ball(name + '_antenna_elbow', points[1], .011, purple, 1)
    ball(name + '_beacon', points[-1], size, color, 2)

# One export mesh with six materials; studio, cameras, lights and helpers stay separate.
bpy.ops.object.select_all(action='DESELECT')
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
hat = bpy.context.object
hat.name = 'DF_UFO_TEST_HAT'
scene.cursor.location = (0, 0, 0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bm = bmesh.new()
bm.from_mesh(hat.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=.00001)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.triangulate(bm, faces=bm.faces)
bm.to_mesh(hat.data)
bm.free()
hat.data.update()
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.151917, island_margin=.025)
bpy.ops.object.mode_set(mode='OBJECT')

# Collapse duplicate slots introduced by joining, preserving polygon assignments.
old_slots = list(hat.data.materials)
unique = []
for m in old_slots:
    if m not in unique:
        unique.append(m)
assignments = [unique.index(old_slots[p.material_index]) for p in hat.data.polygons]
hat.data.materials.clear()
for m in unique:
    hat.data.materials.append(m)
for poly, index in zip(hat.data.polygons, assignments):
    poly.material_index = index
hat['purpose'] = 'Additional UFO test headwear; intended shop price 1 shard; SKU ufo_test_hat'
hat['attachment'] = 'Origin = head-seat center; Blender +Z up, -Y front'
hat['head_opening_m'] = [.290, .261]
hat['deformation'] = 'Rigid accessory. Parent to a head bone; no own rig, collider or animation.'

triangles = len(hat.data.polygons)
assert all(len(p.vertices) == 3 for p in hat.data.polygons)
assert triangles < 5500, f'Mesh budget exceeded: {triangles}'
assert len(hat.data.materials) == 6
assert all(math.isfinite(c) for v in hat.data.vertices for c in v.co)
assert hat.location.length < .00001 and all(abs(s - 1) < .00001 for s in hat.scale)

# Unity consumes the actual Blender mesh, normalized around its head opening.
# This avoids importing FBX units, extra transforms or incompatible material shaders at runtime.
unity = {'vertices': [round(c, 7) for v in hat.data.vertices
    for c in (v.co.x / .145, v.co.z / .145, -v.co.y / .145)],
    'triangles': [index for p in hat.data.polygons for index in p.vertices],
    'submeshes': [p.material_index for p in hat.data.polygons], 'materials': []}
for m in hat.data.materials:
    bsdf = m.node_tree.nodes['Principled BSDF']
    color = bsdf.inputs['Base Color'].default_value
    unity['materials'].append({'r': color[0], 'g': color[1], 'b': color[2],
        'metallic': bsdf.inputs['Metallic'].default_value,
        'smoothness': 1 - bsdf.inputs['Roughness'].default_value,
        'emission': bsdf.inputs['Emission Strength'].default_value})
(out / 'UfoTestHat.json').write_text(json.dumps(unity, separators=(',', ':')), encoding='utf-8')

bpy.ops.export_scene.fbx(filepath=str(out / 'UFO-Testhut.fbx'), use_selection=True,
    object_types={'MESH'}, global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    bake_space_transform=False, use_mesh_modifiers=True, use_triangles=True,
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', path_mode='AUTO')
bpy.ops.export_scene.gltf(filepath=str(out / 'UFO-Testhut.glb'), export_format='GLB',
    use_selection=True, export_yup=True, export_materials='EXPORT', export_extras=True,
    export_cameras=False, export_lights=False, export_animations=False)

# Neutral presentation stage, deliberately excluded from the accessory exports.
floor_mat = material('Studio_floor', (.022, .030, .055), .12, .64)
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.018))
floor = bpy.context.object
floor.name = 'STUDIO_FLOOR'
floor.data.materials.append(floor_mat)
move_to(floor, studio_collection)

def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()

def light(name, position, energy, color, size):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy
    data.color = color
    data.shape = 'DISK'
    data.size = size
    obj = bpy.data.objects.new(name, data)
    studio_collection.objects.link(obj)
    obj.location = position
    aim(obj, (0, 0, .12))

light('Key_softbox', (-.9, -1.3, 1.8), 160, (.84, .9, 1), 1.3)
light('Warm_fill', (1.1, -.55, .8), 95, (1, .80, .67), 1.0)
light('Mint_rim', (.2, 1.0, 1.4), 200, (.35, 1, .85), 1.0)
camera_data = bpy.data.cameras.new('UFO_BEAUTY')
camera = bpy.data.objects.new('UFO_BEAUTY', camera_data)
studio_collection.objects.link(camera)
camera.location = (.67, -1.60, .89)
aim(camera, (0, 0, .16))
camera_data.type = 'ORTHO'
camera_data.ortho_scale = .95
camera_data.lens = 65
scene.camera = camera

for area in bpy.context.screen.areas if bpy.context.screen else []:
    if area.type == 'VIEW_3D':
        area.spaces.active.region_3d.view_distance = 1.05
        area.spaces.active.region_3d.view_location = Vector((0, 0, .16))

bpy.ops.object.select_all(action='DESELECT')
hat.select_set(True)
bpy.context.view_layer.objects.active = hat
manifest = {'name': 'UFO-Testhut', 'created_with': bpy.app.version_string,
    'units': 'meters', 'vertices': len(hat.data.vertices), 'triangles': triangles,
    'materials': [m.name for m in hat.data.materials], 'dimensions_m': list(hat.dimensions),
    'head_opening_m': [.290, .261], 'pivot': [0, 0, 0],
    'blender_axes': 'X right / -Y front / Z up',
    'exports': ['UFO-Testhut.blend', 'UFO-Testhut.fbx', 'UFO-Testhut.glb'],
    'scope': 'UFO test headwear source, intended shop price 1 shard; deployment is separate.'}
(out / 'model-checks.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
scene.render.filepath = str(out / 'UFO-Testhut-Vorschau.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out / 'UFO-Testhut.blend'), compress=True)
if not options.no_render:
    bpy.ops.render.render(write_still=True)
    # A second angle makes the open headband and attachment plane easy to inspect.
    camera.location = (.7, -1.25, -.55)
    aim(camera, (0, 0, .12))
    floor.hide_render = True
    scene.render.filepath = str(out / 'UFO-Testhut-Unterseite.png')
    scene.render.resolution_x, scene.render.resolution_y = 1000, 800
    bpy.ops.render.render(write_still=True)
print('UFO_MODEL_READY ' + json.dumps(manifest), flush=True)
