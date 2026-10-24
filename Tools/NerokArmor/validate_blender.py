"""Actual armature deformation and FBX reimport checks for additive armor."""
import bpy,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'ArtSource/Heroes/NerokArmor'
bpy.ops.wm.open_mainfile(filepath=str(ART/'NerokArmor.blend'))
arm=bpy.data.objects['NerokArmorRig'];parts=[o for o in bpy.data.objects if o.type=='MESH'];count=len(parts)
assert count==88 and len(arm.data.bones)==18
for o in parts:
 assert o.parent==arm and o.data.uv_layers
 for v in o.data.vertices:assert abs(sum(g.weight for g in v.groups)-1)<1e-5
checked=0
for name in ['Chest','UpperArmL','UpperArmR','ForearmL','ForearmR','ThighL','ThighR','ShinL','ShinR']:
 pb=arm.pose.bones[name];pb.rotation_mode='XYZ'
 for angle in [-.8,0,.8]:
  pb.rotation_euler.x=angle;bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
  for o in parts:
   ev=o.evaluated_get(dg);me=ev.to_mesh()
   for v in me.vertices:
    p=ev.matrix_world@v.co;assert all(math.isfinite(c) for c in p) and p.length<3.5;checked+=1
   ev.to_mesh_clear()
  pb.rotation_euler.x=0
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(ART/'NerokArmor.fbx'))
meshes=[o for o in bpy.data.objects if o.type=='MESH'];rigs=[o for o in bpy.data.objects if o.type=='ARMATURE']
assert len(meshes)==count and len(rigs)==1 and len(rigs[0].data.bones)==18
assert all(o.data.uv_layers and o.vertex_groups for o in meshes)
report={'status':'passed','meshParts':count,'bones':18,'poseConfigurations':27,'verticesChecked':checked,'fbxRoundtrip':'passed','limits':'No Unity render/playtest; no proof of intersection-free armor against old body.'}
(ART/'blender-validation.json').write_text(json.dumps(report,indent=2));print('ARMOR_VALIDATED',json.dumps(report))
