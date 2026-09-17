using Alterverse.Profile;
namespace Alterverse.Shop
{
public static class ShopDiagnostics
{
public static string Summary=>$"{ShopIntegrityChecks.Status} | {PlayerWallet.Balance} {AvatarShopCatalog.CurrencyName} | {OwnedAvatarCount()}/{AvatarShopCatalog.Items.Length} SHOP AVATARS";public static int OwnedAvatarCount(){int count=0;for(int i=0;i<AvatarShopCatalog.Items.Length;i++)if(AvatarInventory.IsOwned(AvatarShopCatalog.Items[i].AvatarId))count++;return count;}public static bool FounderGateValid=>!AvatarInventory.IsOwned(AvatarInventory.FounderAvatarId)||FounderEntitlement.Owned;
}
}
