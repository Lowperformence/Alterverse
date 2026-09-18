using UnityEngine;
namespace Alterverse.Profile
{
public static class RewardEconomy
{
private const float BaseDropChance=.12f,WinBonusChance=.08f,MinShardDrop=6f,MaxShardDrop=24f;
public static float ExpectedShardsPerMatch(float assumedWinRate=.5f)
{
float winRate=Mathf.Clamp01(assumedWinRate);
float dropChance=BaseDropChance+WinBonusChance*winRate;
return dropChance*((MinShardDrop+MaxShardDrop)*.5f);
}
public static int EstimatedMatchesFor(int shardPrice,float assumedWinRate=.5f)
{
float average=Mathf.Max(.01f,ExpectedShardsPerMatch(assumedWinRate));
return Mathf.Max(1,Mathf.CeilToInt(shardPrice/average));
}
public static string ProgressLabel(int shardPrice)
{
int missing=Mathf.Max(0,shardPrice-PlayerWallet.Balance);
if(missing==0)return "READY TO UNLOCK";
return "ABOUT "+EstimatedMatchesFor(missing)+" MATCHES AWAY";
}
}
}