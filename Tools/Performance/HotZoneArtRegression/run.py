"""Compile production art preparation/cache and verify geometry ownership with engine fixtures.

This verifies scheduling and lifetime, not native Unity rendering or device frame time.
"""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts/Visuals'
with tempfile.TemporaryDirectory(prefix='df-hotzone-art-') as d:
 d=Path(d);out=d/'Regression.dll'
 refs=sorted((a.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];csc=sorted((a.dotnet_root/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1].name
 files=[here/'Regression.cs',src/'ResourceCreatureMesh.cs',src/'AuthoredHotZoneArt.cs']
 subprocess.run([str(a.dotnet_root/'dotnet'),str(csc),'-nologo','-langversion:9','-nowarn:0649','-out:'+str(out),*['-r:'+str(x) for x in refs.glob('*.dll')],*map(str,files)],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime}}}))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(out)],check=True)
