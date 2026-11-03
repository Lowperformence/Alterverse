"""Compile the production buff UI and validate layout and selection with Unity substitutes."""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2]
runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
csc=sorted((a.dotnet_root/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
with tempfile.TemporaryDirectory(prefix='df-buff-ui-') as directory:
 directory=Path(directory);out=directory/'Regression.dll';rsp=directory/'compile.rsp'
 rsp.write_text('\n'.join(['-nologo','-langversion:9','-nowarn:0649,0169,0414','-out:'+str(out),*['-r:'+str(x) for x in runtime.glob('*.dll')],str(here/'Fixtures.cs'),str(root/'Assets/Alterverse/Scripts/Match/MidMatchPassiveDirector.cs')]))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(csc),'@'+str(rsp)],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(out)],check=True)
