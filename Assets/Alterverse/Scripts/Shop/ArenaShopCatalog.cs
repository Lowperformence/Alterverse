using Alterverse.Match;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    public readonly struct ArenaShopItem
    {
        public readonly ArenaVariant Variant;
        public readonly string Name;
        public readonly string Subtitle;
        public readonly int Price;
        public readonly Color Accent;

        public ArenaShopItem(ArenaVariant variant,string name,string subtitle,int price,Color accent)
        {
            Variant=variant;Name=name;Subtitle=subtitle;Price=price;Accent=accent;
        }
    }

    public enum ArenaPurchaseResult
    {
        Purchased,
        AlreadyOwned,
        InsufficientShards,
        NotPurchasable
    }

    /// <summary>Content unlock catalog. Arena ownership never changes gameplay values.</summary>
    public static class ArenaShopCatalog
    {
        public const int PremiumArenaPrice=500;
        public static readonly ArenaShopItem[] Items=
        {
            new(ArenaVariant.Moon,"MOON","ORIGINAL BATTLEFIELD",0,new Color(.48f,.58f,.72f)),
            new(ArenaVariant.Mars,"MARS","RED PLANET ARENA",PremiumArenaPrice,new Color(.88f,.28f,.12f)),
            new(ArenaVariant.GreenPlanet,"GREEN PLANET","ALIEN BATTLEFIELD",PremiumArenaPrice,new Color(.20f,.78f,.38f))
        };

        public static ArenaShopItem For(ArenaVariant variant)=>Items[Mathf.Clamp((int)variant,0,Items.Length-1)];
    }

    public static class ArenaUnlockInventory
    {
        internal const string MarsOwnedKey="DF_ArenaOwned_Mars";
        internal const string GreenOwnedKey="DF_ArenaOwned_GreenPlanet";

        private static string Key(ArenaVariant variant)=>variant switch
        {
            ArenaVariant.Mars=>MarsOwnedKey,
            ArenaVariant.GreenPlanet=>GreenOwnedKey,
            _=>string.Empty
        };

        public static bool Owned(ArenaVariant variant)
        {
            if(variant==ArenaVariant.Moon)return true;
            string key=Key(variant);
            return !string.IsNullOrEmpty(key)&&PlayerPrefs.GetInt(key,0)==1;
        }

        public static ArenaPurchaseResult Purchase(ArenaVariant variant)
        {
            ProfileTransaction.Recover();
            if(variant==ArenaVariant.Moon)return ArenaPurchaseResult.AlreadyOwned;
            string key=Key(variant);
            if(string.IsNullOrEmpty(key))return ArenaPurchaseResult.NotPurchasable;
            if(Owned(variant))return ArenaPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(ArenaShopCatalog.PremiumArenaPrice))return ArenaPurchaseResult.InsufficientShards;

            // Wallet debit and ownership are journaled as one idempotent profile transaction.
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-ArenaShopCatalog.PremiumArenaPrice),
                new ProfileTransaction.Write(key,1));

            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return ArenaPurchaseResult.Purchased;
        }
    }
}
