"""Validate the exact compressed scenery used by the Unity map builder (no Unity needed)."""
import base64
import gzip
import math
from pathlib import Path
import re
import struct


def validate(root):
    folder = root / 'Assets/Alterverse/Scripts/Visuals'
    count = 0
    for path in sorted(folder.glob('SpaceKitMapPolishRuntime.*Data.cs')):
        for name, encoded in re.findall(r'(\w+Data)\s*=\s*"([^"]+)"', path.read_text()):
            # Strict Base64 plus gzip CRC/size checks detect edits inside long literals.
            data = gzip.decompress(base64.b64decode(encoded, validate=True))
            vertices, indices = struct.unpack_from('<HH', data)
            assert vertices > 0 and indices > 0 and indices % 3 == 0, name
            assert len(data) == 4 + vertices * 12 + indices * 2, name
            xyz = struct.unpack_from('<' + str(vertices * 3) + 'f', data, 4)
            triangles = struct.unpack_from('<' + str(indices) + 'H', data, 4 + vertices * 12)
            assert all(math.isfinite(v) for v in xyz), name
            assert all(i < vertices for i in triangles), name
            print(f'{name}: {vertices} vertices, {indices // 3} triangles OK')
            count += 1
    assert count == 7, f'Expected seven embedded meshes, found {count}'
    return count


if __name__ == '__main__':
    root = next(p for p in Path(__file__).resolve().parents if (p / 'Assets/Alterverse').is_dir())
    print(f'Validated {validate(root)} embedded map meshes.')
