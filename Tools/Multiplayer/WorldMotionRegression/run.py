#!/usr/bin/env python3
"""Compile real world-entity interpolation with 8/20 Hz timing and packet-jitter fixtures."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
runtime = args.dotnet_root.resolve()
refs = sorted((runtime / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc = sorted((runtime / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
version = sorted((runtime / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix='df-world-motion-') as temporary:
    temporary = Path(temporary)
    output = temporary / 'Regression.dll'
    files = [here / 'Regression.cs', root / 'Assets/Alterverse/Scripts/Multiplayer/CoopWorldEntityKey.cs',
             root / 'Assets/Alterverse/Scripts/Visuals/HeroPresentationBuffer.cs']
    response = temporary / 'compile.rsp'
    response.write_text('\n'.join(['-nologo', '-target:exe', '-langversion:9', '-nowarn:0649', '-out:' + str(output),
                                  *['-r:' + str(p) for p in refs.glob('*.dll')], *map(str, files)]))
    subprocess.run([str(runtime / 'dotnet'), str(csc), '@' + str(response)], check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': version}}}))
    subprocess.run([str(runtime / 'dotnet'), str(output)], check=True)

