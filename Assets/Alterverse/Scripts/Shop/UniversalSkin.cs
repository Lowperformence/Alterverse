using Alterverse.Profile;
using Alterverse.Characters;
using UnityEngine;
namespace Alterverse.Shop
{
    public enum UniversalHeroCosmetic
    {
        Original=0,
        Voidfall=1,
        BetaSupporterWings=2,
        PlagueWings=3
    }

    /// <summary>Account ownership, one exclusive cosmetic slot per hero. Legacy keys are retained.</summary>
    public static class UniversalCosmeticSelection
    {
        public const string EquippedKey="DF_Skin_UniversalEquipped";
        public const string MigrationKey="DF_Skin_HeroEquip_v1";
        public static string HeroKey(HeroId hero)=>"DF_Skin_HeroEquipped_"+(int)hero;
        public static UniversalHeroCosmetic Equipped=>EquippedFor(PlayerHeroSelection.Selected);
        public static bool IsOwned(UniversalHeroCosmetic cosmetic)=>CosmeticCatalog.IsOwned(cosmetic);

        public static void EnsureMigrated(HeroId selectedHero)
        {
            CosmeticProfile.Recover();
            if(CosmeticProfile.GetInt(MigrationKey,0)==1)return;
            int raw=CosmeticProfile.HasKey(EquippedKey)?CosmeticProfile.GetInt(EquippedKey):
                CosmeticProfile.GetInt(UniversalSkin.EquippedKey,0)==1?(int)UniversalHeroCosmetic.Voidfall:0;
            var legacy=(UniversalHeroCosmetic)raw;
            // Keep valid legacy intent even if a server entitlement has not synchronized yet.
            if(CosmeticCatalog.Find(legacy)==null)legacy=UniversalHeroCosmetic.Original;
            var writes=new System.Collections.Generic.List<ProfileTransaction.Write>(7);
            // Preserve any already-existing per-hero slot, including a deliberate Original choice.
            for(int i=0;i<HeroIdentity.HeroCount;i++)
            {
                var hero=(HeroId)i;string key=HeroKey(hero);
                if(!CosmeticProfile.HasKey(key))writes.Add(new ProfileTransaction.Write(key,hero==selectedHero?(int)legacy:0));
            }
            writes.Add(new ProfileTransaction.Write(MigrationKey,1));
            CosmeticProfile.Commit(writes.ToArray());
        }
        public static UniversalHeroCosmetic EquippedFor(HeroId hero)
        {
            EnsureMigrated(PlayerHeroSelection.Selected);
            var selected=(UniversalHeroCosmetic)CosmeticProfile.GetInt(HeroKey(hero),0);
            // Invalid or temporarily unavailable entitlements fall back without destroying saved intent.
            return IsOwned(selected)?selected:UniversalHeroCosmetic.Original;
        }
        public static bool EquipFor(HeroId hero,UniversalHeroCosmetic cosmetic)
        {
            EnsureMigrated(PlayerHeroSelection.Selected);
            if((int)hero<0||(int)hero>=HeroIdentity.HeroCount||!IsOwned(cosmetic))return false;
            CosmeticProfile.Commit(new ProfileTransaction.Write(HeroKey(hero),(int)cosmetic));
            return true;
        }
        public static bool Equip(UniversalHeroCosmetic cosmetic)=>EquipFor(PlayerHeroSelection.Selected,cosmetic);
    }

    public static class UniversalSkin
    {
        public const string Name="VOIDFALL",OwnedKey="DF_SkinOwned_Voidfall",EquippedKey="DF_Skin_Voidfall";
        public const int Price=2000;
        public static bool Owned=>CosmeticProfile.GetInt(OwnedKey,0)==1||CosmeticProfile.PaidVoidfall;
        public static bool Equipped=>Owned&&UniversalCosmeticSelection.Equipped==UniversalHeroCosmetic.Voidfall;
        public static AvatarPurchaseResult Purchase()=>CosmeticCatalog.Purchase(UniversalHeroCosmetic.Voidfall);

        public static bool Equip(bool enabled)
        {
            if(enabled)return UniversalCosmeticSelection.Equip(UniversalHeroCosmetic.Voidfall);
            return !Equipped||UniversalCosmeticSelection.Equip(UniversalHeroCosmetic.Original);
        }
    }

    public static class BetaSupporterWings
    {
        public const string Name="BETA SUPPORTER WINGS";
        public const string OwnedKey="DF_SkinOwned_BetaSupporterWings";
        public const int Price=8000;

        public static bool Owned=>CosmeticProfile.GetInt(OwnedKey,0)==1;
        public static bool Equipped=>Owned&&UniversalCosmeticSelection.Equipped==UniversalHeroCosmetic.BetaSupporterWings;

        public static AvatarPurchaseResult Purchase()=>CosmeticCatalog.Purchase(UniversalHeroCosmetic.BetaSupporterWings);

        public static bool Equip(bool enabled)
        {
            if(enabled)return UniversalCosmeticSelection.Equip(UniversalHeroCosmetic.BetaSupporterWings);
            return !Equipped||UniversalCosmeticSelection.Equip(UniversalHeroCosmetic.Original);
        }
    }
    public static class PlagueWings
    {
        public const string Name="PLAGUE WINGS";
        public const string OwnedKey="DF_SkinOwned_PlagueWings";
        public const int Price=4000;

        public static bool Owned=>CosmeticProfile.GetInt(OwnedKey,0)==1;
        public static bool Equipped=>Owned&&UniversalCosmeticSelection.Equipped==UniversalHeroCosmetic.PlagueWings;

        public static AvatarPurchaseResult Purchase()=>CosmeticCatalog.Purchase(UniversalHeroCosmetic.PlagueWings);

        public static bool Equip(bool enabled)
        {
            if(enabled)return UniversalCosmeticSelection.Equip(UniversalHeroCosmetic.PlagueWings);
            return !Equipped||UniversalCosmeticSelection.Equip(UniversalHeroCosmetic.Original);
        }
    }

}

