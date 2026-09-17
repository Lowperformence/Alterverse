using Alterverse.Profile;
namespace Alterverse.Shop
{
public static class ShopIntegrityChecks
{
public static bool AvatarCatalogValid(){if(AvatarShopCatalog.Items==null||AvatarShopCatalog.Items.Length==0)return false;for(int i=0;i<AvatarShopCatalog.Items.Length;i++){AvatarShopItem a=AvatarShopCatalog.Items[i];if(a.AvatarId<=AvatarInventory.FounderAvatarId||a.Price<=0||string.IsNullOrWhiteSpace(a.Name)||string.IsNullOrWhiteSpace(a.Theme))return false;for(int j=i+1;j<AvatarShopCatalog.Items.Length;j++){AvatarShopItem b=AvatarShopCatalog.Items[j];if(a.AvatarId==b.AvatarId||string.Equals(a.Name,b.Name,System.StringComparison.OrdinalIgnoreCase))return false;}}return true;}public static string Status=>AvatarCatalogValid()?"SHOP DATA OK":"SHOP DATA INVALID";
}
}
