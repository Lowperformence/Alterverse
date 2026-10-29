"""Validate real Blender geometry, binding, protected contacts and shoulder visibility."""
from pathlib import Path
import bpy, math, json
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
HERE=Path(__file__).resolve().parent
ARM={'UpperArmL','ForearmL','HandL','WeaponGripL','UpperArmR','ForearmR','HandR','WeaponGripR'}
CONTACT={'Attack1':9,'Attack2':9,'Attack3':9,'Counter':7,'Skill0':9,'Skill1':10,'Skill2':7,'Ultimate':21}
def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path));rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    rig.data.pose_position='POSE'
    for track in rig.animation_data.nla_tracks:track.mute=True
    rest={b.name:([list(r) for r in b.matrix_local],b.parent.name if b.parent else None) for b in rig.data.bones}
    frames={}
    for action in bpy.data.actions:
        rig.animation_data.action=action
        for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
            bpy.context.scene.frame_set(frame)
            frames[action.name,frame]={b.name:b.matrix.copy() for b in rig.pose.bones}
    return rest,frames
before_rest,before=snapshot(HERE/'VoidKnight-v1-reference.blend')
after_rest,after=snapshot(HERE/'VoidKnight.blend')
assert before_rest==after_rest,'Rest rig or hierarchy changed'
assert set(before)==set(after) and len(after)==384
error=max(abs(x-y) for key in before for name in before[key] if name not in ARM for r,s in zip(before[key][name],after[key][name]) for x,y in zip(r,s))
assert error<1e-5,('Leg, root, head or torso drift',error)
contact_error=max(abs(x-y) for action,frame in CONTACT.items() for name in before[action,frame] for r,s in zip(before[action,frame][name],after[action,frame][name]) for x,y in zip(r,s))
assert contact_error<1e-5,('Strike contact changed',contact_error)
# Wrist/forearm continuity catches displaced hands between the protected contact poses.
wrist_gaps=[]
for key,pose in after.items():
    for label in ['L','R']:
        bone_name='Forearm'+label
        length=(bpy.data.armatures[0].bones[bone_name].tail_local-bpy.data.armatures[0].bones[bone_name].head_local).length
        wrist=pose['Hand'+label].translation
        end=pose[bone_name]@Vector((0,length,0))
        wrist_gaps.append(((wrist-end).length,key,label))
carry_gap=max(g[0] for g in wrist_gaps if g[1][0] in ['Idle','Run'])
return_end={'Attack1':21,'Attack2':21,'Attack3':22,'Counter':19,'Skill0':21,'Skill1':25,'Skill2':24,'Ultimate':33,'Hit':16}
return_gap=max(g[0] for g in wrist_gaps if g[1][0] in return_end and g[1][1]>=return_end[g[1][0]])
assert carry_gap<.002 and return_gap<.002,('Detached hand in carry or completed recovery',carry_gap,return_gap)
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.data.objects if o.type=='MESH'];assert 200<=len(meshes)<=480, len(meshes)
triangles=0
for obj in meshes:
    assert obj.get('bone') in rig.data.bones
    assert any(m.type=='ARMATURE' and m.object==rig for m in obj.modifiers)
    assert obj.data.uv_layers
    obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
    for vertex in obj.data.vertices:
        assert all(math.isfinite(x) for x in vertex.co)
        assert abs(sum(g.weight for g in vertex.groups)-1)<1e-6
assert triangles<=35000,triangles
for label in ['L','R']:
    coords=[v.co for o in meshes if o.get('bone')=='Hand'+label for v in o.data.vertices]
    ratios=[(max(v[i] for v in coords)-min(v[i] for v in coords))/rig['hand_unscaled_extent_'+label][i] for i in range(3)]
    assert all(abs(r-1.3)<1e-5 for r in ratios),ratios
assert len({s.material.name for o in meshes for s in o.material_slots})==5
# No newly stretched armor: all arm scale channels remain unit length throughout all clips.
scale_error=0
for action in bpy.data.actions:
    rig.animation_data.action=action
    for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
        bpy.context.scene.frame_set(frame)
        for name in ARM:
            scale_error=max(scale_error,max(abs(v-1) for v in rig.pose.bones[name].scale))
assert scale_error<1e-4,('Arm scale changed',scale_error)
# Trace actual evaluated blade/guard geometry toward each eye from three front viewing angles.
# A sword behind the head is fine; a sword between the camera and eyes fails.
visibility_samples=0
for action_name in ['Idle','Run']:
    rig.animation_data.action=bpy.data.actions[action_name]
    for frame in range(1,int(rig.animation_data.action.frame_range[1])+1,3):
        bpy.context.scene.frame_set(frame);deps=bpy.context.evaluated_depsgraph_get()
        vertices=[];polygons=[]
        for obj in meshes:
            if obj.get('bone')!='WeaponGripR':continue
            evaluated=obj.evaluated_get(deps);data=evaluated.to_mesh();offset=len(vertices)
            vertices.extend(evaluated.matrix_world@v.co for v in data.vertices)
            polygons.extend(tuple(offset+i for i in p.vertices) for p in data.polygons)
            evaluated.to_mesh_clear()
        tree=BVHTree.FromPolygons(vertices,polygons)
        for label in ['L','R']:
            eye=bpy.data.objects['VK_Eye_'+label].evaluated_get(deps);data=eye.to_mesh()
            point=sum((eye.matrix_world@v.co for v in data.vertices),Vector())/len(data.vertices);eye.to_mesh_clear()
            for yaw in [-45,0,45]:
                a=math.radians(yaw);camera=Vector((math.sin(a)*6,-math.cos(a)*6,3.1))
                delta=point-camera;hit=tree.ray_cast(camera,delta.normalized(),delta.length-.01)
                assert hit[0] is None,('Sword occludes eye',action_name,frame,label,yaw)
                visibility_samples+=1
report={'result':'PASS','source_meshes':len(meshes),'triangles':triangles,'materials':5,'bones':len(rig.data.bones),'animation_frames':len(after),'non_arm_pose_error':error,'protected_contact_pose_error':contact_error,'maximum_arm_scale_error':scale_error,'eye_visibility_rays':visibility_samples,'hands_scale':1.3,'shoulder_carry':True,'maximum_idle_run_wrist_gap':carry_gap,'maximum_completed_return_wrist_gap':return_gap}
(HERE/'Validation').mkdir(exist_ok=True)
(HERE/'Validation/refinement-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print('REFINEMENT_VALIDATION',report,flush=True)


