namespace Alterverse.Characters
{
public static class PlayerHeroSelection
{
private static HeroId selected=HeroId.Veyr;public static HeroId Selected=>selected;public static void Select(HeroId hero){selected=hero;}
}
}
