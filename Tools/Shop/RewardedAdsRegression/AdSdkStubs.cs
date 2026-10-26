using System;
namespace GoogleMobileAds.Api
{
    public sealed class AdRequest { }
    public sealed class LoadAdError { }
    public sealed class AdError { }
    public sealed class InitializationStatus { }
    public sealed class Reward { public double Amount = 2; public string Type = "Shards"; }
    public static class MobileAds
    {
        public static int Calls;
        public static void Initialize(Action<InitializationStatus> done) { Calls++; done(new InitializationStatus()); }
    }
    public sealed class RewardedAd
    {
        public static int Loads, Shows;
        public static string LastUnit;
        public static Action<RewardedAd, LoadAdError> Pending;
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        public Action<Reward> Earned;
        public bool Destroyed, Shown;
        public bool CanShowAd() => !Destroyed && !Shown;
        public static void Load(string unit, AdRequest request, Action<RewardedAd, LoadAdError> done)
        { Loads++; LastUnit = unit; Pending = done; }
        public void Show(Action<Reward> earned) { Shows++; Shown = true; Earned = earned; }
        public void Destroy() { Destroyed = true; }
        public void Close() => OnAdFullScreenContentClosed?.Invoke();
        public void Fail() => OnAdFullScreenContentFailed?.Invoke(new AdError());
    }
}
namespace GoogleMobileAds.Ump.Api
{
    public sealed class ConsentRequestParameters { }
    public sealed class FormError { }
    public enum PrivacyOptionsRequirementStatus { Unknown, NotRequired, Required }
    public static class ConsentInformation
    {
        public static int Updates;
        public static bool Allowed = true;
        public static PrivacyOptionsRequirementStatus PrivacyOptionsRequirementStatus = PrivacyOptionsRequirementStatus.Required;
        public static Action<FormError> Pending;
        public static bool CanRequestAds() => Allowed;
        public static void Update(ConsentRequestParameters parameters, Action<FormError> done) { Updates++; Pending = done; }
    }
    public static class ConsentForm
    {
        public static Action<FormError> PrivacyDone;
        public static void LoadAndShowConsentFormIfRequired(Action<FormError> done) => done(null);
        public static void ShowPrivacyOptionsForm(Action<FormError> done) { PrivacyDone = done; }
    }
}
