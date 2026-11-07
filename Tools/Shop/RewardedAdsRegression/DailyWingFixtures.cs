using System.Collections.Generic;

namespace Alterverse.Characters
{
    public enum HeroId { Veyr, Rook }
    public static class PlayerHeroSelection { public static HeroId Selected; }
}

namespace Alterverse.Shop
{
    public enum UniversalHeroCosmetic { Original, Voidfall, PlagueWings, BetaSupporterWings, DeluxeWings, AngelWings, RgbRiftWings }
    public static class AngelWings { public const string Name = "ANGEL WINGS"; }
    public static class RgbRiftWings { public const string Name = "RGB WINGS"; }

    // The real EquippedFor checks each hero's slot and entitlement; the fixture
    // models those inputs while compiling the real daily-reward implementation.
    public static class UniversalCosmeticSelection
    {
        public static readonly Dictionary<Alterverse.Characters.HeroId, UniversalHeroCosmetic> Slots = new();
        public static readonly HashSet<UniversalHeroCosmetic> Owned = new();
        public static bool IsOwned(UniversalHeroCosmetic cosmetic) => cosmetic == UniversalHeroCosmetic.Original || Owned.Contains(cosmetic);
        public static UniversalHeroCosmetic EquippedFor(Alterverse.Characters.HeroId hero) =>
            Slots.TryGetValue(hero, out var cosmetic) && IsOwned(cosmetic) ? cosmetic : UniversalHeroCosmetic.Original;
    }
}
