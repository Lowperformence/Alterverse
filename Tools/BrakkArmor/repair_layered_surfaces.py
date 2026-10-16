"""Recover the shipped support contour while restoring the closed authored solids.

Run once against the v1 surface-fitted resource with Blender's Python. Runtime
exports subsequently reuse surface-profiles.json and the untouched design solids.
"""
from pathlib import Path
import gzip, json, sys
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/Heroes/BrakkArmor'
RESOURCE = ROOT / 'Assets/Alterverse/Resources/BrakkArmor/Model.bytes'
sys.path.insert(0, str(Path(__file__).resolve().parent))
from plate_surface import rebuild, REVISION


def recover(data, plates):
    assert data['surfaceFit'] == 'connected-anatomy-contour-v1'
    points = np.array(data['vertices']).reshape(-1, 3)
    profiles = {}
    for plate in plates:
        frame = np.array(plate['frameModelUnity']); samples = {}
        for part in data['parts']:
            if not part['id'].startswith(plate['id'] + '_'): continue
            center = next(z for name, z in [('01_mount', -.009), ('02_liner', -.005), ('03_gold_shell', .0035), ('04_graphite_face', .012), ('05_fastener', .016)] if name in part['id'])
            local = (points[part['firstVertex']:part['firstVertex'] + part['vertexCount']] - frame[:3, 3]) @ frame[:3, :3]
            # Front/back and bevel duplicates belong to the same footprint.
            # The midpoint of each symmetric extrusion recovers its support.
            groups = {}
            for x, y, z in local:
                groups.setdefault((round(float(x), 5), round(float(y), 5)), []).append(float(z))
            for xy, depths in groups.items():
                height = (min(depths) + max(depths)) * .5 - center - .019
                samples.setdefault(xy, []).append(height)
        profiles[plate['id']] = [[x, y, round(float(np.median(z)), 8)] for (x, y), z in sorted(samples.items())]
    return profiles


if __name__ == '__main__':
    data = json.loads(gzip.decompress(RESOURCE.read_bytes()))
    diagrams = json.loads((ART / 'diagram-geometry.json').read_text())
    profile_path = ART / 'surface-profiles.json'
    if data.get('surfaceFit') == REVISION:
        print('Already repaired; no changes.'); sys.exit(0)
    profiles = recover(data, [d['plate'] for d in diagrams])
    profile_path.write_text(json.dumps({'revision': REVISION, 'coordinateSpace': 'plate-local metres', 'profiles': profiles}, separators=(',', ':')) + '\n')
    result = rebuild(data, diagrams, profiles)
    RESOURCE.write_bytes(gzip.compress(json.dumps(result, separators=(',', ':')).encode(), mtime=0))
    print('REBUILT', len(result['parts']), 'parts;', len(result['vertices']) // 3, 'vertices;', sum(len(s['triangles']) // 3 for s in result['submeshes']), 'triangles')
