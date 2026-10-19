"""Exercise real reward services/journal and wing percentages through retry receipts.
Uses the existing profile engine fixture; no live game data is read or written.
"""
import argparse,json,subprocess,tempfile
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root',type=Path,required=True)
args=parser.parse_args()
here=Path(__file__).resolve().parent
root=here.parents[2]
source=root/'Assets/Alterverse/Scripts'
sdk=sorted((args.dotnet_root/'sdk').glob('*'))[-1]
refs=sorted((args.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]

def block(text,start):
    a=text.index('{',start);depth=1;b=a+1
    while depth:
        depth+=(text[b]=='{')-(text[b]=='}');b+=1
    return text[start:b],b

stubs=(root/'Tools/Profile/Regression/EngineStubs.cs').read_text()
old,_=block(stubs,stubs.index('    public static class WingMatchRewardBonus'))
stubs=stubs.replace(old,'')
enums=(source/'Shop/UniversalSkin.cs').read_text()
enum,_=block(enums,enums.index('public enum UniversalHeroCosmetic'))
stubs=stubs.replace('public enum UniversalHeroCosmetic { Original }',enum+'''
public static class UniversalCosmeticSelection
{
    public static UniversalHeroCosmetic Selected;
    public static UniversalHeroCosmetic EquippedFor(Alterverse.Characters.HeroId hero)=>Selected;
    public static bool IsOwned(UniversalHeroCosmetic cosmetic)=>true;
}
public static class AngelWings { public const string Name="ANGEL WINGS"; }
public static class RgbRiftWings { public const string Name="RGB RIFT WINGS"; }
''')
stubs+='\nnamespace Alterverse.Characters { public enum HeroId { Traveler } }\n'
stubs=stubs.replace('CalculateMatchXp(bool won,int kills,int assists,int objectives)=>10','CalculateMatchXp(bool won,int kills,int assists,int objectives)=>400')
stubs=stubs.replace('Roll(bool won,int kills,int assists,int objectives)=>1','Roll(bool won,int kills,int assists,int objectives)=>400')
program='''
using System;
using Alterverse.Shop;
using Alterverse.Profile;
using Alterverse.Match;
using Alterverse.Characters;
class ReceiptRegression
{
    static void Assert(bool ok,string label){if(!ok)throw new Exception(label);}
    static void Main()
    {
        foreach(var id in new[]{UniversalHeroCosmetic.DeluxeWings,UniversalHeroCosmetic.AngelWings,UniversalHeroCosmetic.RgbRiftWings})
        {
            AccountProfilePrefs.Account="wing-receipt-"+id;
            UniversalCosmeticSelection.Selected=id;
            WingMatchRewardBonus.BeginMatch(HeroId.Traveler);
            float percent=WingMatchRewardBonus.Percent(id);
            int bonus=WingMatchRewardBonus.CalculateBonus(400,id);
            var first=Alterverse.Match.MatchRewardService.GrantMatchReward(true,0,0,0,"receipt");
            Assert(first.BonusPercent==percent&&first.BonusCosmetic==id,"Service wrapper retains full percentage and enum ID");
            Assert(first.Xp==400+bonus&&first.Shards==400+bonus,"Both rewards receive exact percentage");
            UniversalCosmeticSelection.Selected=UniversalHeroCosmetic.Original;
            WingMatchRewardBonus.BeginMatch(HeroId.Traveler);
            var retry=Alterverse.Match.MatchRewardService.GrantMatchReward(true,0,0,0,"receipt");
            Assert(retry.BonusCosmetic==id&&retry.BonusPercent==percent,"Stored receipt retains IDs 4/5/6 after loadout changes");
            Assert(retry.Shards==first.Shards&&retry.Xp==first.Xp,"Retry returns original whole reward amounts");
            Assert(AccountProfilePrefs.GetInt("DF_Shards")==first.Shards,"No duplicate currency on retry");
            AccountProfilePrefs.SetInt("DF_LastRewardBonusCosmetic",999);
            var invalid=Alterverse.Match.MatchRewardService.GrantMatchReward(true,0,0,0,"receipt");
            Assert(invalid.BonusPercent==0&&invalid.BonusCosmetic==UniversalHeroCosmetic.Original,"Invalid saved cosmetic falls back safely");
        }
        Console.WriteLine("PASS: Deluxe/Angel/RGB receipt identity, fractional XP/shard totals, reload, invalid ID and idempotent retry.");
    }
}
'''
with tempfile.TemporaryDirectory(prefix='df-wing-receipts-') as folder:
    tmp=Path(folder)
    (tmp/'Engine.cs').write_text(stubs);(tmp/'Regression.cs').write_text(program)
    out=tmp/'Regression.dll'
    files=['Profile/MatchRewardService.cs','Profile/ProfileTransaction.cs','Match/MatchRewardService.cs','Match/WingMatchRewardBonus.cs']
    command=[str(args.dotnet_root/'dotnet'),'exec',str(sdk/'Roslyn/bincore/csc.dll'),'-nologo','-target:exe','-out:'+str(out)]
    command+=['-r:'+str(f) for f in refs.glob('*.dll')]
    command+=[str(tmp/'Engine.cs'),str(tmp/'Regression.cs')]+[str(source/f) for f in files]
    subprocess.run(command,check=True)
    (tmp/'Regression.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'},'rollForward':'LatestPatch'}}))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(out)],check=True)
