"""Run real BOLT-7 weapon catalog, inventory and skinned weapon-mask code with fixtures."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);p.add_argument('--catalog-source',type=Path);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts';sdk=a.dotnet_root.resolve()
hero=re.search(r'public enum HeroId\s*\{[^}]+\}',(src/'Characters/HeroIdentity.cs').read_text()).group()
catalog=root/'Assets/Alterverse/Resources/Commerce/ShardCatalog.json';data=json.loads(catalog.read_text())
for variant in (0,1):
 item=next(x for x in data['cosmetics'] if x['id']==f'weapon_10_{variant}')
 assert item['price']==500 and item.get('available',True) and not item.get('consumable',False)
 writes={w['key']:w['number'] for w in item['writes']}
 assert writes[f'DF_WeaponSkinOwnedItem_10_{variant}']==1 and writes['DF_WeaponSkinSelected_10']==variant and writes['DF_WeaponSkinEquipped_10']==1
 assert all('_10' in key for key in writes)
refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];csc=sorted((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];runtime=sorted((sdk/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='bolt7-weapon-') as d:
 d=Path(d);(d/'HeroId.cs').write_text('namespace Alterverse.Characters{'+hero+'}')
 out=d/'Regression.dll';rsp=d/'compile.rsp'
 files=[here/'Fixtures.cs',here/'Regression.cs',d/'HeroId.cs',a.catalog_source or src/'Shop/WeaponSkinCatalog.cs',src/'Shop/WeaponColorwayChecks.cs',src/'Visuals/Bolt7WeaponColorwayMask.cs']
 rsp.write_text('\n'.join(['-nologo','-langversion:9','-define:UNITY_EDITOR','-out:'+str(out),*['-r:'+str(f) for f in refs.glob('*.dll')],*map(str,files)]))
 subprocess.run([str(sdk/'dotnet'),str(csc),'-noconfig','@'+str(rsp)],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
 subprocess.run([str(sdk/'dotnet'),str(out)],check=True)
