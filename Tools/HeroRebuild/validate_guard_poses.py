import bpy,math,json
from pathlib import Path
from mathutils import Vector,Matrix
root=next(p for p in Path(__file__).resolve().parents if (p/'Assets/Alterverse').is_dir())
T={'Veyr':[(-.2397,1.122,.207),(.2397,1.122,.207)],'Kaelon':[(-.3192,1.134,.257),(.349,1.1664,.236)],'Nyra':[(.15,1.26,.48),(.09,1.2648,.23)],'Elyra':[(-.187,1.199,.1932),(.292,1.199,.1472)],'Mira':[(-.192,1.1016,.152),(.223,1.1016,.171)],'Rook':[(-.368,1.2376,.364),(.368,1.2376,.364)],'Traveler':[(-.244,1.1342,.155),(.2575,1.177,.194)]}
rows=[]
for hero,targets in T.items():
 bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/Heroes'/f'{hero}.blend'))
 arm=bpy.data.objects['DF_Skeleton'];arm.animation_data_clear()
 for bone in arm.pose.bones:bone.matrix_basis=Matrix.Identity(4)
 bpy.context.view_layer.update()
 def solve(side,target):
  a=arm.pose.bones['UpperArm'+side];b=arm.pose.bones['Forearm'+side];hand=arm.pose.bones['Hand'+side];origin=a.head.copy()
  l1=(b.head-origin).length;l2=(hand.head-b.head).length;delta=target-origin;dist=min(max(delta.length,abs(l1-l2)+.001),l1+l2-.001);direction=delta.normalized()
  bend=Vector((-1 if side=='L' else 1,-.10,-.40));bend-=direction*bend.dot(direction);bend.normalize()
  along=(l1*l1+dist*dist-l2*l2)/(2*dist);elbow=origin+direction*along+bend*math.sqrt(max(0,l1*l1-along*along));tip=origin+direction*dist
  for bone,start,end in [(a,origin,elbow),(b,elbow,tip)]:
   rest=bone.bone.matrix_local.to_quaternion();axis=rest@Vector((0,1,0));rot=axis.rotation_difference((end-start).normalized())@rest
   bone.matrix=Matrix.LocRotScale(start,rot,Vector((1,1,1)));bpy.context.view_layer.update()
  rot=b.matrix.to_quaternion()@b.bone.matrix_local.to_quaternion().inverted()@hand.bone.matrix_local.to_quaternion()
  hand.matrix=Matrix.LocRotScale(tip,rot,Vector((1,1,1)));bpy.context.view_layer.update()
 for side,p in zip(['L','R'],targets):
  if hero=='Nyra' and side=='L':continue
  solve(side,Vector((p[0],-p[2],p[1])))
 if hero=='Nyra':
  for i in range(2):
   offset=bpy.data.objects['DF_PalmGripL'].matrix_world.translation-arm.pose.bones['HandL'].head
   solve('L',bpy.data.objects['DF_SupportGrip'].matrix_world.translation-offset)
  miss=(bpy.data.objects['DF_PalmGripL'].matrix_world.translation-bpy.data.objects['DF_SupportGrip'].matrix_world.translation).length
  assert miss<.012,('rifle support',miss)
 else:miss=0
 errors=[]
 for side in ['L','R']:
  contact=bpy.data.objects.get('DF_HandleContact'+side)
  if contact:
   error=(contact.matrix_world.translation-bpy.data.objects['DF_PalmGrip'+side].matrix_world.translation).length
   assert error<.002,(hero,side,error);errors.append(error)
 bpy.context.scene.cycles.samples=4;bpy.context.scene.cycles.use_denoising=False;bpy.context.scene.render.filepath=str(root/'ArtSource/Heroes/Preview'/f'{hero}-GripCheck.png');bpy.ops.render.render(write_still=True)
 rows.append({'hero':hero,'handlePalmDistanceMeters':errors,'rifleSupportDistanceMeters':miss})
 print('POSE_GRIP_CHECK',hero,errors,miss,flush=True)
(root/'grip-pose-validation.json').write_text(json.dumps(rows,indent=2))
