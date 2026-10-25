namespace Alterverse.Characters
{
public static class PlayerHeroSelection
{
private static HeroId selected=HeroId.Veyr;public static HeroId Selected=>Alterverse.Shop.HeroUnlockInventory.Owned(selected)?selected:HeroId.Veyr;public static bool Select(HeroId hero){if(!Alterverse.Shop.HeroUnlockInventory.Owned(hero))return false;selected=hero;return true;}
}
}
