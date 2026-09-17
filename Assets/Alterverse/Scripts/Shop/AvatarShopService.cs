using Alterverse.Profile;
namespace Alterverse.Shop
{
public enum AvatarPurchaseResult{Purchased,AlreadyOwned,InsufficientShards,InvalidItem}
public static class AvatarShopService
{
public static AvatarPurchaseResult TryPurchase(AvatarShopItem item){if(item.AvatarId<=AvatarInventory.FounderAvatarId||item.Price<0)return AvatarPurchaseResult.InvalidItem;if(AvatarInventory.IsOwned(item.AvatarId))return AvatarPurchaseResult.AlreadyOwned;if(!PlayerWallet.TrySpend(item.Price))return AvatarPurchaseResult.InsufficientShards;AvatarInventory.Unlock(item.AvatarId);PlayerProfile.Avatar=item.AvatarId;return AvatarPurchaseResult.Purchased;}
}
}
