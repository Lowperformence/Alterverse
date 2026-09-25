using Alterverse.Profile;
namespace Alterverse.Shop
{
    public static class ShopReadinessReport
    {
        public static bool SoftCurrencyReady=>ShopIntegrityChecks.AvatarCatalogValid();
        public static bool PremiumBillingReady
        {
            get
            {
                GooglePlayShardStore store=GooglePlayShardStore.Instance;
                if(store==null||ShardPacks.All.Length==0)return false;
                foreach(ShardPack pack in ShardPacks.All)
                    if(pack==null||string.IsNullOrWhiteSpace(pack.Id)||pack.Amount<=0||!store.CanBuy(pack.Id))return false;
                return true;
            }
        }
        public static string AvatarStatus=>SoftCurrencyReady?"SHARDS FLOW READY FOR LOCAL TEST":"SHARDS DATA INVALID";
        public static string PremiumStatus=>PremiumBillingReady?"GOOGLE PLAY SHARD PACKS READY":"GOOGLE PLAY SHARD PACKS UNAVAILABLE";
        public static string FounderGate=>AvatarInventory.IsOwned(AvatarInventory.FounderAvatarId)?"FOUNDER ENTITLED":"FOUNDER LOCKED";
    }
}
