using UnityEngine;
namespace Alterverse.Profile
{
public static class AvatarInventory
{
private const string Prefix="DF_AvatarOwned_";public static bool IsOwned(int avatarId)=>avatarId>=0&&avatarId<=6||PlayerPrefs.GetInt(Prefix+avatarId,0)==1;public static void Unlock(int avatarId){if(avatarId<0)return;PlayerPrefs.SetInt(Prefix+avatarId,1);PlayerPrefs.Save();}
}
}
