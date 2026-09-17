using Alterverse.Performance;
namespace Alterverse.Audio
{
public static class AudioStressBudget
{
public static int RecommendedVoices=>MobilePerformanceProfile.Current==MobileQuality.Battery?5:MobilePerformanceProfile.Current==MobileQuality.Balanced?7:8;public static float DecorativeGain=>RuntimePerformanceMonitor.UnderBudget?.55f:MobilePerformanceProfile.Current==MobileQuality.Battery?.7f:1f;public static bool AllowAmbientLayer=>!RuntimePerformanceMonitor.UnderBudget&&MobilePerformanceProfile.Current!=MobileQuality.Battery;
}
}
