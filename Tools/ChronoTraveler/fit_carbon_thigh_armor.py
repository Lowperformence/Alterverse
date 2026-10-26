#!/usr/bin/env python3
"""Replace the current Traveler's two chronometer phones with slim carbon plates.

Run after exporting the 20261012 Traveler concept. Existing body vertices, rig,
material slots and all triangles outside the two phone assemblies are preserved.
The payload carries UVs only for the new, seventh carbon material slot.
"""
import argparse
import copy
import gzip
import hashlib
import json
import math
from pathlib import Path

SOURCE_BLOB = 'c7baa170d71916f4a28b8a429110e2bce322d956'
# Exact phone/casing/telemetry/cable assemblies, excluding the knee chevrons.
PHONE_RANGES = ((11817, 11979), (11992, 12154))
REMOVED_TRIANGLES = (360, 44, 128, 44, 0, 0)
REVISION = 'traveler-carbon-thighs-20261026'


def git_blob(raw):
    return hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()


def replace_phones(data):
    original = copy.deepcopy(data)
    removed = set(i for first, end in PHONE_RANGES for i in range(first, end))
    assert len(data['boneIndices']) == 14969 and len(data['submeshes']) == 6
    assert data['legEquipmentAuthored'] is True
    for (first, end), bone in zip(PHONE_RANGES, (7, 13)):
        assert set(data['boneIndices'][first:end]) == {bone}
        assert data['boneNames'][bone] in ('ThighL', 'ThighR')
    for slot, submesh in enumerate(data['submeshes']):
        kept, count = [], 0
        indices = submesh['triangles']
        for i in range(0, len(indices), 3):
            triangle = indices[i:i + 3]
            hits = [index in removed for index in triangle]
            assert not any(hits) or all(hits), 'Phone connected to body geometry'
            if all(hits):
                count += 1
            else:
                kept.extend(triangle)
        assert count == REMOVED_TRIANGLES[slot], (slot, count)
        submesh['triangles'] = kept

    data['uvs'] = [0.0] * (len(data['boneIndices']) * 2)
    triangles = []
    # Small tapered front plates: 6 mm shell, knee and original cyan stripe clear.
    outline = [(-.029, .115), (.029, .115), (.033, .085),
               (.029, -.090), (0, -.115), (-.029, -.090), (-.033, .085)]
    for name in ('ThighL', 'ThighR'):
        bone = data['boneNames'].index(name)
        anchor = data['anchors'][bone]
        head, tail = anchor['head'], anchor['tail']

        def position(dx, dy, z):
            y = .720 + dy
            t = (y - head[1]) / (tail[1] - head[1])
            return [head[0] + (tail[0] - head[0]) * t + dx, y, z]

        positions = [position(0, 0, .096)]
        coords = [(0, 0)]
        for scale, z in ((.90, .094), (1.0, .087), (1.0, .081)):
            for dx, dy in outline:
                positions.append(position(dx * scale, dy * scale, z))
                coords.append((dx * scale, dy * scale))
        faces = []
        for j in range(7):
            n = (j + 1) % 7
            faces.append((0, 1 + n, 1 + j))
            for a, b in ((1, 8), (8, 15)):
                faces.extend(((a + j, a + n, b + n), (a + j, b + n, b + j)))
        faces.extend((15, 15 + j, 15 + j + 1) for j in range(1, 6))
        normals = [[0.0, 0.0, 0.0] for _ in positions]
        for face in faces:
            a, b, c = [positions[i] for i in face]
            u, v = [b[i] - a[i] for i in range(3)], [c[i] - a[i] for i in range(3)]
            normal = [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]
            for index in face:
                normals[index] = [normals[index][i] + normal[i] for i in range(3)]
        offset = len(data['boneIndices'])
        for point, normal, (dx, dy) in zip(positions, normals, coords):
            length = math.sqrt(sum(x*x for x in normal))
            assert length > 0
            data['vertices'].extend(point)
            data['normals'].extend(x / length for x in normal)
            data['boneIndices'].append(bone)
            # One weave repeat per 12 mm, same physical density in both axes.
            data['uvs'].extend(((dx + .04) / .012, (dy + .12) / .012))
        for face in faces:
            triangles.extend(offset + i for i in face)
    data['submeshes'].append({'triangles': triangles})
    data['carbonThighArmorRevision'] = REVISION
    # Suppress the legacy procedural phone fallback after removing authored phones.
    data['legEquipmentAuthored'] = True
    assert data['vertices'][:len(original['vertices'])] == original['vertices']
    assert data['normals'][:len(original['normals'])] == original['normals']
    assert data['boneIndices'][:14969] == original['boneIndices']
    assert data['anchors'] == original['anchors'] and data['boneNames'] == original['boneNames']
    assert len(triangles) // 3 == 80
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('model', nargs='?', type=Path, default=Path(__file__).resolve().parents[2] / 'Assets/Alterverse/Resources/ChronoTraveler/Model.bytes')
    args = parser.parse_args()
    raw = args.model.read_bytes()
    data = json.loads(gzip.decompress(raw))
    if data.get('carbonThighArmorRevision') == REVISION:
        print('Traveler carbon thigh plates already fitted; no changes.')
        return
    if git_blob(raw) != SOURCE_BLOB:
        raise SystemExit('Unrecognized Traveler payload; inspect geometry before updating the phone ranges.')
    result = replace_phones(data)
    args.model.write_bytes(gzip.compress(json.dumps(result, separators=(',', ':')).encode(), mtime=0))
    print('Removed 576 phone triangles; added two slim carbon plates (80 triangles). Existing body and rig preserved.')


if __name__ == '__main__':
    main()
