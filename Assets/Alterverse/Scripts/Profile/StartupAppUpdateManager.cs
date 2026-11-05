using System;
using System.Threading.Tasks;
using Alterverse.Shop;
using Alterverse.UI;
using UnityEngine;

namespace Alterverse.Profile
{
    public sealed class StartupAppUpdateManager : MonoBehaviour
    {
        private const int ImmediateUpdateType = 1;
        private const int UpdateAvailabilityAvailable = 2;
        private const int UpdateAvailabilityInProgress = 3;
        private const int UpdateAvailabilityNotAvailable = 1;
        private const int InstallStatusDownloaded = 11;
        private const int InstallStatusFailed = 5;
        private const int InstallStatusCanceled = 6;
        private const int UpdateRequestCode = 9417;

        public static StartupAppUpdateManager Instance { get; private set; }

        public bool CheckCompleted { get; private set; }
        public bool Checking { get; private set; }
        public bool UpdateRequired { get; private set; }
        public bool UpdateFlowActive { get; private set; }
        public int InstalledVersionCode { get; private set; }
        public int AvailableVersionCode { get; private set; }
        public string RequiredVersionLabel { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;

        private float lastCheckTime = -999f;
        private bool applicationFocused = true;
        private bool updateCompletionPending;
        private float nextUpdatePoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
#if !UNITY_SERVER
            if (!Application.isBatchMode &&
                !Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Requested &&
                Instance == null)
            {
                new GameObject("DF_StartupAppUpdateManager").AddComponent<StartupAppUpdateManager>();
            }
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InstalledVersionCode = ReadInstalledVersionCode();
            PaidShardCommerce.EntitlementsChanged += EvaluateServerMinimumVersion;
        }

