using Alterverse.Characters;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    public enum HeroUnlockResult { Purchased,AlreadyOwned,InsufficientShards,NotPurchasable,Pending }

    public static class HeroUnlockCatalog
    {
        public const int TravelerPrice=1500;
        public const string TravelerOwnedKey="DF_HeroOwned_Traveler";

        // The Traveler is a paid shard unlock in normal play.
        public const bool TravelerFreeTestAccess=false;

        public static int Price(HeroId hero)=>hero==HeroId.Traveler?TravelerPrice:0;
        public static bool Purchasable(HeroId hero)=>hero==HeroId.Traveler;
    }

    public static class HeroUnlockInventory
    {
        public static bool Owned(HeroId hero)
            =>hero!=HeroId.Traveler||HeroUnlockCatalog.TravelerFreeTestAccess||
              Alterverse.Profile.AccountProfilePrefs.GetInt(HeroUnlockCatalog.TravelerOwnedKey,0)==1;

        public static HeroUnlockResult Purchase(HeroId hero)
        {
            ProfileTransaction.Recover();
            if(!HeroUnlockCatalog.Purchasable(hero))return HeroUnlockResult.NotPurchasable;
            if(Owned(hero))return HeroUnlockResult.AlreadyOwned;
            int price=HeroUnlockCatalog.Price(hero);
            if(!PlayerWallet.CanAfford(price))return HeroUnlockResult.InsufficientShards;
            if(PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.Ready)
                return PlayerWallet.BeginServerPurchase("hero_traveler")?HeroUnlockResult.Pending:HeroUnlockResult.NotPurchasable;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.EarnedBalance-price),
                new ProfileTransaction.Write(HeroUnlockCatalog.TravelerOwnedKey,1));
            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.72f);
            return HeroUnlockResult.Purchased;
        }
    }
}

