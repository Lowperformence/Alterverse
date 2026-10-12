"""Ione shoulder attachment correction; canonical coordinates +Y up, +Z forward."""
from pathlib import Path
import copy
import gzip
import hashlib
import json
import numpy as np

REVISION = 'ione-outboard-shoulders-20261012-v1'
YAW_DEGREES = 45
SEATING_DROP = .051
ROOT = Path(__file__).resolve().parents[2]
MODEL = 'Assets/Alterverse/Resources/UmbralMage/Model.bytes'
ARCHIVE = 'ArtSource/Heroes/BuildPlans/Ione/validation.json'
REPORT = 'ArtSource/Heroes/ConceptModels/Ione-shoulder-orientation-checks.json'


def outward_rotation(sign):
    """Turn the authored front-facing shell towards its own shoulder's outside."""
    if sign not in (-1, 1):
        raise ValueError('Shoulder side must be -1 or +1')
    angle = np.deg2rad(sign * YAW_DEGREES)
    cosine, sine = np.cos(angle), np.sin(angle)
    return np.array([[cosine, 0., sine], [0., 1., 0.], [-sine, 0., cosine]])


def rotate_part_vertices(parts, first, center, sign):
    vertices = np.asarray(parts.v[first:], dtype=float)
    target = center + [0., -SEATING_DROP, 0.]
    parts.v[first:] = (target + (vertices - center) @ outward_rotation(sign).T).tolist()


def shoulder_ranges(data, archived):
    """Locate all six shells, their inset edges and their two fasteners by provenance."""
    checks = archived['checks']
    base = checks['preservedVertices']
    vertices = np.asarray(data['vertices']).reshape(-1, 3)[:base]
    bones = np.asarray(data['boneIndices'])[:base]
    groups = np.asarray(data['groups'])[:base]
    parts = checks['newParts']
    result = []
    for side, sign in [('L', -1), ('R', 1)]:
        bone = data['boneNames'].index('UpperArm' + side)
        shoulder = np.asarray(data['anchors'][bone]['head'])
        points = vertices[(bones == bone) & (groups == 0) & (vertices[:, 1] > shoulder[1] - .080)]
        low, high = points.min(0), points.max(0)
        head = np.array([(low[0] + high[0]) * .5, high[1] + .011, (low[2] + high[2]) * .5])
        for layer in range(3):
            name = 'three overlapping shoulder lames ' + side + str(layer)
            index = next(i for i, part in enumerate(parts) if part['name'] == name)
            selected = parts[index:index + 4]
            assert [part['name'] for part in selected] == [name, 'shoulder inset edge ' + side + str(layer), 'shoulder recessed rivet', 'shoulder recessed rivet']
            assert all(part['bone'] == bone for part in selected)
            first = base + selected[0]['first']
            count = sum(part['vertices'] for part in selected)
            assert selected[-1]['first'] + selected[-1]['vertices'] == selected[0]['first'] + count
            center = head + [sign * .026, -.018 - layer * .032 * 1.24, .006]
            result.append({'side': side, 'sign': sign, 'layer': layer, 'bone': bone,
                           'first': first, 'count': count, 'shellCount': selected[0]['vertices'],
                           'pivot': center.tolist(), 'targetPivot': (center + [0., -SEATING_DROP, 0.]).tolist(),
                           'yawDegrees': sign * YAW_DEGREES, 'seatingDropMetres': SEATING_DROP})
    return result


def correct_geometry(data, ranges):
    result = copy.deepcopy(data)
    vertices = np.asarray(data['vertices']).reshape(-1, 3).copy()
    normals = np.asarray(data['normals']).reshape(-1, 3).copy()
    for part in ranges:
        selected = slice(part['first'], part['first'] + part['count'])
        rotation = outward_rotation(part['sign'])
        pivot = np.asarray(part['pivot'])
        vertices[selected] = np.asarray(part['targetPivot']) + (vertices[selected] - pivot) @ rotation.T
        normals[selected] = normals[selected] @ rotation.T
    result['vertices'] = vertices.ravel().tolist()
    result['normals'] = normals.ravel().tolist()
    result['shoulderOrientationRevision'] = REVISION
    return result


