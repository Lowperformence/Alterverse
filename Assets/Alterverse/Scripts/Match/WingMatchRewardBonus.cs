using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Match
{
    /// <summary>
    /// Central, non-stacking progression bonus for the player's equipped universal cosmetic.
    /// Match rewards use a match-start snapshot; daily rewards use the selected hero at claim time.
    /// </summary>
    public static class WingMatchRewardBonus
    {
        private static UniversalHeroCosmetic matchCosmetic=UniversalHeroCosmetic.Original;
        private static bool hasSnapshot;

        public static UniversalHeroCosmetic MatchCosmetic=>hasSnapshot?matchCosmetic:UniversalHeroCosmetic.Original;
        public static float MatchPercent=>Percent(MatchCosmetic);

        public static void BeginMatch(HeroId playerHero)
        {
            UniversalHeroCosmetic selected=UniversalCosmeticSelection.EquippedFor(playerHero);
            matchCosmetic=UniversalCosmeticSelection.IsOwned(selected)?selected:UniversalHeroCosmetic.Original;
            hasSnapshot=true;
        }

        public static float Percent(UniversalHeroCosmetic cosmetic)=>cosmetic switch
        {
            UniversalHeroCosmetic.Voidfall=>5,
            UniversalHeroCosmetic.PlagueWings=>10,
            UniversalHeroCosmetic.BetaSupporterWings=>20,
            UniversalHeroCosmetic.DeluxeWings=>50,
            UniversalHeroCosmetic.AngelWings=>27.5f,
            UniversalHeroCosmetic.RgbRiftWings=>37.5f,
            _=>0
        };

        public static int CalculateBonus(int baseAmount,UniversalHeroCosmetic cosmetic)
        {
            if(baseAmount<=0)return 0;
            float percent=Percent(cosmetic);
            if(percent<=0)return 0;
            // Keep half-percents exact until the final whole-unit rounding.
            return (int)System.Math.Round(baseAmount*(double)percent/100d,System.MidpointRounding.ToEven);
        }

        public static int DailyShardReward(int baseAmount)
        {
            if(baseAmount<=0)return 0;
            UniversalHeroCosmetic cosmetic=UniversalCosmeticSelection.EquippedFor(PlayerHeroSelection.Selected);
            return (int)System.Math.Min(int.MaxValue,(long)baseAmount+CalculateBonus(baseAmount,cosmetic));
        }

        public static string DisplayName(UniversalHeroCosmetic cosmetic)=>cosmetic switch
        {
            UniversalHeroCosmetic.Voidfall=>"VOIDFALL",
            UniversalHeroCosmetic.PlagueWings=>"PLAGUE WINGS",
            UniversalHeroCosmetic.BetaSupporterWings=>"BETA SUPPORTER",
            UniversalHeroCosmetic.DeluxeWings=>"DELUXE WINGS",
            UniversalHeroCosmetic.AngelWings=>AngelWings.Name,
            UniversalHeroCosmetic.RgbRiftWings=>RgbRiftWings.Name,
            _=>"NO WING BONUS"
        };

        public static string PercentLabel(float percent)=>percent.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture);

        public static string ShopBonus(UniversalHeroCosmetic cosmetic)
        {
            float percent=Percent(cosmetic);
            return percent<=0?"NO MATCH REWARD BONUS":$"+{PercentLabel(percent)}% MATCH XP  /  +{PercentLabel(percent)}% MATCH & DAILY SHARDS";
        }
    }
}

