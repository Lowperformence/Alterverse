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
        NotPurchasable,
        Pending
    }

    /// <summary>Premium arenas are persistent shard unlocks; zero-price arenas remain immediately available.</summary>
    public static class ArenaShopCatalog
    {
        public const int MoorPrice=900;
        public const int PyraxisPrice=900;
        public const int PsrPrice=900;
        public const int NoxaraPrice=900;
        public const int EarthPrice=900;
        public const int Dark00Price=900;
        public static readonly ArenaShopItem[] Items=
        {
            new(ArenaVariant.Moon,"MOON","ORIGINAL BATTLEFIELD",0,new Color(.48f,.58f,.72f)),
            new(ArenaVariant.Mars,"MARS","RED PLANET ARENA",0,new Color(.88f,.28f,.12f)),
            new(ArenaVariant.GreenPlanet,"MOOR12C","ALIEN BATTLEFIELD",MoorPrice,new Color(.20f,.78f,.38f)),
            new(ArenaVariant.Noxara,"NOXARA","DEEP BLUE FRONT",NoxaraPrice,new Color(.16f,.36f,.92f)),
            new(ArenaVariant.Kryon,"KRYON","FROZEN FRONT",0,new Color(.52f,.90f,1f)),
            new(ArenaVariant.Earth,"EARTH","LIVING OVERGROWN FRONT",EarthPrice,new Color(.24f,.68f,.31f)),
            new(ArenaVariant.Pyraxis,"PYRAXIS","VOLCANIC LAVA FRONT",PyraxisPrice,new Color(1f,.34f,.04f)),
            new(ArenaVariant.Neptune,"NEPTUNE","ABYSSAL BLUE FRONT",0,new Color(.05f,.25f,.72f)),
            new(ArenaVariant.Dark00,"DARK 00","BLACKOUT INDUSTRIAL FRONT",Dark00Price,new Color(.92f,.08f,.08f)),
            new(ArenaVariant.DiamondCore,"PSR J1719-1438 b","SHATTERED CRYSTAL WORLD",PsrPrice,new Color(.62f,.90f,1f)),
            new(ArenaVariant.GJ504b,"GJ 504 b","MAGENTA SANDSTONE FRONT",0,new Color(.90f,.16f,.58f))
        };

        // The PLAY menu needs every arena, while the STORE must only list arenas that
        // actually require a purchase. Keeping this as a derived list means future
        // zero-price maps are automatically excluded and future paid maps are included.
        public static readonly ArenaShopItem[] PurchasableItems=BuildPurchasableItems();

        private static ArenaShopItem[] BuildPurchasableItems()
        {
            int count=0;
            for(int i=0;i<Items.Length;i++)if(Items[i].Price>0)count++;

            ArenaShopItem[] result=new ArenaShopItem[count];
            int write=0;
            for(int i=0;i<Items.Length;i++)
                if(Items[i].Price>0)result[write++]=Items[i];
            return result;
        }

        public static ArenaShopItem For(ArenaVariant variant)
        {
            for(int i=0;i<Items.Length;i++)if(Items[i].Variant==variant)return Items[i];
            return Items.Length>0?Items[0]:default;
        }

        public static int IndexOf(ArenaVariant variant)
        {
            for(int i=0;i<Items.Length;i++)if(Items[i].Variant==variant)return i;
            return 0;
        }
    }

    public static class ArenaUnlockInventory
    {
        internal const string MoorOwnedKey="DF_ArenaOwned_GreenPlanet";
        internal const string PyraxisOwnedKey="DF_ArenaOwned_Pyraxis";
        internal const string PsrOwnedKey="DF_ArenaOwned_DiamondCore";
        internal const string NoxaraOwnedKey="DF_ArenaOwned_Noxara";
        internal const string EarthOwnedKey="DF_ArenaOwned_Earth";
        internal const string Dark00OwnedKey="DF_ArenaOwned_Dark00";

        private static string OwnedKey(ArenaVariant variant)
        {
            if(variant==ArenaVariant.GreenPlanet)return MoorOwnedKey;
            if(variant==ArenaVariant.Pyraxis)return PyraxisOwnedKey;
            if(variant==ArenaVariant.DiamondCore)return PsrOwnedKey;
            if(variant==ArenaVariant.Noxara)return NoxaraOwnedKey;
            if(variant==ArenaVariant.Earth)return EarthOwnedKey;
            if(variant==ArenaVariant.Dark00)return Dark00OwnedKey;
            return null;
        }

        public static bool Owned(ArenaVariant variant)
        {
            string key=OwnedKey(variant);
            return string.IsNullOrEmpty(key)||Alterverse.Profile.AccountProfilePrefs.GetInt(key,0)==1;
        }

        public static string UnlockRequirement(ArenaVariant variant)
        {
            int price=ArenaShopCatalog.For(variant).Price;
            return price>0?price+" SHARDS":"FREE";
        }

        public static ArenaPurchaseResult Purchase(ArenaVariant variant)
        {
            ProfileTransaction.Recover();
            string ownedKey=OwnedKey(variant);
            int price=ArenaShopCatalog.For(variant).Price;
            if(price<=0||string.IsNullOrEmpty(ownedKey))return ArenaPurchaseResult.AlreadyOwned;
            if(Owned(variant))return ArenaPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(price))return ArenaPurchaseResult.InsufficientShards;

            if(PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.Ready)
                return PlayerWallet.BeginServerPurchase("arena_"+variant)?ArenaPurchaseResult.Pending:ArenaPurchaseResult.NotPurchasable;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.EarnedBalance-price),
                new ProfileTransaction.Write(ownedKey,1));

            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return ArenaPurchaseResult.Purchased;
        }
    }
}


