import bpy,json,gzip,math,hashlib
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(__file__).resolve().parents[2];art=root/'ArtSource/Heroes/BrakkArmor'
bpy.ops.wm.open_mainfile(filepath=str(art/'BrakkArmor-concept.blend'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE' and o.name=='DF_Skeleton');arm.data.pose_position='POSE'
for track in arm.animation_data.nla_tracks:track.mute=True
belt=[o for o in bpy.data.objects if o.name.startswith('BRK_U') and o.type=='MESH']
assert len(belt)==34
samples=0;worst=0.
for action in bpy.data.actions:
 if action.name not in ['Attack','Death','Hit','Idle','Run','Skill0','Skill1','Skill2','Ultimate']:continue
 arm.animation_data.action=action
 for frame in [int(action.frame_range[0]),int(sum(action.frame_range)/2),int(action.frame_range[1])]:
  bpy.context.scene.frame_set(frame);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
  transform=arm.matrix_world@arm.pose.bones['Hips'].matrix@arm.data.bones['Hips'].matrix_local.inverted()
  for ob in belt:
   assert all(len(v.groups)==1 and ob.vertex_groups[v.groups[0].group].name=='Hips' and v.groups[0].weight==1 for v in ob.data.vertices)
   evaluated=ob.evaluated_get(deps);mesh=evaluated.to_mesh()
   for index in [0,len(mesh.vertices)//2,len(mesh.vertices)-1]:
    error=(evaluated.matrix_world@mesh.vertices[index].co-transform@ob.data.vertices[index].co).length
    worst=max(worst,error);assert error<2e-5,error
   evaluated.to_mesh_clear()
  samples+=1
assert samples==27,samples
report=json.loads((art/'validation.json').read_text());report['checks']['beltAnimationSamples']=samples;report['checks']['maxBeltAttachmentErrorMetres']=worst
# All plate layers are closed solids; test real edges, rather than generator claims.
import bmesh
plateObjects=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('BRK_') and not o.name.startswith('BRK_U')]
assert len(plateObjects)==126
for ob in plateObjects+belt:
 bm=bmesh.new();bm.from_mesh(ob.data)
 assert all(e.is_manifold for e in bm.edges),(ob.name,'open or nonmanifold edge')
 assert abs(bm.calc_volume())>1e-10,(ob.name,'zero volume')
 bm.free()
report['checks']['all160PartsClosedManifoldNonzeroVolume']=True
(art/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('ARMOR_RUNTIME_DATA_VALIDATED',json.dumps(report),flush=True)
