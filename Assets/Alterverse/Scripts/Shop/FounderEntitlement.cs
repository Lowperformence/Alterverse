using UnityEngine;
namespace Alterverse.Shop
{
public static class FounderEntitlement
{
private const string Key="DF_FoundersPack_Owned";public static bool Owned=>Alterverse.Profile.AccountProfilePrefs.GetInt(Key,0)==1;
#if UNITY_EDITOR
public static void GrantVerified(){Alterverse.Profile.AccountProfilePrefs.SetInt(Key,1);Alterverse.Profile.AccountProfilePrefs.Save();}
public static void SetTestOwnership(bool owned){Alterverse.Profile.AccountProfilePrefs.SetInt(Key,owned?1:0);Alterverse.Profile.AccountProfilePrefs.Save();}
#endif
}
}

