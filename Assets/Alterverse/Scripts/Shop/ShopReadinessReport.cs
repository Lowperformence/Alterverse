using Alterverse.Profile;
namespace Alterverse.Shop
{
public static class ShopReadinessReport
{
public static bool SoftCurrencyReady=>ShopIntegrityChecks.AvatarCatalogValid();public static bool PremiumBillingReady=>false;public static string AvatarStatus=>SoftCurrencyReady?"SHARDS FLOW READY FOR LOCAL TEST":"SHARDS DATA INVALID";public const string PremiumStatus="PREMIUM BILLING NOT CONNECTED";public static string FounderGate=>AvatarInventory.IsOwned(AvatarInventory.FounderAvatarId)?"FOUNDER ENTITLED":"FOUNDER LOCKED";
}
}
