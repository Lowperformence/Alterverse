"""Compile the Android leaderboard path with controlled native/account boundaries."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root',type=Path,required=True)
args=parser.parse_args()
here=Path(__file__).resolve().parent
root=here.parents[2]
sdk=sorted((args.dotnet_root/'sdk').iterdir())[-1]
runtime=sorted((args.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-google-score-') as folder:
    output=Path(folder)/'LeaderboardRegression.dll'
    response=Path(folder)/'compile.rsp'
    response.write_text('\n'.join(['-nologo','-nowarn:0649,0414','-langversion:9','-define:UNITY_ANDROID','-out:'+str(output),
        *['-r:'+str(p) for p in runtime.glob('*.dll')],str(here/'Regression.cs'),
        str(root/'Assets/Alterverse/Scripts/UI/GooglePlayLevelLeaderboard.cs')]))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(output)],check=True)
