using UnityEngine;
namespace Alterverse.Profile
{
public static class PlayerWallet
{
private const string BalanceKey="DF_Shards";private const int StartingBalance=0;public static int Balance{get=>Mathf.Max(0,PlayerPrefs.GetInt(BalanceKey,StartingBalance));}public static bool CanAfford(int amount)=>amount>=0&&Balance>=amount;public static bool TrySpend(int amount){if(amount<0||!CanAfford(amount))return false;PlayerPrefs.SetInt(BalanceKey,Balance-amount);PlayerPrefs.Save();return true;}public static void Grant(int amount){if(amount<=0)return;PlayerPrefs.SetInt(BalanceKey,ProfileTransaction.Add(Balance,amount));PlayerPrefs.Save();}
}
}
