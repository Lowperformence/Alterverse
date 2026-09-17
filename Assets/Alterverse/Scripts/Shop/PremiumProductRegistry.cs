namespace Alterverse.Shop
{
public enum PremiumProduct{FirstFracture,RealityShatterRecall}
public static class PremiumProductRegistry
{
public static string Id(PremiumProduct product)=>product==PremiumProduct.RealityShatterRecall?RecallCosmeticCatalog.ProductId:FounderPackCatalog.ProductId;public static string FallbackPrice(PremiumProduct product)=>product==PremiumProduct.RealityShatterRecall?RecallCosmeticCatalog.TargetPrice:FounderPackCatalog.TargetPrice;public static string LocalizedPrice(PremiumProduct product)=>StorePurchaseGateway.Price(Id(product),FallbackPrice(product));public static bool Owned(PremiumProduct product)=>product==PremiumProduct.RealityShatterRecall?RecallCosmeticEntitlement.Owned:FounderEntitlement.Owned;
}
}
