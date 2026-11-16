from pathlib import Path
import argparse,json,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];hud=(root/'Assets/Alterverse/Scripts/UI/CombatFeedbackHUD.cs').read_text(encoding='utf-8-sig')
start=hud.index('public static void ApplyNetworkReward(');br=hud.index('{',start);depth=1;i=br+1
while depth:
 if hud[i]=='{':depth+=1
 if hud[i]=='}':depth-=1
 i+=1
method=hud[start:i]
assert 'hStreak < 3' in hud and 'LocalizedText.Draw(textRect,bountyText' in hud and 'Three small prongs and a base form a crown' in hud
sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1];refs=sorted((a.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];version=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1].name
with tempfile.TemporaryDirectory() as d:
 d=Path(d);dll=d/'Regression.dll';gen=d/'Regression.cs';gen.write_text((here/'Regression.cs').read_text().replace('/*PRODUCTION_METHOD*/',method))
 rsp=d/'compile.rsp';rsp.write_text('\n'.join(['-nologo','-langversion:latest','-out:'+str(dll),*['-r:'+str(x) for x in refs.glob('*.dll')],str(gen)]))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
 dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':version}}}));subprocess.run([str(a.dotnet_root/'dotnet'),str(dll)],check=True)
