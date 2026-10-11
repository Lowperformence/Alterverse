"""Validate the Blender runtime export; optionally prove the combat edit is art-only."""
import argparse
import gzip
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HOOK = '''
            // Replace geometry only. Banking, thruster pulse, audio and bomb gameplay
            // continue to use the same visual root and engine sockets.
            if(AuthoredBomberModel.TryBuild(visual,armor,dark,energy,out engineGlowL,out engineGlowR))return;
'''

def vector_sub(a, b):
    return [x-y for x, y in zip(a, b)]

def cross(a, b):
    return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]

def validate():
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', type=Path, help='Previous SpaceBomberEvent.cs for byte comparison')
    args = parser.parse_args()
    raw = gzip.decompress((ROOT/'Assets/Alterverse/Resources/AuthoredBomber/Model.bytes').read_bytes())
    data = json.loads(raw)
    assert data['version'] == 1
    names = ['Hull', 'Mechanics', 'Energy', 'ThrusterL', 'ThrusterR']
    assert [p['name'] for p in data['parts']] == names
    triangles = 0
    for i, p in enumerate(data['parts']):
        assert p['material'] == (i if i < 3 else 2)
        v = list(zip(*[iter(p['vertices'])]*3))
        n = list(zip(*[iter(p['normals'])]*3))
        assert len(p['vertices']) > 0 and len(p['vertices']) % 3 == 0
        assert len(n) == len(v) and len(p['uv']) == 2*len(v)
        assert all(math.isfinite(x) for key in ['vertices','normals','uv','position','scale'] for x in p[key])
        assert all(abs(sum(x*x for x in normal)-1) < .00001 for normal in n)
        t = p['triangles']
        assert len(t) > 0 and len(t) % 3 == 0 and all(0 <= x < len(v) for x in t)
        assert set(t) == set(range(len(v))), 'Unused exported vertices'
        for a, b, c in zip(*[iter(t)]*3):
            normal = cross(vector_sub(v[b], v[a]), vector_sub(v[c], v[a]))
            assert sum(x*x for x in normal) > 1e-20, 'Degenerate triangle'
            face_n = [sum(n[k][axis] for k in [a,b,c])/3 for axis in range(3)]
            assert sum(x*y for x,y in zip(normal, face_n)) >= -1e-8, 'Inward winding'
        if i < 3:
            assert p['position'] == [0,0,0] and p['scale'] == [1,1,1]
            assert all(-2.12 <= x <= 2.12 and -.31 <= y <= .75 and -1.67 <= z <= 2.09 for x,y,z in v)
        else:
            assert p['position'] == [(-.54 if i == 3 else .54), .02, -1.78]
            assert p['scale'] == [.18,.18,.34]
            assert all(abs(x) <= .500001 for xyz in v for x in xyz)
        triangles += len(t)//3
    assert triangles <= 8000, 'Mobile art budget exceeded'
    source = (ROOT/'Assets/Alterverse/Scripts/Combat/SpaceBomberEvent.cs').read_text()
    assert source.count(HOOK) == 1
    if args.baseline:
        assert source.replace(HOOK, '') == args.baseline.read_text(), 'Change outside the geometry hook'
    print(f'PASS: {len(data["parts"])} meshes, {triangles} triangles; UVs, normals, winding, envelope and original thruster sockets')
    if args.baseline:
        print('PASS: combat source byte-identical after removing the art hook')

if __name__ == '__main__':
    validate()
