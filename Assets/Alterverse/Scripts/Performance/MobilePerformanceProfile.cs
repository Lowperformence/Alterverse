using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alterverse.Performance
{
    public enum MobileQuality { Battery, Balanced, Performance }

    public static class MobilePerformanceProfile
    {
        private const string Key = "DF_MobileQuality";
        private static UniversalRenderPipelineAsset runtimePipeline;
        private static int cachedFrame = -1;
        private static string cachedAccount;
        private static MobileQuality cachedQuality;

        public static MobileQuality Current
        {
            get
            {
                string account = Alterverse.Profile.AccountProfilePrefs.Account;
                if (cachedFrame != Time.frameCount || cachedAccount != account)
                {
                    cachedQuality = (MobileQuality)Mathf.Clamp(Alterverse.Profile.AccountProfilePrefs.GetInt(Key, (int)MobileQuality.Balanced), 0, 2);
                    cachedAccount = account;
                    cachedFrame = Time.frameCount;
                }
                return cachedQuality;
            }
            set
            {
                value = (MobileQuality)Mathf.Clamp((int)value, 0, 2);
                Alterverse.Profile.AccountProfilePrefs.SetInt(Key, (int)value);
                Alterverse.Profile.AccountProfilePrefs.Save();
                cachedFrame = -1;
                Apply(value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            cachedFrame = -1;
            cachedAccount = null;
            if (runtimePipeline != null)
            {
                if (QualitySettings.renderPipeline == runtimePipeline)
                    QualitySettings.renderPipeline = sourcePipeline;
                Object.Destroy(runtimePipeline);
            }
            runtimePipeline = null;
            sourcePipeline = null;
        }

        private static RenderPipelineAsset sourcePipeline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoApply() => Apply(Current);

        public static void Apply(MobileQuality quality)
        {
            // Desktop uses its own quality level; opening mobile settings must not overwrite it.
            if (!Application.isMobilePlatform) return;
            quality = (MobileQuality)Mathf.Clamp((int)quality, 0, 2);
            Application.targetFrameRate = quality == MobileQuality.Battery ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.lodBias = quality == MobileQuality.Battery ? .65f :
                quality == MobileQuality.Balanced ? .85f : 1f;
            float distance = quality == MobileQuality.Battery ? 18f :
                quality == MobileQuality.Balanced ? 28f : 38f;
            QualitySettings.shadowDistance = distance;
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsMemoryBudget = quality == MobileQuality.Battery ? 128f :
                quality == MobileQuality.Balanced ? 256f : 384f;

            // URP reads shadow distance, cascades and MSAA from its asset, not the
            // Built-in QualitySettings controls. Clone it so play mode cannot edit the asset.
            if (runtimePipeline == null)
            {
                var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (source == null) return;
                sourcePipeline = QualitySettings.renderPipeline;
                runtimePipeline = Object.Instantiate(source);
                runtimePipeline.name = source.name + " [Mobile Runtime]";
                QualitySettings.renderPipeline = runtimePipeline;
            }
            runtimePipeline.shadowDistance = distance;
            runtimePipeline.shadowCascadeCount = quality == MobileQuality.Battery ? 1 : 2;
            runtimePipeline.msaaSampleCount = quality == MobileQuality.Performance ? 2 : 1;
            // Keep HDR, render scale, grading, lighting and full texture resolution intact.
        }
    }
}


