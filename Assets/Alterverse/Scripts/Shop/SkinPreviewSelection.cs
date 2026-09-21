using UnityEngine;
namespace Alterverse.Shop
{
    // Cosmetic preview only; never grants ownership.
    public sealed class SkinPreviewSelection:MonoBehaviour
    {
        public UniversalHeroCosmetic UniversalCosmetic=UniversalHeroCosmetic.Original;
        public bool WeaponOverdrive;
        public int WeaponVariant=-1;

        // Compatibility for existing preview/regression code.
        public bool Voidfall
        {
            get=>UniversalCosmetic==UniversalHeroCosmetic.Voidfall;
            set
            {
                if(value)UniversalCosmetic=UniversalHeroCosmetic.Voidfall;
                else if(UniversalCosmetic==UniversalHeroCosmetic.Voidfall)UniversalCosmetic=UniversalHeroCosmetic.Original;
            }
        }

        public bool BetaSupporter
        {
            get=>UniversalCosmetic==UniversalHeroCosmetic.BetaSupporterWings;
            set
            {
                if(value)UniversalCosmetic=UniversalHeroCosmetic.BetaSupporterWings;
                else if(UniversalCosmetic==UniversalHeroCosmetic.BetaSupporterWings)UniversalCosmetic=UniversalHeroCosmetic.Original;
            }
        }
    }
}
