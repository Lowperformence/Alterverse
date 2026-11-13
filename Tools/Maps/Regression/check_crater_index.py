#!/usr/bin/env python3
"""Check crater-index coverage against an exhaustive spatial model.

This is a numeric source-level check, not a Unity compilation or a rendering test.
"""
import math
import random
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
source = (ROOT / 'Assets/Alterverse/Scripts/Visuals/LunarSurfaceField.cs').read_text()
block = re.search(r'craters\s*=\s*\{(.*?)\};', source, re.S).group(1)
number = r'[-+]?(?:\d+(?:\.\d*)?|\.\d+)f?'
pattern = r'new\(\s*(' + number + r')\s*,\s*(' + number + r')\s*,\s*(' + number + r')\s*\)'
craters = [tuple(float(n.rstrip('f')) for n in values) for values in re.findall(pattern, block)]
assert len(craters) > 50

def constant(name):
    match = re.search(r'\b' + name + r'\s*=\s*(' + number + r')', source)
    assert match, name
    return float(match.group(1).rstrip('f'))

size, min_x, min_z = (constant(name) for name in ('BucketSize', 'BucketMinX', 'BucketMinZ'))
columns, rows = (int(constant(name)) for name in ('BucketColumns', 'BucketRows'))

def index(value, origin, count):
    return max(0, min(count - 1, math.floor((value - origin) / size)))

# Model the production bucket assignment, then compare it to the original full scan.
cells = [[] for _ in range(columns * rows)]
for i, (cx, radius, cz) in enumerate(craters):
    r = radius * 1.65
    for z in range(index(cz-r, min_z, rows), index(cz+r, min_z, rows) + 1):
        for x in range(index(cx-r, min_x, columns), index(cx+r, min_x, columns) + 1):
            cells[z * columns + x].append(i)

rng = random.Random(8411)
points = [(rng.uniform(-48, 48), rng.uniform(-42, 42)) for _ in range(30000)]
points += [(min_x + x * size, min_z + z * size)
           for x in range(columns + 1) for z in range(rows + 1)
           if min_x + x * size <= 48 and min_z + z * size <= 42]
for cx, radius, cz in craters:
    r = radius * 1.65
    for x, z in ((cx-r, cz), (cx+r, cz), (cx, cz-r), (cx, cz+r)):
        if -48 <= x <= 48 and -42 <= z <= 42:
            points.append((x, z))

for x, z in points:
    expected = [i for i, (cx, r, cz) in enumerate(craters)
                if abs(x-cx) <= r*1.65 and abs(z-cz) <= r*1.65]
    actual = [i for i in cells[index(z, min_z, rows) * columns + index(x, min_x, columns)]
              if abs(x-craters[i][0]) <= craters[i][1]*1.65 and
                 abs(z-craters[i][2]) <= craters[i][1]*1.65]
    assert actual == expected, (x, z, actual, expected)

assert 'candidates!=null?candidates[candidate]:candidate' in source
assert 'x<BucketMinX||x>48f||z<BucketMinZ||z>42f)return null' in source
assert 'for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)buckets[z*BucketColumns+x].Add(i)' in source
print(f'Crater coverage and order: {len(points)} samples, {len(craters)} craters')
