"""Bake a shoulder carry into existing clips; preserve strike contacts and all leg keys.
Executed by refine_void_knight.py after rebuilding the unchanged rest rig.
"""
import bpy, math
from mathutils import Vector, Matrix
rig.data.pose_position='POSE'
for track in rig.animation_data.nla_tracks: track.mute=True
bones=rig.pose.bones
arm_names=['UpperArmL','ForearmL','HandL','WeaponGripL','UpperArmR','ForearmR','HandR','WeaponGripR']
actions=list(bpy.data.actions)
original={}
original_basis={}
for action in actions:
    rig.animation_data.action=action
    original[action.name]={}
    original_basis[action.name]={}
    for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
        bpy.context.scene.frame_set(frame)
        original[action.name][frame]={b.name:b.matrix.copy() for b in bones}
        original_basis[action.name][frame]={b.name:b.matrix_basis.copy() for b in bones}
# The source blade axis and hilt centre are unchanged; only their posed transform changes.
source_axis=Vector(rig['grip_axis']).normalized()
source_across=scope['across'].copy();source_normal=scope['normal'].copy()
source=Matrix((source_across,source_normal,source_axis)).transposed().to_4x4()
source.translation=Vector(rig['grip_center'])
carry_axis=Vector((.48,.80,.36)).normalized()
carry_across=(Vector((1,0,0))-carry_axis*carry_axis.x).normalized()
carry_normal=carry_axis.cross(carry_across).normalized()
carry=Matrix((carry_across,carry_normal,carry_axis)).transposed().to_4x4()
carry.translation=Vector((.13,-.25,1.36))
carry_deform=carry@source.inverted()
chest_rest=original['Idle'][1]['Chest']
idle_weapon=original['Idle'][1]['WeaponGripR']@rig.data.bones['WeaponGripR'].matrix_local.inverted()
idle_relative={name:idle_weapon.inverted()@original['Idle'][1][name] for name in arm_names}
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}

def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
RETURN_START={'Attack1':11,'Attack2':11,'Attack3':12,'Counter':9,'Skill0':11,'Skill1':20,'Skill2':14,'Ultimate':23,'Hit':11}
def carry_weight(name,frame,end):
    if name in ['Idle','Run']:return 1
    # Contacts and guard/thrust holds stay intact. Return from one fixed follow-through
    # pose instead of combining the old sweeping recovery with the new shoulder carry.
    first={'Skill2':4,'Ultimate':9,'Death':5,'Hit':4}.get(name,6)
    weight=1-smooth((frame-1)/max(1,first-1))
    if name in RETURN_START:
        start=RETURN_START[name];duration=min(10,end-start)
        weight=max(weight,smooth((frame-start)/max(1,duration)))
    return weight

def segment_matrix(name,start,end,reference,stretch=1):
    direction=(end-start).normalized()
    oldrot=reference.to_quaternion()
    rotation=(oldrot@Vector((0,1,0))).rotation_difference(direction)@oldrot
    scale=reference.to_scale();scale.y*=stretch
    return Matrix.LocRotScale(start,rotation,scale)

