#!/usr/bin/env python3
"""Validate imported VFX assets/links, not a native Unity/GPU rendering test."""
import hashlib
import json
from pathlib import Path
import re
from PIL import Image, ImageChops

root = Path(__file__).resolve().parents[3]
art = root / 'ArtSource/CombatVfx'
assets = root / 'Assets/Alterverse/Resources/CombatVfx'
manifest = json.loads((art / 'AttackAtlas.sources.json').read_text())
checks = 0


def check(value, reason):
    global checks
    assert value, reason
    checks += 1


atlas_path = assets / 'AttackAtlas.png'
check(hashlib.sha256(atlas_path.read_bytes()).hexdigest() == manifest['atlas_sha256'], 'Packed image integrity')
atlas = Image.open(atlas_path)
atlas.load()
check(atlas.mode == 'RGBA' and atlas.size == (1024, 1024), 'Runtime atlas format and dimensions')
check(len(manifest['frames']) == 64 and len(manifest['clips']) == 24, 'Complete frame/clip catalog')
check([f['cell'] for f in manifest['frames']] == list(range(64)), 'Cells have no overlaps or gaps')
r, g, b, alpha = atlas.split()
check(ImageChops.difference(r, g).getbbox() is None and ImageChops.difference(r, b).getbbox() is None,
      'Runtime hero tint uses neutral RGB masks')

covered = []
for clip in manifest['clips']:
    cells = list(range(clip['first'], clip['first'] + clip['count']))
    covered.extend(cells)
    images = []
    for cell in cells:
        x, y = cell % 8 * 128, cell // 8 * 128
        tile = atlas.crop((x, y, x + 128, y + 128))
        bounds = tile.getchannel('A').getbbox()
        check(bounds is None or (bounds[0] >= 4 and bounds[1] >= 4 and bounds[2] <= 124 and bounds[3] <= 124),
              'Transparent padding protects every sampled cell from bleeding')
        images.append(tile.tobytes())
    check(any(Image.frombytes('RGBA', (128, 128), data).getchannel('A').getbbox() for data in images),
          'Clip contains visible art: ' + clip['name'])
    if clip['count'] > 1:
        check(len(set(images)) >= 5, 'Animated source contains distinct frames: ' + clip['name'])
check(covered == list(range(64)), 'Clip partitions cover the whole atlas exactly once')
check(all(s['license'] == 'CC0 1.0' and len(s['sha256']) == 64 and s['url'].startswith('https://')
          for s in manifest['sources']), 'All source authors, links, licenses and original hashes retained')
check('CC0 1.0 Universal' in (art / 'CC0-1.0.txt').read_text(), 'Full CC0 legal text retained')


def guid(path):
    return re.search(r'^guid: ([a-f0-9]{32})$', Path(str(path) + '.meta').read_text(), re.M)[1]


material = (assets / 'AttackAtlas.mat').read_text()
check('guid: ' + guid(assets / 'AttackAtlas.shader') in material, 'Material points to the retained shader asset')
check('guid: ' + guid(atlas_path) in material, 'Material points to the checked atlas image')
meta = Path(str(atlas_path) + '.meta').read_text()
check('enableMipMap: 0' in meta and 'isReadable: 0' in meta, 'No mip-chain/CPU pixel copy')
android = meta.split('buildTarget: Android', 1)[1]
check('maxTextureSize: 1024' in android and 'textureFormat: 50' in android and 'overridden: 1' in android,
      'Android has its explicit ASTC 6x6 override')
check('wrapU: 1' in meta and 'wrapV: 1' in meta and 'filterMode: 1' in meta, 'Clamp/bilinear sampling')
shader = (assets / 'AttackAtlas.shader').read_text()
check(shader.count('SAMPLE_TEXTURE2D(') == 1 and 'ZWrite Off' in shader and 'Cull Off' in shader,
      'Single-fetch, transparent, two-sided mobile pass')
check('_Time' not in shader and '_CameraOpaqueTexture' not in shader and '_CameraDepthTexture' not in shader,
      'No global animation clock or camera texture dependency')
check('7.0 - floor(cell / 8.0)' in shader, 'Unity UV origin matches the source cell order')
check('CombatVfx/AttackAtlas' in (root / 'Assets/Alterverse/Editor/ProjectShaderValidation.cs').read_text(),
      'Native Unity shader/material preflight includes the imported atlas')
print(f'PASS {checks} imported attack atlas asset checks (PNG, provenance, frames, padding, references, mobile settings)')
