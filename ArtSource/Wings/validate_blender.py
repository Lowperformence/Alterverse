"""Run in Blender after opening ApprovedWings.blend; no scene changes are saved."""
import bpy
import json
from pathlib import Path
source=json.loads((Path(__file__).resolve().parent/'PremiumWingParts.json').read_text())
for key in ['Angel','RGB']:
 scene=bpy.data.scenes['WingShop_'+key];bpy.context.window.scene=scene
 objects=[o for o in scene.objects if o.type=='MESH']
 assert len(objects)==len(source['models'][key])
 rig=next(o for o in scene.objects if o.type=='ARMATURE')
 assert len(rig.data.bones)==5
 for obj in objects:
  assert not obj.data.validate(verbose=False),obj.name
  assert len(obj.vertex_groups)==1
  assert obj.modifiers['Rigid wing rig'].object==rig
 rig['Explosion']=1;rig.update_tag();scene.frame_set(2);bpy.context.view_layer.update()
 deps=bpy.context.evaluated_depsgraph_get()
 assert any(o.evaluated_get(deps).location.length>.1 for o in objects)
 rig['Explosion']=0;rig.update_tag();scene.frame_set(1);bpy.context.view_layer.update()
 deps=bpy.context.evaluated_depsgraph_get()
 assert all(o.evaluated_get(deps).location.length<1e-6 for o in objects)
 assert rig.animation_data.action is not None
 print('PASS native Blender',key,len(objects),'meshes, rigid rig, idle clip and explosion control')
assert any(m.name.startswith('WS_RGB_Channel') for m in bpy.data.materials)
print('PASS .blend source validation')