        private void Start()
        {
            _ = CheckAndTriggerMandatoryUpdateAsync(launchNativeFlow: true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            bool resumed = !applicationFocused;
            applicationFocused = hasFocus;
            if (!hasFocus) return;
            if (resumed && !updateCompletionPending) UpdateFlowActive = false;
            if (UpdateRequired || Time.unscaledTime - lastCheckTime >= 15f)
            {
                // Resume an existing flow, but do not reopen a cancelled consent dialog.
                _ = CheckAndTriggerMandatoryUpdateAsync(launchNativeFlow: false);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            OnApplicationFocus(!paused);
        }

        private void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            // A downloaded update may become ready without another focus callback.
            if (applicationFocused && UpdateRequired && !Checking && Time.unscaledTime >= nextUpdatePoll)
                _ = CheckAndTriggerMandatoryUpdateAsync(launchNativeFlow: false);
#endif
        }

        private void OnDestroy()
        {
            PaidShardCommerce.EntitlementsChanged -= EvaluateServerMinimumVersion;
            if (Instance == this) Instance = null;
        }

        public void TriggerUpdateNow()
        {
            _ = CheckAndTriggerMandatoryUpdateAsync(launchNativeFlow: true, fallbackToStorePage: true);
        }

        public async Task CheckAndTriggerMandatoryUpdateAsync(bool launchNativeFlow, bool fallbackToStorePage = false)
        {
            if (Checking) return;
            Checking = true;
            lastCheckTime = Time.unscaledTime;
            nextUpdatePoll = Time.unscaledTime + 2f;

            try
            {
                EvaluateServerMinimumVersion();

#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
                bool handledByPlayCore = await CheckGooglePlayImmediateUpdateAsync(launchNativeFlow, fallbackToStorePage);
                if (!handledByPlayCore && UpdateRequired && fallbackToStorePage)
                {
                    NativeStoreReview.OpenStore();
                }
#else
                await Task.CompletedTask;
                if (UpdateRequired && fallbackToStorePage)
                {
                    NativeStoreReview.OpenStore();
                }
#endif
            }
            catch (Exception)
            {
                if (UpdateRequired && fallbackToStorePage)
                {
                    NativeStoreReview.OpenStore();
                }
            }
            finally
            {
                Checking = false;
                CheckCompleted = true;
            }
        }

        private void EvaluateServerMinimumVersion()
        {
            var commerce = PaidShardCommerce.Instance;
            if (commerce == null || !commerce.Ready) return;

            int minCode = commerce.MinAndroidVersionCode;
            string minVersion = commerce.MinClientVersion;

            if (minCode > 0 && InstalledVersionCode > 0 && InstalledVersionCode < minCode)
            {
                UpdateRequired = true;
                AvailableVersionCode = Mathf.Max(AvailableVersionCode, minCode);
                RequiredVersionLabel = !string.IsNullOrEmpty(minVersion) ? minVersion : ("Build #" + minCode);
                StatusMessage = "Ein Pflicht-Update ist verfügbar (" + RequiredVersionLabel + "). Bitte aktualisiere jetzt im Google Play Store.";
                return;
            }

            if (!string.IsNullOrEmpty(minVersion) && IsVersionLower(Application.version, minVersion))
            {
                UpdateRequired = true;
                RequiredVersionLabel = minVersion;
                StatusMessage = "Version " + minVersion + " ist erforderlich (installiert: " + Application.version + ").";
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        private async Task<bool> CheckGooglePlayImmediateUpdateAsync(bool launchNativeFlow, bool fallbackToStorePage)
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return false;

                using var factory = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateManagerFactory");
                using var manager = factory.CallStatic<AndroidJavaObject>("create", activity);
                if (manager == null) return false;

                using var task = manager.Call<AndroidJavaObject>("getAppUpdateInfo");
                if (task == null) return false;

                float deadline = Time.realtimeSinceStartup + 12f;
                while (!task.Call<bool>("isComplete"))
                {
                    if (Time.realtimeSinceStartup >= deadline) return false;
                    await Task.Delay(80);
                }

                if (!task.Call<bool>("isSuccessful")) return false;

                using var info = task.Call<AndroidJavaObject>("getResult");
                if (info == null) return false;

                InstalledVersionCode = ReadInstalledVersionCode();
                int availability = info.Call<int>("updateAvailability");
                int installStatus = info.Call<int>("installStatus");
                if (installStatus == InstallStatusFailed || installStatus == InstallStatusCanceled)
                    UpdateFlowActive = updateCompletionPending = false;
                if (installStatus == InstallStatusDownloaded)
                {
                    UpdateRequired = true;
                    AvailableVersionCode = Mathf.Max(AvailableVersionCode, info.Call<int>("availableVersionCode"));
                    StatusMessage = "Update heruntergeladen. Das Spiel wird automatisch aktualisiert und neu gestartet…";
                    if (!applicationFocused) return true;
                    if (updateCompletionPending) return true;
                    UpdateFlowActive = true;
                    using var completion = manager.Call<AndroidJavaObject>("completeUpdate");
                    float completionDeadline = Time.realtimeSinceStartup + 12f;
                    while (completion != null && !completion.Call<bool>("isComplete"))
                    {
                        if (Time.realtimeSinceStartup >= completionDeadline)
                        {
                            UpdateFlowActive = false;
                            StatusMessage = "Update-Abschluss noch nicht bestätigt. Bitte erneut versuchen.";
                            return true;
                        }
                        await Task.Delay(80);
                    }
                    updateCompletionPending = completion != null && completion.Call<bool>("isSuccessful");
                    UpdateFlowActive = updateCompletionPending;
                    if (!updateCompletionPending)
                        StatusMessage = "Update konnte nicht abgeschlossen werden. Bitte erneut versuchen.";
                    // Google Play completes installation and relaunches the main activity.
                    return true;
                }

                if (availability == UpdateAvailabilityAvailable || availability == UpdateAvailabilityInProgress)
                {
                    UpdateRequired = true;
                    AvailableVersionCode = Mathf.Max(AvailableVersionCode, info.Call<int>("availableVersionCode"));
                    RequiredVersionLabel = AvailableVersionCode > 0 ? ("Build #" + AvailableVersionCode) : Application.version;
                    StatusMessage = "Neues Update im Google Play Store gefunden (" + RequiredVersionLabel + "). Update wird gestartet…";

                    bool immediateAllowed = false;
                    try { immediateAllowed = info.Call<bool>("isUpdateTypeAllowed", ImmediateUpdateType); } catch { immediateAllowed = false; }

                    bool resumeFlow = availability == UpdateAvailabilityInProgress && !UpdateFlowActive;
                    if ((launchNativeFlow || resumeFlow) && (immediateAllowed || availability == UpdateAvailabilityInProgress))
                    {
                        UpdateFlowActive = true;
                        if (await StartImmediateFlowOnUiThread(manager, info, activity)) return true;
                        UpdateFlowActive = false;
                    }

                    if (fallbackToStorePage) { NativeStoreReview.OpenStore(); return true; }
                }
                else if (availability == UpdateAvailabilityNotAvailable)
                {
                    // A successful post-install query must release the previous process's update gate.
                    UpdateRequired = UpdateFlowActive = updateCompletionPending = false;
                    AvailableVersionCode = 0;
                    RequiredVersionLabel = StatusMessage = string.Empty;
                    EvaluateServerMinimumVersion(); // A real backend minimum must remain enforced.
                }
                return false;
            }
            catch { UpdateFlowActive = false; return false; }
        }

        private static async Task<bool> StartImmediateFlowOnUiThread(AndroidJavaObject manager, AndroidJavaObject info, AndroidJavaObject activity)
        {
            var completion = new TaskCompletionSource<bool>();
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    using var optionsClass = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateOptions");
                    using var builder = optionsClass.CallStatic<AndroidJavaObject>("newBuilder", ImmediateUpdateType);
                    using var options = builder.Call<AndroidJavaObject>("build");
                    // The Java API returns boolean, not int. JNI signatures include the return type.
                    completion.TrySetResult(manager.Call<bool>("startUpdateFlowForResult", info, activity, options, UpdateRequestCode));
                }
                catch { completion.TrySetResult(false); }
            }));
            if (await Task.WhenAny(completion.Task, Task.Delay(5000)) != completion.Task) return false;
            return await completion.Task;
        }
#endif

        private static int ReadInstalledVersionCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                string pkgName = activity.Call<string>("getPackageName");
                using var pkgInfo = pm.Call<AndroidJavaObject>("getPackageInfo", pkgName, 0);
                return pkgInfo.Get<int>("versionCode");
            }
            catch { return 0; }
#else
            return 0;
#endif
        }

        private static bool IsVersionLower(string currentVersion, string minimumVersion)
        {
            if (string.IsNullOrWhiteSpace(currentVersion) || string.IsNullOrWhiteSpace(minimumVersion)) return false;
            string[] curParts = currentVersion.Trim().Split('.');
            string[] minParts = minimumVersion.Trim().Split('.');
            int count = Mathf.Max(curParts.Length, minParts.Length);
            for (int i = 0; i < count; i++)
            {
                int curVal = i < curParts.Length && int.TryParse(curParts[i], out int c) ? c : 0;
                int minVal = i < minParts.Length && int.TryParse(minParts[i], out int m) ? m : 0;
                if (curVal < minVal) return true;
                if (curVal > minVal) return false;
            }
            return false;
        }
    }
}
