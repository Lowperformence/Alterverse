#!/usr/bin/env python3
"""Run production skill delivery against deterministic movement/engine fixtures, not Unity."""
import argparse
import ast
import json
from pathlib import Path
import re
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet-root', type=Path, required=True)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = here.parents[2]
    dotnet = args.dotnet_root.resolve()
    refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1]
    names = ast.literal_eval(re.search(r'^names=(\[[^\n]+\])', (here / 'run.py').read_text(), re.M)[1])
    names += ['Characters/VoidKnightBalance.cs', 'Characters/VoidKnightState.cs',
              'Characters/HeroAbilityExecution.VoidKnight.cs']
    # Extend the shared fixtures for the appended hero. Combat sources stay unchanged.
    stubs = (here / 'EngineStubs.cs').read_text().replace(
        'public class SerializeField:Attribute{}',
        'public class DisallowMultipleComponent:Attribute{} public class SerializeField:Attribute{}')
    stubs = stubs.replace('public static bool CanSimulateHostGameplay=>',
                          'public static bool CanMutateAuthoritativeState=>CanSimulateHostGameplay;public static bool CanSimulateHostGameplay=>')
    with tempfile.TemporaryDirectory(prefix='df-skill-entry-') as directory:
        directory = Path(directory)
        fixtures = directory / 'EngineStubs.cs'
        fixtures.write_text(stubs)
        presentation = directory / 'VoidPresentation.cs'
        presentation.write_text('''namespace Alterverse.Visuals {
public class VoidKnightPresentation:UnityEngine.MonoBehaviour {public void BeginCounter(){}}
public sealed partial class HeroSkillVfx {
 public static void CancelVoidKnightWarnings(UnityEngine.GameObject a){}
 public static void VoidKnightField(UnityEngine.GameObject a,UnityEngine.Vector3 d,float r,float s,bool w,bool dash=false){}
}}
''')
        output = directory / 'Regression.dll'
        tests = ['Regression.cs', 'TimmyRegression.cs', 'TimmyVfxRegression.cs', 'ConnectionTiming.cs',
                 'ConeCaptureRegression.cs', 'MovingConnectionRegression.cs', 'EntryRegression.cs']
        files = [fixtures, presentation, *[here / n for n in tests],
                 root / 'Assets/Alterverse/Editor/AttackDeliveryRegression.cs',
                 root / 'Assets/Alterverse/Editor/BasicAttackDeliveryRegression.cs',
                 *[root / 'Assets/Alterverse/Scripts' / n for n in names]]
        subprocess.run([str(dotnet / 'dotnet'), str(csc), '-nologo', '-noconfig', '-langversion:9',
                        '-define:UNITY_EDITOR', '-main:EntryPoint', '-out:' + str(output),
                        *['-r:' + str(p) for p in refs.glob('*.dll')], *map(str, files)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        subprocess.run([str(dotnet / 'dotnet'), str(output)], check=True)


if __name__ == '__main__':
    main()
