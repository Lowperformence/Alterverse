#!/usr/bin/env python3
"""Compile the production server builder and shader preserver against editor substitutes.
This verifies build setup and restoration, not Unity's native asset pipeline or a live match.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet-root', type=Path, required=True)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = here.parents[2]
    dotnet = args.dotnet_root.resolve()
    sdk = sorted((dotnet / 'sdk').iterdir())[-1]
    runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1]
    with tempfile.TemporaryDirectory(prefix='df-server-build-') as directory:
        output = Path(directory) / 'Regression.dll'
        response = Path(directory) / 'compile.rsp'
        runtime_source = (root / 'Assets/Alterverse/Scripts/Multiplayer/DedicatedServerNetworkRuntime.cs').read_text()
        start = runtime_source.index('private static void SuppressClientOnlyRuntime()')
        end = runtime_source.index('public static void ReleaseMatchBootstrap()', start)
        suppression = Path(directory) / 'RuntimeSuppression.cs'
        suppression.write_text('using UnityEngine;using Alterverse.Match;using Alterverse.Prototype;using Alterverse.UI;namespace Alterverse.Multiplayer {public sealed class DedicatedServerNetworkRuntime:UnityEngine.Object {' + runtime_source[start:end] + 'public static void Tick()=>SuppressClientOnlyRuntime();}}')
        sources = [suppression, here / 'EngineStubs.cs', here / 'Regression.cs',
                   root / 'Assets/Alterverse/Editor/DimensionsFrontDedicatedServerBuilder.cs',
                   root / 'Assets/Alterverse/Editor/AndroidRuntimeShaderPreserver.cs']
        response.write_text('\n'.join(['-nologo', '-target:exe', '-langversion:9', '-define:UNITY_EDITOR',
                                       '-out:' + str(output),
                                       *['-r:' + str(path) for path in runtime.glob('*.dll')],
                                       *map(str, sources)]))
        subprocess.run([str(dotnet / 'dotnet'), str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(response)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        subprocess.run([str(dotnet / 'dotnet'), str(output)], check=True)


if __name__ == '__main__':
    main()
