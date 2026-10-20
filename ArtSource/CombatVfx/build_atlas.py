#!/usr/bin/env python3
"""Build the runtime mask atlas from the three unchanged CC0 downloads.

Only selected PNG frames enter the game. No samples, scripts, original projects,
extra materials, or runtime texture loading/packing are imported.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import zipfile
from PIL import Image, ImageOps

STATIC = [
    ('Glow', 'circle_05.png'), ('Burst', 'spark_01.png'),
    ('Muzzle', 'star_09.png'), ('Smoke', 'smoke_04.png'),
    ('Rune', 'magic_02.png'), ('Vortex', 'twirl_03.png'),
    ('ClawMark', 'scratch_01.png'), ('Slash', 'slash_02.png'),
    ('Flame', 'flame_06.png'), ('ShockRing', 'circle_03.png'),
    ('Flare', 'flare_01.png'), ('Trace', 'trace_03.png'),
    ('ShieldGlyph', 'symbol_02.png'), ('Dust', 'dirt_01.png'),
    ('Portal', 'circle_04.png'), ('FireSpark', 'fire_02.png'),
    ('Star', 'star_03.png'), ('SoftStar', 'star_06.png'),
]
PACKS = [
    ('01_Kenney_Particle_Pack.zip', 'Kenney Vleugels / Kenney.nl',
     'https://kenney.nl/assets/particle-pack'),
    ('02_Cethiel_Weapon_Slash.zip', 'Cethiel',
     'https://opengameart.org/content/weapon-slash-effect'),
    ('03_Rubberduck_Sparkling_Fireball.zip', 'rubberduck',
     'https://opengameart.org/content/sparkling-fireball-effect'),
]
CELL, PAD, COLUMNS, ROWS = 128, 4, 8, 8


def build(packs_root, root):
    atlas = Image.new('RGBA', (CELL * COLUMNS, CELL * ROWS))
    frames, clips, sources = [], [], []

    def add(archive, name, clip):
        original = Image.open(io.BytesIO(archive.read(name))).convert('RGBA')
        # Neutral luminance masks let the real hero/skill colour drive the result.
        # Preserve source alpha; colour and transparent gutters never become boxes.
        gray = ImageOps.grayscale(original)
        mask = Image.merge('RGBA', (gray, gray, gray, original.getchannel('A')))
        mask.thumbnail((CELL - 2 * PAD, CELL - 2 * PAD), Image.Resampling.LANCZOS)
        cell = len(frames)
        x = cell % COLUMNS * CELL + (CELL - mask.width) // 2
        y = cell // COLUMNS * CELL + (CELL - mask.height) // 2
        atlas.paste(mask, (x, y))
        frames.append(dict(cell=cell, clip=clip, archive=archive.filename.split('/')[-1],
                           member=name, source_size=list(original.size)))

    archives = [zipfile.ZipFile(packs_root / name) for name, _, _ in PACKS]
    try:
        for name, author, url in PACKS:
            data = (packs_root / name).read_bytes()
            sources.append(dict(archive=name, author=author, url=url, license='CC0 1.0',
                                sha256=hashlib.sha256(data).hexdigest()))
        for clip, filename in STATIC:
            start = len(frames)
            add(archives[0], 'PNG (Transparent)/' + filename, clip)
            clips.append(dict(name=clip, first=start, count=1))
        for group, clip in enumerate(['SlashLight', 'SlashHeavy', 'SlashClaw', 'SlashCross', 'SlashLunge'], 1):
            start = len(frames)
            for frame in range((group - 1) * 6 + 1, group * 6 + 1):
                add(archives[1], f'Classic/{group}/Classic_{frame:02}.png', clip)
            clips.append(dict(name=clip, first=start, count=6))
        start = len(frames)
        for frame in [round(1 + i * 49 / 15) for i in range(16)]:
            add(archives[2], f'particles_fireball/{frame:04}.png', 'Fireball')
        clips.append(dict(name='Fireball', first=start, count=16))
        assert len(frames) == COLUMNS * ROWS
    finally:
        for archive in archives:
            archive.close()
    target = root / 'Assets/Alterverse/Resources/CombatVfx/AttackAtlas.png'
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name('.' + target.name + '.tmp')
    atlas.save(temporary, format='PNG', optimize=True)
    temporary.replace(target)
    manifest = dict(size=list(atlas.size), cell=CELL, padding=PAD, columns=COLUMNS,
                    rows=ROWS, license='CC0 1.0', sources=sources, clips=clips, frames=frames,
                    atlas_sha256=hashlib.sha256(target.read_bytes()).hexdigest())
    (root / 'ArtSource/CombatVfx/AttackAtlas.sources.json').write_text(
        json.dumps(manifest, indent=2) + '\n')
    print(f'{target}: {len(frames)} frames, {target.stat().st_size} bytes')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packs-root', type=Path, required=True)
    parser.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    build(args.packs_root, args.project_root)
