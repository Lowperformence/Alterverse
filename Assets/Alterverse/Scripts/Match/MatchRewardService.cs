using Alterverse.Profile;
namespace Alterverse.Match
{
public readonly struct MatchRewardResult
{
public readonly int Shards,Xp;public MatchRewardResult(int shards,int xp){Shards=shards;Xp=xp;}
}
public static class MatchRewardService
{
public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives)
{
int xp=PlayerExperience.GrantMatchXp(won,kills,assists,objectives);
int shards=MatchShardRewards.Award(won,kills,assists,objectives);
return new MatchRewardResult(shards,xp);
}
}
}