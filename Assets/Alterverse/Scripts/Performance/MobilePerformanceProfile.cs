using System;
using System.Reflection;
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
        private static RenderPipelineAsset sourcePipeline;
        private static int cachedFrame = -1;
        private static string cachedAccount;
        private static MobileQuality cachedQuality;
        private static bool hardwareDetected;
        private static MobileQuality recommendedQuality = MobileQuality.Balanced;
        private static string hardwareSummary = string.Empty;
        private static bool autoModeEnabled = true;

        public static MobileQuality RecommendedQuality
        {
            get
            {
                EnsureHardwareDetected();
                return recommendedQuality;
            }
        }

        public static string HardwareSummary
        {
            get
            {
                EnsureHardwareDetected();
                return hardwareSummary;
            }
        }

        public static bool AutoModeEnabled
        {
            get
            {
                EnsureHardwareDetected();
                return autoModeEnabled;
            }
            set
            {
                EnsureHardwareDetected();
                autoModeEnabled = value;
                if (value)
                    Current = recommendedQuality;
            }
        }

        public static MobileQuality Current
        {
            get
            {
                EnsureHardwareDetected();
                string account = Alterverse.Profile.AccountProfilePrefs.Account;
                if (cachedFrame != Time.frameCount || cachedAccount != account)
                {
                    cachedQuality = (MobileQuality)Mathf.Clamp(
                        Alterverse.Profile.AccountProfilePrefs.GetInt(Key, (int)recommendedQuality), 0, 2);
                    cachedAccount = account;
                    cachedFrame = Time.frameCount;
                }
                return cachedQuality;
            }
            set
            {
                value = (MobileQuality)Mathf.Clamp((int)value, 0, 2);
                autoModeEnabled = value == recommendedQuality && autoModeEnabled;
                Alterverse.Profile.AccountProfilePrefs.SetInt(Key, (int)value);
                Alterverse.Profile.AccountProfilePrefs.Save();
                cachedFrame = -1;
                Apply(value);
            }
        }

        public static void EnableAutoHardwareProfile()
        {
            EnsureHardwareDetected();
            autoModeEnabled = true;
            Alterverse.Profile.AccountProfilePrefs.SetInt(Key, (int)recommendedQuality);
            Alterverse.Profile.AccountProfilePrefs.Save();
            cachedFrame = -1;
            Apply(recommendedQuality);
        }

        private static int ReadSystemInt(Type sysInfo, string prop, int fallback)
        {
            var p = sysInfo?.GetProperty(prop, BindingFlags.Public | BindingFlags.Static);
            return p != null && p.PropertyType == typeof(int) ? (int)p.GetValue(null) : fallback;
        }

        private static string ReadSystemString(Type sysInfo, string prop)
        {
            var p = sysInfo?.GetProperty(prop, BindingFlags.Public | BindingFlags.Static);
            return p != null && p.PropertyType == typeof(string) ? ((string)p.GetValue(null) ?? string.Empty) : string.Empty;
        }

        private static void SetStaticProperty(Type target, string prop, object value)
        {
            try
            {
                var p = target?.GetProperty(prop, BindingFlags.Public | BindingFlags.Static);
                if (p != null && p.CanWrite)
                {
                    if (p.PropertyType.IsEnum && value is int i)
                        p.SetValue(null, Enum.ToObject(p.PropertyType, i));
                    else
                        p.SetValue(null, value);
                    return;
                }
                var f = target?.GetField(prop, BindingFlags.Public | BindingFlags.Static);
                if (f != null)
                {
                    if (f.FieldType.IsEnum && value is int i)
                        f.SetValue(null, Enum.ToObject(f.FieldType, i));
                    else
                        f.SetValue(null, value);
                }
            }
            catch { }
        }

        private static void SetInstanceProperty(object instance, string prop, object value)
        {
            if (instance == null) return;
            try
            {
                var t = instance.GetType();
                var p = t.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanWrite) { p.SetValue(instance, value); return; }
                var f = t.GetField(prop, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) f.SetValue(instance, value);
            }
            catch { }
        }

        private static bool IsUnderBudget()
        {
            var monitorType = Type.GetType("Alterverse.Performance.RuntimePerformanceMonitor");
            var prop = monitorType?.GetProperty("UnderBudget", BindingFlags.Public | BindingFlags.Static);
            return prop != null && prop.PropertyType == typeof(bool) && (bool)prop.GetValue(null);
        }

        private static void EnsureHardwareDetected()
        {
            if (hardwareDetected) return;
            hardwareDetected = true;

            Type sysInfo = Type.GetType("UnityEngine.SystemInfo, UnityEngine.CoreModule") ??
                           Type.GetType("UnityEngine.SystemInfo");
            if (sysInfo == null)
            {
                recommendedQuality = MobileQuality.Balanced;
                hardwareSummary = "Adaptive Mobile Profile → BALANCED";
                return;
            }

            int ramMb = Mathf.Max(0, ReadSystemInt(sysInfo, "systemMemorySize", 0));
            int vramMb = Mathf.Max(0, ReadSystemInt(sysInfo, "graphicsMemorySize", 0));
            int cores = Mathf.Max(1, ReadSystemInt(sysInfo, "processorCount", 4));
            int freqMhz = Mathf.Max(0, ReadSystemInt(sysInfo, "processorFrequency", 0));
            string gpu = ReadSystemString(sysInfo, "graphicsDeviceName").Trim();
            string gpuUpper = gpu.ToUpperInvariant();

            bool lowGpu =
                gpuUpper.Contains("MALI-G31") ||
                gpuUpper.Contains("MALI-G51") ||
                gpuUpper.Contains("MALI-G52") ||
                gpuUpper.Contains("MALI-G57") ||
                gpuUpper.Contains("MALI-G68") ||
                gpuUpper.Contains("MALI-G71") ||
                gpuUpper.Contains("MALI-G72") ||
                gpuUpper.Contains("MALI-G76") ||
                gpuUpper.Contains("MALI-T") ||
                gpuUpper.Contains("MALI-4") ||
                gpuUpper.Contains("POWERVR") ||
                gpuUpper.Contains("IMG GE") ||
                gpuUpper.Contains("ADRENO (TM) 3") ||
                gpuUpper.Contains("ADRENO (TM) 4") ||
                gpuUpper.Contains("ADRENO (TM) 5") ||
                gpuUpper.Contains("ADRENO (TM) 61") ||
                gpuUpper.Contains("ADRENO (TM) 62") ||
                gpuUpper.Contains("ADRENO 5") ||
                gpuUpper.Contains("ADRENO 61") ||
                gpuUpper.Contains("ADRENO 62") ||
                gpuUpper.Contains("XCLIPSE 5");

            bool highGpu =
                gpuUpper.Contains("ADRENO (TM) 73") ||
                gpuUpper.Contains("ADRENO (TM) 74") ||
                gpuUpper.Contains("ADRENO (TM) 75") ||
                gpuUpper.Contains("ADRENO (TM) 8") ||
                gpuUpper.Contains("ADRENO 73") ||
                gpuUpper.Contains("ADRENO 74") ||
                gpuUpper.Contains("ADRENO 75") ||
                gpuUpper.Contains("ADRENO 8") ||
                gpuUpper.Contains("MALI-G710") ||
                gpuUpper.Contains("MALI-G715") ||
                gpuUpper.Contains("MALI-G720") ||
                gpuUpper.Contains("IMMORTALIS") ||
                gpuUpper.Contains("APPLE A15") ||
                gpuUpper.Contains("APPLE A16") ||
                gpuUpper.Contains("APPLE A17") ||
                gpuUpper.Contains("APPLE A18") ||
                gpuUpper.Contains("APPLE M");

            if (lowGpu ||
                (ramMb > 0 && ramMb <= 4600) ||
                (vramMb > 0 && vramMb <= 1536) ||
                cores <= 4 ||
                (freqMhz > 0 && freqMhz < 2050))
            {
                recommendedQuality = MobileQuality.Battery;
            }
            else if (highGpu && ramMb >= 7200 && cores >= 6 && (freqMhz == 0 || freqMhz >= 2500))
            {
                recommendedQuality = MobileQuality.Performance;
            }
            else
            {
                recommendedQuality = MobileQuality.Balanced;
            }

            string tierName = recommendedQuality == MobileQuality.Battery ? "LOW / SAVER"
                : recommendedQuality == MobileQuality.Balanced ? "BALANCED" : "HIGH";
            string shortGpu = string.IsNullOrEmpty(gpu) ? "Mobile GPU" : (gpu.Length > 24 ? gpu.Substring(0, 24) : gpu);
            float ramGb = ramMb > 0 ? ramMb / 1024f : 0f;
            hardwareSummary = ramGb > 0f
                ? $"{shortGpu} • {ramGb:0.0} GB RAM • {cores} Cores → {tierName}"
                : $"{shortGpu} • {cores} Cores → {tierName}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            cachedFrame = -1;
            cachedAccount = null;
            hardwareDetected = false;
            autoModeEnabled = true;
            if (runtimePipeline != null)
            {
                if (QualitySettings.renderPipeline == runtimePipeline)
                    QualitySettings.renderPipeline = sourcePipeline;
                UnityEngine.Object.Destroy(runtimePipeline);
            }
            runtimePipeline = null;
            sourcePipeline = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoApply()
        {
            EnsureHardwareDetected();
            Apply(Current);
        }

        public static void Apply(MobileQuality quality)
        {
            // Desktop uses its own quality level; opening mobile settings must not overwrite it.
            if (!Application.isMobilePlatform) return;
            quality = (MobileQuality)Mathf.Clamp((int)quality, 0, 2);
            bool underBudget = IsUnderBudget();

            Application.targetFrameRate = quality == MobileQuality.Battery ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.lodBias = quality == MobileQuality.Battery ? .55f :
                quality == MobileQuality.Balanced ? .82f : 1f;
            float distance = quality == MobileQuality.Battery ? 0f :
                quality == MobileQuality.Balanced ? (underBudget ? 18f : 26f) : (underBudget ? 28f : 38f);
            QualitySettings.shadowDistance = distance;
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsMemoryBudget = quality == MobileQuality.Battery ? 96f :
                quality == MobileQuality.Balanced ? 192f : 384f;

            // Apply extra QualitySettings for older phones via reflection so lightweight test stubs still compile.
            Type qsType = typeof(QualitySettings);
            SetStaticProperty(qsType, "shadows", quality == MobileQuality.Battery ? 0 : 1);
            SetStaticProperty(qsType, "globalTextureMipmapLimit", quality == MobileQuality.Battery ? 1 : 0);
            SetStaticProperty(qsType, "skinWeights", quality == MobileQuality.Battery ? 2 : 4);
            SetStaticProperty(qsType, "anisotropicFiltering", quality == MobileQuality.Battery ? 0 : 1);
            SetStaticProperty(qsType, "softParticles", false);
            SetStaticProperty(qsType, "realtimeReflectionProbes", false);
            SetStaticProperty(qsType, "particleRaycastBudget", quality == MobileQuality.Battery ? 16 :
                quality == MobileQuality.Balanced ? 64 : 256);

            // URP reads shadow distance, cascades, HDR, renderScale and MSAA from its asset.
            if (runtimePipeline == null)
            {
                var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (source == null) return;
                sourcePipeline = QualitySettings.renderPipeline;
                runtimePipeline = UnityEngine.Object.Instantiate(source);
                runtimePipeline.name = source.name + " [Mobile Runtime]";
                QualitySettings.renderPipeline = runtimePipeline;
            }
            runtimePipeline.shadowDistance = distance;
            runtimePipeline.shadowCascadeCount = quality == MobileQuality.Battery ? 1 : 2;
            runtimePipeline.msaaSampleCount = quality == MobileQuality.Performance && !underBudget ? 2 : 1;

            float baseScale = quality == MobileQuality.Battery ? .65f :
                quality == MobileQuality.Balanced ? .82f : 1f;
            if (underBudget)
                baseScale = Mathf.Max(.55f, baseScale - .10f);
            SetInstanceProperty(runtimePipeline, "renderScale", baseScale);
            SetInstanceProperty(runtimePipeline, "supportsHDR", quality == MobileQuality.Performance && !underBudget);
        }
    }
}



