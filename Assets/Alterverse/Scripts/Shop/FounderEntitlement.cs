using UnityEngine;
namespace Alterverse.Shop
{
public static class FounderEntitlement
{
private const string Key="DF_FoundersPack_Owned";public static bool Owned=>PlayerPrefs.GetInt(Key,0)==1;
#if UNITY_EDITOR
public static void SetTestOwnership(bool owned){PlayerPrefs.SetInt(Key,owned?1:0);PlayerPrefs.Save();}
#endif
}
}
