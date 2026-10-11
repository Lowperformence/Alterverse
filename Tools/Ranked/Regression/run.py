"""Compile production ranked/network classes and run roster invariants with engine substitutes."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root',type=Path,required=True)
a=p.parse_args()
sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1]
runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
here=Path(__file__).resolve().parent
root=here.parents[2]
source=root/'Assets/Alterverse/Scripts'
names=['Multiplayer/MatchSession.cs','Multiplayer/OnlineMatchPlan.cs','Multiplayer/CoopPlanHandshake.cs',
       'Multiplayer/RankedService.cs','Multiplayer/RankedMatchReporter.cs','Multiplayer/EdgegapMatchmakerClient.cs',
       'Multiplayer/DedicatedOnlineMatchHandshake.cs','Multiplayer/EdgegapDedicatedServerLifecycle.cs','UI/RankedPanel.cs']
with tempfile.TemporaryDirectory(prefix='df-ranked-') as directory:
    output=Path(directory)/'RankedRegression.dll'
    response=Path(directory)/'compile.rsp'
    response.write_text('\n'.join(['-nologo','-nowarn:0649,0067','-target:exe','-langversion:9','-out:'+str(output),
        *['-r:'+str(path) for path in runtime.glob('*.dll')],str(here/'EngineStubs.cs'),str(here/'Regression.cs'),
        *[str(source/name) for name in names]]))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(output)],check=True)
