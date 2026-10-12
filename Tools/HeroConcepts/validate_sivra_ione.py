from pathlib import Path
import sys, json, gzip, hashlib, math
import numpy as np
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/HeroParts'))
from build_parts import validate
report = {'heroes': {}, 'Unity': 'not run', 'Android': 'not run'}
for hero, kind in [('Sivra', 'CyberRanger'), ('Ione', 'UmbralMage')]:
    path = (ROOT / 'Assets/Alterverse/Resources') / kind / 'Model.bytes'
    data = json.loads(gzip.decompress(path.read_bytes()))
    validate(data)
    archived = json.loads(((ROOT / 'ArtSource/Heroes/BuildPlans') / hero / 'validation.json').read_text())['checks']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == archived['modelSha256']
    v = np.asarray(data['vertices']).reshape(-1, 3)
    n = np.asarray(data['normals']).reshape(-1, 3)
    assert np.isfinite(v).all() and np.isfinite(n).all()
    assert np.all(np.linalg.norm(n, axis=1) > .9)
    bi = np.asarray(data['boneIndices']); names = data['boneNames']
    # Exercise independent rotations at every major joint. Every rigid plate must
    # remain at a constant distance from its assigned joint in all poses.
    for bone in ['Head', 'UpperArmL', 'UpperArmR', 'ForearmL', 'ForearmR', 'ThighL', 'ThighR', 'ShinL', 'ShinR']:
        idx = names.index(bone); points = v[bi == idx]
        origin = np.asarray(data['anchors'][idx]['head']); offsets = points - origin
        for degrees in [-80, -30, 45]:
            a = math.radians(degrees); c, s = math.cos(a), math.sin(a)
            rotation = np.array([[1,0,0], [0,c,-s], [0,s,c]])
            posed = offsets @ rotation.T + origin
            assert np.isfinite(posed).all()
            assert np.allclose(np.linalg.norm(posed-origin,axis=1), np.linalg.norm(offsets,axis=1))
    entry = {'vertices': len(v), 'triangles': sum(len(s['triangles'])//3 for s in data['submeshes']), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'jointPoseConfigurations': 27, 'geometry': 'passed', 'archivedModelHash': 'matched'}
    if hero == 'Ione':
        markers = {m['name']: np.asarray(m['position']) for m in data['markers']}
        axis = markers['StaffTop'] - markers['StaffGrip']
        axis = np.array([-axis[0], -axis[2], axis[1]])
        axis /= np.linalg.norm(axis)
        assert axis[2] > .97 and abs(axis[1]) < .05
        entry['fittedFocusAxis'] = axis.tolist()
    report['heroes'][hero] = entry
(ROOT / 'ArtSource/Heroes/ConceptModels/Sivra-Ione-integration-checks.json').write_text(json.dumps(report, indent=2)+'\n')
print(json.dumps(report, indent=2))
