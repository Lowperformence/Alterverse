using Alterverse.Multiplayer;
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
private static int grantSequence;
public static int GrantSequence=>grantSequence;

[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetSequence(){grantSequence=0;}

public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives,string matchId=null,int enemyKills=-1,int hotZoneCaptures=0,int soldierKills=0,int deaths=0)
{
var reward=Alterverse.Profile.MatchRewardService.GrantMatchReward(won,kills,assists,objectives,matchId,enemyKills,hotZoneCaptures,soldierKills,!CoopAuthority.Active,deaths);
grantSequence++;
return new MatchRewardResult(reward.Shards,reward.Xp,reward.BaseShards,reward.ShardBonus,reward.BaseXp,reward.XpBonus,reward.BonusCosmetic,reward.BonusPercent);
}
}
}
