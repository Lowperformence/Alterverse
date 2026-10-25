using Alterverse.Characters;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    public enum HeroUnlockResult { Purchased,AlreadyOwned,InsufficientShards,NotPurchasable,Pending }

    public static class HeroUnlockCatalog
    {
        public const int TravelerPrice=1500;
        public const int NerokPrice=1900;
        public const int TimmyPrice=1900;
        public const int VoidKnightPrice=1600;
        public const string TravelerOwnedKey="DF_HeroOwned_Traveler";
        public const string NerokOwnedKey="DF_HeroOwned_Nerok";
        public const string TimmyOwnedKey="DF_HeroOwned_Timmy";
        public const string VoidKnightOwnedKey="DF_HeroOwned_VoidKnight";
        // Listed for preview, but purchase and deployment remain blocked until release.
        public const bool TimmyReleased=false;
        public const bool VoidKnightReleased=false;
        public static bool Released(HeroId hero)=>hero==HeroId.VoidKnight?VoidKnightReleased:hero!=HeroId.Timmy||TimmyReleased;

        // The Traveler is a paid shard unlock in normal play.
        public const bool TravelerFreeTestAccess=false;

        public static int Price(HeroId hero)=>hero switch
        {
            HeroId.Traveler=>TravelerPrice,
            HeroId.Nerok=>NerokPrice,
            HeroId.Timmy=>TimmyPrice,
            HeroId.VoidKnight=>VoidKnightPrice,
            _=>0
        };
        public static bool Purchasable(HeroId hero)=>hero==HeroId.Traveler||hero==HeroId.Nerok||hero==HeroId.Timmy||hero==HeroId.VoidKnight;
        public static string OwnedKey(HeroId hero)=>hero==HeroId.Traveler?TravelerOwnedKey:
            hero==HeroId.Nerok?NerokOwnedKey:hero==HeroId.Timmy?TimmyOwnedKey:hero==HeroId.VoidKnight?VoidKnightOwnedKey:null;
        public static string PurchaseSku(HeroId hero)=>hero==HeroId.Traveler?"hero_traveler":
            hero==HeroId.Nerok?"hero_nerok":hero==HeroId.Timmy?"hero_timmy":hero==HeroId.VoidKnight?"hero_void_knight":null;
    }

    public static class HeroUnlockInventory
    {
        public static bool Owned(HeroId hero)
            =>HeroUnlockCatalog.Released(hero)&&(!HeroUnlockCatalog.Purchasable(hero)||
              hero==HeroId.Traveler&&HeroUnlockCatalog.TravelerFreeTestAccess||
              Alterverse.Profile.AccountProfilePrefs.GetInt(HeroUnlockCatalog.OwnedKey(hero),0)==1);

        public static HeroUnlockResult Purchase(HeroId hero)
        {
            if(!HeroUnlockCatalog.Released(hero))return HeroUnlockResult.NotPurchasable;
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

