using UnityEngine;
namespace Alterverse.Performance
{
public static class PerformanceSafety
{
public static bool AllowExpensiveDecoration=>AdaptiveVfxBudget.AllowDecorative;public static int VfxCount(int ideal,int minimum=1)=>Mathf.Max(minimum,AdaptiveVfxBudget.Count(ideal));public static float EffectLifetime(float ideal)=>RuntimePerformanceMonitor.UnderBudget?ideal*.72f:ideal;public static float AnimationRate(float ideal)=>MobilePerformanceProfile.Current==MobileQuality.Battery?ideal*.8f:ideal;
}
}
