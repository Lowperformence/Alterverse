using UnityEngine;
namespace Alterverse.Profile
{
public static class ProgressionSummary
{
public static int Deaths=>MatchRewardService.LifetimeDeaths;
// Aggregate lifetime counts from every mode; never average individual match ratios.
// A deathless profile displays its kills as the ratio, without persisting a fictional death.
public static double KillDeathRatio=>Kills/(double)System.Math.Max(1,Deaths);
public static string KillDeathRatioLabel=>KillDeathRatio.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
public static int Matches=>MatchRewardService.MatchesCompleted;public static int Wins=>MatchRewardService.MatchesWon;public static int Losses=>Mathf.Max(0,Matches-Wins);public static int Kills=>MatchRewardService.LifetimeKills;public static int Assists=>MatchRewardService.LifetimeAssists;public static int Objectives=>MatchRewardService.LifetimeObjectives;public static float WinRate=>Matches<=0?0f:Wins*100f/Matches;public static string Record=>$"{Wins} W  •  {Losses} L";public static string WinRateLabel=>Matches<=0?"NO MATCHES YET":$"{WinRate:0}% WIN RATE";
}
}
