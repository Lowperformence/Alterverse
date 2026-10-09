"""Compile exact grounded planner methods with substitute flat-floor/engine math; not Unity gameplay."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def extract_block(source, token):
    start = source.index(token)
    opening = source.index('{', start)
    depth, end = 1, opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root', type=Path)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    repo = here.parents[2]
    visuals = repo / 'Assets/Alterverse/Scripts/Visuals'
    source = (visuals / 'HeroGroundedLocomotion.cs').read_text()
    methods = '\n'.join(extract_block(source, token) for token in [
        'private sealed class Leg', 'private void PlanAuthored(', 'private void Plan(', 'private void PrepareFoot('])
    # Reuse the existing System.Numerics-backed quaternion/Transform substitute.
    stubs = (here.parent / 'UpperBodyRegression/EngineStubs.cs').read_text()
    stubs = stubs.replace('public const float PI=', 'public static float Exp(float x){return (float)Math.Exp(x);} public const float PI=')
    stubs = stubs.replace('public class Transform:Component {',
                          'public struct Matrix4x4 { public Vector3 MultiplyVector(Vector3 v){return v;} }\n'
                          ' public class Transform:Component { public Matrix4x4 localToWorldMatrix=>new Matrix4x4();')
    stubs = stubs.replace('public UnityEngine.Quaternion PresentedRotation=',
                          'public UnityEngine.Vector3 PresentedPosition; public UnityEngine.Quaternion PresentedRotation=')
    wrapper = '''using System;
using UnityEngine;
using Alterverse.Visuals;
partial class PlanHarness {
public Transform transform=new GameObject("actor").transform;
public HeroVisualMotion motion=new GameObject("motion").AddComponent<HeroVisualMotion>();
private float CycleStride(float run){return 1f;}
private Vector3 Nominal(Leg leg){return motion.PresentedPosition+leg.rest;}
private Vector3 Ground(Vector3 point,Leg leg,out Vector3 normal){normal=Vector3.up;point.y=leg.sole;return point;}
''' + methods + '\n}\n'
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
        compiler, reference = [mcs], 'System.Numerics'
    with tempfile.TemporaryDirectory(prefix='alterverse-grounded-clips-') as temporary:
        temp = Path(temporary)
        (temp / 'EngineStubs.cs').write_text(stubs)
        (temp / 'Planner.cs').write_text(wrapper)
        files = [temp / 'EngineStubs.cs', temp / 'Planner.cs', here / 'Regression.cs'] + [
            visuals / name for name in ('HeroUpperBodyGait.cs', 'MeleeBasicAttackAnimation.cs', 'CombatMotion.cs')]
        binary = temp / 'Regression.exe'
        subprocess.run(compiler + ['-langversion:latest', '-r:' + reference, '-out:' + str(binary)] +
                       [str(file) for file in files], env=env, check=True)
        subprocess.run([runtime, str(binary)], env=env, check=True)


if __name__ == '__main__':
    main()
