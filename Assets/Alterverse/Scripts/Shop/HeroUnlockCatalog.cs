using Alterverse.Characters;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    public enum HeroUnlockResult { Purchased,AlreadyOwned,InsufficientShards,NotPurchasable,Pending }

    public static class HeroUnlockCatalog
    {
        public const int TravelerPrice=1500;
        public const int NerokPrice=2200;
        public const string TravelerOwnedKey="DF_HeroOwned_Traveler";
        public const string NerokOwnedKey="DF_HeroOwned_Nerok";

        // The Traveler is a paid shard unlock in normal play.
        public const bool TravelerFreeTestAccess=false;

        public static int Price(HeroId hero)=>hero switch
        {
            HeroId.Traveler=>TravelerPrice,
            HeroId.Nerok=>NerokPrice,
            _=>0
        };
        public static bool Purchasable(HeroId hero)=>hero==HeroId.Traveler||hero==HeroId.Nerok;
        public static string OwnedKey(HeroId hero)=>hero==HeroId.Traveler?TravelerOwnedKey:
            hero==HeroId.Nerok?NerokOwnedKey:null;
        public static string PurchaseSku(HeroId hero)=>hero==HeroId.Traveler?"hero_traveler":
            hero==HeroId.Nerok?"hero_nerok":null;
    }

    public static class HeroUnlockInventory
    {
        public static bool Owned(HeroId hero)
            =>!HeroUnlockCatalog.Purchasable(hero)||
              hero==HeroId.Traveler&&HeroUnlockCatalog.TravelerFreeTestAccess||
              Alterverse.Profile.AccountProfilePrefs.GetInt(HeroUnlockCatalog.OwnedKey(hero),0)==1;

        public static HeroUnlockResult Purchase(HeroId hero)
        {
            ProfileTransaction.Recover();
            if(!HeroUnlockCatalog.Purchasable(hero))return HeroUnlockResult.NotPurchasable;
            if(Owned(hero))return HeroUnlockResult.AlreadyOwned;
            int price=HeroUnlockCatalog.Price(hero);
            if(!PlayerWallet.CanAfford(price))return HeroUnlockResult.InsufficientShards;
            if(PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.Ready)
                return PlayerWallet.BeginServerPurchase(HeroUnlockCatalog.PurchaseSku(hero))?HeroUnlockResult.Pending:HeroUnlockResult.NotPurchasable;
            // Paid shards live on the server. Offline fallback may debit earned shards only.
            if(PlayerWallet.EarnedBalance<price)return HeroUnlockResult.NotPurchasable;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.EarnedBalance-price),
                new ProfileTransaction.Write(HeroUnlockCatalog.OwnedKey(hero),1));
            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.72f);
            return HeroUnlockResult.Purchased;
        }
    }
}
