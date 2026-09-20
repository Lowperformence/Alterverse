using UnityEngine;
namespace Alterverse.Profile
{
public static class AvatarInventory
{
private const string Prefix="DF_AvatarOwned_";public const int BuiltInAvatarCount=6,FounderAvatarId=6;public static bool IsOwned(int avatarId){if(avatarId>=0&&avatarId<BuiltInAvatarCount)return true;if(avatarId==FounderAvatarId)return Alterverse.Shop.FounderEntitlement.Owned;return avatarId>FounderAvatarId&&(PlayerPrefs.GetInt(Prefix+avatarId,0)==1||Alterverse.Shop.PaidShardCommerce.OwnsAvatar(avatarId));}public static void Unlock(int avatarId){if(avatarId<=FounderAvatarId)return;PlayerPrefs.SetInt(Prefix+avatarId,1);PlayerPrefs.Save();}internal static string StorageKey(int avatarId)=>Prefix+avatarId;
}
}
