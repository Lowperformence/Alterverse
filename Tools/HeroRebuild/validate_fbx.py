import bpy,sys,os,json
from pathlib import Path
root=str(next(p for p in Path(__file__).resolve().parents if (p/'Assets/Alterverse').is_dir()));rows=[]
for hero in ['Veyr','Kaelon','Nyra','Elyra','Mira','Rook','Traveler']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=os.path.join(root,'Assets/Alterverse/Resources/AuthoredHeroes',hero+'.fbx'))
 meshes=[o for o in bpy.data.objects if o.type=='MESH'];arms=[o for o in bpy.data.objects if o.type=='ARMATURE'];actions=[a.name for a in bpy.data.actions]
 assert len(meshes)==1,(hero,'mesh count',len(meshes))
 assert len(arms)==1,(hero,'armature count',len(arms))
 for name in ['Idle','Run','Attack','Skill0','Skill1','Skill2','Ultimate','Hit','Death']:assert any(a.endswith('|'+name) or a==name for a in actions),(hero,'missing animation',name,actions)
 assert bpy.data.objects.get('DF_Muzzle'),(hero,'missing muzzle')
 m=meshes[0];assert len(m.vertex_groups)>=16
 assert all(len(v.groups)>0 for v in m.data.vertices),(hero,'unweighted vertex')
 assert m.data.uv_layers,(hero,'no UVs')
 bounds=[(min(v.co[i] for v in m.data.vertices),max(v.co[i] for v in m.data.vertices)) for i in range(3)]
 rows.append({'hero':hero,'meshCount':len(meshes),'bones':len(arms[0].data.bones),'animations':actions,'vertices':len(m.data.vertices),'bounds':bounds,'materials':len(m.data.materials),'weighted':True})
 print('VALIDATED',hero,actions,flush=True)
with open(os.path.join(root,'fbx-validation.json'),'w') as f:json.dump(rows,f,indent=2)
