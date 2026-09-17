using Alterverse.Profile;
namespace Alterverse.Shop
{
public static class ShopIntegrityChecks
{
public static bool AvatarCatalogValid(){for(int i=0;i<AvatarShopCatalog.Items.Length;i++){AvatarShopItem a=AvatarShopCatalog.Items[i];if(a.AvatarId<=AvatarInventory.FounderAvatarId||a.Price<=0||string.IsNullOrWhiteSpace(a.Name))return false;for(int j=i+1;j<AvatarShopCatalog.Items.Length;j++)if(a.AvatarId==AvatarShopCatalog.Items[j].AvatarId)return false;}return true;}public static string Status=>AvatarCatalogValid()?"SHOP DATA OK":"SHOP DATA INVALID";
}
}
