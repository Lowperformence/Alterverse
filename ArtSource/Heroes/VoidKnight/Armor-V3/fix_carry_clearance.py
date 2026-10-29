"""Rebake a fitted two-hand shoulder carry; retain rest geometry and strike contacts."""
from pathlib import Path
import bpy, json, runpy, hashlib, sys
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
SOURCE=HERE.parent/'VoidKnight.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
def geometry():
    return {o.name:hashlib.sha256(repr(([tuple(v.co) for v in o.data.vertices],
        [tuple(p.vertices) for p in o.data.polygons],
        [[(o.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in o.data.vertices])).encode()).hexdigest()
        for o in bpy.context.scene.objects if o.type=='MESH'}
old_geometry=geometry()
old_rest={b.name:tuple(x for row in b.matrix_local for x in row) for b in rig.data.bones}
axis=Vector(rig['grip_axis']).normalized()
blade=bpy.data.objects['VKC_Blade_Hull']
coords=np.array([tuple(v.co) for v in blade.data.vertices]);_,_,vt=np.linalg.svd(coords-coords.mean(axis=0))
normal=Vector(vt[-1]);normal*=1 if normal.y<0 else -1
across=normal.cross(axis).normalized();normal=axis.cross(across).normalized()
verify_only='--verify-only' in sys.argv
scope=({'arm_names':[], 'original':{a.name:{} for a in bpy.data.actions}} if verify_only else
    runpy.run_path(str(HERE.parent/'shoulder_carry.py'),init_globals={'rig':rig,'scope':{'across':across,'normal':normal}}))
assert old_geometry==geometry(),'The carry fix must not alter geometry'
assert old_rest=={b.name:tuple(x for row in b.matrix_local for x in row) for b in rig.data.bones},'Rest rig changed'
arms=set(scope['arm_names']);original=scope['original']
contacts={'Attack1':9,'Attack2':9,'Attack3':9,'Counter':7,'Skill0':9,'Skill1':10,'Skill2':7,'Ultimate':21}
non_arm_error=0;contact_error=0
for action in bpy.data.actions:
    rig.animation_data.action=action
    for frame,pose in original[action.name].items():
        bpy.context.scene.frame_set(frame)
        for b in rig.pose.bones:
            error=max(abs(x-y) for row,old in zip(b.matrix,pose[b.name]) for x,y in zip(row,old))
            if b.name not in arms:non_arm_error=max(non_arm_error,error)
            if contacts.get(action.name)==frame:contact_error=max(contact_error,error)
assert non_arm_error<1e-5,non_arm_error
assert contact_error<1e-5,contact_error

def tree(objects,deps):
    vertices=[];faces=[]
    for o in objects:
        evaluated=o.evaluated_get(deps);m=evaluated.to_mesh();offset=len(vertices)
        vertices.extend(evaluated.matrix_world@v.co for v in m.vertices)
        faces.extend(tuple(offset+i for i in p.vertices) for p in m.polygons)
        evaluated.to_mesh_clear()
    return BVHTree.FromPolygons(vertices,faces)
parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
blade_parts=[o for o in parts if o.name.startswith('VKC_Blade_Hull')]
checked=0;maximum={name:0 for name in ['UpperArmL','UpperArmR','ForearmL','ForearmR','Head','Chest']}
for name in ['Idle','Run']:
    rig.animation_data.action=bpy.data.actions[name]
    for frame in range(int(rig.animation_data.action.frame_range[0]),int(rig.animation_data.action.frame_range[1])+1):
        bpy.context.scene.frame_set(frame);deps=bpy.context.evaluated_depsgraph_get();blade_tree=tree(blade_parts,deps)
        for bone in maximum:
            count=len(blade_tree.overlap(tree([o for o in parts if o.get('bone')==bone],deps)))
            maximum[bone]=max(maximum[bone],count)
        checked+=1
print('CARRY_CLEARANCE',maximum,flush=True)
assert max(maximum.values())==0,maximum
rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
if not verify_only:bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
report=dict(result='PASS',carry_center=list(rig['shoulder_carry_center']),carry_axis=list(rig['shoulder_carry_axis']),
    idle_run_frames_checked=checked,maximum_blade_surface_intersections=maximum,
    protected_mesh_geometry_unchanged=True,rest_rig_unchanged=True,verification_only=verify_only,
    non_arm_pose_error=None if verify_only else non_arm_error,
    strike_contact_pose_error=None if verify_only else contact_error,clips=len(bpy.data.actions),bones=len(rig.data.bones))
(HERE/'carry-clearance-validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('CARRY_FIX_VALIDATION',json.dumps(report),flush=True)
