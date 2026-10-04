"""Blender 4.5: extract the uploaded Standing Idle's first animated pose.

Usage: blender -b --python Tools/CombatAnimation/bake_mixamo_standing_idle.py -- source.fbx
The result is a stationary baseline, not root-motion playback. Mixamo's T-pose
is used only for anatomical calibration; the pose comes from action frame 1.
"""
import bpy
import gzip
import hashlib
import json
import sys
import tempfile
from pathlib import Path
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(sys.argv[sys.argv.index('--') + 1])
NAMES = ['Hips', 'Spine', 'Chest', 'Head', 'UpperArmL', 'ForearmL', 'HandL',
         'UpperArmR', 'ForearmR', 'HandR', 'ThighL', 'ShinL', 'FootL', 'ThighR', 'ShinR', 'FootR']
MIXAMO = ['Hips', 'Spine', 'Spine2', 'Head', 'LeftArm', 'LeftForeArm', 'LeftHand',
          'RightArm', 'RightForeArm', 'RightHand', 'LeftUpLeg', 'LeftLeg', 'LeftFoot',
          'RightUpLeg', 'RightLeg', 'RightFoot']
# Imported Blender world: anatomical left +X, up +Z, forward -Y.
# Runtime model space: anatomical left -X, up +Y, forward +Z.
C = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))

def frame(along):
    z = along.normalized()
    up = Vector((0, 0, 1))
    if abs(z.dot(up)) > .98:
        up = Vector((0, 1, 0))
    x = up.cross(z).normalized()
    return Matrix((x, z.cross(x).normalized(), z)).transposed()

bpy.ops.wm.read_factory_settings(use_empty=True)
source_bytes = gzip.decompress(SOURCE.read_bytes()) if SOURCE.suffix == '.gz' else SOURCE.read_bytes()
with tempfile.TemporaryDirectory() as directory:
    imported = Path(directory) / 'StandingIdle.fbx'
    imported.write_bytes(source_bytes)
    bpy.ops.import_scene.fbx(filepath=str(imported))
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
names = ['mixamorig:' + name for name in MIXAMO]
assert all(name in rig.data.bones for name in names)
action = rig.animation_data.action
assert action is not None
rest = [rig.matrix_world @ rig.data.bones[name].matrix_local for name in names]
offsets = []
for i, matrix in enumerate(rest):
    nxt = i + 1 if i in (0, 1, 2, 4, 5, 7, 8, 10, 11, 13, 14) else -1
    along = C @ (rest[nxt].translation - matrix.translation) if nxt >= 0 else (
        C @ (matrix.translation - rest[i - 1].translation) if i in (6, 9) else Vector((0, 1, 0)))
    if i in (12, 15):
        along = Vector((0, -.3, 1))
    rotation = C @ matrix.to_3x3().normalized() @ C.inverted()
    offsets.append(rotation.inverted() @ frame(along))

bpy.context.scene.frame_set(int(action.frame_range[0]))
bpy.context.view_layer.update()
rotations, positions = [], []
for i, name in enumerate(names):
    world = rig.matrix_world @ rig.pose.bones[name].matrix
    rotation = C @ world.to_3x3().normalized() @ C.inverted()
    q = (rotation @ offsets[i]).to_quaternion().normalized()
    rotations.extend(round(v, 7) for v in (q.x, q.y, q.z, q.w))
    positions.append([round(v, 6) for v in C @ world.translation])
assert positions[6][1] < positions[5][1] < positions[4][1]
assert positions[9][1] < positions[8][1] < positions[7][1]
assert positions[4][0] < positions[7][0], 'Left/right mapping reversed'
out = ROOT / 'Assets/Alterverse/Resources/CombatAnimation/StandingIdle.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(dict(name='mixamo_standing_idle', frames=2, fps=30,
    loop=True, contact=0, rotations=rotations * 2), separators=(',', ':')) + '\n')
report = dict(source='Standing Idle (1).fbx', sha256=hashlib.sha256(source_bytes).hexdigest(),
    action=action.name, sourceFrames=list(action.frame_range), sampledFrame=int(action.frame_range[0]),
    mode='Static starting pose; duplicated frame for existing clip sampler', bones=NAMES,
    positions=positions, coordinateConversion=[list(row) for row in C])
(ROOT / 'ArtSource/Animations/mixamo-standing-idle-report.json').write_text(json.dumps(report, indent=2) + '\n')
print('MIXAMO_BASELINE', out, report['sha256'])
