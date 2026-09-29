#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEditor;
using UnityEngine;

namespace Alterverse.Shop
{
    public static class LegendaryWeaponChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Legendary weapons (non-destructive)")]
        public static void Run()
        {
            HeroId[] heroes={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Rook,HeroId.Traveler};
            var names=new HashSet<string>(StringComparer.Ordinal);
            Assert(WeaponSkinCatalog.PremiumPrice==1100,"Premium legendary price must remain 1100 shards.");
            Assert(WeaponSkinCatalog.PremiumPrice>WeaponSkinCatalog.Price,"Legendary tier must cost more than EPIC.");

            for(int i=0;i<heroes.Length;i++)
            {
                HeroId hero=heroes[i];
                Assert(WeaponSkinCatalog.Contains(hero,1),hero+" is missing legendary variant 1.");
                WeaponSkinItem item=WeaponSkinCatalog.For(hero,1);
                Assert(item.Variant==1,hero+" legendary must use variant 1.");
                Assert(item.Price==WeaponSkinCatalog.PremiumPrice,hero+" legendary must cost 1100 shards.");
                Assert(item.Name.Contains("LEGENDARY"),hero+" legendary store card must be labelled LEGENDARY.");
                Assert(!string.IsNullOrWhiteSpace(item.WeaponName)&&names.Add(item.WeaponName),hero+" legendary weapon name must be unique.");
                Assert(!string.IsNullOrWhiteSpace(item.Description)&&item.Description.Length>=70,hero+" legendary needs a premium-detail store description.");
            }

            Debug.Log("DIMENSIONS FRONT: all 7 legendary weapons have unique names, variant-1 ownership entries and the 1100-shard premium price. Visual motion remains an in-Unity presentation check.");
        }

        private static void Assert(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("Legendary weapon regression: "+message);
        }
    }
}
#endif
