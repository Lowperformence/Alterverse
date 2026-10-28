"""Exercise real hero unlock, selection, wallet, journal, account cache and commerce coroutines.
HTTP and PlayerPrefs are controlled fixtures; no production account is accessed.
"""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args();sdk=a.dotnet_root.resolve();here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts'
hero=re.search(r'public enum HeroId\s*\{[^}]+\}',(src/'Characters/HeroIdentity.cs').read_text()).group()
refs=next((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'));csc=next((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='bolt7-purchase-') as d:
 d=Path(d);(d/'HeroId.cs').write_text('namespace Alterverse.Characters{'+hero+'}');out=d/'Regression.dll'
 names=['Shop/HeroUnlockCatalog.cs','Shop/PaidShardCommerce.cs','Profile/AccountProfilePrefs.cs','Profile/PlayerWallet.cs','Profile/ProfileTransaction.cs','Characters/PlayerHeroSelection.cs','AI/BotHeroPool.cs']
 r=subprocess.run([str(sdk/'dotnet'),str(csc),'-nologo','-nowarn:0649,0067','-langversion:9','-out:'+str(out),*['-r:'+str(f) for f in refs.glob('*.dll')],str(here.parent/'AccountRegression/EngineStubs.cs'),str(d/'HeroId.cs'),str(here/'Regression.cs'),*[str(src/n) for n in names]])
 if r.returncode:raise SystemExit(r.returncode)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
 subprocess.run([str(sdk/'dotnet'),str(out),str(root/'Assets/Alterverse/Resources/Commerce/ShardCatalog.json')],check=True)
