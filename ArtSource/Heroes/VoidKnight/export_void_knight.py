"""Export a runtime copy; never overwrite the modular Blender source."""
from pathlib import Path
import bpy,json,math
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
DEST=ROOT/'Assets/Alterverse/Resources/AuthoredHeroes/VoidKnight.fbx'
bpy.ops.wm.open_mainfile(filepath=str(HERE/'VoidKnight.blend'))
scene=bpy.context.scene
rig=next(o for o in scene.objects if o.type=='ARMATURE')
meshes=[o for o in scene.objects if o.type=='MESH']
assert 145 <= len(meshes) <= 480 and len(bpy.data.actions)==12
for m in meshes:
 assert m.get('bone') in rig.data.bones
 for v in m.data.vertices:
  assert all(math.isfinite(x) for x in v.co)
  assert abs(sum(g.weight for g in v.groups)-1)<1e-5
groups={weapon:[m for m in meshes if (m.get('bone')=='WeaponGripR')==weapon] for weapon in (False,True)}
for weapon,name in [(False,'DF_Anatomy'),(True,'DF_WeaponR')]:
 bpy.ops.object.select_all(action='DESELECT')
 for m in groups[weapon]:m.select_set(True)
 bpy.context.view_layer.objects.active=groups[weapon][0];bpy.ops.object.join();bpy.context.object.name=name
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
rig.data.pose_position='POSE';scene.render.fps=30
DEST.parent.mkdir(parents=True,exist_ok=True);bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(DEST),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,use_mesh_modifiers=True,apply_scale_options='FBX_SCALE_UNITS')
print('Exported',DEST)

