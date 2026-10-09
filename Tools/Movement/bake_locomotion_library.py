"""Bake selected free KayKit 1.1 GLB clips to anatomical Unity frames.

blender -b --python Tools/Movement/bake_locomotion_library.py -- KAYKIT_ROOT
Source animation rotations/trajectories are sampled, never recreated from sine curves.
"""
import bpy
import sys
import json
import gzip
import math
import hashlib
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(sys.argv[sys.argv.index('--') + 1])
OUT = ROOT / 'Assets/Alterverse/Resources/LocomotionAnimation'
ART = ROOT / 'ArtSource/Animations/Locomotion'
OUT.mkdir(parents=True, exist_ok=True)
ART.mkdir(parents=True, exist_ok=True)
NAMES = ['ThighL', 'ShinL', 'FootL', 'ThighR', 'ShinR', 'FootR']
SOURCE_NAMES = ['upperleg.l', 'lowerleg.l', 'foot.l', 'upperleg.r', 'lowerleg.r', 'foot.r']
# KayKit Blender coordinates: left +X, forward -Y, up +Z.
# Unity/game model: left -X, forward +Z, up +Y. Reflected basis is intentional.
C = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))

def anatomical(direction):
    z = direction.normalized()
    up = Vector((0, 0, 1))
    if abs(z.dot(up)) > .98:
        up = Vector((0, 1, 0))
    x = up.cross(z).normalized()
    y = z.cross(x).normalized()
    return Matrix((x, y, z)).transposed()

def capture_sole_vertices():
    """Only actual skinned foot/toe geometry, excluding shin/cuff and other objects."""
    captures = []
    totals = [0, 0]
    for obj in bpy.data.objects:
        if obj.type != 'MESH':
            continue
        side_indices = []
        for side in ('l', 'r'):
            groups = {g.index for g in obj.vertex_groups if g.name in ('foot.' + side, 'toes.' + side)}
            indices = [v.index for v in obj.data.vertices
                       if sum(g.weight for g in v.groups if g.group in groups) >= .35]
            side_indices.append(indices)
            totals[len(side_indices) - 1] += len(indices)
        if any(side_indices):
            captures.append((obj, side_indices))
    if min(totals) == 0:
        raise ValueError('Source foot/toe weighted mesh vertices missing; cannot infer real sole contacts')
    return captures, totals


