using Alterverse.Profile;
using Alterverse.Shop;
namespace Alterverse.Match
{
public readonly struct MatchRewardResult
{
public readonly int Shards,Xp,BaseShards,ShardBonus,BaseXp,XpBonus,BonusPercent;
public readonly UniversalHeroCosmetic BonusCosmetic;

public MatchRewardResult(int shards,int xp)
{
Shards=shards;Xp=xp;BaseShards=shards;ShardBonus=0;BaseXp=xp;XpBonus=0;BonusPercent=0;BonusCosmetic=UniversalHeroCosmetic.Original;
}

public MatchRewardResult(int shards,int xp,int baseShards,int shardBonus,int baseXp,int xpBonus,UniversalHeroCosmetic bonusCosmetic,int bonusPercent)
{
Shards=shards;Xp=xp;BaseShards=baseShards;ShardBonus=shardBonus;BaseXp=baseXp;XpBonus=xpBonus;BonusCosmetic=bonusCosmetic;BonusPercent=bonusPercent;
}
}
public static class MatchRewardService
{
public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives,string matchId=null)
{
var reward=Alterverse.Profile.MatchRewardService.GrantMatchReward(won,kills,assists,objectives,matchId);
return new MatchRewardResult(reward.Shards,reward.Xp,reward.BaseShards,reward.ShardBonus,reward.BaseXp,reward.XpBonus,reward.BonusCosmetic,reward.BonusPercent);
}
}
}
