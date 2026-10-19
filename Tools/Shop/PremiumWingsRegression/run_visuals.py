"""Compile unchanged production wing presenters against deterministic engine fixtures."""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);p.add_argument('--export-icons',type=Path,help='Export unchanged legacy geometry for Blender shop renders');a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];dotnet=a.dotnet_root.resolve()
csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
refs=sorted((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
with tempfile.TemporaryDirectory() as folder:
 out=Path(folder)/'Visuals.dll'
 files=[here/'VisualFixtures.cs',here/('LegacyIconExport.cs' if a.export_icons else 'VisualRegression.cs'),root/'Assets/Alterverse/Scripts/Visuals/VoidWingPresentation.cs',root/'Assets/Alterverse/Scripts/Visuals/VoidWingPresentation.Premium.cs']
 subprocess.run([str(dotnet/'dotnet'),str(csc),'-nologo','-out:'+str(out),*['-r:'+str(f) for f in refs.glob('*.dll')],*map(str,files)],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'},'rollForward':'LatestPatch'}}))
 subprocess.run([str(dotnet/'dotnet'),str(out),*([str(a.export_icons.resolve())] if a.export_icons else [])],cwd=root,check=True)
