using Alterverse.Shop;
namespace Alterverse.Profile
{
public static class AvatarPresentation
{
public static string Name(int id){if(id==AvatarInventory.FounderAvatarId)return "FIRST FRACTURE";if(id>=0&&id<AvatarInventory.BuiltInAvatarCount)return "FRONT ID "+(id+1);return AvatarShopCatalog.TryGet(id,out AvatarShopItem item)?item.Name:"UNKNOWN";}public static string Theme(int id){if(id==AvatarInventory.FounderAvatarId)return "FOUNDER";if(id>=0&&id<AvatarInventory.BuiltInAvatarCount)return id<3?"DIMENSION ZERO":"LUMEN";return AvatarShopCatalog.TryGet(id,out AvatarShopItem item)?item.Theme:"";}public static string Counter=>$"{AvatarCollection.Count} OWNED";
}
}
