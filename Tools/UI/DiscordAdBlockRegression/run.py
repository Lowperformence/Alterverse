from pathlib import Path
import argparse,json,re,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts/UI';menu=(src/'MainMenuController.cs').read_text(encoding='utf-8-sig');ad=(src/'MainMenuAdvertising.cs').read_text(encoding='utf-8-sig')
def method(source,name):
 start=source.index(name);br=source.index('{',start);depth=1;i=br+1
 while depth:
  if source[i]=='{':depth+=1
  if source[i]=='}':depth-=1
  i+=1
 return source[start:i]
prop=re.search(r'public static bool IsHomeBannerBlocked\s*=>.*?;',ad,re.S).group()
assert 'if (HomeAdvertisingActive)' in menu and 'if (!AllowsHomeBanner) return;' in ad
button=method(menu,'private void DrawDiscordButton(')
assert 'if(!DiscordClickReward.Claimed)' in button and 'JOIN AND EARN 5 SHARDS' in button and 'DiscordClickReward.TryGrant()' in button and 'DISCORD +5' not in button
sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1];refs=sorted((a.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];version=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1].name
with tempfile.TemporaryDirectory() as d:
 d=Path(d);dll=d/'Regression.dll';gen=d/'Generated.cs';gen.write_text('using Alterverse.Profile;using UnityEngine; namespace Alterverse.UI { public partial class MainMenuController { public const string BannerBlockedPrefKey="DF_HomeBannerBlocked_v1";public const int BannerBlockPrice=700;'+prop+method(menu,'private static void SyncHomeBannerBlockState()')+method(menu,'private static void PurchaseHomeBannerBlock()')+'public static void BuyTest()=>PurchaseHomeBannerBlock(); public static void SyncTest()=>SyncHomeBannerBlockState(); }}')
 rsp=d/'compile.rsp';rsp.write_text('\n'.join(['-nologo','-langversion:latest','-out:'+str(dll),*['-r:'+str(x) for x in refs.glob('*.dll')],str(here/'Regression.cs'),str(gen)]))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
 dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':version}}}));subprocess.run([str(a.dotnet_root/'dotnet'),str(dll)],check=True)
