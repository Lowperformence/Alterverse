using Alterverse.Audio;
namespace Alterverse.Shop
{
public static class ShopPurchaseFeedback
{
public static string For(AvatarPurchaseResult result){switch(result){case AvatarPurchaseResult.Purchased:UiSoundRouter.PurchaseSuccess();return "AVATAR UNLOCKED • EQUIPPED";case AvatarPurchaseResult.AlreadyOwned:return "ALREADY IN COLLECTION";case AvatarPurchaseResult.InsufficientShards:return "NOT ENOUGH SHARDS";default:return "ITEM UNAVAILABLE";}}
}
}
