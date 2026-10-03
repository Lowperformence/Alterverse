using Alterverse.Profile;
namespace Alterverse.Shop
{
public enum AvatarPurchaseResult{Purchased,AlreadyOwned,InsufficientShards,InvalidItem,Pending}
public static class AvatarShopService
{
public static AvatarPurchaseResult TryPurchase(AvatarShopItem item){ProfileTransaction.Recover();if(!AvatarShopCatalog.TryGet(item.AvatarId,out AvatarShopItem listed)||listed.Price!=item.Price||listed.Name!=item.Name)return AvatarPurchaseResult.InvalidItem;if(AvatarInventory.IsOwned(item.AvatarId))return AvatarPurchaseResult.AlreadyOwned;if(!PlayerWallet.CanAfford(item.Price))return AvatarPurchaseResult.InsufficientShards;if(PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.Ready){return PlayerWallet.BeginServerPurchase("avatar_"+item.AvatarId)?AvatarPurchaseResult.Pending:AvatarPurchaseResult.InvalidItem;}ProfileTransaction.Commit(new ProfileTransaction.Write("DF_Shards",PlayerWallet.EarnedBalance-item.Price),new ProfileTransaction.Write("DF_AvatarOwned_"+item.AvatarId,1),new ProfileTransaction.Write("DF_Avatar",item.AvatarId));Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);return AvatarPurchaseResult.Purchased;}
}
}

