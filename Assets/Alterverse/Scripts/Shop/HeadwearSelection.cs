using Alterverse.Characters;
using Alterverse.Profile;

namespace Alterverse.Shop
{
    // Independent of the existing wing/body slot; one owned cap covers every hero.
    public static class HeadwearSelection
    {
        public const string BetaCapName = "BETA CAPPY";
        public const string BetaCapOwnedKey = "DF_HeadwearOwned_BetaCap";
        public const string UfoName = "UFO TEST HAT";
        public const string UfoOwnedKey = "DF_HeadwearOwned_UfoTestHat";
        public const int UfoPrice = 1;
        public static string ResourceFor(UniversalHeroCosmetic value) => value switch
        {
            UniversalHeroCosmetic.BetaCap => "BetaCap",
            UniversalHeroCosmetic.UfoTestHat => "UfoTestHat",
            UniversalHeroCosmetic.WizardHat => "WizardHat",
            UniversalHeroCosmetic.PropellerCap => "PropellerCap",
            UniversalHeroCosmetic.MushroomHat => "MushroomHat",
            _ => null
        };
        public static bool IsFreeTestHat(UniversalHeroCosmetic value) =>
            value == UniversalHeroCosmetic.WizardHat || value == UniversalHeroCosmetic.PropellerCap ||
            value == UniversalHeroCosmetic.MushroomHat;
        public static string HeroKey(HeroId hero) => "DF_HeadwearEquipped_" + (int)hero;
        public static bool IsHeadwear(UniversalHeroCosmetic value) =>
            ResourceFor(value) != null;
        public static bool Supported(UniversalHeroCosmetic value) =>
            value == UniversalHeroCosmetic.Original || IsHeadwear(value);
        public static UniversalHeroCosmetic EquippedFor(HeroId hero)
        {
            if ((int)hero < 0 || (int)hero >= HeroIdentity.HeroCount) return UniversalHeroCosmetic.Original;
            var value = (UniversalHeroCosmetic)CosmeticProfile.GetInt(HeroKey(hero), 0);
            return IsHeadwear(value) && CosmeticCatalog.IsOwned(value)
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

