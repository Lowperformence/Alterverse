using System;
using System.Reflection;
using System.Threading.Tasks;
using Alterverse.Profile;
using Alterverse.Shop;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

static class Regression
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Call(RewardedShardAds ads, string name) => typeof(RewardedShardAds).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ads, null);
    static void Pump(RewardedShardAds ads) => Call(ads, "Update");
    static AccountProfilePrefs.Entry[] Profile(int balance) => new[] { new AccountProfilePrefs.Entry { key = "DF_Shards", number = balance } };
    static RewardedAd Load(RewardedShardAds ads)
    {
        Time.unscaledTime += 70;
        ads.Prepare();
        var loaded = new RewardedAd();
        var callback = RewardedAd.Pending;
        Check(callback != null, "Expected a pending ad load");
        RewardedAd.Pending = null;
        callback(loaded, null);
        Pump(ads);
        Check(ads.CanShow, "Loaded ad should be available");
        return loaded;
    }

    public static void Main()
    {
        PlayerPrefs.SetInt("DF_Shards", 0);
        var ads = RewardedShardAds.Ensure();
        Call(ads, "Start");
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        ads.Prepare();
        Check(MobileAds.Calls == 0, "Never initialize from cached consent before this launch's Update completes");
        Check(ConsentInformation.Updates == 1, "One consent update per launch");
        ConsentInformation.Pending(null);
        Pump(ads);
        ads.Prepare();
        Pump(ads);
        Check(MobileAds.Calls == 1, "SDK initialized once");
        var first = Load(ads);
#if DEVELOPMENT_BUILD
        Check(RewardedAd.LastUnit == "ca-app-pub-3940256099942544/5224354917", "Development builds must use a rewarded test unit");
#else
        Check(RewardedAd.LastUnit == RewardedShardAds.AndroidAdUnitId, "Release builds use the user's rewarded placement");
#endif
        ads.Show(); ads.Show();
        Check(RewardedAd.Shows == 1, "Repeated taps must not open two ads");
        Task.Run(() => { first.Earned(new Reward()); first.Earned(new Reward()); }).GetAwaiter().GetResult();
        Check(PlayerWallet.EarnedBalance == 0, "Worker callbacks must not touch Unity/profile state");
        Pump(ads);
        Check(PlayerWallet.EarnedBalance == 2, "Successful completion grants exactly two, once");
        first.Close(); Pump(ads);
        Check(first.Destroyed && PlayerWallet.EarnedBalance == 2, "Close cleans up and never adds a second reward");
        var cancelled = Load(ads); ads.Show(); cancelled.Close(); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 2, "Closing early must not grant shards");
        var failed = Load(ads); ads.Show(); failed.Fail(); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 2 && failed.Destroyed, "Failed presentation must not grant shards");
        var late = Load(ads); ads.Show(); late.Close(); Pump(ads); late.Earned(new Reward()); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 4, "A genuine earned event after close must still be counted");
        first.Earned(new Reward()); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 4, "A late duplicate from an older ad is ignored");

        AccountProfilePrefs.Attach("account-a", 1, Profile(10));
        var switched = Load(ads); ads.Show();
        AccountProfilePrefs.Attach("account-b", 1, Profile(20));
        switched.Earned(new Reward()); switched.Close(); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 20 && RewardedShardPayout.HasPending, "Never give account A's reward to account B");
        Check(!ads.CanShow, "Collect an outstanding reward before another ad");
        AccountProfilePrefs.Attach("account-a", 1, Profile(10));
        Time.unscaledTime += 2; Pump(ads);
        Check(PlayerWallet.EarnedBalance == 12 && !RewardedShardPayout.HasPending, "Collect the durable pending reward on the original account");
        Check(AccountProfilePrefs.Dirty, "Earned shards must enter the existing cloud profile save path");

        var replay = RewardedShardPayout.NewReceipt();
        RewardedShardPayout.Enqueue(replay); RewardedShardPayout.Collect();
        RewardedShardPayout.Enqueue(replay); RewardedShardPayout.Collect();
        Check(PlayerWallet.EarnedBalance == 14, "Replaying a persisted receipt after interruption must be idempotent");
        var r1 = RewardedShardPayout.NewReceipt(); var r2 = RewardedShardPayout.NewReceipt();
        RewardedShardPayout.Enqueue(r1); RewardedShardPayout.Enqueue(r2);
        Check(RewardedShardPayout.Collect() == 4 && PlayerWallet.EarnedBalance == 18, "Collect separate rewards individually");

        // Change privacy preferences during an in-flight load: discard the old result.
        Time.unscaledTime += 70; ads.Prepare();
        var staleCallback = RewardedAd.Pending; var stale = new RewardedAd();
        ads.ShowPrivacyOptions();
        ConsentInformation.Allowed = false; ConsentForm.PrivacyDone(null); Pump(ads);
        staleCallback(stale, null); Pump(ads);
        Check(stale.Destroyed && !ads.CanShow, "Withdrawn consent must invalidate an in-flight ad");
        Check(PlayerWallet.EarnedBalance == 18, "Privacy updates never grant currency");
        Time.unscaledTime += 70; ads.Prepare(); ConsentInformation.Pending(null); Pump(ads);
        int consentCalls = ConsentInformation.Updates;
        for (int i = 0; i < 100; i++) ads.Prepare();
        Check(ConsentInformation.Updates == consentCalls, "A denied/unavailable consent state must not loop each GUI event");
        ConsentInformation.Allowed = true; Time.unscaledTime += 70;
        ads.ShowPrivacyOptions(); ConsentForm.PrivacyDone(null); Pump(ads);
        var ready = Load(ads);
        Time.unscaledTime += 3301; Pump(ads);
        Check(ready.Destroyed && !ads.CanShow, "Expire cached ads before Google's one-hour limit");
        ads.Prepare(); RewardedAd.Pending(null, new LoadAdError()); Pump(ads);
        int attempts = RewardedAd.Loads;
        for (int i = 0; i < 100; i++) ads.Prepare();
        Check(RewardedAd.Loads == attempts, "Failed load retries must be rate limited");
        Console.WriteLine("PASS: Android rewarded callbacks, duplicate/late events, account switch, persistence, consent, expiry and retry.");
#else
        ads.Prepare(); ads.Show(); ads.ShowPrivacyOptions(); Pump(ads);
        Check(!ads.CanShow && MobileAds.Calls == 0 && RewardedAd.Shows == 0 && PlayerWallet.EarnedBalance == 0,
            "Editor/desktop/server builds must not run mobile SDK or grant pretend rewards");
        Console.WriteLine("PASS: Non-mobile/editor/server reward path disabled.");
#endif
    }
}
