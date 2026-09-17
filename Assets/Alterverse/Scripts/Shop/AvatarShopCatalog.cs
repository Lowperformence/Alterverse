namespace Alterverse.Shop
{
public readonly struct AvatarShopItem
{
public readonly int AvatarId;public readonly string Name,Theme;public readonly int Price;public AvatarShopItem(int avatarId,string name,string theme,int price){AvatarId=avatarId;Name=name;Theme=theme;Price=price;}
}
public static class AvatarShopCatalog
{
public const string CurrencyName="SHARDS";public static readonly AvatarShopItem[] Items={new AvatarShopItem(10,"NULL FRACTURE","DIMENSION ZERO",450),new AvatarShopItem(11,"LUMEN SIGNAL","LUMEN",450),new AvatarShopItem(12,"FRONT WALKER","COLLISION FRONT",600),new AvatarShopItem(13,"VOID CROWN","DIMENSION ZERO",750),new AvatarShopItem(14,"PRISM EYE","LUMEN",750),new AvatarShopItem(15,"REALITY SCAR","FRONT ELITE",900)};
}
}
