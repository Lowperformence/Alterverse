"""Compile unchanged production C# with a small substitute engine for targeted pose regression.

Requires Python 3 and Mono (mcs + mono), or --mono-root pointing to an extracted Mono distribution.
Does not run Unity, render a mesh, simulate physics, or load real attack clips.
"""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root', type=Path)
    parser.add_argument('--amplitudes', type=Path, help='Optional CSV destination for measured bone angles')
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    source = here.parents[2] / 'Assets/Alterverse/Scripts/Visuals'
    env = os.environ.copy()
    if args.mono_root:
        root = args.mono_root.resolve()
        runtime = str(root / 'usr/bin/mono-sgen')
        compiler = [runtime, str(root / 'usr/lib/mono/4.5/mcs.exe')]
        env['MONO_PATH'] = str(root / 'usr/lib/mono/4.5')
        env['MONO_CFG_DIR'] = str(root / 'etc')
        reference = str(root / 'usr/lib/mono/4.5/System.Numerics.dll')
    else:
        runtime, mcs = shutil.which('mono'), shutil.which('mcs')
        if not runtime or not mcs:
            parser.error('Mono runtime/compiler missing. Install Mono or pass --mono-root.')
        compiler = [mcs]
        reference = 'System.Numerics'
    with tempfile.TemporaryDirectory(prefix='alterverse-upper-body-') as temporary:
        binary = Path(temporary) / 'Regression.exe'
        amplitudes = args.amplitudes.resolve() if args.amplitudes else Path(temporary) / 'amplitudes.csv'
        if args.amplitudes:
            amplitudes.parent.mkdir(parents=True, exist_ok=True)
        files = [here / 'EngineStubs.cs', here / 'Regression.cs', source / 'HeroUpperBodyGait.cs',
                 source / 'MeleeBasicAttackAnimation.cs', source / 'CombatMotion.cs']
        subprocess.run(compiler + ['-langversion:latest', '-r:' + reference, '-out:' + str(binary)] +
                       [str(file) for file in files], check=True, env=env)
        subprocess.run([runtime, str(binary), str(here / 'nerok-rest-rig.csv'), str(amplitudes)], check=True, env=env)


if __name__ == '__main__':
    main()
