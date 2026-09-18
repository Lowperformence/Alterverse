namespace Alterverse.Shop
{
public readonly struct AvatarShopItem
{
public readonly int AvatarId;public readonly string Name,Theme;public readonly int Price;public AvatarShopItem(int avatarId,string name,string theme,int price){AvatarId=avatarId;Name=name;Theme=theme;Price=price;}
}
public static class AvatarShopCatalog
{
public const string CurrencyName="SHARDS";public static readonly AvatarShopItem[] Items={new AvatarShopItem(10,"SHATTERED MOON","ZERO",ShardPacks.CosmeticPrice("avatar_10")),new AvatarShopItem(11,"LUMEN LENS","LUMEN",ShardPacks.CosmeticPrice("avatar_11")),new AvatarShopItem(12,"REALITY COLLISION","COLLISION FRONT",ShardPacks.CosmeticPrice("avatar_12")),new AvatarShopItem(13,"VOID MONOLITH","ZERO",ShardPacks.CosmeticPrice("avatar_13")),new AvatarShopItem(14,"QUELL PRISM","LUMEN",ShardPacks.CosmeticPrice("avatar_14")),new AvatarShopItem(15,"ECLIPSE RELIC","FRONT ELITE",ShardPacks.CosmeticPrice("avatar_15"))};public static bool TryGet(int avatarId,out AvatarShopItem item){for(int i=0;i<Items.Length;i++)if(Items[i].AvatarId==avatarId){item=Items[i];return true;}item=default;return false;}
}
}
