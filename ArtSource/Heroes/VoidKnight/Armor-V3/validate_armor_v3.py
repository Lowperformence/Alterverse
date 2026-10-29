"""Validate the saved artifact; optionally compare all protected data to a pre-change .blend."""
from pathlib import Path
import argparse, sys, json, math, hashlib
import bpy, bmesh
from mathutils import Vector

HERE=Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument('--before',type=Path)
parser.add_argument('--allow-carry-fix',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])

def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    protected={}
    for o in bpy.context.scene.objects:
        if o.type!='MESH':continue
        if not (o.get('bone') in {'WeaponGripR','HandL','HandR'} or
                o.name.startswith(('VK_Undersuit','VK_Joint','VK_Neck','VK_Palm','VK_Finger','VK_Thumb'))):continue
        record=([tuple(v.co) for v in o.data.vertices],[(tuple(p.vertices),p.material_index) for p in o.data.polygons],
            [s.material.name for s in o.material_slots],
            [[(o.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in o.data.vertices],
            [[tuple(v.uv) for v in layer.data] for layer in o.data.uv_layers])
        protected[o.name]=hashlib.sha256(repr(record).encode()).hexdigest()
    actions={a.name:[(f.data_path,f.array_index,[(tuple(k.co),tuple(k.handle_left),tuple(k.handle_right),k.interpolation,k.easing) for k in f.keyframe_points])
        for layer in a.layers for strip in layer.strips for slot in a.slots
        for f in (strip.channelbag(slot).fcurves if strip.channelbag(slot) else [])] for a in bpy.data.actions}
    rest={b.name:(tuple(b.head_local),tuple(b.tail_local),tuple(x for row in b.matrix_local for x in row),b.parent.name if b.parent else None) for b in rig.data.bones}
    dimensions={}
    for side in ['L','R']:
        bone=rig.data.bones['Thigh'+side];head=bone.head_local.copy();axis=(bone.tail_local-head).normalized()
        across=Vector((1,0,0));across=(across-axis*across.dot(axis)).normalized();normal=across.cross(axis).normalized()
        points=[v.co-head for o in bpy.context.scene.objects if o.type=='MESH' and o.get('bone')=='Thigh'+side and o.get('v3_module')=='A13' for v in o.data.vertices]
        if points:dimensions[side]=dict(width_m=max(p.dot(across) for p in points)-min(p.dot(across) for p in points),depth_m=max(p.dot(normal) for p in points)-min(p.dot(normal) for p in points))
    return protected,actions,rest,dimensions

before=snapshot(args.before) if args.before else None
after=snapshot(HERE.parent/'VoidKnight.blend')
if before:
    assert before[0]==after[0],'Changed protected weapon/hands/undersuit'
    assert before[2]==after[2],'Changed protected rest skeleton'
    if args.allow_carry_fix:
        arm_names={'UpperArmL','ForearmL','HandL','WeaponGripL','UpperArmR','ForearmR','HandR','WeaponGripR'}
        def fixed_curves(actions):
            return {name:[f for f in curves if not any('"'+bone+'"' in f[0] for bone in arm_names)] for name,curves in actions.items()}
        assert fixed_curves(before[1])==fixed_curves(after[1]),'Non-arm action keys changed'
    else:assert before[1]==after[1],'Changed action keys and handles'
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
new=[o for o in meshes if o.name.startswith(('VK7_','VK8_','VK_Eye_'))]
assert 200<=len(meshes)<=480,len(meshes)
assert len(rig.data.bones)==19
expected={'Idle','Run','Attack1','Attack2','Attack3','Skill0','Skill1','Skill2','Ultimate','Counter','Hit','Death'}
assert set(after[1])==expected
materials={s.material.name for o in meshes for s in o.material_slots}
assert materials=={'Void black metal','Void bevel metal','Void woven undersuit','Void energy','Void core energy'}
triangles=0;checked_vertices=0;closed_shells=0
for o in meshes:
    assert o.get('bone') in rig.data.bones,o.name
    assert any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers),o.name
    assert o.data.uv_layers,o.name
    o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
    for v in o.data.vertices:
        assert all(math.isfinite(x) for x in v.co),o.name
        assert abs(sum(g.weight for g in v.groups)-1)<1e-6,o.name
        checked_vertices+=1
    if o in new and 'Machining_' not in o.name:
        bm=bmesh.new();bm.from_mesh(o.data)
        bad=sum(not e.is_manifold for e in bm.edges)
        bm.free()
        assert bad==0,('Open armor shell',o.name,bad)
        closed_shells+=1
assert triangles<=35000,triangles
modules={o.get('v3_module') for o in new}
required={f'H{i:02d}' for i in range(2,13)}|{f'A{i:02d}' for i in list(range(2,9))+list(range(10,19))}
assert required<=modules,sorted(required-modules)
assert all(i.packed_file for i in bpy.data.images if i.name.startswith('VoidKnight'))
report=dict(result='PASS',source_meshes=len(meshes),triangles=triangles,materials=len(materials),bones=19,clips=12,
    weighted_vertices_checked=checked_vertices,closed_new_shells_checked=closed_shells,
    source_modules=sorted(modules),packed_surface_images=True,
    original_baseline_compared=bool(before),protected_meshes_compared=len(after[0]) if before else 0,
    protected_geometry_unchanged=True if before else None,
    all_action_keys_unchanged=before[1]==after[1] if before else None,
    requested_carry_arm_adjustment=args.allow_carry_fix,
    non_arm_action_keys_unchanged=True if before else None,rest_skeleton_unchanged=True if before else None,
    thigh_armor_dimensions_before=before[3] if before else None,thigh_armor_dimensions_after=after[3])
(HERE/'source-validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('ARMOR_V3_ARTIFACT_VALIDATION',json.dumps(report),flush=True)
