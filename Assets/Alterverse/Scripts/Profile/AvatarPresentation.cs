using Alterverse.Shop;
namespace Alterverse.Profile
{
public static class AvatarPresentation
{
public static string Name(int id){if(id==BattlePassProgress.BetaCrownAvatarId)return "BETA CROWN";if(id==AvatarInventory.FounderAvatarId)return "FIRST FRACTURE";if(id>=0&&id<AvatarInventory.BuiltInAvatarCount)return id%2==0?"VOID COMET":"ORBITAL WATCH";return AvatarShopCatalog.TryGet(id,out AvatarShopItem item)?item.Name:"UNKNOWN";}public static string Theme(int id){if(id==BattlePassProgress.BetaCrownAvatarId)return BattlePassProgress.SeasonName;if(id==AvatarInventory.FounderAvatarId)return "FOUNDER";if(id>=0&&id<AvatarInventory.BuiltInAvatarCount)return id%2==0?"DIMENSION ZERO":"LUMEN";return AvatarShopCatalog.TryGet(id,out AvatarShopItem item)?item.Theme:"";}public static string Counter=>$"{AvatarCollection.Count} OWNED";
}
}
