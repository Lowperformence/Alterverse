using UnityEngine;
namespace Alterverse.Profile
{
public static class MatchRewardService
{
private const string MatchesKey="DF_MatchesCompleted",WinsKey="DF_MatchesWon";public const int CompletionShards=70,VictoryBonusShards=30;public static int MatchesCompleted=>Mathf.Max(0,PlayerPrefs.GetInt(MatchesKey,0));public static int MatchesWon=>Mathf.Max(0,PlayerPrefs.GetInt(WinsKey,0));public static int GrantMatchReward(bool won){int reward=CompletionShards+(won?VictoryBonusShards:0);PlayerWallet.Grant(reward);PlayerPrefs.SetInt(MatchesKey,MatchesCompleted+1);if(won)PlayerPrefs.SetInt(WinsKey,MatchesWon+1);PlayerPrefs.Save();return reward;}
}
}
