import bpy,json,bmesh
from pathlib import Path
art=Path(__file__).resolve().parents[2]/'ArtSource/Heroes/BrakkArmor'
items=json.loads((art/'individual-models.json').read_text())
for item in items:
 bpy.ops.wm.open_mainfile(filepath=str(art/item['file']))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 assert len(meshes)==len(item['objects']),(item['file'],len(meshes))
 assert bpy.context.scene.unit_settings.scale_length==1
 assert bpy.context.scene.camera is not None
 assert not any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
 for ob in meshes:
  assert ob.parent is None and len(ob.modifiers)==0
  bm=bmesh.new();bm.from_mesh(ob.data)
  assert all(e.is_manifold for e in bm.edges) and abs(bm.calc_volume())>1e-10
  bm.free()
 print('OPENED',item['file'],flush=True)
print('INDIVIDUAL_FILES_VALIDATED',len(items),flush=True)
