using System;
using System.Collections.Concurrent;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace Alterverse.Shop
{
    /// <summary>One opt-in shop placement. Scene-independent; no ads on desktop or server.</summary>
    public sealed class RewardedShardAds : MonoBehaviour
    {
        public const int RewardAmount = RewardedShardPayout.Amount;
        public const string AndroidAdUnitId = "ca-app-pub-1716027263297702/1813938170";
        // Google's rewarded test unit from the pinned v11.5.0 Unity sample.
        const string AndroidTestAdUnitId = "ca-app-pub-3940256099942544/5224354917";
        public static RewardedShardAds Instance { get; private set; }
        readonly ConcurrentQueue<Action> callbacks = new ConcurrentQueue<Action>();
        bool disposed;
        float nextCollection;
        public string Status { get; private set; } = "Rewarded ads are available in the Android build.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate() => Ensure();

        public static RewardedShardAds Ensure()
        {
            if (Instance == null) new GameObject("DF_RewardedShardAds").AddComponent<RewardedShardAds>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            GatherConsent();
#endif
        }

        void Post(Action callback) => callbacks.Enqueue(callback);

        void Update()
        {
            while (callbacks.TryDequeue(out var callback))
            {
                if (disposed) continue;
                try { callback(); }
                catch (Exception) { Status = "Ad service unavailable. Please try again."; }
            }
            if (Time.unscaledTime >= nextCollection)
            {
                nextCollection = Time.unscaledTime + 1;
                CollectRewards();
            }
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            if (ad != null && Time.unscaledTime - loadedAt >= 3300) DestroyLoadedAd();
#endif
        }

        void CollectRewards()
        {
            try
            {
                int amount = RewardedShardPayout.Collect();
                if (amount > 0) Status = "+" + amount + " shards added!";
                else if (RewardedShardPayout.HasPending)
                    Status = "Reward saved. Sign in to the account that watched the ad to collect it.";
            }
            catch (Exception) { Status = "Your saved ad reward needs support. It has been kept on this device."; }
        }

        public bool CanShow
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
                return consentChecked && !showing && !consentBusy && !privacyBusy && !RewardedShardPayout.HasPending &&
                    ConsentInformation.CanRequestAds() && ad != null && ad.CanShowAd() &&
                    Time.unscaledTime - loadedAt < 3300;
#else
                return false;
#endif
            }
        }

        public bool PrivacyOptionsRequired
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
                return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
#else
                return false;
#endif
            }
        }

        // Called while the shard shop is open. Retries are delayed and capped;
        // loading a replacement never opens it automatically.
        public void Prepare()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            if (showing || consentBusy || privacyBusy || RewardedShardPayout.HasPending || Time.unscaledTime < nextAttempt) return;
            if (!consentChecked || !ConsentInformation.CanRequestAds()) { GatherConsent(); return; }
            if (!initialized) { InitializeAds(); return; }
            if (ad != null && !ad.CanShowAd()) DestroyLoadedAd();
            if (ad == null && !loading) LoadAd();
#endif
        }

        public void Show()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            if (!CanShow) return;
            var shown = ad;
            var receipt = RewardedShardPayout.NewReceipt();
            bool rewardReported = false, finished = false;
            ad = null;
            showing = true;
            Status = $"Complete the ad to earn {RewardAmount} shards.";
            Action<bool> finish = failed =>
            {
                if (finished) return;
                finished = true;
                showing = false;
                nextAttempt = Time.unscaledTime + 1;
                shown.Destroy();
                if (!rewardReported) Status = failed ? "Ad could not open. No shards awarded." : "Ad closed. No reward earned.";
            };
            shown.OnAdFullScreenContentClosed += () => Post(() => finish(false));
            shown.OnAdFullScreenContentFailed += error => Post(() => finish(true));
            try
            {
                shown.Show(reward =>
                {
                    int completedDay = Alterverse.Profile.DailyCalendar.Today;
                    Post(() =>
                    {
                        if (rewardReported) return;
                        RewardedShardPayout.Enqueue(receipt, completedDay);
                        rewardReported = true;
                        CollectRewards();
                    });
                });
            }
            catch (Exception) { finish(true); }
#endif
        }

        public void ShowPrivacyOptions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            if (!PrivacyOptionsRequired || privacyBusy || consentBusy || showing) return;
            privacyBusy = true;
            consentVersion++;
            DestroyLoadedAd();
            ConsentForm.ShowPrivacyOptionsForm(error => Post(() =>
            {
                privacyBusy = false;
                nextAttempt = Time.unscaledTime + (error == null ? 0 : 15);
                Status = error == null ? "Ad privacy preferences updated." : "Privacy preferences could not be opened. Please retry.";
            }));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        RewardedAd ad;
        bool consentChecked, consentBusy, privacyBusy, initialized, initializing, loading, showing;
        int consentVersion, loadFailures;
        float nextAttempt, loadedAt;

        void GatherConsent()
        {
            if (consentBusy || privacyBusy || showing) return;
            consentBusy = true;
            Status = "Checking ad privacy preferences...";
            ConsentInformation.Update(new ConsentRequestParameters(), error => Post(() =>
            {
                if (error != null)
                {
                    consentBusy = false;
                    consentChecked = true;
                    nextAttempt = Time.unscaledTime + 30;
                    Status = "Ad privacy check unavailable. Please try again.";
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => Post(() =>
                {
                    consentBusy = false;
                    consentChecked = true;
                    nextAttempt = Time.unscaledTime + (formError != null ? 30 : ConsentInformation.CanRequestAds() ? 0 : 60);
                    Status = ConsentInformation.CanRequestAds() ? "Ready to load a rewarded ad." : "Ads unavailable for your privacy settings.";
                }));
            }));
        }

        void InitializeAds()
        {
            if (initializing || initialized) return;
            initializing = true;
            Status = "Starting ad service...";
            MobileAds.Initialize(status => Post(() =>
            {
                initializing = false;
                initialized = true;
            }));
        }

        void LoadAd()
        {
            loading = true;
            int version = consentVersion;
            Status = "Loading a video...";
#if DEVELOPMENT_BUILD
            string unit = AndroidTestAdUnitId;
#else
            string unit = AndroidAdUnitId;
#endif
            RewardedAd.Load(unit, new AdRequest(), (loaded, error) => Post(() =>
            {
                loading = false;
                // Preferences can change while a network request is in flight.
                if (version != consentVersion || !ConsentInformation.CanRequestAds())
                {
                    loaded?.Destroy();
                    return;
                }
                if (error != null || loaded == null)
                {
                    loaded?.Destroy();
                    loadFailures = Math.Min(loadFailures + 1, 3);
                    nextAttempt = Time.unscaledTime + (float)Math.Min(60, 10 * (1 << loadFailures));
                    Status = "No video available right now. Please try again shortly.";
                    return;
                }
                ad = loaded;
                loadedAt = Time.unscaledTime;
                loadFailures = 0;
                Status = $"Watch an ad to earn {RewardAmount} shards.";
            }));
        }

        void DestroyLoadedAd()
        {
            ad?.Destroy();
            ad = null;
        }
#endif

        void OnApplicationPause(bool paused)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            // Cached ads may have expired while Unity was suspended.
            if (!paused && !showing) DestroyLoadedAd();
#endif
        }

        void OnDestroy()
        {
            disposed = true;
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            DestroyLoadedAd();
#endif
            if (Instance == this) Instance = null;
        }
    }
}
