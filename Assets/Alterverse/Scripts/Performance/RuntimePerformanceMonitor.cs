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
        }

        private void Update()
        {
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
