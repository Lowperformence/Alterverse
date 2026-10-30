using System;
using System.Collections.Concurrent;
using Alterverse.UI;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
using GoogleMobileAds.Api;
#endif

namespace Alterverse.Shop
{
    /// <summary>One menu-only banner. Shares consent/initialization with rewarded ads.</summary>
    public sealed class MenuBannerAds : MonoBehaviour
    {
        public const string AndroidAdUnitId = "ca-app-pub-1716027263297702/9930233354";
        const string TestAdUnitId = "ca-app-pub-3940256099942544/6300978111";
        public static MenuBannerAds Instance { get; private set; }
        public string Status { get; private set; } = "Waiting for ad service...";
        public bool Loaded { get; private set; }
        bool disposed, paused;
        Rect requestedPixels;
        bool requestedLargeBanner;
        int lastRequestedFrame = -100;
        readonly ConcurrentQueue<Action> callbacks = new ConcurrentQueue<Action>();
        static float density = 1, nextDensityCheck;
        public static float DeviceScale
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
                if (Time.unscaledTime >= nextDensityCheck)
                {
                    nextDensityCheck = Time.unscaledTime + 1;
                    try
                    {
                        float value = MobileAds.Utils.GetDeviceScale();
                        if (value > 0 && !float.IsNaN(value) && !float.IsInfinity(value)) density = value;
                    }
                    catch (Exception) { }
                }
#endif
                return density;
            }
        }
        public static MenuBannerAds Ensure()
        {
            if (Instance == null) new GameObject("DF_MenuBannerAds").AddComponent<MenuBannerAds>();
            return Instance;
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }
        public void Request(Rect topLeftPixels, bool largeBanner = false)
        { requestedPixels = topLeftPixels; requestedLargeBanner = largeBanner; lastRequestedFrame = Time.frameCount; }
        void LateUpdate()
        {
            while (callbacks.TryDequeue(out var callback)) if (!disposed) callback();
            if (disposed) return;
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            var ads = RewardedShardAds.Ensure();
            if (privacyVersion != ads.PrivacyRevision) { DestroyBanner(); privacyVersion = ads.PrivacyRevision; }
            bool visible = !paused && MainMenuController.AllowsHomeBanner &&
                Time.frameCount - lastRequestedFrame <= 2;
            if (!visible) { Hide(); return; }
            if (!ads.ReadyForRequests) { Status = ads.Status; Hide(); return; }
            if (retryPending)
            {
                if (Time.unscaledTime < retryAt) { Status = failureStatus; Hide(); return; }
                DestroyBanner();
            }
            int x = Mathf.RoundToInt(requestedPixels.x / DeviceScale);
            int y = Mathf.RoundToInt(requestedPixels.y / DeviceScale);
            if (banner != null && currentLargeBanner != requestedLargeBanner) DestroyBanner();
            if (banner == null) Create(x, y);
            if (banner == null) return;
            if (x != positionX || y != positionY) { banner.SetPosition(x, y); positionX = x; positionY = y; }
            if (!shown) { banner.Show(); shown = true; }
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        BannerView banner;
        bool shown, retryPending;
        int loadFailures;
        float retryAt;
        string failureStatus;
        bool currentLargeBanner;
        int privacyVersion, positionX, positionY;
        void Create(int x, int y)
        {
#if DEVELOPMENT_BUILD
            string unit = TestAdUnitId;
#else
            string unit = AndroidAdUnitId;
#endif
            currentLargeBanner = requestedLargeBanner;
            var created = new BannerView(unit, currentLargeBanner ? new AdSize(320, 100) : AdSize.Banner, x, y);
            banner = created; positionX = x; positionY = y;
            Status = "Loading advertisement...";
            created.OnBannerAdLoaded += () => callbacks.Enqueue(() =>
            {
                if (banner != created) return;
                Loaded = true; retryPending = false; loadFailures = 0; Status = "Advertisement";
            });
            created.OnBannerAdLoadFailed += error => callbacks.Enqueue(() =>
            {
                if (banner != created) return;
                Debug.LogWarning("[DF Ads/Banner] Load failed: " + error);
                // A failed SDK refresh must not discard an already displayed creative.
                if (Loaded || retryPending) return;
                loadFailures = Math.Min(loadFailures + 1, 3);
                retryAt = Time.unscaledTime + 30 * (1 << (loadFailures - 1));
                retryPending = true;
                failureStatus = error == null ? "No advertisement returned. Retrying shortly." :
                    "Ad could not load (" + error.GetCode() + "): " + error.GetMessage() + " Retrying shortly.";
                Status = failureStatus;
                Hide();
            });
            // AdMob controls successful-banner refresh. Only an empty, failed initial load is retried.
            created.LoadAd(new AdRequest());
        }
        void Hide()
        { if (banner != null && shown) { banner.Hide(); shown = false; } }
        void DestroyBanner()
        { banner?.Destroy(); banner = null; shown = false; Loaded = false; retryPending = false; }
#endif
        void OnApplicationPause(bool value)
        {
            paused = value;
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            if (paused) Hide();
#endif
        }
        void OnDestroy()
        {
            disposed = true;
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            DestroyBanner();
#endif
            if (Instance == this) Instance = null;
        }
    }
}
