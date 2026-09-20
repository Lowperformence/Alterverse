using Alterverse.Profile;
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

    /// <summary>
    /// One exclusive universal cosmetic slot. The legacy VOIDFALL bool is preserved for save compatibility.
    /// </summary>
    public static class UniversalCosmeticSelection
    {
        public const string EquippedKey="DF_Skin_UniversalEquipped";

        public static UniversalHeroCosmetic Equipped
        {
            get
            {
                ProfileTransaction.Recover();
                if(PlayerPrefs.HasKey(EquippedKey))
                {
                    UniversalHeroCosmetic selected=(UniversalHeroCosmetic)Mathf.Clamp(PlayerPrefs.GetInt(EquippedKey,0),0,3);
                    if(IsOwned(selected))return selected;
                    if(selected!=UniversalHeroCosmetic.Original)
                    {
                        ProfileTransaction.Commit(
                            new ProfileTransaction.Write(EquippedKey,(int)UniversalHeroCosmetic.Original),
                            new ProfileTransaction.Write(UniversalSkin.EquippedKey,0));
                    }
                    return UniversalHeroCosmetic.Original;
                }

                // Migrate the existing VOIDFALL equip bit without resetting ownership or requiring a repurchase.
                if(PlayerPrefs.GetInt(UniversalSkin.EquippedKey,0)==1&&UniversalSkin.Owned)
                {
                    ProfileTransaction.Commit(new ProfileTransaction.Write(EquippedKey,(int)UniversalHeroCosmetic.Voidfall));
                    return UniversalHeroCosmetic.Voidfall;
                }
                return UniversalHeroCosmetic.Original;
            }
        }

        public static bool IsOwned(UniversalHeroCosmetic cosmetic)=>cosmetic switch
        {
            UniversalHeroCosmetic.Original=>true,
            UniversalHeroCosmetic.Voidfall=>UniversalSkin.Owned,
            UniversalHeroCosmetic.BetaSupporterWings=>BetaSupporterWings.Owned,
            UniversalHeroCosmetic.PlagueWings=>PlagueWings.Owned,
            _=>false
        };

        public static bool Equip(UniversalHeroCosmetic cosmetic)
        {
            ProfileTransaction.Recover();
            if(!IsOwned(cosmetic))return false;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write(EquippedKey,(int)cosmetic),
                // Old builds and legacy code still see the correct VOIDFALL state.
                new ProfileTransaction.Write(UniversalSkin.EquippedKey,cosmetic==UniversalHeroCosmetic.Voidfall?1:0));
            return true;
        }
    }

    public static class UniversalSkin
    {
        public const string Name="VOIDFALL",OwnedKey="DF_SkinOwned_Voidfall",EquippedKey="DF_Skin_Voidfall";
        public static int Price=>ShardPacks.CosmeticPrice("voidfall");
        public static bool Owned=>PlayerPrefs.GetInt(OwnedKey,0)==1||PaidShardCommerce.OwnsVoidfall;
        public static bool Equipped=>Owned&&UniversalCosmeticSelection.Equipped==UniversalHeroCosmetic.Voidfall;
        public static AvatarPurchaseResult Purchase()
        {
            ProfileTransaction.Recover();
            if(Owned)return AvatarPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(Price))return AvatarPurchaseResult.InsufficientShards;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-Price),
                new ProfileTransaction.Write(OwnedKey,1),
                new ProfileTransaction.Write(EquippedKey,1),
                new ProfileTransaction.Write(UniversalCosmeticSelection.EquippedKey,(int)UniversalHeroCosmetic.Voidfall));
            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return AvatarPurchaseResult.Purchased;
        }
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

        public static bool Owned=>PlayerPrefs.GetInt(OwnedKey,0)==1;
        public static bool Equipped=>Owned&&UniversalCosmeticSelection.Equipped==UniversalHeroCosmetic.BetaSupporterWings;

        public static AvatarPurchaseResult Purchase()
        {
            ProfileTransaction.Recover();
            if(Owned)return AvatarPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(Price))return AvatarPurchaseResult.InsufficientShards;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-Price),
                new ProfileTransaction.Write(OwnedKey,1),
                new ProfileTransaction.Write(UniversalCosmeticSelection.EquippedKey,(int)UniversalHeroCosmetic.BetaSupporterWings),
                new ProfileTransaction.Write(UniversalSkin.EquippedKey,0));
            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return AvatarPurchaseResult.Purchased;
        }

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

        public static bool Owned=>PlayerPrefs.GetInt(OwnedKey,0)==1;
        public static bool Equipped=>Owned&&UniversalCosmeticSelection.Equipped==UniversalHeroCosmetic.PlagueWings;

        public static AvatarPurchaseResult Purchase()
        {
            ProfileTransaction.Recover();
            if(Owned)return AvatarPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(Price))return AvatarPurchaseResult.InsufficientShards;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-Price),
                new ProfileTransaction.Write(OwnedKey,1),
                new ProfileTransaction.Write(UniversalCosmeticSelection.EquippedKey,(int)UniversalHeroCosmetic.PlagueWings),
                new ProfileTransaction.Write(UniversalSkin.EquippedKey,0));
            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return AvatarPurchaseResult.Purchased;
        }

        public static bool Equip(bool enabled)
        {
            if(enabled)return UniversalCosmeticSelection.Equip(UniversalHeroCosmetic.PlagueWings);
            return !Equipped||UniversalCosmeticSelection.Equip(UniversalHeroCosmetic.Original);
        }
    }

}
