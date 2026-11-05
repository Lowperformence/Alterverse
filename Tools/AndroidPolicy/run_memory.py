"""Compile and exercise the actual Android text-cache cleanup path without Unity."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
args = parser.parse_args()
here = Path(__file__).resolve().parent
runtime = sorted((args.dotnet_root / 'shared/Microsoft.NETCore.App').iterdir())[-1]
sdk = sorted((args.dotnet_root / 'sdk').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-memory-check-') as tmp:
    output = Path(tmp) / 'MemoryRegression.dll'
    response = Path(tmp) / 'compile.rsp'
    response.write_text('\n'.join([
        '-nologo', '-define:UNITY_ANDROID', '-target:exe', '-out:' + str(output),
        *['-r:' + str(p) for p in runtime.glob('*.dll')],
        str(here / 'MemoryRegression.cs'),
        str(here.parents[1] / 'Assets/Alterverse/Scripts/UI/LocalizedText.cs')]))
    subprocess.run([str(args.dotnet_root / 'dotnet'), str(sdk / 'Roslyn/bincore/csc.dll'),
                    '@' + str(response)], check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({
        'runtimeOptions': {'tfm': 'net8.0', 'framework': {
            'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
    subprocess.run([str(args.dotnet_root / 'dotnet'), str(output)], check=True)
