using Alterverse.Characters;
using Alterverse.Profile;

namespace Alterverse.Shop
{
    // Independent of the existing wing/body slot; one owned cap covers every hero.
    public static class HeadwearSelection
    {
        public const string BetaCapName = "BETA CAPPY";
        public const string BetaCapOwnedKey = "DF_HeadwearOwned_BetaCap";
        public static string HeroKey(HeroId hero) => "DF_HeadwearEquipped_" + (int)hero;
        public static bool Supported(UniversalHeroCosmetic value) =>
            value == UniversalHeroCosmetic.Original || value == UniversalHeroCosmetic.BetaCap;
        public static UniversalHeroCosmetic EquippedFor(HeroId hero)
        {
            if ((int)hero < 0 || (int)hero >= HeroIdentity.HeroCount) return UniversalHeroCosmetic.Original;
            var value = (UniversalHeroCosmetic)CosmeticProfile.GetInt(HeroKey(hero), 0);
            return value == UniversalHeroCosmetic.BetaCap && CosmeticCatalog.IsOwned(value)
                ? value : UniversalHeroCosmetic.Original;
        }
        public static bool EquipFor(HeroId hero, UniversalHeroCosmetic value)
        {
            if ((int)hero < 0 || (int)hero >= HeroIdentity.HeroCount || !Supported(value) ||
                value != UniversalHeroCosmetic.Original && !CosmeticCatalog.IsOwned(value)) return false;
            CosmeticProfile.Commit(new ProfileTransaction.Write(HeroKey(hero), (int)value));
            return true;
        }
    }
}
