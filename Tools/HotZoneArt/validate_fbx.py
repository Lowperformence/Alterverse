import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(root/'ArtSource/HotZoneMonsters/HotZoneParts.fbx'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(objects)==35
for o in objects:
 assert o.data.uv_layers and len(o.data.polygons)>0
 assert all(abs(a-1)<1e-5 for a in o.dimensions),(o.name,list(o.dimensions))
assert not any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
(root/'ArtSource/HotZoneMonsters/fbx-validation.json').write_text(json.dumps({'parts':35,'localDimensions':[1,1,1],'uvs':'passed','rigsReplaced':0,'result':'passed'},indent=2));print('HOTZONE_FBX_VALIDATED',len(objects))
