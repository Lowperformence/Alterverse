using Alterverse.Match;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Shop
{
    public readonly struct ArenaShopItem
    {
        public readonly ArenaVariant Variant;
        public readonly string Name;
        public readonly string Description;
        public readonly int Price;

        public ArenaShopItem(ArenaVariant variant,string name,string description,int price)
        {
            Variant=variant;
            Name=name;
            Description=description;
            Price=price;
        }
    }

    public enum ArenaPurchaseResult
    {
        Purchased,
        AlreadyOwned,
        InsufficientShards,
        NotForSale
    }

    public static class ArenaUnlockInventory
    {
        public const string NoxaraOwnedKey="DF_Arena_Noxara_Owned";
        public const string EarthOwnedKey="DF_Arena_Earth_Owned";
        public const string Dark00OwnedKey="DF_Arena_Dark00_Owned";

        public static bool Owned(ArenaVariant variant)
        {
            return variant switch
            {
                ArenaVariant.Noxara=>PlayerPrefs.GetInt(NoxaraOwnedKey,0)!=0,
                ArenaVariant.Earth=>PlayerPrefs.GetInt(EarthOwnedKey,0)!=0,
                ArenaVariant.Dark00=>PlayerPrefs.GetInt(Dark00OwnedKey,0)!=0,
                _=>true
            };
        }

        public static ArenaPurchaseResult Purchase(ArenaVariant variant)
        {
            if(Owned(variant))return ArenaPurchaseResult.AlreadyOwned;

            ArenaShopItem item=ArenaShopCatalog.For(variant);
            if(item.Price<=0)return ArenaPurchaseResult.NotForSale;
            if(!PlayerWallet.TrySpend(item.Price))return ArenaPurchaseResult.InsufficientShards;

            if(variant==ArenaVariant.Noxara)PlayerPrefs.SetInt(NoxaraOwnedKey,1);
            else if(variant==ArenaVariant.Earth)PlayerPrefs.SetInt(EarthOwnedKey,1);
            else if(variant==ArenaVariant.Dark00)PlayerPrefs.SetInt(Dark00OwnedKey,1);
            else return ArenaPurchaseResult.NotForSale;

            PlayerPrefs.Save();
            return ArenaPurchaseResult.Purchased;
        }

        public static string UnlockRequirement(ArenaVariant variant)
        {
            if(Owned(variant))return "OWNED";
            ArenaShopItem item=ArenaShopCatalog.For(variant);
            return item.Price>0?$"{item.Price} SHARDS":"FREE";
        }
    }

    public static class ArenaShopCatalog
    {
        public const int NoxaraPrice=1000;
        public const int EarthPrice=500;
        public const int Dark00Price=1800;

        public static readonly ArenaShopItem[] Items=
        {
            new ArenaShopItem(ArenaVariant.Moon,"Lunar Prime","Launch arena included for every Traveler.",0),
            new ArenaShopItem(ArenaVariant.Noxara,"Noxara","Void jungle battlefield. Unlock permanently with shards.",NoxaraPrice),
            new ArenaShopItem(ArenaVariant.Earth,"Earth","Grounded forest battlefield. Unlock permanently with shards.",EarthPrice),
            new ArenaShopItem(ArenaVariant.Mars,"Mars","Red frontier battlefield.",0),
            new ArenaShopItem(ArenaVariant.Neptune,"Neptune","Deep-ocean battlefield.",0),
            new ArenaShopItem(ArenaVariant.Pyraxis,"Pyraxis","Volcanic battlefield.",0),
            new ArenaShopItem(ArenaVariant.Dark00,"Dark 00","Axis-aligned cosmic-space arena: void floor, magenta energy seams and starfield depth. Unlock permanently with shards.",Dark00Price),
        };

        public static ArenaShopItem For(ArenaVariant variant)
        {
            for(int i=0;i<Items.Length;i++)if(Items[i].Variant==variant)return Items[i];
            return Items[0];
        }
    }
}
