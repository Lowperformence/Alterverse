using UnityEngine;
namespace Alterverse.Shop
{
    [System.Serializable] public sealed class ShardPack
    {
        public string id,name,basePlanId;public int amount;
        public string Id=>id;public string Name=>name;public int Amount=>amount;
    }
    public static class ShardPacks
    {
        [System.Serializable] sealed class Cosmetic {public string id;public int price;}
        [System.Serializable] sealed class Catalog {public ShardPack[] packs;public Cosmetic[] cosmetics;public ShardPack subscription;}
        static Catalog catalog;
        public static readonly ShardPack[] All=Load();
        static ShardPack[] Load(){var json=Resources.Load<TextAsset>("Commerce/ShardCatalog");catalog=json!=null?JsonUtility.FromJson<Catalog>(json.text):null;return catalog?.packs??System.Array.Empty<ShardPack>();}
        public static ShardPack Subscription=>catalog?.subscription;
        public static bool IsSubscription(string id)=>Subscription!=null&&Subscription.Id==id;
        public static bool IsProduct(string id)=>Contains(id)||IsSubscription(id);
        public static int CosmeticPrice(string id){if(catalog?.cosmetics!=null)foreach(var c in catalog.cosmetics)if(c.id==id)return c.price;return -1;}
        public static bool Contains(string id){foreach(var p in All)if(p.Id==id)return true;return false;}
    }
}

