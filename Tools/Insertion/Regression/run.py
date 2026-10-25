"""Compile actual R35/transport sources and exercise lifecycle, audio and opening with managed fixtures."""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root',type=Path,required=True)
a=p.parse_args();here=Path(__file__).resolve().parent;root=here.parents[2]
sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1]
runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-insertion-') as temp:
 temp=Path(temp);output=temp/'Regression.dll';rsp=temp/'compile.rsp'
 files=[here/'Fixtures.cs',here/'Regression.cs',*[root/'Assets/Alterverse/Scripts/Visuals'/f for f in ['InsertionTransportModel.cs','MatchInsertionTransport.cs']],root/'Assets/Alterverse/Scripts/Match/MatchOpening.cs',root/'Assets/Alterverse/Scripts/Match/MatchClock.cs',root/'Assets/Alterverse/Scripts/Audio/InsertionTransportAudio.cs',root/'Assets/Alterverse/Scripts/Camera/MobaCamera.cs']
 rsp.write_text('\n'.join(['-nologo','-target:exe','-langversion:9','-out:'+str(output),*['-r:'+str(f) for f in runtime.glob('*.dll')],*map(str,files)]))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
 output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(output)],check=True)
