"""Compile production Google sign-in/name adoption for Android and desktop boundaries."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
sdk = sorted((args.dotnet_root / 'sdk').iterdir())[-1]
runtime = sorted((args.dotnet_root / 'shared/Microsoft.NETCore.App').iterdir())[-1]
files = ['GooglePlayAccountSession', 'AccountServerBootstrap', 'PlayerProfile', 'AccountProfilePrefs', 'SafeIdentityCatalog']
for target, defines in [('android', 'UNITY_ANDROID'), ('desktop', 'UNITY_EDITOR')]:
    with tempfile.TemporaryDirectory(prefix='df-google-name-') as directory:
        output = Path(directory) / 'GoogleNameRegression.dll'
        response = Path(directory) / 'compile.rsp'
        response.write_text('\n'.join(['-nologo', '-nowarn:0649,0067', '-langversion:9', '-target:exe',
            '-define:' + defines, '-out:' + str(output),
            *['-r:' + str(path) for path in runtime.glob('*.dll')],
            str(here / 'EngineStubs.cs'), str(here / 'Regression.cs'),
            *[str(root / 'Assets/Alterverse/Scripts/Profile' / (name + '.cs')) for name in files]]))
        subprocess.run([str(args.dotnet_root / 'dotnet'), str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(response)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        subprocess.run([str(args.dotnet_root / 'dotnet'), str(output)], check=True,
            env=dict(os.environ, DF_TEST_IDENTITY_CATALOG=str(root / 'Assets/Alterverse/Resources/Social/SafeIdentityCatalog.json')))
