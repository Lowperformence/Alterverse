import bpy,addon_utils,math,json
from pathlib import Path
for name in ('rigify','rokoko'):assert addon_utils.check(name)[1]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.object.armature_human_metarig_add();bpy.ops.object.mode_set(mode='OBJECT');bpy.ops.pose.rigify_generate()
rig=bpy.context.object;rig.name='RigifyBreathSource'
control=rig.pose.bones['chest'];control.rotation_mode='XYZ'
for f in range(1,50):
 control.rotation_euler.x=.014*(.85*math.sin((f-1)/48*math.tau)+.15*math.sin((f-1)/48*math.tau*2))
 control.keyframe_insert(data_path='rotation_euler',frame=f)
# Sample Rigify's solved deform bones, then bake that three-bone skeleton for retargeting.
mapping=[('DEF-spine','Hips'),('DEF-spine.003','Chest'),('DEF-spine.006','Head')]
samples=[]
for frame in range(1,50):
 bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
 samples.append([rig.pose.bones[n].matrix.copy() for n,_ in mapping])
source_arm=bpy.data.armatures.new('SolvedRigifyMotion');source=bpy.data.objects.new('SolvedRigifyMotion',source_arm);bpy.context.collection.objects.link(source)
bpy.context.view_layer.objects.active=source;bpy.ops.object.select_all(action='DESELECT');source.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
parent=None
for src,dst in mapping:
 b=source_arm.edit_bones.new(src);b.matrix=rig.data.bones[src].matrix_local.copy();b.length=rig.data.bones[src].length;b.parent=parent;parent=b
bpy.ops.object.mode_set(mode='OBJECT')
for frame,matrices in enumerate(samples,1):
 bpy.context.scene.frame_set(frame)
 for (name,_),matrix in zip(mapping,matrices):
  bone=source.pose.bones[name];bone.rotation_mode='QUATERNION';bone.matrix=matrix
  bpy.context.view_layer.update()
  for prop in ('location','rotation_quaternion','scale'):bone.keyframe_insert(data_path=prop,frame=frame)
arm=bpy.data.armatures.new('GameBreathTarget');target=bpy.data.objects.new('GameBreathTarget',arm);bpy.context.collection.objects.link(target)
bpy.context.view_layer.objects.active=target;bpy.ops.object.select_all(action='DESELECT');target.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
mapping=[('DEF-spine','Hips'),('DEF-spine.003','Chest'),('DEF-spine.006','Head')]
parent=None
for src,dst in mapping:
 bone=arm.edit_bones.new(dst);bone.matrix=source.data.bones[src].matrix_local.copy();bone.length=source.data.bones[src].length;bone.parent=parent;parent=bone
bpy.ops.object.mode_set(mode='OBJECT')
scene=bpy.context.scene;scene.rsl_retargeting_armature_source=source;scene.rsl_retargeting_armature_target=target;scene.rsl_retargeting_auto_scaling=False;scene.rsl_retargeting_use_pose='REST'
for src,dst in mapping:
 item=scene.rsl_retargeting_bone_list.add();item.bone_name_source=src;item.bone_name_target=dst
assert bpy.ops.rsl.retarget_animation()=={'FINISHED'}
values=[]
for frame in range(1,50):
 scene.frame_set(frame);bpy.context.view_layer.update();values.append(target.pose.bones['Chest'].rotation_quaternion.to_euler().x)
assert max(values)-min(values)>.003,'Retargeted motion must move the chest'
report={'rigifyBones':len(rig.data.bones),'rokokoVersion':'1.4.3','retargetedBones':3,'frames':49,'chestRadiansRange':[min(values),max(values)],'scope':'Rigify control deformation and official Rokoko offline retarget bake; no live motion capture or Unity runtime test.'}
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/HeroMotion';ART.mkdir(parents=True,exist_ok=True)
(ART/'motion-tools-report.json').write_text(json.dumps(report,indent=2)+'\n')
# Runtime uses a bounded additive chest offset, rather than replacing animation controllers.
offsets=[v-values[0] for v in values]
bound=max(abs(v) for v in offsets);keys=[v/bound for v in offsets]
keys[-1]=keys[0]
source_code="using UnityEngine;\nnamespace Alterverse.Visuals\n{\n    // Sampled Rigify chest deformation, transferred with Rokoko 1.4.3.\n    // Unit curve only; HeroVisualMotion owns amplitude, combat blending and joints.\n    public static class HeroIdleBreath\n    {\n        static readonly float[] keys={"+','.join(f'{v:.7f}f' for v in keys)+"};\n        public static float Sample(float time)\n        {\n            float p=Mathf.Repeat(time/4.6f,1f)*(keys.Length-1);\n            int i=(int)p;\n            return Mathf.Lerp(keys[i],keys[i+1],p-i);\n        }\n    }\n}\n"
(ROOT/'Assets/Alterverse/Scripts/Visuals/HeroIdleBreath.cs').write_text(source_code)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'RigifyRokokoBreath.blend'),compress=True)
print('MOTION_TOOLS_PASS',json.dumps(report))
