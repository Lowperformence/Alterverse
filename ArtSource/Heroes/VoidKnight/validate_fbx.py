import bpy,math,json
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Alterverse/Resources/AuthoredHeroes/VoidKnight.fbx'))
meshes=[o for o in bpy.data.objects if o.type=='MESH']
assert {o.name for o in meshes}=={'DF_Anatomy','DF_WeaponR'}
expected={'Idle','Run','Attack1','Attack2','Attack3','Skill0','Skill1','Skill2','Ultimate','Counter','Hit','Death'}
assert {a.name.rsplit('|',1)[-1] for a in bpy.data.actions}==expected
triangles=0
for mesh in meshes:
 mesh.data.calc_loop_triangles();triangles+=len(mesh.data.loop_triangles)
 for v in mesh.data.vertices:
  assert all(math.isfinite(x) for x in v.co)
  assert abs(sum(g.weight for g in v.groups)-1)<1e-5
assert triangles <= 35000, triangles
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
assert len(rig.data.bones)==19
for track in rig.animation_data.nla_tracks:track.mute=True
frames=0
for action in bpy.data.actions:
 rig.animation_data.action=action
 for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
  bpy.context.scene.frame_set(frame);frames+=1
  for bone in rig.pose.bones:assert all(math.isfinite(x) for row in bone.matrix for x in row)
report={'mesh_count':len(meshes),'triangles':triangles,'bones':len(rig.pose.bones),'clips':len(expected),'finite_frames':frames,'result':'PASS'}
(HERE/'fbx-validation.json').write_text(json.dumps(report,indent=2)+'\n');print(report)
