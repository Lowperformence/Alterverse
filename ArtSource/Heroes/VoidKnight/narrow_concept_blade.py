"""Narrow only the existing V6 blade to 75% of its authored width.

Blender 5.0.1. Repeated runs are idempotent. The complete blade assembly is
scaled in the original grip frame; guard/hilt/body geometry stays byte-identical.
Use rebuild_concept_sword.py for a full repeatable V6 rebuild at the same width.
"""
from pathlib import Path
import bpy,hashlib,json,math
import numpy as np
from mathutils import Vector
HERE=Path(__file__).resolve().parent
TARGET=.75
PREFIXES=('VKC_Blade_Hull','VKC_Violet_CuttingEdge_','VKC_Spine_Panel_','VKC_Spine_SilverKey_')
bpy.ops.wm.open_mainfile(filepath=str(HERE/'VoidKnight.blend'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
rig.data.pose_position='REST'
weapons=[o for o in bpy.data.objects if o.type=='MESH' and o.get('bone')=='WeaponGripR']
blade=[o for o in weapons if o.name.startswith(PREFIXES)]
assert len(weapons)==69 and len(blade)==23,'Unexpected V6 assembly; inspect before modifying.'
def digest(o):
 coords=np.array([list(v.co) for v in o.data.vertices],dtype=np.float32).tobytes()
 topology=str([(tuple(p.vertices),p.material_index) for p in o.data.polygons]).encode()
 return hashlib.sha256(coords+topology).hexdigest()
def action_data():
 return {a.name:[(f.data_path,f.array_index,[(tuple(p.co),p.interpolation) for p in f.keyframe_points]) for layer in a.layers for st in layer.strips for slot in a.slots for f in (st.channelbag(slot).fcurves if st.channelbag(slot) else [])] for a in bpy.data.actions}
def bone_data():return {b.name:(tuple(b.head_local),tuple(b.tail_local),b.parent.name if b.parent else None) for b in rig.data.bones}
fixed={o.name:digest(o) for o in bpy.data.objects if o.type=='MESH' and o not in blade}
actions=action_data();bones=bone_data()
grip=Vector(rig['grip_center']);axis=Vector(rig['grip_axis']).normalized()
hull=next(o for o in blade if o.name=='VKC_Blade_Hull')
pts=np.array([list(v.co) for v in hull.data.vertices]);_,_,vt=np.linalg.svd(pts-pts.mean(axis=0))
normal=Vector(vt[-1]);normal*=1 if normal.y<0 else -1
across=normal.cross(axis).normalized();normal=axis.cross(across).normalized()
current={float(o.get('blade_width_factor',1.0)) for o in blade}
assert len(current)==1 and next(iter(current))>0,'Inconsistent authored blade-width metadata.'
current=next(iter(current));factor=TARGET/current
width=lambda: max((v.co-grip).dot(across) for v in hull.data.vertices)-min((v.co-grip).dot(across) for v in hull.data.vertices)
before=width();max_length_error=0;max_depth_error=0
for part in blade:
 for vertex in part.data.vertices:
  old=vertex.co.copy();local=old-grip
  if abs(factor-1)>1e-9:vertex.co=old+across*(local.dot(across)*(factor-1))
  delta=vertex.co-old
  max_length_error=max(max_length_error,abs(delta.dot(axis)))
  max_depth_error=max(max_depth_error,abs(delta.dot(normal)))
 part.data.update();part['blade_width_factor']=TARGET
rig['sword_blade_width_factor']=TARGET
after=width()
assert abs(after-before*factor)<1e-6
assert max_length_error<1e-6 and max_depth_error<1e-6
assert fixed=={o.name:digest(o) for o in bpy.data.objects if o.type=='MESH' and o not in blade}
assert actions==action_data() and bones==bone_data()
assert all(math.isfinite(c) for o in blade for v in o.data.vertices for c in v.co)
assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-6 for o in weapons for v in o.data.vertices)
report={'blade_width_factor':TARGET,'maximum_blade_width_before_m':before,'maximum_blade_width_after_m':after,'changed_blade_parts':len(blade),'unchanged_guard_grip_pommel_parts':len(weapons)-len(blade),'unchanged_body_meshes':len(fixed)-(len(weapons)-len(blade)),'original_bones_unchanged':len(bones),'original_actions_unchanged':len(actions),'maximum_length_error_m':max_length_error,'maximum_depth_error_m':max_depth_error,'topology_materials_and_skin_weights':'unchanged'}
rig.data.pose_position='POSE';rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'VoidKnight.blend'),compress=True)
out=HERE/'Sword-V6';(out/'width-adjustment.json').write_text(json.dumps(report,indent=2)+'\n')
validation=json.loads((out/'validation.json').read_text());validation.update(blade_width_factor=TARGET,maximum_blade_width_m=after)
(out/'validation.json').write_text(json.dumps(validation,indent=2)+'\n')
# Export exactly the same edited geometry as standalone local-space sources.
rig.data.pose_position='REST';bpy.context.view_layer.update()
for o in list(bpy.data.objects):
 if o not in weapons:bpy.data.objects.remove(o,do_unlink=True)
for o in weapons:
 o.modifiers.clear();o.parent=None
 for v in o.data.vertices:
  d=v.co-grip;v.co=(d.dot(across),d.dot(normal),d.dot(axis))
 o.vertex_groups.clear()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'VoidKnight-Conceptschwert.blend'),compress=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=str(out/'VoidKnight-Conceptschwert.glb'),export_format='GLB',use_selection=True)
print('BLADE_WIDTH_ONLY',json.dumps(report),flush=True)
