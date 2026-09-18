using Alterverse.Profile;using UnityEngine;
namespace Alterverse.Match
{
public static class MatchShardRewards
{
public const float BaseDropChance=.12f,WinDropBonus=.08f,MaxDropChance=.38f,MaxPerformanceBonus=.18f;public const int MinDrop=6,MaxDrop=24;
public static int Roll(bool won,int kills,int assists,int objectives){kills=Mathf.Max(0,kills);assists=Mathf.Max(0,assists);objectives=Mathf.Max(0,objectives);float performance=Mathf.Clamp(kills*.025f+assists*.012f+objectives*.035f,0f,MaxPerformanceBonus);float chance=Mathf.Clamp(BaseDropChance+(won?WinDropBonus:0f)+performance,0f,MaxDropChance);if(Random.value>chance)return 0;int bonus=Mathf.Clamp(kills/3+assists/5+objectives*2,0,10);return Mathf.Clamp(Random.Range(MinDrop,15)+bonus,MinDrop,MaxDrop);}
public static int Award(bool won,int kills,int assists,int objectives){int amount=Roll(won,kills,assists,objectives);if(amount>0)PlayerWallet.Grant(amount);return amount;}
}
}
