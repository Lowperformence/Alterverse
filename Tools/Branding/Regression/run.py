"""Compile the production icon helper against fixtures that reject Unity 6.6's removed icon kinds."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
args = parser.parse_args()
sdk = args.dotnet_root.resolve()
here = Path(__file__).resolve().parent
root = here.parents[2]
refs = next((sdk / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))
csc = next((sdk / 'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='df-launcher-regression-') as temporary:
    temporary = Path(temporary)
    binary = temporary / 'Regression.dll'
    result = subprocess.run([str(sdk / 'dotnet'), str(csc), '-nologo', '-noconfig',
                    '-define:UNITY_EDITOR,UNITY_ANDROID', '-out:' + str(binary),
                    *['-r:' + str(p) for p in refs.glob('*.dll')], str(here / 'Regression.cs'),
                    str(root / 'Assets/Alterverse/Editor/AndroidLauncherIcons.cs')])
    if result.returncode:
        raise SystemExit(result.returncode)
    (temporary / 'Regression.runtimeconfig.json').write_text(json.dumps({
        'runtimeOptions': {'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '8.0.0'}}
    }))
    subprocess.run([str(sdk / 'dotnet'), str(binary)], check=True)
