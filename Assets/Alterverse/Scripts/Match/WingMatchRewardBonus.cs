using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Match
{
    /// <summary>
    /// Central, non-stacking post-match progression bonus for the player's equipped universal cosmetic.
    /// The selection is snapshotted once when a match starts so changing loadout cannot alter a live match reward.
    /// </summary>
    public static class WingMatchRewardBonus
    {
        private static UniversalHeroCosmetic matchCosmetic=UniversalHeroCosmetic.Original;
        private static bool hasSnapshot;

        public static UniversalHeroCosmetic MatchCosmetic=>hasSnapshot?matchCosmetic:UniversalHeroCosmetic.Original;
        public static int MatchPercent=>Percent(MatchCosmetic);

        public static void BeginMatch(HeroId playerHero)
        {
            UniversalHeroCosmetic selected=UniversalCosmeticSelection.EquippedFor(playerHero);
            matchCosmetic=UniversalCosmeticSelection.IsOwned(selected)?selected:UniversalHeroCosmetic.Original;
            hasSnapshot=true;
        }

        public static int Percent(UniversalHeroCosmetic cosmetic)=>cosmetic switch
        {
            UniversalHeroCosmetic.Voidfall=>5,
            UniversalHeroCosmetic.PlagueWings=>10,
            UniversalHeroCosmetic.BetaSupporterWings=>20,
            _=>0
        };

        public static int CalculateBonus(int baseAmount,UniversalHeroCosmetic cosmetic)
        {
            if(baseAmount<=0)return 0;
            int percent=Percent(cosmetic);
            if(percent<=0)return 0;
            return Mathf.Max(0,Mathf.RoundToInt(baseAmount*(percent/100f)));
        }

        public static string DisplayName(UniversalHeroCosmetic cosmetic)=>cosmetic switch
        {
            UniversalHeroCosmetic.Voidfall=>"VOIDFALL",
            UniversalHeroCosmetic.PlagueWings=>"PLAGUE WINGS",
            UniversalHeroCosmetic.BetaSupporterWings=>"BETA SUPPORTER",
            _=>"NO WING BONUS"
        };

        public static string ShopBonus(UniversalHeroCosmetic cosmetic)
        {
            int percent=Percent(cosmetic);
            return percent<=0?"NO MATCH REWARD BONUS":$"+{percent}% MATCH XP  /  +{percent}% MATCH SHARDS";
        }
    }
}
