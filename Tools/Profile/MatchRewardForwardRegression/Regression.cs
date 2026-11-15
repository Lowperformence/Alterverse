using System;
using Alterverse.Match;
using Alterverse.Multiplayer;

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) {} }
}
namespace Alterverse.Shop { public enum UniversalHeroCosmetic { Original } }
namespace Alterverse.Multiplayer { public static class CoopAuthority { public static bool Active; } }
namespace Alterverse.Profile
{
    public struct MatchRewardResult
    {
        public int Shards,Xp,BaseShards,ShardBonus,BaseXp,XpBonus;
        public Alterverse.Shop.UniversalHeroCosmetic BonusCosmetic;
        public float BonusPercent;
    }
    public static class MatchRewardService
    {
        public static bool Invited, Mvp, Adaptive;
        public static int Turrets, Deaths;
        public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives,string matchId=null,int enemyKills=-1,int hotZoneCaptures=0,int soldierKills=0,bool adaptiveDifficulty=true,int deaths=0,bool invitedFriend=false,int deployedTurrets=0,bool matchMvp=false)
        {
            Invited=invitedFriend;Turrets=deployedTurrets;Mvp=matchMvp;Adaptive=adaptiveDifficulty;Deaths=deaths;
            return new MatchRewardResult {Shards=13,Xp=27};
        }
    }
}
static class Regression
{
    static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    public static void Main()
    {
        CoopAuthority.Active=true;
        var result=Alterverse.Match.MatchRewardService.GrantMatchReward(true,1,2,3,"receipt",4,5,6,deaths:7,invitedFriend:true,deployedTurrets:2,matchMvp:true);
        Check(Alterverse.Profile.MatchRewardService.Invited&&Alterverse.Profile.MatchRewardService.Turrets==2&&Alterverse.Profile.MatchRewardService.Mvp,"Quest results forwarded");
        Check(Alterverse.Profile.MatchRewardService.Deaths==7&&!Alterverse.Profile.MatchRewardService.Adaptive,"Network rewards retain deaths and disable solo difficulty adaptation");
        Check(result.Shards==13&&result.Xp==27,"Match reward response propagated");
        CoopAuthority.Active=false;
        Alterverse.Match.MatchRewardService.GrantMatchReward(false,0,0,0);
        Check(!Alterverse.Profile.MatchRewardService.Invited&&Alterverse.Profile.MatchRewardService.Turrets==0&&!Alterverse.Profile.MatchRewardService.Mvp&&Alterverse.Profile.MatchRewardService.Adaptive,"Existing offline callers retain defaults");
        Check(Alterverse.Match.MatchRewardService.GrantSequence==2,"Existing grant sequence remains functional");
        Console.WriteLine("PASS: scoreboard named arguments compile and quest results reach profile rewards.");
    }
}
