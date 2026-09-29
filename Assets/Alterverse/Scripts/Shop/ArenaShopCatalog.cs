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

    /// <summary>All arenas are free except Noxara, which is a shard unlock.</summary>
    public static class ArenaShopCatalog
    {
        public const int NoxaraPrice=1000;
        public static readonly ArenaShopItem[] Items=
        {
            new(ArenaVariant.Moon,"MOON","ORIGINAL BATTLEFIELD",0,new Color(.48f,.58f,.72f)),
            new(ArenaVariant.Mars,"MARS","RED PLANET ARENA",0,new Color(.88f,.28f,.12f)),
            new(ArenaVariant.GreenPlanet,"GREEN PLANET","ALIEN BATTLEFIELD",0,new Color(.20f,.78f,.38f)),
            new(ArenaVariant.Noxara,"NOXARA","DEEP BLUE FRONT",NoxaraPrice,new Color(.16f,.36f,.92f)),
            new(ArenaVariant.Kryon,"KRYON","FROZEN FRONT",0,new Color(.52f,.90f,1f)),
            new(ArenaVariant.Earth,"EARTH","LIVING OVERGROWN FRONT",0,new Color(.24f,.68f,.31f)),
            new(ArenaVariant.Pyraxis,"PYRAXIS","VOLCANIC LAVA FRONT",0,new Color(1f,.34f,.04f)),
            new(ArenaVariant.Neptune,"NEPTUNE","ABYSSAL BLUE FRONT",0,new Color(.05f,.25f,.72f))
        };

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
        internal const string NoxaraOwnedKey="DF_ArenaOwned_Noxara";

        public static bool Owned(ArenaVariant variant)
        {
            if(variant!=ArenaVariant.Noxara)return true;
            return PlayerPrefs.GetInt(NoxaraOwnedKey,0)==1;
        }

        public static string UnlockRequirement(ArenaVariant variant)=>variant==ArenaVariant.Noxara
            ? ArenaShopCatalog.NoxaraPrice+" SHARDS"
            : "FREE";

        public static ArenaPurchaseResult Purchase(ArenaVariant variant)
        {
            ProfileTransaction.Recover();
            if(variant!=ArenaVariant.Noxara)return ArenaPurchaseResult.AlreadyOwned;
            if(Owned(variant))return ArenaPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(ArenaShopCatalog.NoxaraPrice))return ArenaPurchaseResult.InsufficientShards;

            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-ArenaShopCatalog.NoxaraPrice),
                new ProfileTransaction.Write(NoxaraOwnedKey,1));

            Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            return ArenaPurchaseResult.Purchased;
        }
    }
}
