#if UNITY_EDITOR
using System;
using Alterverse.Characters;
using UnityEditor;
using UnityEngine;

namespace Alterverse.Shop
{
    public static class LegendaryWeaponChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Weapon colorways (non-destructive)")]
        public static void Run()
        {
            HeroId[] heroes={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Rook,HeroId.Traveler};

            Assert(WeaponSkinCatalog.Items!=null,"Weapon colorway catalog is missing.");
            Assert(WeaponSkinCatalog.Items.Length==heroes.Length*2,"Weapon shop must contain exactly two purchasable colorways per hero.");

            for(int i=0;i<heroes.Length;i++)
            {
                HeroId hero=heroes[i];
                Assert(WeaponSkinCatalog.VariantCount(hero)==2,hero+" must have exactly two colorways.");
                Assert(WeaponSkinCatalog.Contains(hero,0),hero+" is missing colorway 0.");
                Assert(WeaponSkinCatalog.Contains(hero,1),hero+" is missing colorway 1.");

                WeaponSkinItem first=WeaponSkinCatalog.For(hero,0);
                WeaponSkinItem second=WeaponSkinCatalog.For(hero,1);
                Assert(first.Variant==0&&second.Variant==1,hero+" has invalid colorway ids.");
                Assert(first.Price==WeaponSkinCatalog.Price&&second.Price==WeaponSkinCatalog.Price,hero+" colorways must use the standard weapon-skin price.");
                Assert(string.Equals(first.WeaponName,second.WeaponName,StringComparison.Ordinal),hero+" colorways must use the same authored standard weapon.");
                Assert(!string.Equals(first.Name,second.Name,StringComparison.OrdinalIgnoreCase),hero+" colorway names must be distinct.");
                Assert(first.Glow!=second.Glow,hero+" colorways must have visibly different colors.");
            }

            Debug.Log("DIMENSIONS FRONT: weapon shop contains exactly two cosmetic colorways per hero, both using the same authored standard weapon silhouette.");
        }

        private static void Assert(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("Weapon colorway regression: "+message);
        }
    }
}
#endif
