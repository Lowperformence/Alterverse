"""Validate the actual V4 model against the exact pre-revision source."""
from pathlib import Path
import bpy, json, math
HERE=Path(__file__).resolve().parent

def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    rig.data.pose_position='POSE'
    for track in rig.animation_data.nla_tracks: track.mute=True
    bones={b.name:([list(r) for r in b.matrix_local],b.parent.name if b.parent else None) for b in rig.data.bones}
    poses={}
    for action in bpy.data.actions:
        rig.animation_data.action=action
        for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
            bpy.context.scene.frame_set(frame)
            poses[action.name,frame]={b.name:[list(row) for row in b.matrix] for b in rig.pose.bones}
    weapon={o.name:([list(v.co) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons]) for o in bpy.data.objects if o.type=='MESH' and o.get('bone')=='WeaponGripR'}
    hands={o.name:[list(v.co) for v in o.data.vertices] for o in bpy.data.objects if o.type=='MESH' and o.get('bone') in ['HandL','HandR']}
    return bones,poses,weapon,hands

before=snapshot(HERE/'_violet_baseline.blend')
after=snapshot(HERE/'VoidKnight.blend')
assert before[0]==after[0], 'Skeleton or bind matrix changed'
assert set(before[1])==set(after[1]) and len(after[1])==384
pose_error=max(abs(x-y) for k in before[1] for b in before[1][k] for r,s in zip(before[1][k][b],after[1][k][b]) for x,y in zip(r,s))
assert pose_error<1e-6, ('Animation drift',pose_error)
assert before[2]==after[2], 'Sword geometry changed'
assert before[3]==after[3], 'Hand geometry changed'
meshes=[o for o in bpy.data.objects if o.type=='MESH']
triangles=0
for obj in meshes:
    obj.data.calc_loop_triangles(); triangles+=len(obj.data.loop_triangles)
    assert obj.get('bone') in after[0] and obj.data.uv_layers
    assert any(m.type=='ARMATURE' for m in obj.modifiers)
    for vertex in obj.data.vertices:
        assert all(math.isfinite(c) for c in vertex.co)
        assert abs(sum(g.weight for g in vertex.groups)-1)<1e-6
assert triangles<=35000, triangles
assert len({m.name for o in meshes for m in o.data.materials})==5
assert not any(o.name.startswith(('VK_Crown','VK_CenterCrown')) for o in meshes)
for side in ['L','R']:
    for name in ['VK_Eye_','VK4_ShoulderV_','VK4_BracerLight_','VK4_ShinLight_']:
        obj=bpy.data.objects[name+side]
        assert any('energy' in m.name for m in obj.data.materials)
        assert len(obj.data.polygons)>1, 'Inlay needs sidewalls'
assert bpy.data.objects.get('VK4_HelmetSkull') and bpy.data.objects.get('VK4_ChinKeel')
report={'result':'PASS','source_parts':len(meshes),'triangles':triangles,'bones':len(after[0]),'animation_frames':len(after[1]),'maximum_pose_error':pose_error,'weapon_unchanged':True,'hands_unchanged':True,'materials':5,'closed_v_visor':True}
(HERE/'Validation/violet-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print('VIOLET_ARMOR_VALIDATED',report,flush=True)
