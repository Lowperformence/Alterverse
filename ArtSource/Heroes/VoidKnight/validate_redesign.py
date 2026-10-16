"""Check the new meshes preserve all original skeletal animation and grip geometry."""
from pathlib import Path
import bpy,json,math,argparse
from mathutils import Vector
HERE=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--before',type=Path);args=parser.parse_args()
baseline=None
if args.before:
 bpy.ops.wm.open_mainfile(filepath=str(args.before))
 baseline={o.name:[v.co.copy() for v in o.data.vertices] for o in bpy.data.objects if o.type=='MESH'}

def poses(path):
 bpy.ops.wm.open_mainfile(filepath=str(path));rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.data.pose_position='POSE'
 for track in rig.animation_data.nla_tracks:track.mute=True
 result={}
 for action in bpy.data.actions:
  rig.animation_data.action=action
  for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
   bpy.context.scene.frame_set(frame)
   result[(action.name,frame)]={b.name:tuple(x for row in b.matrix for x in row) for b in rig.pose.bones}
 return result
before=poses(HERE/'VoidKnight-v1-reference.blend');after=poses(HERE/'VoidKnight.blend')
if next(o for o in bpy.data.objects if o.type=='ARMATURE').get('design_version',2)>=3:
 import runpy
 runpy.run_path(str(HERE/'validate_refinement.py'),run_name='__main__')
else:
 assert before.keys()==after.keys()
 error=max(abs(x-y) for key in before for bone in before[key] for x,y in zip(before[key][bone],after[key][bone]))
 assert error<1e-6,error
 rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');center=Vector(rig['grip_center']);axis=Vector(rig['grip_axis'])
 g=bpy.data.objects['VK2_SwordGrip'];coords=[(v.co-center).dot(axis) for v in g.data.vertices]
 assert abs(min(coords)+.19)<.002 and abs(max(coords)-.19)<.002
 meshes=[o for o in bpy.data.objects if o.type=='MESH'];assert len(meshes)==214
 hand_extents={}
 assert abs(rig['hand_scale']-1.3)<1e-6
 for label in ['L','R']:
  parts=[o for o in meshes if o.get('bone')=='Hand'+label]
  coords=[v.co for o in parts for v in o.data.vertices]
  extent=[max(v[i] for v in coords)-min(v[i] for v in coords) for i in range(3)]
  ratio=[extent[i]/rig['hand_unscaled_extent_'+label][i] for i in range(3)]
  assert all(abs(r-1.3)<1e-5 for r in ratio),ratio
  assert all(abs(o['hand_scale']-1.3)<1e-6 for o in parts)
  hand_extents[label]={'parts':len(parts),'axis_scale_ratios':ratio}
 for mesh in meshes:
  assert mesh.get('bone') in rig.data.bones
  for vertex in mesh.data.vertices:
   assert all(math.isfinite(v) for v in vertex.co)
   assert abs(sum(g.weight for g in vertex.groups)-1)<1e-6
 report={'source_parts':len(meshes),'compared_animation_frames':len(before),'maximum_bone_matrix_change':error,'two_hand_grip_span':round(max((v.co-center).dot(axis) for v in g.data.vertices)-min((v.co-center).dot(axis) for v in g.data.vertices),4),'hand_scale':rig['hand_scale'],'hands':hand_extents,'result':'PASS'}
 if baseline:
  assert set(baseline)=={o.name for o in meshes}
  maximum_error=0;hand_parts=0
  for obj in meshes:
   old=baseline[obj.name];assert len(old)==len(obj.data.vertices)
   is_hand=obj.get('bone') in ['HandL','HandR']
   if is_hand:hand_parts+=1
   pivot=Vector(rig['hand_scale_pivot_'+obj['bone'][-1]]) if is_hand else Vector()
   for original,current in zip(old,obj.data.vertices):
    expected=pivot+(original-pivot)*1.3 if is_hand else original
    maximum_error=max(maximum_error,(current.co-expected).length)
  assert maximum_error<1e-6,maximum_error
  report['baseline_vertex_comparison']={'scaled_hand_parts':hand_parts,'unchanged_other_parts':len(meshes)-hand_parts,'maximum_vertex_error':maximum_error}
 (HERE/'Validation/redesign-checks.json').write_text(json.dumps(report,indent=2)+'\n');print(report)
