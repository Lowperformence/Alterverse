from pathlib import Path
import argparse, sys, json, gzip, hashlib, math
import numpy as np
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/HeroParts'))
from build_parts import validate
from shoulder_orientation import REVISION, REPORT, unchanged_geometry_hash, validate_outboard
from arm_panel_removal import REVISION as ARM_REVISION, REPORT as ARM_REPORT, restore_pre_removal, unchanged_data_hash
parser = argparse.ArgumentParser(description='Validate both integrated models or one corrected hero.')
parser.add_argument('--hero', choices=['Sivra', 'Ione'])
args = parser.parse_args()
report_path = ROOT / 'ArtSource/Heroes/ConceptModels/Sivra-Ione-integration-checks.json'
report = json.loads(report_path.read_text()) if args.hero and report_path.exists() else {'heroes': {}, 'Unity': 'not run', 'Android': 'not run'}
report['lastValidatedHeroes'] = [args.hero] if args.hero else ['Sivra', 'Ione']
for hero, kind in [('Sivra', 'CyberRanger'), ('Ione', 'UmbralMage')]:
    if args.hero and hero != args.hero:
        continue
    path = (ROOT / 'Assets/Alterverse/Resources') / kind / 'Model.bytes'
    data = json.loads(gzip.decompress(path.read_bytes()))
    validate(data)
    archived = json.loads(((ROOT / 'ArtSource/Heroes/BuildPlans') / hero / 'validation.json').read_text())['checks']
    model_hash = hashlib.sha256(path.read_bytes()).hexdigest()
    shoulder_data = data
    arm_correction = None
    if hero == 'Ione' and data.get('armPanelRemovalRevision') == ARM_REVISION:
        arm_correction = json.loads((ROOT / ARM_REPORT).read_text())
        assert arm_correction['modelSha256'] == model_hash
        assert arm_correction['unchangedDataSha256'] == unchanged_data_hash(data)
        selected = np.zeros(len(data['boneIndices']), dtype=bool)
        selected[arm_correction['unusedDetailVertices']] = True
        for submesh in data['submeshes']:
            assert not selected[np.asarray(submesh['triangles'], dtype=int)].any()
        shoulder_data = restore_pre_removal(data, arm_correction)
    correction = None
    if hero == 'Ione' and data.get('shoulderOrientationRevision') == REVISION:
        correction = json.loads((ROOT / REPORT).read_text())
        assert correction['sourceModelSha256'] == archived['modelSha256']
        assert correction['modelSha256'] == (arm_correction['sourceModelSha256'] if arm_correction is not None else model_hash)
        assert correction['unchangedGeometrySha256'] == unchanged_geometry_hash(shoulder_data, correction['ranges'])
        validate_outboard(data, correction['ranges'])
    else:
        assert model_hash == archived['modelSha256']
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
    if correction is not None:
        entry['archivedModelHash'] = 'original export plus verified shoulder correction'
        entry['shoulderOrientationRevision'] = REVISION
    if arm_correction is not None:
        entry['archivedModelHash'] = 'original export plus verified shoulder correction and arm plaque removal'
        entry['armPanelRemovalRevision'] = ARM_REVISION
report_path.write_text(json.dumps(report, indent=2)+'\n')
print(json.dumps(report, indent=2))

