using UnityEngine;
namespace Alterverse.Shop
{
public static class RecallCosmeticEntitlement
{
private const string Key="DF_Recall_RealityShatter";public static bool Owned=>PlayerPrefs.GetInt(Key,0)==1;
#if UNITY_EDITOR
public static void GrantDevelopmentOnly(){PlayerPrefs.SetInt(Key,1);PlayerPrefs.Save();}
#endif
}
}
