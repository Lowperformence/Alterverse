namespace Alterverse.Shop
{
public readonly struct AvatarShopItem
{
public readonly int AvatarId;public readonly string Name,Theme;public readonly int Price;public AvatarShopItem(int avatarId,string name,string theme,int price){AvatarId=avatarId;Name=name;Theme=theme;Price=price;}
}
public static class AvatarShopCatalog
{
public const string CurrencyName="SHARDS";public static readonly AvatarShopItem[] Items={new AvatarShopItem(10,"SHATTERED MOON","ZERO",450),new AvatarShopItem(11,"LUMEN LENS","LUMEN",450),new AvatarShopItem(12,"REALITY COLLISION","COLLISION FRONT",600),new AvatarShopItem(13,"VOID MONOLITH","ZERO",750),new AvatarShopItem(14,"QUELL PRISM","LUMEN",750),new AvatarShopItem(15,"ECLIPSE RELIC","FRONT ELITE",900)};public static bool TryGet(int avatarId,out AvatarShopItem item){for(int i=0;i<Items.Length;i++)if(Items[i].AvatarId==avatarId){item=Items[i];return true;}item=default;return false;}
}
}
