using Alterverse.Characters;
using Alterverse.Profile;

namespace Alterverse.Shop
{
    // Independent of the existing wing/body slot; one owned cap covers every hero.
    public static class HeadwearSelection
    {
        public const string BetaCapName = "BETA CAPPY";
        public const string BetaCapOwnedKey = "DF_HeadwearOwned_BetaCap";
        public const string UfoName = "UFO CRUISER";
        public const string UfoOwnedKey = "DF_HeadwearOwned_UfoTestHat";
        public const int ShopPrice = 100;
        public const int UfoPrice = ShopPrice;
        // One catalog definition drives shop cards, purchase SKU, ownership and art.
        public static string ResourceFor(UniversalHeroCosmetic value)
        {
            var definition=CosmeticCatalog.Find(value);
            return definition!=null&&definition.Headwear?definition.HeadwearResource:null;
        }
        public static bool IsSerializedResource(string resource)
        {
            if(string.IsNullOrEmpty(resource))return false;
            foreach(var definition in CosmeticCatalog.All)
                if(definition.Headwear&&definition.Id!=UniversalHeroCosmetic.BetaCap&&definition.HeadwearResource==resource)return true;
            return false;
        }
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

