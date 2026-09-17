using Alterverse.Profile;
namespace Alterverse.Shop
{
public enum AvatarPurchaseResult{Success,AlreadyOwned,UnknownItem,InsufficientShards}
public static class AvatarPurchaseService
{
public static AvatarPurchaseResult Buy(int avatarId){if(!AvatarShopCatalog.TryGet(avatarId,out AvatarShopItem item))return AvatarPurchaseResult.UnknownItem;if(AvatarInventory.IsOwned(avatarId))return AvatarPurchaseResult.AlreadyOwned;if(!PlayerWallet.TrySpend(item.Price))return AvatarPurchaseResult.InsufficientShards;AvatarInventory.Unlock(avatarId);return AvatarPurchaseResult.Success;}public static string Label(AvatarPurchaseResult result)=>result switch{AvatarPurchaseResult.Success=>"UNLOCKED",AvatarPurchaseResult.AlreadyOwned=>"OWNED",AvatarPurchaseResult.InsufficientShards=>"NOT ENOUGH SHARDS",_=>"UNAVAILABLE"};
}
}
