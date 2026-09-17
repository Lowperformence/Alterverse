using Alterverse.Profile;
namespace Alterverse.Shop
{
public readonly struct AvatarShopCard
{
public readonly int Id,Price;public readonly string Name,Theme,Action,Progress;public readonly bool Owned,Affordable;public AvatarShopCard(AvatarShopItem item){Id=item.AvatarId;Price=item.Price;Name=item.Name;Theme=item.Theme;Owned=AvatarInventory.IsOwned(Id);Affordable=PlayerWallet.CanAfford(Price);Action=Owned?"OWNED":Affordable?"UNLOCK":"EARN SHARDS";Progress=Owned?"IN COLLECTION":RewardEconomy.ProgressLabel(Price);}
}
public static class AvatarShopViewModel
{
public static AvatarShopCard Card(int index)=>new AvatarShopCard(AvatarShopCatalog.Items[index]);public static int Count=>AvatarShopCatalog.Items.Length;public static string Balance=>$"{PlayerWallet.Balance} {AvatarShopCatalog.CurrencyName}";
}
}
