"""Run: blender --background --python import_blender.py
Rebuilds meshes directly from local original source; does not import old assets.
Creates assembled collection and a hidden exploded collection, no armature yet.
"""
from pathlib import Path
import sys
import bpy
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
import build_traveler as src
src.build();src.validate()
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
assembled=bpy.data.collections.new('Traveler_Nocturne_V01')
exploded=bpy.data.collections.new('Traveler_Exploded_V01')
scene.collection.children.link(assembled);scene.collection.children.link(exploded)
materials=[]
for name,color,metal,rough in src.MATS:
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bsdf=m.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(*color,1);bsdf.inputs['Metallic'].default_value=metal;bsdf.inputs['Roughness'].default_value=rough;materials.append(m)
# Unity Y-up Z-front -> Blender Z-up -Y-front, proper rotation (no reflection).
def convert(v):return (float(v[0]),-float(v[2]),float(v[1]))
for p in src.PARTS:
    mesh=bpy.data.meshes.new(p['name']);mesh.from_pydata([convert(v) for v in p['v']],[],p['f'].tolist());mesh.update();mesh.materials.append(materials[p['mat']])
    obj=bpy.data.objects.new(p['name'],mesh);assembled.objects.link(obj)
    obj['suggested_bone']=p['bone'];obj['part_group']=p['group'];obj['provenance']='Original procedural geometry, ChatGPT-assisted, 2026-10-12'
    bevel=obj.modifiers.new('MicroEdge','BEVEL');bevel.width=.002;bevel.segments=2
    # Shell-wall geometry is already modeled; bevel is nondestructive.
    clone=obj.copy();clone.name='EXP_'+p['name'];exploded.objects.link(clone);clone.location=convert(p['explode'])
exploded.hide_render=True;exploded.hide_viewport=True
socket=bpy.data.objects.new('DF_Muzzle',None);assembled.objects.link(socket);socket.location=convert((.69,1.24,.16))
socket['note']='Orb centre. Parent to WeaponGripR during final rigging.'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Traveler_Nocturne_V01.blend'))
print('Saved editable Blender prototype, unrigged.')
