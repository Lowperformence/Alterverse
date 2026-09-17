using UnityEngine;
namespace Alterverse.Performance
{
public sealed class RuntimePerformanceMonitor:MonoBehaviour
{
public static float SmoothedFps{get;private set;}public static bool UnderBudget{get;private set;}private float elapsed;private int frames;[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoCreate(){if(FindAnyObjectByType<RuntimePerformanceMonitor>()==null)new GameObject("DF_PerformanceMonitor").AddComponent<RuntimePerformanceMonitor>();}private void Update(){elapsed+=Time.unscaledDeltaTime;frames++;if(elapsed<1f)return;SmoothedFps=frames/Mathf.Max(.001f,elapsed);int target=MobilePerformanceProfile.Current==MobileQuality.Battery?30:60;UnderBudget=SmoothedFps<target*.82f;elapsed=0;frames=0;}
}
}
