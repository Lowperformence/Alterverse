using Alterverse.Profile;
namespace Alterverse.Match
{
public readonly struct MatchRewardResult
{
public readonly int Shards,Xp;public MatchRewardResult(int shards,int xp){Shards=shards;Xp=xp;}
}
public static class MatchRewardService
{
public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives,string matchId=null)
{
var reward=Alterverse.Profile.MatchRewardService.GrantMatchReward(won,kills,assists,objectives,matchId);
return new MatchRewardResult(reward.Shards,reward.Xp);
}
}
}