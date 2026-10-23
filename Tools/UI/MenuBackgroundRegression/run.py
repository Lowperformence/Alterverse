#!/usr/bin/env python3
"""Compile the complete production menu classes against lifecycle/draw fixtures."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dotnet-root', type=Path, required=True)
    parser.add_argument('--source-root', type=Path)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = args.source_root or here.parents[2]
    dotnet = args.dotnet_root.resolve()
    refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
    sources = [root / 'Assets/Alterverse/Scripts/UI' / name
               for name in ('MenuBackgroundVideo.cs', 'MenuCosmicBackdrop.cs')]
    with tempfile.TemporaryDirectory(prefix='df-menu-background-') as temporary:
        for server in (False, True):
            output = Path(temporary) / ('Server.dll' if server else 'Client.dll')
            command = [str(dotnet / 'dotnet'), str(csc), '-nologo', '-langversion:9',
                       '-out:' + str(output), *['-r:' + str(p) for p in refs.glob('*.dll')],
                       *map(str, sources), str(here / 'Fixtures.cs'), str(here / 'Regression.cs')]
            if server:
                command.insert(3, '-define:UNITY_SERVER')
            subprocess.run(command, check=True)
            output.with_suffix('.runtimeconfig.json').write_text(json.dumps({
                'runtimeOptions': {'tfm': 'net8.0', 'framework': {
                    'name': 'Microsoft.NETCore.App', 'version': runtime}}}))
            subprocess.run([str(dotnet / 'dotnet'), str(output)], check=True)


if __name__ == '__main__':
    try:
        main()
    except subprocess.CalledProcessError as error:
        raise SystemExit(error.returncode)