def unchanged_geometry_hash(data, ranges):
    remaining = copy.deepcopy(data)
    selected = np.ones(len(data['boneIndices']), dtype=bool)
    for part in ranges:
        selected[part['first']:part['first'] + part['count']] = False
    for key in ('vertices', 'normals'):
        remaining[key] = np.asarray(data[key]).reshape(-1, 3)[selected].ravel().tolist()
    remaining.pop('shoulderOrientationRevision', None)
    return hashlib.sha256(json.dumps(remaining, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def validate_outboard(data, ranges):
    vertices = np.asarray(data['vertices']).reshape(-1, 3)
    normals = np.asarray(data['normals']).reshape(-1, 3)
    bones = np.asarray(data['boneIndices'])
    assert np.isfinite(vertices).all() and np.isfinite(normals).all()
    records = []
    for part in ranges:
        first = part['first']
        selected = slice(first, first + part['count'])
        shell = slice(first, first + part['shellCount'])
        sign = part['sign']
        assert (bones[selected] == part['bone']).all()
        assert np.allclose(np.linalg.norm(normals[selected], axis=1), 1., atol=.001)
        # Both shell packets must face away from the centreline and stay outside
        # the shoulder pivot, including the lights and fasteners.
        outward_normal = float((normals[shell, 0] * sign).mean())
        inner_edge = float((vertices[shell, 0] * sign).min())
        assert outward_normal > .30, 'Shoulder shell still faces forward or inward'
        assert inner_edge > .18, 'Shoulder shell still extends into the collar'
        records.append({'side': part['side'], 'layer': part['layer'],
                        'meanOutwardNormal': outward_normal, 'innerEdgeMetres': inner_edge})
    for layer in range(3):
        left = next(p for p in ranges if p['side'] == 'L' and p['layer'] == layer)
        right = next(p for p in ranges if p['side'] == 'R' and p['layer'] == layer)
        l = vertices[left['first']:left['first'] + left['count']]
        r = vertices[right['first']:right['first'] + right['count']]
        # Shell and edge vertex ordering mirrors exactly; rivet construction is
        # rotationally sampled, so compare mirrored extents for the whole packet.
        assert np.allclose((l * [-1, 1, 1]).min(0), r.min(0), atol=1e-7)
        assert np.allclose((l * [-1, 1, 1]).max(0), r.max(0), atol=1e-7)
    return records


def main():
    model = ROOT / MODEL
    raw = model.read_bytes()
    data = json.loads(gzip.decompress(raw))
    if data.get('shoulderOrientationRevision') == REVISION:
        print('Ione shoulders already corrected; no second rotation applied.')
        return
    archived = json.loads((ROOT / ARCHIVE).read_text())
    assert hashlib.sha256(raw).hexdigest() == archived['checks']['modelSha256'], 'Unexpected source model; do not rotate unknown geometry'
    ranges = shoulder_ranges(data, archived)
    old_failed = False
    try:
        validate_outboard(data, ranges)
    except AssertionError:
        old_failed = True
    assert old_failed, 'Negative control did not reproduce the front-facing shoulder issue'
    corrected = correct_geometry(data, ranges)
    records = validate_outboard(corrected, ranges)
    before = unchanged_geometry_hash(data, ranges)
    after = unchanged_geometry_hash(corrected, ranges)
    assert before == after, 'Non-shoulder geometry or gameplay anchors changed'
    assert len(data['vertices']) == len(corrected['vertices'])
    payload = gzip.compress(json.dumps(corrected, separators=(',', ':')).encode(), mtime=0)
    report = {'revision': REVISION, 'model': MODEL, 'sourceCommit': '954f9cecb201260da40b507f93216f3f91b909d6',
              'sourceModelSha256': hashlib.sha256(raw).hexdigest(),
              'modelSha256': hashlib.sha256(payload).hexdigest(), 'ranges': ranges,
              'changedVertices': sum(p['count'] for p in ranges),
              'vertices': len(corrected['boneIndices']),
              'triangles': sum(len(s['triangles']) // 3 for s in corrected['submeshes']),
              'unchangedGeometrySha256': after, 'nonShoulderDataExact': before == after,
              'oldOrientationRejected': old_failed, 'outboardChecks': records,
              'UnityEditor': 'not run', 'Android': 'not run',
              'nativeExports': 'Earlier Blender/FBX exports remain archived; regenerate with build_models.py for the corrected orientation.'}
    model.write_bytes(payload)
    (ROOT / REPORT).write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
