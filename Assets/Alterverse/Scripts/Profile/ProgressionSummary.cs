using UnityEngine;
namespace Alterverse.Profile
{
public static class ProgressionSummary
{
public static int Matches=>MatchRewardService.MatchesCompleted;public static int Wins=>MatchRewardService.MatchesWon;public static int Losses=>Mathf.Max(0,Matches-Wins);public static float WinRate=>Matches<=0?0f:Wins*100f/Matches;public static string Record=>$"{Wins} W  •  {Losses} L";public static string WinRateLabel=>Matches<=0?"NO MATCHES YET":$"{WinRate:0}% WIN RATE";
}
}
