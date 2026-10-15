"""Check the new meshes preserve all original skeletal animation and grip geometry."""
from pathlib import Path
import bpy,json,math
from mathutils import Vector
HERE=Path(__file__).resolve().parent

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
assert before.keys()==after.keys()
error=max(abs(x-y) for key in before for bone in before[key] for x,y in zip(before[key][bone],after[key][bone]))
assert error<1e-6,error
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');center=Vector(rig['grip_center']);axis=Vector(rig['grip_axis'])
g=bpy.data.objects['VK2_SwordGrip'];coords=[(v.co-center).dot(axis) for v in g.data.vertices]
assert abs(min(coords)+.19)<.002 and abs(max(coords)-.19)<.002
meshes=[o for o in bpy.data.objects if o.type=='MESH'];assert len(meshes)==214
for mesh in meshes:
 assert mesh.get('bone') in rig.data.bones
 for vertex in mesh.data.vertices:
  assert all(math.isfinite(v) for v in vertex.co)
  assert abs(sum(g.weight for g in vertex.groups)-1)<1e-6
report={'source_parts':len(meshes),'compared_animation_frames':len(before),'maximum_bone_matrix_change':error,'two_hand_grip_span':round(max(coords)-min(coords),4),'result':'PASS'}
(HERE/'Validation/redesign-checks.json').write_text(json.dumps(report,indent=2)+'\n');print(report)
