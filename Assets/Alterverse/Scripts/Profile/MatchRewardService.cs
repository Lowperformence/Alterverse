using Alterverse.Match;using UnityEngine;
namespace Alterverse.Profile
{
public readonly struct MatchRewardResult{public readonly int Shards,Xp;public readonly bool ShardDropped;public MatchRewardResult(int shards,int xp){Shards=shards;Xp=xp;ShardDropped=shards>0;}}
public static class MatchRewardService
{
private const string MatchesKey="DF_MatchesCompleted",WinsKey="DF_MatchesWon";public static int MatchesCompleted=>Mathf.Max(0,PlayerPrefs.GetInt(MatchesKey,0));public static int MatchesWon=>Mathf.Max(0,PlayerPrefs.GetInt(WinsKey,0));
public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives){int shards=MatchShardRewards.Award(won,kills,assists,objectives);int xp=PlayerExperience.GrantMatchXp(won,kills,assists,objectives);PlayerPrefs.SetInt(MatchesKey,MatchesCompleted+1);if(won)PlayerPrefs.SetInt(WinsKey,MatchesWon+1);PlayerPrefs.Save();return new MatchRewardResult(shards,xp);}
}
}