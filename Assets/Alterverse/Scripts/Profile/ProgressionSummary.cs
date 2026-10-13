using UnityEngine;
namespace Alterverse.Profile
{
public static class ProgressionSummary
{
public static int Deaths=>MatchRewardService.LifetimeDeaths;
// Divide aggregate kills by deaths captured over the same period in every mode.
// Missing history is unknown; zero deaths must not be replaced with a fictional death.
public static double KillDeathRatio=>!MatchRewardService.HasKillDeathStats?double.NaN:
    MatchRewardService.KillDeathDeaths>0?MatchRewardService.KillDeathKills/(double)MatchRewardService.KillDeathDeaths:
    MatchRewardService.KillDeathKills>0?double.PositiveInfinity:0d;
public static string KillDeathRatioLabel=>double.IsNaN(KillDeathRatio)?"—":
    double.IsPositiveInfinity(KillDeathRatio)?"∞":KillDeathRatio.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
public static int Matches=>MatchRewardService.MatchesCompleted;public static int Wins=>MatchRewardService.MatchesWon;public static int Losses=>Mathf.Max(0,Matches-Wins);public static int Kills=>MatchRewardService.LifetimeKills;public static int Assists=>MatchRewardService.LifetimeAssists;public static int Objectives=>MatchRewardService.LifetimeObjectives;public static float WinRate=>Matches<=0?0f:Wins*100f/Matches;public static string Record=>$"{Wins} W  •  {Losses} L";public static string WinRateLabel=>Matches<=0?"NO MATCHES YET":$"{WinRate:0}% WIN RATE";
}
}
