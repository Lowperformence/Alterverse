"""Run inside Blender's Text Editor after extracting the complete package."""
from pathlib import Path
import bpy, json
# Change this only if Blender's Text Editor has no backing filepath.
ROOT = Path(__file__).resolve().parent
data=json.loads((ROOT/'parts.json').read_text())
collection=bpy.data.collections.new('BRACK_P01')
bpy.context.scene.collection.children.link(collection)
colors={'skin':(.40,.22,.13,1),'hair':(.045,.035,.025,1),'graphite':(.13,.16,.18,1),
        'petrol':(.055,.18,.20,1),'ochre':(.63,.36,.12,1),'amber':(.95,.58,.12,1)}
mats={}
for name,color in colors.items():
    mat=bpy.data.materials.new('BRACK_'+name); mat.diffuse_color=color
    mat.use_nodes=True; node=mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value=color
    node.inputs['Metallic'].default_value=.65 if name=='graphite' else 0
    node.inputs['Roughness'].default_value=.45 if name=='graphite' else .75
    mats[name]=mat
for p in data:
    mesh=bpy.data.meshes.new(p['name']); mesh.from_pydata(p['vertices'],[],p['faces']); mesh.update()
    obj=bpy.data.objects.new(p['name'],mesh); collection.objects.link(obj)
    obj.data.materials.append(mats[p['material']]); obj['part_group']=p['group']
    bevel=obj.modifiers.new('Prototype edge rounding','BEVEL'); bevel.width=.008; bevel.segments=2
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.scene.unit_settings.scale_length=1
print('BRACK imported as separate meshes. No rig/UV/final sculpt included.')
