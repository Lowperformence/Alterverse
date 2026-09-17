using UnityEngine;
namespace Alterverse.Profile
{
public static class RewardEconomy
{
public static int RewardForMatch(bool won)=>MatchRewardService.CompletionShards+(won?MatchRewardService.VictoryBonusShards:0);public static int EstimatedMatchesFor(int shardPrice,float assumedWinRate=.5f){float average=MatchRewardService.CompletionShards+MatchRewardService.VictoryBonusShards*Mathf.Clamp01(assumedWinRate);return Mathf.Max(1,Mathf.CeilToInt(shardPrice/average));}public static string ProgressLabel(int shardPrice){int missing=Mathf.Max(0,shardPrice-PlayerWallet.Balance);if(missing==0)return "READY TO UNLOCK";return "ABOUT "+EstimatedMatchesFor(missing)+" MATCHES AWAY";}
}
}