def world_sole_heights(captures):
    """Lowest evaluated foot/toe vertex follows heel strike and toe-off, unlike ankle height."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    heights = [float('inf'), float('inf')]
    for original, sides in captures:
        evaluated = original.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
        try:
            if len(mesh.vertices) != len(original.data.vertices):
                raise ValueError('Evaluated source topology changed; sole vertex indices are no longer valid')
            for side, indices in enumerate(sides):
                for index in indices:
                    heights[side] = min(heights[side], (C @ (evaluated.matrix_world @ mesh.vertices[index].co)).y)
        finally:
            evaluated.to_mesh_clear()
    if not all(math.isfinite(h) for h in heights):
        raise ValueError('Nonfinite source sole height')
    return heights


specs = {'MovementBasic': [('walk', 'Walking_A', (0, 0, 1)),
                           ('run', 'Running_A', (0, 0, 1))],
         'MovementAdvanced': [('left', 'Running_Strafe_Left', (-1, 0, 0)),
                              ('right', 'Running_Strafe_Right', (1, 0, 0)),
                              ('back', 'Walking_Backwards', (0, 0, -1))]}
clips = []
report = []
source_length = None
rest_positions = None
for pack, selected in specs.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = SOURCE / 'Animations/gltf/Rig_Medium' / ('Rig_Medium_' + pack + '.glb')
    bpy.ops.import_scene.gltf(filepath=str(path))
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    rig.animation_data_create()
    rig.animation_data.use_nla = False
    sole_vertices, sole_vertex_counts = capture_sole_vertices()
    # The source geometry is authored on a fixed floor in its bind pose. Dynamic
    # toe/heel penetration must not move that plane down and label the entire stance airborne.
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    source_rest_floor = min(world_sole_heights(sole_vertices))
    rig.data.pose_position = 'POSE'
    bpy.context.view_layer.update()
    actions = {a.name: a for a in bpy.data.actions}
    source_hips = C @ (rig.matrix_world @ rig.data.bones['hips'].head_local)
    rest = [C @ (rig.matrix_world @ rig.data.bones[n].head_local) - source_hips for n in SOURCE_NAMES]
    length = ((rest[0] - rest[1]).length + (rest[1] - rest[2]).length +
              (rest[3] - rest[4]).length + (rest[4] - rest[5]).length) / 2
    if source_length is None:
        source_length = length
        rest_positions = [round(v, 7) for p in rest for v in p]
    assert abs(source_length - length) < .0001
    offsets = []
    rest_foot_axis_error = 0.
    for i, n in enumerate(SOURCE_NAMES):
        bone = rig.data.bones[n]
        world = rig.matrix_world @ bone.matrix_local
        rest_rotation = C @ world.to_3x3().normalized() @ C.inverted()
        direction = C @ (rig.matrix_world.to_3x3() @ (bone.tail_local - bone.head_local))
        # Foot calibration uses the same anatomical down/forward axis as the target
        # retargeter. Raw source bone tails have a steeper tilt and would add a false pitch.
        if i in (2, 5):
            direction = Vector((0, -.3, 1))
        offsets.append(rest_rotation.inverted() @ anatomical(direction))
        if i in (2, 5):
            expected = anatomical(Vector((0, -.3, 1)))
            actual = rest_rotation @ offsets[-1]
            error = max(abs(actual[row][column] - expected[row][column])
                        for row in range(3) for column in range(3))
            rest_foot_axis_error = max(rest_foot_axis_error, error)
            assert error < 1e-5, 'Source rest foot axis differs from target calibration' 
    for name, action_name, direction in selected:
        action = actions[action_name]
        rig.animation_data.action = action
        if len(action.slots):
            rig.animation_data.action_slot = action.slots[0]
        first, last = action.frame_range
        blender_fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
        duration = (last - first) / blender_fps
        unique_frames = max(2, round(duration * 30))
        rotations, positions, world_sole = [], [], []
        for frame in range(unique_frames):
            at = first + (last - first) * frame / unique_frames
            bpy.context.scene.frame_set(math.floor(at), subframe=at % 1)
            bpy.context.view_layer.update()
            hips = C @ (rig.matrix_world @ rig.pose.bones['hips'].matrix).translation
            rotations.append([])
            positions.append([])
            world_sole.append(world_sole_heights(sole_vertices))
            for i, n in enumerate(SOURCE_NAMES):
                world = rig.matrix_world @ rig.pose.bones[n].matrix
                rotation = C @ world.to_3x3().normalized() @ C.inverted()
                q = (rotation @ offsets[i]).to_quaternion().normalized()
                rotations[-1].append(q.copy())
                world_position = C @ world.translation
                positions[-1].append(world_position - hips)
        # Contacts follow the evaluated sole in WORLD space: pelvis bob must not
        # invent flight, and a raised ankle during toe-off can still have a grounded toe.
        # One common source floor lets both soles refer to the same ground plane.
        floor = source_rest_floor
        low = [floor, floor]
        along = Vector(direction)
        # Central periodic horizontal velocity separates real support from a foot
        # scraping the source floor during its forward recovery. Only stanceward
        # (or almost stationary) ground touches may acquire a persistent world anchor.
        contact_velocity_tolerance = .02
        contacts = []
        contact_velocities = []
        for i in range(unique_frames):
            before, after = (i - 1) % unique_frames, (i + 1) % unique_frames
            velocities = [(positions[after][foot] - positions[before][foot]).dot(along) *
                          unique_frames / (2 * duration) for foot in (2, 5)]
            contact_velocities.append(velocities)
            contacts.append([int(world_sole[i][side] <= floor + length * .04 and
                                 velocities[side] <= contact_velocity_tolerance) for side in (0, 1)])
        # Align all directional clips to the left foot beginning a real supporting contact.
        eligible = [i for i, contact in enumerate(contacts) if contact[0]]
        if not eligible:
            raise ValueError('No grounded stanceward left-foot contact for ' + action_name)
        start = max(eligible, key=lambda i: positions[i][2].dot(along))
        rotations = rotations[start:] + rotations[:start]
        positions = positions[start:] + positions[:start]
        world_sole = world_sole[start:] + world_sole[:start]
        contacts = contacts[start:] + contacts[:start]
        contact_velocities = contact_velocities[start:] + contact_velocities[:start]
        heights = [[max(0., h[side] - low[side]) for side in (0, 1)]
                   for h in world_sole]
        # Estimate cycle travel from the source stance-foot velocity, not a guessed timer.
        stance_speeds = []
        for i in range(unique_frames):
            j = (i + 1) % unique_frames
            for side, foot in enumerate((2, 5)):
                if contacts[i][side] and contacts[j][side]:
                    speed = -(positions[j][foot] - positions[i][foot]).dot(along) * unique_frames / duration
                    if speed > .01:
                        stance_speeds.append(speed)
        stance_speeds.sort()
        if not stance_speeds:
            raise ValueError('No directional stance speed available for ' + action_name)
        speed = stance_speeds[len(stance_speeds) // 2]
        stride = max(length * .6, min(length * 3.5, speed * duration))
        rotations.append([q.copy() for q in rotations[0]])
        positions.append([p.copy() for p in positions[0]])
        contacts.append(contacts[0][:])
        heights.append(heights[0][:])
        flat_rotation = []
        previous = [None] * 6
        for pose in rotations:
            for i, q in enumerate(pose):
                if previous[i] is not None and previous[i].dot(q) < 0:
                    q.negate()
                previous[i] = q.copy()
                flat_rotation.extend(round(v, 7) for v in (q.x, q.y, q.z, q.w))
        flat_positions = [round(v, 7) for pose in positions for p in pose for v in p]
        assert all(math.isfinite(v) for v in flat_rotation + flat_positions)
        clips.append(dict(name=name, frames=unique_frames + 1, fps=unique_frames / duration,
                          rotations=flat_rotation, positions=flat_positions,
                          contacts=[v for c in contacts for v in c], heights=[round(v, 7) for h in heights for v in h], floor=low,
                          stride=stride / length))
        report.append(dict(name=name, action=action_name, source=str(path.relative_to(SOURCE)),
                           sha256=hashlib.sha256(path.read_bytes()).hexdigest(), duration=duration,
                           alignedStart=start, frames=unique_frames + 1, stridePerLeg=stride / length,
                           stanceFractions=[sum(c[s] for c in contacts[:-1]) / unique_frames for s in (0, 1)],
                           contactBasis="lowest evaluated skinned foot/toe world vertex (combined foot/toe weight >= 0.35)",
                           soleVertexCounts=sole_vertex_counts, contactTolerancePerLeg=.04,
                           contactVelocityMaxMetersPerSecond=contact_velocity_tolerance,
                           contactVelocityMethod="central periodic hips-relative ankle derivative along source travel direction",
                           sourceWorldFloor=floor,
                           sourceMinAnimatedSole=[min(h[side] for h in world_sole) for side in (0, 1)],
                           footCalibrationAxis=[0, -.3, 1],
                           maxRestFootAxisMatrixError=rest_foot_axis_error,
                           bothAirborneFraction=sum(not (c[0] or c[1]) for c in contacts[:-1]) / unique_frames,
                           maxSwingHeights=[max(h[s] for h in heights[:-1]) for s in (0, 1)],
                           measuredStridePerLeg=speed * duration / length))
    # Keep the free GLB action subset as reproducible editable source, without mannequin meshes.
    rig.animation_data.action = None
    for track in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(track)
    wanted = {a for _, a, _ in selected}
    for action in list(bpy.data.actions):
        if action.name not in wanted:
            bpy.data.actions.remove(action)
        else:
            action.use_fake_user = True
    for ob in list(bpy.data.objects):
        if ob != rig:
            bpy.data.objects.remove(ob, do_unlink=True)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(ART / ('KayKit_' + pack + '.blend')), compress=True)

bank = dict(version=1, bones=NAMES, sourceLegLength=source_length, restPositions=rest_positions, clips=clips)
raw = json.dumps(bank, separators=(',', ':')).encode()
(OUT / 'Library.bytes').write_bytes(gzip.compress(raw, mtime=0))
(ART / 'sampling-report.json').write_text(json.dumps(report, indent=2))
print('LOCOMOTION_BAKE', len(clips), 'clips', sum(c['frames'] for c in clips), 'frames',
      len(raw), 'raw bytes', len(gzip.compress(raw, mtime=0)), 'compressed bytes')
