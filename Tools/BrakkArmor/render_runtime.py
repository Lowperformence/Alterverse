"""Render actual shipped armor, separately from the unavailable Unity renderer."""
import argparse, bpy, gzip, json
from pathlib import Path
from mathutils import Matrix, Vector
ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/Heroes/BrakkArmor'
parser = argparse.ArgumentParser(); parser.add_argument('--resource', type=Path); parser.add_argument('--output', type=Path); args = parser.parse_args()
data = json.loads(gzip.decompress((args.resource or ROOT / 'Assets/Alterverse/Resources/BrakkArmor/Model.bytes').read_bytes()))
bpy.ops.wm.read_factory_settings(use_empty=True)
materials = []
for surface in data['materials']:
    mat = bpy.data.materials.new(surface['name']); mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*surface['color'], 1)
    shader.inputs['Metallic'].default_value = surface['metallic']; shader.inputs['Roughness'].default_value = 1-surface['smoothness']
    materials.append(mat)
B = Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
points = [B @ Vector(data['vertices'][i:i+3]) for i in range(0, len(data['vertices']), 3)]
normals = [B.to_3x3() @ Vector(data['normals'][i:i+3]) for i in range(0,len(data['normals']),3)]
for part in data['parts']:
    first = part['firstVertex']; end = first + part['vertexCount']; indices = data['submeshes'][part['material']]['triangles']
    triangles = [tuple(i-first for i in indices[t:t+3]) for t in range(0, len(indices), 3) if first <= indices[t] < end]
    mesh = bpy.data.meshes.new(part['id']); mesh.from_pydata(points[first:end], [], triangles); mesh.update()
    for polygon in mesh.polygons:polygon.use_smooth=True
    mesh.normals_split_custom_set_from_vertices(normals[first:end])
    obj = bpy.data.objects.new(part['id'], mesh); bpy.context.collection.objects.link(obj); mesh.materials.append(materials[part['material']])
scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.samples = 20
scene.render.resolution_x = 1000; scene.render.resolution_y = 1200; scene.render.resolution_percentage = 100
world = bpy.data.worlds.new('Neutral studio'); world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (.06,.07,.09,1); world.node_tree.nodes['Background'].inputs[1].default_value = .4; scene.world = world
def aim(obj, target): obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for location, power, size in [((2,-4,4),500,3),((-3,-2,3),400,3),((1,3,3),500,2)]:
    bpy.ops.object.light_add(type='AREA', location=location); obj=bpy.context.object; obj.data.energy=power; obj.data.size=size; aim(obj,(0,0,1))
bpy.ops.object.camera_add(location=(1.7,-6,2.2)); scene.camera=bpy.context.object; scene.camera.data.type='ORTHO'; scene.camera.data.ortho_scale=1.95; aim(scene.camera,(0,0,.98))
scene.render.filepath = str(args.output or ART/'BrakkArmor-runtime-repaired.png')
bpy.ops.render.render(write_still=True)
