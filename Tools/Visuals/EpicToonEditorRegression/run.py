#!/usr/bin/env python3
"""Exercise production validation, emission conversion and ability composition with adapters.
Does not execute native Unity import, particle simulation, rendering or shader compilation.
"""
import argparse
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
    source = (root / 'Assets/Alterverse/Editor/EpicToonReplacementBuilder.cs').read_text()
    output = re.search(r'private const string Output=[^;]+;', source)[0]
    start = source.index('private static readonly (string key,string path,float scale)[] Sources=')
    sources = source[start:source.index('};', start)+2]
    def method(signature):
        start = source.index(signature)
        brace = source.index('{', start)
        end, depth = brace+1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        return source[start:end]
    # Compile the exact production method and its registry; only editor/engine dependencies
    # are replaced. No test-side copy of the validation policy.
    validator = ('using System.Collections.Generic;using Alterverse.Visuals;using UnityEngine;using UnityEditor;using UnityEditor.Build;'
                 'namespace Alterverse.Editor {public sealed class EpicToonReplacementBuilder {'
                 + output + sources + method('public static void Validate()')
                 + method('private static void PreparePortalGeometry(')
                 + method('private static void BuildAbilityLooks(')
                 + method('private static float MeasureRadius(')
                 + method('private static void StripTypography(')
                 + method('private static void PrepareManualEmission(') + '}}')
    dotnet = args.dotnet_root.resolve()
    refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1]
    with tempfile.TemporaryDirectory(prefix='df-epic-validator-') as directory:
        directory = Path(directory)
        extracted = directory / 'ProductionValidator.cs'
        extracted.write_text(validator)
        binary = directory / 'Regression.dll'
        files = [extracted, here / 'Fixtures.cs', here / 'Regression.cs',
                 root / 'Assets/Alterverse/Scripts/Visuals/EpicVfxLibrary.cs']
        subprocess.run([str(dotnet / 'dotnet'), str(csc), '-nologo', '-noconfig', '-langversion:9',
                        '-out:' + str(binary), *['-r:' + str(p) for p in refs.glob('*.dll')],
                        *map(str, files)], check=True)
        binary.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        subprocess.run([str(dotnet / 'dotnet'), str(binary)], check=True)


if __name__ == '__main__':
    main()
