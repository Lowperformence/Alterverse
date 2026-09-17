namespace Alterverse.Profile
{
public readonly struct ProfileStats
{
public readonly int Matches,Wins,Losses;public readonly float WinRate;public ProfileStats(int matches,int wins){Matches=matches;Wins=wins;Losses=System.Math.Max(0,matches-wins);WinRate=matches>0?wins*100f/matches:0f;}
}
public static class ProfileStatsSummary
{
public static ProfileStats Current=>new ProfileStats(MatchRewardService.MatchesCompleted,MatchRewardService.MatchesWon);
}
}