reach=[]
for action in actions:
    rig.animation_data.action=action
    previous={}
    end=int(action.frame_range[1])
    for frame,old in original[action.name].items():
        bpy.context.scene.frame_set(frame)
        # Global matrices restore this frame independently of newly inserted scale keys.
        for bone in bones:bone.matrix_basis=original_basis[action.name][frame][bone.name]
        desired_bones=dict(old)
        reference=dict(old)
        if action.name in RETURN_START and frame>RETURN_START[action.name]:
            anchor=original[action.name][RETURN_START[action.name]]
            body_delta=old['Chest']@anchor['Chest'].inverted()
            for name in arm_names:reference[name]=body_delta@anchor[name]
        weight=carry_weight(action.name,frame,end)
        if weight>1e-7:
            old_weapon=reference['WeaponGripR']@rest['WeaponGripR'].inverted()
            chest_delta=old['Chest']@chest_rest.inverted()
            target=chest_delta@carry_deform
            desired=old_weapon.lerp(target,weight)
            for label in ['L','R']:
                upper,forearm,hand='UpperArm'+label,'Forearm'+label,'Hand'+label
                relative=(old_weapon.inverted()@reference[hand]).lerp(idle_relative[hand],weight)
                hand_matrix=desired@relative
                # Solve the carried pose without stretching armor, then blend arm FK.
                # Grip bones remain on the blade throughout the preparation/recovery.
                carry_hand=target@idle_relative[hand]
                shoulder=old[upper].translation;wrist=carry_hand.translation
                direction=wrist-shoulder;distance=direction.length;direction.normalize()
                l1=(rig.data.bones[upper].tail_local-rig.data.bones[upper].head_local).length
                l2=(rig.data.bones[forearm].tail_local-rig.data.bones[forearm].head_local).length
                # The carried pose must fit the original arm lengths without scaling armor.
                solved_l2=l2
                assert distance<l1+l2+.001,(action.name,frame,label,distance)
                reach.append((solved_l2/l2,action.name,frame,label,tuple(wrist),tuple(shoulder)))
                along=max(-l1,min(l1,(l1*l1-solved_l2*solved_l2+distance*distance)/(2*max(.0001,distance))))
                pole=Vector((-.8 if label=='L' else .8,-.25,-.65))
                pole=(pole-direction*pole.dot(direction)).normalized()
                elbow=shoulder+direction*along+pole*math.sqrt(max(0,l1*l1-along*along))
                carry_upper=segment_matrix(upper,shoulder,elbow,old[upper])
                carry_forearm=segment_matrix(forearm,elbow,wrist,old[forearm])
                # Blend only rotation/location channels, retaining original unit scale.
                for name,full,parent_full in [(upper,carry_upper,old['Chest']),(forearm,carry_forearm,carry_upper)]:
                    source_bone=rig.data.bones[name];parent=source_bone.parent
                    full_basis=source_bone.convert_local_to_pose(full,source_bone.matrix_local,parent_matrix=parent_full,parent_matrix_local=parent.matrix_local,invert=True)
                    reference_basis=source_bone.convert_local_to_pose(reference[name],source_bone.matrix_local,parent_matrix=reference[parent.name],parent_matrix_local=parent.matrix_local,invert=True)
                    basis=reference_basis.lerp(full_basis,weight)
                    desired_bones[name]=source_bone.convert_local_to_pose(basis,source_bone.matrix_local,parent_matrix=desired_bones[parent.name],parent_matrix_local=parent.matrix_local)
                desired_bones[hand]=hand_matrix
                grip_name='WeaponGrip'+label
                desired_bones[grip_name]=desired@rest[grip_name] if label=='R' else desired@(old_weapon.inverted()@reference[grip_name]).lerp(idle_relative[grip_name],weight)
        for name in arm_names:
            bone=bones[name];bone.rotation_mode='QUATERNION'
            source_bone=rig.data.bones[name];parent=source_bone.parent
            bone.matrix_basis=source_bone.convert_local_to_pose(desired_bones[name],source_bone.matrix_local,parent_matrix=desired_bones[parent.name],parent_matrix_local=parent.matrix_local,invert=True)
            q=bone.rotation_quaternion
            if name in previous and q.dot(previous[name])<0:bone.rotation_quaternion=-q
            previous[name]=bone.rotation_quaternion.copy()
            for channel in ['location','rotation_quaternion','scale']:
                bone.keyframe_insert(data_path=channel,frame=frame,group=name)
    for layer in action.layers:
        for strip in layer.strips:
            bag=strip.channelbag(action.slots[0])
            for curve in bag.fcurves:
                if any('"'+name+'"' in curve.data_path for name in arm_names):
                    for point in curve.keyframe_points:point.interpolation='LINEAR'
rig['shoulder_carry_version']=1
rig['shoulder_carry_center']=list(carry.translation)
rig['shoulder_carry_axis']=list(carry_axis)
rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
print('SHOULDER_CARRY_MAX_FOREARM_SCALE',max(reach),flush=True)
print('SHOULDER_CARRY_IDLE_SCALE',max(r for r in reach if r[1] in ['Idle','Run']),flush=True)
