"""Blender 4.5: bake CC0 Quaternius Standard clips into anatomical world frames.
Usage: blender -b --python Tools/CombatAnimation/bake_combat_library.py -- SOURCE_DIRECTORY
No root motion or gameplay data is exported. Never transfer FBX local Euler angles.
"""
import bpy, sys, json, gzip, math
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
SOURCE=Path(sys.argv[sys.argv.index('--')+1])
OUT=ROOT/'Assets/Alterverse/Resources/CombatAnimation'
ART=ROOT/'ArtSource/Animations'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
NAMES=['Hips','Spine','Chest','Head','UpperArmL','ForearmL','HandL','UpperArmR','ForearmR','HandR','ThighL','ShinL','FootL','ThighR','ShinR','FootR']
SOURCE_NAMES=['pelvis','spine_01','spine_03','Head','upperarm_l','lowerarm_l','hand_l','upperarm_r','lowerarm_r','hand_r','thigh_l','calf_l','foot_l','thigh_r','calf_r','foot_r']
# Source FBX WORLD has +Y forward/+Z up. Unity canonical is +Z forward/+Y up.
C=Matrix(((1,0,0),(0,0,1),(0,1,0)))
def canonical_frame(direction):
 z=direction.normalized();up=Vector((0,0,1))
 if abs(z.dot(up))>.98:up=Vector((0,1,0))
 x=up.cross(z).normalized();y=z.cross(x).normalized()
 return Matrix((x,y,z)).transposed()
def spec(name,parts,contact=0,loop=False):return dict(name=name,parts=parts,contact=contact,loop=loop)
SPECS={1:[
 spec('idle_relaxed',[('Idle_Loop',1,76)],0,True),
 spec('jab',[('Punch_Jab',1,27)],7),
 spec('cross',[('Punch_Cross',1,31)],10),
 spec('spell',[('Spell_Simple_Enter',1,17),('Spell_Simple_Shoot',2,16),('Spell_Simple_Exit',2,14)],17)],
 2:[
 spec('slash_a',[('Sword_Regular_A',1,14),('Sword_Regular_A_Rec',2,30)],8),
 spec('heavy_sweep',[('Sword_Heavy_Combo',1,23)],15)]}
clips=[];report=[]
for pack,items in SPECS.items():
 bpy.ops.wm.read_factory_settings(use_empty=True)
 paths=list(SOURCE.glob('*/**/Unity/UAL'+str(pack)+'_Standard.fbx'));assert len(paths)==1,paths
 bpy.ops.import_scene.fbx(filepath=str(paths[0]))
 rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
 actions={a.name.split('|')[-1]:a for a in bpy.data.actions}
 rig.animation_data_create()
 offsets=[]
 for name in SOURCE_NAMES:
  b=rig.data.bones[name]
  rest=(rig.matrix_world@b.matrix_local).to_3x3().normalized()
  rest=C@rest@C.inverted()
  axis=C@(rig.matrix_world.to_3x3()@(b.tail_local-b.head_local))
  offsets.append(rest.inverted()@canonical_frame(axis))
 for item in items:
  values=[];positions=[];previous=[None]*len(NAMES);count=0
  for action,first,last in item['parts']:
   a=actions[action];rig.animation_data.action=a
   if len(a.slots):rig.animation_data.action_slot=a.slots[0]
   for f in range(first,last+1):
    bpy.context.scene.frame_set(f);bpy.context.view_layer.update();count+=1
    for i,name in enumerate(SOURCE_NAMES):
     p=rig.pose.bones[name];world=rig.matrix_world@p.matrix
     rot=C@world.to_3x3().normalized()@C.inverted()
     q=(rot@offsets[i]).to_quaternion().normalized()
     if previous[i] is not None and q.dot(previous[i])<0:q.negate()
     previous[i]=q.copy();values.extend(round(v,7) for v in (q.x,q.y,q.z,q.w))
     positions.extend(round(v,6) for v in (C@world.translation))
  assert all(math.isfinite(v) for v in values)
  clips.append(dict(name=item['name'],frames=count,fps=30,loop=item['loop'],contact=max(0,item['contact']-1),rotations=values))
  report.append(dict(name=item['name'],pack=pack,source=item['parts'],frames=count,contactFrame=item['contact'],positions=positions))
 # Editable source keeps only selected actions, avoiding unrelated locomotion assets.
 wanted={p[0] for item in items for p in item['parts']}
 rig.animation_data.action=None
 for a in list(bpy.data.actions):
  if a.name.split('|')[-1] not in wanted:bpy.data.actions.remove(a)
  else:a.use_fake_user=True
 # Source deliverable is the editable skeleton/actions, not duplicate mannequin meshes.
 for ob in list(bpy.data.objects):
  if ob != rig:bpy.data.objects.remove(ob,do_unlink=True)
 keep=set(SOURCE_NAMES)|{'root','spine_02','neck_01','clavicle_l','clavicle_r'}
 for a in bpy.data.actions:
  for fc in list(a.fcurves):
   if 'pose.bones[' in fc.data_path and fc.data_path.split('"')[1] not in keep:a.fcurves.remove(fc)
 bpy.ops.outliner.orphans_purge(do_recursive=True)
 bpy.ops.wm.save_as_mainfile(filepath=str(ART/('CombatSourceUAL'+str(pack)+'.blend')),compress=True)
data=dict(version=1,bones=NAMES,clips=clips)
raw=json.dumps(data,separators=(',',':')).encode()
(OUT/'Library.bytes').write_bytes(gzip.compress(raw,mtime=0))
(ART/'sampling-report.json').write_text(json.dumps(dict(bones=NAMES,clips=report),separators=(',',':')))
print('COMBAT_BAKE',len(clips),'clips',sum(c['frames'] for c in clips),'frames',len(raw),'json bytes',len(gzip.compress(raw,mtime=0)),'compressed')
