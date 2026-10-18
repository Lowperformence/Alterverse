using UnityEngine;
namespace Alterverse.Performance
{
    public sealed class RuntimePerformanceMonitor : MonoBehaviour
    {
        public static float SmoothedFps { get; private set; }
        public static bool UnderBudget { get; private set; }
        private readonly VfxQualityHysteresis budget = new VfxQualityHysteresis();
        private float elapsed;
        private int frames, sampleTarget;
        private readonly FrameTimingWindow serverFrames=new();
        private int serverGcCollections;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState() { SmoothedFps = 0f; UnderBudget = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<RuntimePerformanceMonitor>() == null)
                new GameObject("DF_PerformanceMonitor").AddComponent<RuntimePerformanceMonitor>();
        }

        private void OnEnable() => ResetSamples();
        private void OnDisable() => ResetSamples();
        private void OnApplicationPause(bool paused) => ResetSamples();
        private void OnApplicationFocus(bool focused) => ResetSamples();

        private void ResetSamples()
        {
            elapsed = 0f; frames = 0; sampleTarget = 0;
            budget.Reset(); UnderBudget = false; SmoothedFps = 0f;
            serverFrames.Reset();serverGcCollections=System.GC.CollectionCount(0);
        }

        private void Update()
        {
            // Headless processes need no window focus. Keep their wall-clock frame data
            // separate from the mobile VFX quality controller, including during countdowns.
            if(Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Requested)
            {
                if(serverFrames.Observe(Time.unscaledDeltaTime))
                {
                    SmoothedFps=serverFrames.Fps;
                    int collections=System.GC.CollectionCount(0);
                    Debug.Log($"DF SERVER PERF: fps={serverFrames.Fps:F1} p95_ms={serverFrames.P95Milliseconds:F1} max_ms={serverFrames.MaxMilliseconds:F1} gc0={collections-serverGcCollections} timeScale={Time.timeScale:F2}");
                    serverGcCollections=collections;
                }
                return;
            }
            if (Time.timeScale <= 0f || !Application.isFocused) return;
            int target = Application.isMobilePlatform
                ? (MobilePerformanceProfile.Current == MobileQuality.Battery ? 30 : 60)
                : (Application.targetFrameRate > 0 ? Application.targetFrameRate : 60);
            if (sampleTarget != target) { ResetSamples(); sampleTarget = target; }
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < 1f) return;
            SmoothedFps = frames / Mathf.Max(.001f, elapsed);
            budget.Observe(SmoothedFps, elapsed, target);
            UnderBudget = budget.UnderBudget;
            elapsed = 0f; frames = 0;
        }
    }
}
