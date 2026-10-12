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
import re


def tank_pose_methods(path):
    # Compile the actual wrist calibration and guard-axis methods; replace only
    # unrelated mesh/material construction in this targeted engine harness.
    source = path.read_text()
    methods = []
    for name in ('GuardHandOffset', 'GuardHandTarget', 'HammerGuardShaft', 'OrientHammerGuard', 'AlignGuard', 'AlignHand'):
        match = re.search(r'(?m)^        (?:public|private) (?:static )?(?:void|Vector3) ' + name + r'\(', source)
        if not match:
            raise ValueError('Missing production method: ' + name)
        start = match.start()
        brace = source.find('{', match.end())
        arrow = source.find('=>', match.end())
        if 0 <= arrow < brace:
            end = source.index(';', arrow) + 1
        else:
            depth, end = 1, brace + 1
            while depth:
                if source[end] == '{': depth += 1
                elif source[end] == '}': depth -= 1
                end += 1
        methods.append(source[start:end])
    return '''using System.Collections.Generic;using UnityEngine;
namespace Alterverse.Visuals {public partial class SiegeTankAppearance {
private readonly Dictionary<string,Transform> markers=new Dictionary<string,Transform>();
private Transform calibratedHammerHand;private Quaternion hammerFrameInHand;private readonly ShieldGuardFrame shieldGuard=new ShieldGuardFrame();
public void ConfigureMarkers(Transform grip,Transform top,Transform front) {
markers["HammerGrip"]=grip;markers["HammerTop"]=top;markers["HammerFront"]=front;
calibratedHammerHand=null;}
''' + '\n'.join(methods) + '\n}}'


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
        files = [here / 'EngineStubs.cs', here / 'Regression.cs', here / 'ShieldGuardRegression.cs', source / 'ShieldGuardFrame.cs', source / 'HeroVisualDiagnostics.cs', source / 'HeroUpperBodyGait.cs',
                 source / 'MeleeBasicAttackAnimation.cs', source / 'CombatMotion.cs']
        tank = Path(temporary) / 'TankPose.cs'
        tank.write_text(tank_pose_methods(source / 'SiegeTankAppearance.cs'))
        files += [tank, here / 'HammerPoseRegression.cs']
        subprocess.run(compiler + ['-langversion:latest', '-r:' + reference, '-out:' + str(binary)] +
                       [str(file) for file in files], check=True, env=env)
        subprocess.run([runtime, str(binary), str(here / 'nerok-rest-rig.csv'), str(amplitudes)], check=True, env=env)


if __name__ == '__main__':
    main()
