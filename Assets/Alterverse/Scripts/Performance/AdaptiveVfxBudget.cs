using UnityEngine;
namespace Alterverse.Performance
{
public static class AdaptiveVfxBudget
{
public static float Intensity{get{if(RuntimePerformanceMonitor.UnderBudget)return .55f;return MobilePerformanceProfile.Current==MobileQuality.Battery?.65f:MobilePerformanceProfile.Current==MobileQuality.Balanced?.82f:1f;}}
public static float SurfaceDetail=>RuntimePerformanceMonitor.UnderBudget?.4f:MobilePerformanceProfile.Current==MobileQuality.Battery?.45f:MobilePerformanceProfile.Current==MobileQuality.Balanced?.85f:1f;
public static int Count(int requested)=>Mathf.Max(1,Mathf.RoundToInt(Mathf.Max(1,requested)*Intensity));public static bool AllowDecorative=>!RuntimePerformanceMonitor.UnderBudget&&MobilePerformanceProfile.Current!=MobileQuality.Battery;
}
}
