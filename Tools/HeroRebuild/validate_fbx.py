import bpy,sys,os,json,math
from pathlib import Path
root=str(next(p for p in Path(__file__).resolve().parents if (p/'Assets/Alterverse').is_dir()));rows=[]
selected=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for hero in selected or ['Veyr','Kaelon','Nyra','Elyra','Mira','Rook','Traveler']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=os.path.join(root,'Assets/Alterverse/Resources/AuthoredHeroes',hero+'.fbx'))
 meshes=[o for o in bpy.data.objects if o.type=='MESH'];arms=[o for o in bpy.data.objects if o.type=='ARMATURE'];actions=[a.name for a in bpy.data.actions]
 expected=['DF_GauntletL','DF_GauntletR'] if hero=='Rook' else ['DF_WeaponL','DF_WeaponR'] if hero in ['Veyr','Kaelon'] else ['DF_WeaponR']
 assert len(meshes)==len(expected)+1,(hero,'mesh count',len(meshes))
 assert bpy.data.objects.get('DF_Anatomy'),(hero,'missing body mesh')
 for weapon in expected:
  obj=bpy.data.objects.get(weapon);assert obj is not None and obj.type=='MESH',(hero,'shop weapon missing',weapon)
  assert any(m.type=='ARMATURE' for m in obj.modifiers),(hero,'unbound weapon',weapon)
 assert len(arms)==1,(hero,'armature count',len(arms))
 arm=arms[0]
 assert len(arm.data.bones)==19,(hero,'anatomical + independent weapon bones')
 for side in ['L','R']:
  assert arm.data.bones['WeaponGrip'+side].parent==arm.data.bones['Hand'+side]
 for obj in meshes:
  for v in obj.data.vertices:
   used=[obj.vertex_groups[g.group].name for g in v.groups if g.weight>.00001]
   if obj.name=='DF_Anatomy':assert not any(n.startswith('WeaponGrip') for n in used),(hero,'weapon angle affects body')
   else:
    side=obj.name[-1];expected_group=('Hand' if hero=='Rook' else 'WeaponGrip')+side
    assert used==[expected_group],(hero,obj.name,'wrong independent grip weights',used)
 idle=next(a for a in bpy.data.actions if a.name.endswith('|Idle') or a.name=='Idle')
 arm.animation_data_create();arm.animation_data.action=idle;bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
 for side in ['L','R']:
  forearm=arm.pose.bones['Forearm'+side];hand=arm.pose.bones['Hand'+side];grip=arm.pose.bones['WeaponGrip'+side]
  rest=forearm.bone.matrix_local.to_quaternion().inverted()@hand.bone.matrix_local.to_quaternion()
  posed=forearm.matrix.to_quaternion().inverted()@hand.matrix.to_quaternion()
  angle=rest.rotation_difference(posed).angle
  deviation=min(angle,abs(math.tau-angle))
  assert deviation<.09,(hero,side,'bent idle wrist',deviation)
  assert (hand.head-grip.head).length<.005,(hero,side,'weapon grip detached')
 for name in ['Idle','Run','Attack','Skill0','Skill1','Skill2','Ultimate','Hit','Death']:assert any(a.endswith('|'+name) or a==name for a in actions),(hero,'missing animation',name,actions)
 assert bpy.data.objects.get('DF_Muzzle'),(hero,'missing muzzle')
 m=bpy.data.objects['DF_Anatomy'];assert len(m.vertex_groups)>=16
 for obj in meshes:
  assert all(len(v.groups)>0 for v in obj.data.vertices),(hero,obj.name,'unweighted vertex')
  assert all(abs(sum(g.weight for g in v.groups)-1)<.002 for v in obj.data.vertices),(hero,'non-normalized weights')
 assert m.data.uv_layers,(hero,'no body UVs')
 bounds=[(min(v.co[i] for v in m.data.vertices),max(v.co[i] for v in m.data.vertices)) for i in range(3)]
 rows.append({'hero':hero,'meshCount':len(meshes),'bones':len(arms[0].data.bones),'animations':actions,'vertices':len(m.data.vertices),'bounds':bounds,'materials':len(m.data.materials),'weighted':True})
 print('VALIDATED',hero,actions,flush=True)
with open(os.path.join(root,'fbx-validation.'+selected[0]+'.json' if len(selected)==1 else 'fbx-validation.json'),'w') as f:json.dump(rows,f,indent=2)
