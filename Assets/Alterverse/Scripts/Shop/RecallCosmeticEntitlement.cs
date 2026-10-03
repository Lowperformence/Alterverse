using UnityEngine;
namespace Alterverse.Shop
{
public static class RecallCosmeticEntitlement
{
private const string Key="DF_Recall_RealityShatter";public static bool Owned=>Alterverse.Profile.AccountProfilePrefs.GetInt(Key,0)==1;
#if UNITY_EDITOR
public static void GrantDevelopmentOnly(){Alterverse.Profile.AccountProfilePrefs.SetInt(Key,1);Alterverse.Profile.AccountProfilePrefs.Save();}
#endif
}
}

