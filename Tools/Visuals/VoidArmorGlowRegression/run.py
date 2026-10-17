"""Compile the production glow builder using Unity fixtures; not a native shader render."""
from pathlib import Path
import argparse,json,subprocess,tempfile

parser=argparse.ArgumentParser()
parser.add_argument('--dotnet-root',type=Path,required=True)
args=parser.parse_args()
dotnet=args.dotnet_root.resolve()
here=Path(__file__).resolve().parent
root=here.parents[2]
production=root/'Assets/Alterverse/Scripts/Visuals/VoidKnightArmorGlow.cs'
refs=next((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))
csc=next((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='void-armor-glow-') as temporary:
    for fixture,defines in [('Regression',[]),('ServerRegression',['-define:UNITY_SERVER'])]:
        out=Path(temporary)/(fixture+'.dll')
        subprocess.run([str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-langversion:latest',*defines,'-out:'+str(out),*[f'-r:{path}' for path in refs.glob('*.dll')],str(here/'EngineStubs.cs'),str(here/(fixture+'.cs')),str(production)],check=True)
        out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
        subprocess.run([str(dotnet/'dotnet'),str(out)],check=True)
