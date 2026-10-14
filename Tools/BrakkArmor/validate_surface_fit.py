"""Blender regression: every mounting vertex stays close to the real bind skin.

The old planar resource fails this test (up to 17 cm empty space). This tests
the shipped resource, not just the abstract plate-to-skeleton attachment.
"""
import gzip
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
from fit_surface import skin_surface

bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'ArtSource/Heroes/Rook.blend'))
tree = skin_surface(bpy.data.objects['DF_Anatomy'])
data = json.loads(gzip.decompress((ROOT / 'Assets/Alterverse/Resources/BrakkArmor/Model.bytes').read_bytes()))
points = [Vector(data['vertices'][i:i + 3]) for i in range(0, len(data['vertices']), 3)]
assert data['plateCount'] == 18 and len(data['parts']) == data['solidCount'] == 126
assert len(points) == len(data['boneIndices'])
assert all(math.isfinite(v) for key in ('vertices', 'normals', 'uv') for v in data[key])
worst, checked = 0., 0
for part in data['parts']:
    if not part['id'].endswith('_01_mount'):
        continue
    distances = [tree.find_nearest(points[i])[3]
                 for i in range(part['firstVertex'], part['firstVertex'] + part['vertexCount'])]
    worst = max(worst, max(distances))
    checked += 1
    # 8 mm mount gap, 4 mm mount thickness, and small silhouette overhang.
    assert max(distances) <= .02, (part['id'], max(distances))
assert checked == 18
for sub in data['submeshes']:
    indices = sub['triangles']
    for k in range(0, len(indices), 3):
        a, b, c = indices[k:k + 3]
        assert (points[b] - points[a]).cross(points[c] - points[a]).length > 1e-12
print('BRAKK_SURFACE_FIT_VALIDATED', checked, 'mounts; max gap', worst, 'metres', flush=True)
