"""Run the exact production Ground method with simulated layer-filtered ray hits."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def ground_method(source):
    start = source.index('private Vector3 Ground(')
    opening = source.index('{', start)
    depth, end = 1, opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root', type=Path)
    parser.add_argument('--old-mask', action='store_true', help='Negative control: restore DefaultRaycastLayers; must fail.')
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    repo = here.parents[2]
    source = (repo / 'Assets/Alterverse/Scripts/Visuals/HeroGroundedLocomotion.cs').read_text()
    method = ground_method(source)
    if args.old_mask:
        method = method.replace('1 << GameplayCollision.Ground', 'Physics.DefaultRaycastLayers')
    env = os.environ.copy()
    if args.mono_root:
        root = args.mono_root.resolve()
        runtime = str(root / 'usr/bin/mono-sgen')
        compiler = [runtime, str(root / 'usr/lib/mono/4.5/mcs.exe')]
        env['MONO_PATH'] = str(root / 'usr/lib/mono/4.5')
        env['MONO_CFG_DIR'] = str(root / 'etc')
    else:
        runtime, mcs = shutil.which('mono'), shutil.which('mcs')
        if not runtime or not mcs:
            parser.error('Install Mono or supply --mono-root.')
        compiler = [mcs]
    with tempfile.TemporaryDirectory(prefix='alterverse-ground-raycast-') as temporary:
        temp = Path(temporary)
        generated = temp / 'Ground.cs'
        generated.write_text('partial class GroundHarness {\n' + method + '\n}')
        binary = temp / 'Regression.exe'
        subprocess.run(compiler + ['-langversion:latest', '-out:' + str(binary), str(generated), str(here / 'Regression.cs')], env=env, check=True)
        subprocess.run([runtime, str(binary)], env=env, check=True)


if __name__ == '__main__':
    main()
