"""Compile production full-catalog bot draws and co-op/online rosters with service fixtures."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args();sdk=a.dotnet_root.resolve();here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts'
hero=re.search(r'public enum HeroId\s*\{[^}]+\}',(src/'Characters/HeroIdentity.cs').read_text()).group()
fixture='namespace Alterverse.Characters{'+hero+'}\n'+'''
namespace Alterverse.AI{public enum BotDifficulty{Easy,Normal,Hard}public static class BotDifficultySettings{public const BotDifficulty OnlineDifficulty=BotDifficulty.Normal;}}
namespace Alterverse.Profile{public enum BattleItemType{None}}
namespace Alterverse.Match{public enum ArenaVariant{Moon}}
namespace Alterverse.Multiplayer{
 public static class CoopLobbyService{public const int MinHumanPlayers=2,MaxHumanPlayers=4;public class MemberView{public string PlayerId,DisplayName;public bool IsHost,IsLocal,Ready;public Alterverse.Characters.HeroId Hero;public Alterverse.Profile.BattleItemType BattleItem;}}
 public static class OnlinePartyService{public class MemberView{public string PlayerId,DisplayName;public bool IsHost,IsPartyMember,IsLocal,Ready;public Alterverse.Characters.HeroId Hero;public Alterverse.Profile.BattleItemType BattleItem;}}
}
namespace UnityEngine{public static class Random{public static int Range(int a,int b)=>a;}}
namespace Alterverse.Shop{
 public static class ArenaShopCatalog{public class Item{public int Price;public Alterverse.Match.ArenaVariant Variant;}public static Item[] Items=new Item[0];}
 public static class ArenaUnlockInventory{public static bool Owned(Alterverse.Match.ArenaVariant a)=>false;}
}
'''
# Exercise the production offline selection method without mocking its unrelated actor factory.
offline=(src/'Prototype/FiveVsFiveTestRoster.cs').read_text();start=offline.index('private static HeroId[] BuildAllies(');end=offline.index('private static Vector3 SafeSpawn',start)
fixture+='namespace Alterverse.Prototype{using Alterverse.AI;using Alterverse.Characters;using Alterverse.Multiplayer;public static class OfflineRosterFixture{'+offline[start:end].replace('private static','public static')+'}}'
refs=next((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'));csc=next((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='bot-roster-') as d:
 d=Path(d);(d/'Fixtures.cs').write_text(fixture);out=d/'Regression.dll'
 names=['AI/BotHeroPool.cs','Multiplayer/MatchSession.cs','Multiplayer/CoopMatchPlan.cs','Multiplayer/OnlineMatchPlan.cs']
 r=subprocess.run([str(sdk/'dotnet'),str(csc),'-nologo','-langversion:9','-out:'+str(out),*['-r:'+str(f) for f in refs.glob('*.dll')],str(d/'Fixtures.cs'),str(here/'Regression.cs'),*[str(src/n) for n in names]])
 if r.returncode:raise SystemExit(r.returncode)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
 subprocess.run([str(sdk/'dotnet'),str(out)],check=True)
