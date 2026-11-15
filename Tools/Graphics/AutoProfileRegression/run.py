from pathlib import Path
import argparse,json,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1];refs=sorted((a.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];version=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1].name
with tempfile.TemporaryDirectory() as d:
 d=Path(d);dll=d/'Regression.dll';rsp=d/'compile.rsp';rsp.write_text('\n'.join(['-nologo','-langversion:latest','-out:'+str(dll),*['-r:'+str(x) for x in refs.glob('*.dll')],str(here/'Regression.cs'),str(root/'Assets/Alterverse/Scripts/Performance/MobilePerformanceProfile.cs')]))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
 dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':version}}}))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(dll)],check=True)
