"""Remove Ione's four marked arm plaques, preserving the underlying ceramic guards."""
from pathlib import Path
import copy
import gzip
import hashlib
import json
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
REVISION = 'ione-clean-arm-panels-20261012-v1'
MODEL = 'Assets/Alterverse/Resources/UmbralMage/Model.bytes'
REPORT = 'ArtSource/Heroes/ConceptModels/Ione-arm-panel-removal-checks.json'


def legacy_arm_detail_vertices(data, limit=None):
    """Locate the two inherited forearm plaques/conduits by bone and exact surface bounds."""
    v = np.asarray(data['vertices']).reshape(-1, 3)
    bones = np.asarray(data['boneIndices'])
    indices = np.arange(len(v))
    limit = len(v) if limit is None else limit
    memberships = {}
    for material in (0, 3):
        membership = np.zeros(len(v), dtype=bool)
        membership[np.asarray(data['submeshes'][material]['triangles'], dtype=int)] = True
        memberships[material] = membership
    result = []
    for side in ('L', 'R'):
        bone = data['boneNames'].index('Forearm' + side)
        hand = data['boneNames'].index('Hand' + side)
        elbow = np.asarray(data['anchors'][bone]['head'])
        wrist = np.asarray(data['anchors'][hand]['head'])
        p = elbow + (wrist - elbow) * .52
        base = (indices < limit) & (bones == bone)
        panel = base & memberships[0] & (abs(v[:, 0] - p[0]) < .023) & (v[:, 1] > p[1] - .050) & (v[:, 1] < p[1] + .056) & (v[:, 2] > p[2] + .070) & (v[:, 2] < p[2] + .087)
        conduit = base & memberships[3] & (abs(v[:, 0] - p[0]) < .0035) & (v[:, 1] > p[1] - .024) & (v[:, 1] < p[1] + .038) & (v[:, 2] > p[2] + .084) & (v[:, 2] < p[2] + .092)
        assert panel.sum() == 13 and conduit.sum() == 12, 'Unexpected inherited forearm detail geometry'
        result.extend(indices[panel | conduit].tolist())
    return result


def payload(data):
    return gzip.compress(json.dumps(data, separators=(',', ':')).encode(), mtime=0)


def unchanged_data_hash(data):
    remaining = copy.deepcopy(data)
    remaining.pop('armPanelRemovalRevision', None)
    remaining.pop('submeshes')
    return hashlib.sha256(json.dumps(remaining, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def restore_pre_removal(data, report):
    """Reconstruct the preceding exact resource for the existing shoulder provenance checks."""
    restored = copy.deepcopy(data)
    restored.pop('armPanelRemovalRevision', None)
    for material, submesh in enumerate(restored['submeshes']):
        kept = iter(np.asarray(submesh['triangles']).reshape(-1, 3).tolist())
        removed = {face['index']: face['vertices'] for face in report['removedFaces'] if face['material'] == material}
        original = [removed[i] if i in removed else next(kept) for i in range(report['sourceSubmeshFaceCounts'][material])]
        assert next(kept, None) is None
        submesh['triangles'] = np.asarray(original, dtype=int).ravel().tolist()
    assert hashlib.sha256(payload(restored)).hexdigest() == report['sourceModelSha256'], 'Removal does not reconstruct the reviewed preceding model'
    return restored


def main():
    path = ROOT / MODEL
    raw = path.read_bytes()
    data = json.loads(gzip.decompress(raw))
    if data.get('armPanelRemovalRevision') == REVISION:
        print('Ione arm plaques already removed; no second edit applied.')
        return
    shoulder_report = json.loads((ROOT / 'ArtSource/Heroes/ConceptModels/Ione-shoulder-orientation-checks.json').read_text())
    assert hashlib.sha256(raw).hexdigest() == shoulder_report['modelSha256'], 'Unexpected source model'
    archived = json.loads((ROOT / 'ArtSource/Heroes/BuildPlans/Ione/validation.json').read_text())['checks']
    selected = np.zeros(len(data['boneIndices']), dtype=bool)
    selected[legacy_arm_detail_vertices(data, archived['preservedVertices'])] = True
    removed_parts = []
    for part in archived['newParts']:
        arm = any(part['name'].startswith('Ione ' + segment + side + ' ') for segment in ('UpperArm', 'Forearm') for side in ('L', 'R'))
        detail = part['name'].endswith(('inset service panel', 'protected luminous channel', 'inset vent'))
        if arm and detail:
            first = archived['preservedVertices'] + part['first']
            selected[first:first + part['vertices']] = True
            removed_parts.append(part['name'])
    assert selected.sum() == 270 and len(removed_parts) == 20
    corrected = copy.deepcopy(data)
    removed_faces = []
    source_counts = []
    for material, submesh in enumerate(data['submeshes']):
        faces = np.asarray(submesh['triangles'], dtype=int).reshape(-1, 3)
        source_counts.append(len(faces))
        remove = selected[faces].any(1)
        assert np.array_equal(remove, selected[faces].all(1)), 'Marked detail shares a triangle with preserved armor'
        for index in np.flatnonzero(remove):
            removed_faces.append({'material': material, 'index': int(index), 'vertices': faces[index].tolist()})
        corrected['submeshes'][material]['triangles'] = faces[~remove].ravel().tolist()
    assert len(removed_faces) == 440
    corrected['armPanelRemovalRevision'] = REVISION
    encoded = payload(corrected)
    report = {'revision': REVISION, 'sourceCommit': '5a34bb40783db8b973e654cee44340322972513a',
              'sourceModelSha256': hashlib.sha256(raw).hexdigest(), 'modelSha256': hashlib.sha256(encoded).hexdigest(),
              'unchangedDataSha256': unchanged_data_hash(data), 'unusedDetailVertices': np.flatnonzero(selected).tolist(),
              'markedPlaques': 4, 'inheritedForearmPlaquesAlsoRemoved': 2, 'removedParts': removed_parts,
              'removedTriangles': len(removed_faces), 'sourceSubmeshFaceCounts': source_counts, 'removedFaces': removed_faces,
              'vertices': len(data['boneIndices']), 'triangles': sum(len(s['triangles']) // 3 for s in corrected['submeshes']),
              'scope': 'Four marked arm plaques, their vertical emitters and crossbars; inherited forearm plaques beneath them. Ceramic guards and all other geometry retained.',
              'UnityEditor': 'not run', 'Android': 'not run',
              'nativeExports': 'Earlier Blender/FBX and part plans remain archived preceding this edit; updated generator omits these arm details.'}
    assert unchanged_data_hash(corrected) == report['unchangedDataSha256']
    assert restore_pre_removal(corrected, report) == data
    path.write_bytes(encoded)
    (ROOT / REPORT).write_text(json.dumps(report, indent=2) + '\n')
    print('Removed four marked arm plaques and their inherited forearm underlays: 440 triangles. All other mesh data is exact.')


if __name__ == '__main__':
    main()
