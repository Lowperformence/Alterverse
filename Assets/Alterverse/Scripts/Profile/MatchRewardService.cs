using Alterverse.Match;using UnityEngine;
namespace Alterverse.Profile
{
public readonly struct MatchRewardResult{public readonly int Shards,Xp;public readonly bool ShardDropped;public MatchRewardResult(int shards,int xp){Shards=shards;Xp=xp;ShardDropped=shards>0;}}
public static class MatchRewardService
{
private const string MatchesKey="DF_MatchesCompleted",WinsKey="DF_MatchesWon",KillsKey="DF_LifetimeKills",AssistsKey="DF_LifetimeAssists",ObjectivesKey="DF_LifetimeObjectives";public static int MatchesCompleted=>Mathf.Max(0,PlayerPrefs.GetInt(MatchesKey,0));public static int MatchesWon=>Mathf.Clamp(PlayerPrefs.GetInt(WinsKey,0),0,MatchesCompleted);public static int LifetimeKills=>Mathf.Max(0,PlayerPrefs.GetInt(KillsKey,0));public static int LifetimeAssists=>Mathf.Max(0,PlayerPrefs.GetInt(AssistsKey,0));public static int LifetimeObjectives=>Mathf.Max(0,PlayerPrefs.GetInt(ObjectivesKey,0));
public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives){kills=Mathf.Max(0,kills);assists=Mathf.Max(0,assists);objectives=Mathf.Max(0,objectives);int shards=MatchShardRewards.Award(won,kills,assists,objectives);int xp=PlayerExperience.GrantMatchXp(won,kills,assists,objectives);PlayerPrefs.SetInt(MatchesKey,MatchesCompleted+1);if(won)PlayerPrefs.SetInt(WinsKey,MatchesWon+1);PlayerPrefs.SetInt(KillsKey,LifetimeKills+kills);PlayerPrefs.SetInt(AssistsKey,LifetimeAssists+assists);PlayerPrefs.SetInt(ObjectivesKey,LifetimeObjectives+objectives);PlayerPrefs.Save();return new MatchRewardResult(shards,xp);}
}
}