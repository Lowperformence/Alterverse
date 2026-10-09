"""Validate baked source-clip geometry and loops with Python's standard library."""
import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path


def distance(a, b):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))


def axis(q):
    x, y, z, w = q
    norm = math.sqrt(sum(v * v for v in q))
    x, y, z, w = (v / norm for v in (x, y, z, w))
    return (2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, help='Optional extracted KayKit root, containing Animations/')
    parser.add_argument('--ual-source-root', type=Path, help='Optional Quaternius Standard root, containing Godot/')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[3]
    bank = json.loads(gzip.decompress((repo / 'Assets/Alterverse/Resources/LocomotionAnimation/Library.bytes').read_bytes()))
    report = json.loads((repo / 'ArtSource/Animations/Locomotion/sampling-report.json').read_text())
    expected_bones = ['ThighL', 'ShinL', 'FootL', 'ThighR', 'ShinR', 'FootR']
    assert bank['version'] == 2 and bank['bones'] == expected_bones, 'Unexpected schema or bone order'
    assert bank['sourceLegLength'] > 0 and len(bank['restPositions']) == 18
    assert all(math.isfinite(x) for x in bank['restPositions'])
    assert {c['name'] for c in bank['clips']} == {'walk', 'run', 'left', 'right', 'back'}
    by_name = {item['name']: item for item in report}
    for clip in bank['clips']:
        n = clip['frames']
        assert n > 2 and clip['fps'] > 0
        q, p, contact = clip['rotations'], clip['positions'], clip['contacts']
        heights = clip['heights']
        pelvis = clip['pelvisHeights']
        source_length = clip['sourceLegLength']
        assert source_length > .05 and math.isfinite(clip['pelvisMean'])
        assert len(pelvis) == n and all(math.isfinite(x) and abs(x) < source_length for x in pelvis)
        assert abs(pelvis[0] - pelvis[-1]) < 1e-7, 'Pelvis loop seam differs'
        assert max(pelvis) - min(pelvis) > .01, 'Source pelvis motion was omitted'
        assert len(heights) == n * 2 and all(math.isfinite(x) and x >= 0 for x in heights)
        assert distance(heights[:2], heights[-2:]) < 1e-6, 'World lift loop seam differs'
        for height, grounded in zip(heights, contact):
            assert not grounded or height <= source_length * by_name[clip['name']].get('contactTolerancePerLeg', .06) + 1e-7, 'Ground contact exceeds sole clearance threshold'
        assert len(q) == n * 24 and len(p) == n * 18 and len(contact) == n * 2
        assert all(math.isfinite(x) for x in q + p + clip['floor'])
        assert len(clip['floor']) == 2 and .6 <= clip['stride'] <= 3.5
        assert set(contact) == {0, 1} and contact[0] == 1
        assert distance(q[:24], q[-24:]) < 1e-6 and distance(p[:18], p[-18:]) < 1e-6
        assert contact[:2] == contact[-2:], 'Loop contact seam differs'
        max_norm, max_axis = 0., 0.
        for frame in range(n):
            for bone in range(6):
                quaternion = q[(frame * 6 + bone) * 4:(frame * 6 + bone + 1) * 4]
                norm_error = abs(math.sqrt(sum(v * v for v in quaternion)) - 1)
                max_norm = max(max_norm, norm_error)
                assert norm_error < 1e-5, 'Non-unit source quaternion'
                if bone not in (0, 1, 3, 4):
                    continue
                parent = p[(frame * 6 + bone) * 3:(frame * 6 + bone + 1) * 3]
                child = p[(frame * 6 + bone + 1) * 3:(frame * 6 + bone + 2) * 3]
                line = [b - a for a, b in zip(parent, child)]
                length = math.sqrt(sum(v * v for v in line))
                assert length > .001
                normalized = [v / length for v in line]
                axis_error = distance(axis(quaternion), normalized)
                max_axis = max(max_axis, axis_error)
                assert axis_error < .001, 'Anatomical quaternion points away from child joint'
        fractions = [sum(contact[side:-2:2]) / (n - 1) for side in (0, 1)]
        assert all(0 < f < 1 for f in fractions), 'Each leg must have stance and swing'
        both_airborne = sum(contact[i * 2] + contact[i * 2 + 1] == 0 for i in range(n - 1)) / (n - 1)
        for bone in (2, 5):
            height = [p[(frame * 6 + bone) * 3 + 1] for frame in range(n - 1)]
            assert max(height) - min(height) > source_length * .06, 'Static source foot'
        item = by_name[clip['name']]
        assert abs((n - 1) / clip['fps'] - item['duration']) < 1e-6, 'Source duration changed'
        source_root = args.ual_source_root if item['provider'] == 'Quaternius' else args.source_root
        if source_root:
            assert hashlib.sha256((source_root / item['source']).read_bytes()).hexdigest() == item['sha256']
        print('{}: {} frames; quaternion norm error {:.2g}; axis error {:.2g}; contacts L/R {:.3f}/{:.3f}; both airborne {:.3f}'.format(
            clip['name'], n, max_norm, max_axis, *fractions, both_airborne))
    print('PASS schema, geometry, normalized quaternions, loop seams, contact/swing variation and source duration' +
          ('; source file hashes' if args.source_root else ''))


if __name__ == '__main__':
    main()
