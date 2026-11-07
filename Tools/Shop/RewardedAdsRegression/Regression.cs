using System;
using System.Reflection;
using System.Threading.Tasks;
using Alterverse.Profile;
using Alterverse.Shop;
using Alterverse.Characters;
using Alterverse.Match;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

static class Regression
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Call(RewardedShardAds ads, string name, params object[] args) => typeof(RewardedShardAds).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ads, args);
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
        AccountProfilePrefs.Attach("startup-original", 1, Profile(0));
        RewardedShardPayout.Enqueue(RewardedShardPayout.NewReceipt());
        AccountProfilePrefs.Attach("startup-current", 1, Profile(0));
        ads.Prepare();
        Check(MobileAds.Calls == 0, "Never initialize from cached consent before this launch's Update completes");
        Check(ConsentInformation.Updates == 1, "One consent update per launch");
        ConsentInformation.Pending(null);
        Pump(ads);
        Pump(ads);
        Check(MobileAds.Calls == 1, "SDK initialized once");
        Check(ads.ReadyForRequests && RewardedShardPayout.HasPending && RewardedAd.Loads == 0,
            "A pending reward still permits SDK/banner initialization while blocking another rewarded video");
        AccountProfilePrefs.Attach("startup-original", 1, Profile(0));
        Time.unscaledTime += 2; Pump(ads);
        Check(!RewardedShardPayout.HasPending && PlayerWallet.EarnedBalance == 5,
            "The original account can collect its saved reward without a duplicate payment");
        AccountProfilePrefs.Attach("video-session", 1, Profile(0));
        Check(RewardedAd.Loads == 1 && RewardedAd.Pending != null,
            "Preload immediately after launch consent without opening the shop or calling Prepare");
        Check(!ads.CanShow && RewardedAd.Shows == 0, "Loading never enables an unready ad or opens it automatically");
        var first = Load(ads);
        AccountServerBootstrap.CloudProfileReady = false;
        ads.Show();
        Check(!ads.CanShow && RewardedAd.Shows == 0 && !first.Destroyed,
            "A cached video stays ready but cannot start before the verified cloud account is loaded");
        AccountServerBootstrap.CloudProfileReady = true;
#if DEVELOPMENT_BUILD
        Check(RewardedAd.LastUnit == "ca-app-pub-3940256099942544/5224354917", "Development builds must use a rewarded test unit");
#else
        Check(RewardedAd.LastUnit == RewardedShardAds.AndroidAdUnitId, "Release builds use the user's rewarded placement");
#endif
        int initialLoads = RewardedAd.Loads;
        for (int i = 0; i < 20; i++) { ads.Prepare(); Pump(ads); }
        Call(ads, "OnApplicationPause", true); Call(ads, "OnApplicationPause", false); Pump(ads);
        Check(ads.CanShow && !first.Destroyed && RewardedAd.Loads == initialLoads && RewardedAd.Shows == 0,
            "Keep one ready cached video across GUI requests and a brief app switch");
        ads.Show(); ads.Show();
        Check(RewardedAd.Shows == 1, "Repeated taps must not open two ads");
        Task.Run(() => { first.Earned(new Reward()); first.Earned(new Reward()); }).GetAwaiter().GetResult();
        Check(PlayerWallet.EarnedBalance == 0, "Worker callbacks must not touch Unity/profile state");
        Pump(ads);
        Check(PlayerWallet.EarnedBalance == 5, "Successful completion grants exactly five, once");
        Check(DailyQuestProgress.Progress(4) == 1, "Duplicate earned callbacks advance the daily quest only once");
        int loadsBeforeClose = RewardedAd.Loads;
        first.Close(); Pump(ads);
        Check(RewardedAd.Loads == loadsBeforeClose + 1 && RewardedAd.Pending != null,
            "Closing preloads the next video in the same update, without a one-second timer");
        Check(first.Destroyed && PlayerWallet.EarnedBalance == 5, "Close cleans up and never adds a second reward");
        var cancelled = Load(ads); ads.Show(); cancelled.Close(); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 5 && DailyQuestProgress.Progress(4) == 1, "Closing early must not grant shards or quest progress");
        var failed = Load(ads); ads.Show(); failed.Fail(); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 5 && failed.Destroyed && DailyQuestProgress.Progress(4) == 1, "Failed presentation must not grant shards or quest progress");
        var late = Load(ads); ads.Show(); late.Close(); Pump(ads); late.Earned(new Reward()); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 10 && DailyQuestProgress.Progress(4) == 2, "A genuine earned event after close must still be counted");
        first.Earned(new Reward()); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 10 && DailyQuestProgress.Progress(4) == 2, "A late duplicate from an older ad is ignored");

        AccountProfilePrefs.Attach("account-a", 1, Profile(10));
        var switched = Load(ads); ads.Show();
        AccountProfilePrefs.Attach("account-b", 1, Profile(20));
        switched.Earned(new Reward()); switched.Close(); Pump(ads);
        Check(PlayerWallet.EarnedBalance == 20 && RewardedShardPayout.HasPending && DailyQuestProgress.Progress(4) == 0, "Never give account A's reward or quest progress to account B");
        Check(!ads.CanShow, "Collect an outstanding reward before another ad");
        AccountProfilePrefs.Attach("account-a", 1, Profile(10));
        Time.unscaledTime += 2; Pump(ads);
        Check(PlayerWallet.EarnedBalance == 15 && !RewardedShardPayout.HasPending && DailyQuestProgress.Progress(4) == 1, "Collect the durable pending reward and quest progress on the original account");
        Check(AccountProfilePrefs.Dirty, "Earned shards must enter the existing cloud profile save path");

        var replay = RewardedShardPayout.NewReceipt();
        RewardedShardPayout.Enqueue(replay); RewardedShardPayout.Collect();
        RewardedShardPayout.Enqueue(replay); RewardedShardPayout.Collect();
        Check(PlayerWallet.EarnedBalance == 20 && DailyQuestProgress.Progress(4) == 2, "Replaying a persisted receipt after interruption must be idempotent");
        var r1 = RewardedShardPayout.NewReceipt(); var r2 = RewardedShardPayout.NewReceipt();
        RewardedShardPayout.Enqueue(r1); RewardedShardPayout.Enqueue(r2);
        Check(RewardedShardPayout.Collect() == 10 && PlayerWallet.EarnedBalance == 30 && DailyQuestProgress.Progress(4) == 4, "Collect separate rewards individually");

        // Change privacy preferences during an in-flight load: discard the old result.
        Time.unscaledTime += 70; ads.Prepare();
        var staleCallback = RewardedAd.Pending; var stale = new RewardedAd();
        ads.ShowPrivacyOptions();
        ConsentInformation.Allowed = false; ConsentForm.PrivacyDone(null); Pump(ads);
        staleCallback(stale, null); Pump(ads);
        Check(stale.Destroyed && !ads.CanShow, "Withdrawn consent must invalidate an in-flight ad");
        Check(PlayerWallet.EarnedBalance == 30, "Privacy updates never grant currency");
        Time.unscaledTime += 70; ads.Prepare(); ConsentInformation.Pending(null); Pump(ads);
        int consentCalls = ConsentInformation.Updates;
        for (int i = 0; i < 100; i++) ads.Prepare();
        Check(ConsentInformation.Updates == consentCalls, "A denied/unavailable consent state must not loop each GUI event");
        ConsentInformation.Allowed = true; Time.unscaledTime += 70;
        ads.ShowPrivacyOptions(); ConsentForm.PrivacyDone(null); Pump(ads);
        var suspended = Load(ads);
        typeof(RewardedShardAds).GetField("loadedAtUtc", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(ads, DateTime.UtcNow.AddMinutes(-56));
        int loadsBeforeResume = RewardedAd.Loads;
        Call(ads, "OnApplicationPause", false); Pump(ads);
        Check(suspended.Destroyed && !ads.CanShow && RewardedAd.Loads == loadsBeforeResume + 1,
            "A long suspension expires and replaces an ad even if Unity's frame clock has not advanced");
        var ready = Load(ads);
        Time.unscaledTime += 3301; Pump(ads);
        Check(ready.Destroyed && !ads.CanShow, "Expire cached ads before Google's one-hour limit");
        ads.Prepare(); RewardedAd.Pending(null, new LoadAdError()); Pump(ads);
        Check(ads.Status.Contains("Network unavailable") && ads.Status.Contains("(2)"),
            "Rewarded failures expose the actual SDK error instead of claiming every error is missing demand");
        int attempts = RewardedAd.Loads;
        for (int i = 0; i < 100; i++) ads.Prepare();
        Check(RewardedAd.Loads == attempts, "Failed load retries must be rate limited");
        CheckDailyAdQuest();
        Console.WriteLine("PASS: background preload, one cached video, immediate replacement, short resume and suspended expiry.");
        Console.WriteLine("PASS: Android rewarded callbacks, duplicate/late events, account switch, persistence, consent, expiry and retry.");
#else
        ads.Prepare(); ads.Show(); ads.ShowPrivacyOptions(); Pump(ads);
        Check(!ads.CanShow && MobileAds.Calls == 0 && RewardedAd.Shows == 0 && PlayerWallet.EarnedBalance == 0,
            "Editor/desktop/server builds must not run mobile SDK or grant pretend rewards");
        Console.WriteLine("PASS: Non-mobile/editor/server reward path disabled.");
#endif
        CheckDailyWingRewards();
    }

    static void CheckDailyWingRewards()
    {
        UniversalCosmeticSelection.Slots.Clear();
        UniversalCosmeticSelection.Owned.Clear();
        PlayerHeroSelection.Selected = HeroId.Veyr;
        Check(DailyQuestProgress.ShardReward(0) == 20 && LoginStreakProgress.ShardRewardForDay(2) == 3,
            "Without wings daily quest and login amounts must remain unchanged");
        Check(DailyQuestProgress.ShardReward(-1) == 0 && LoginStreakProgress.ShardRewardForDay(8) == 0,
            "Invalid rewards do not grant shards");

        var cosmetics = new[] { UniversalHeroCosmetic.Voidfall, UniversalHeroCosmetic.PlagueWings,
            UniversalHeroCosmetic.BetaSupporterWings, UniversalHeroCosmetic.AngelWings,
            UniversalHeroCosmetic.RgbRiftWings, UniversalHeroCosmetic.DeluxeWings };
        int[] expected = { 21, 22, 24, 26, 28, 30 };
        for (int i = 0; i < cosmetics.Length; i++)
        {
            UniversalCosmeticSelection.Owned.Add(cosmetics[i]);
            UniversalCosmeticSelection.Slots[HeroId.Veyr] = cosmetics[i];
            Check(DailyQuestProgress.ShardReward(0) == expected[i], "Each wing uses its existing percentage and rounding");
        }
        Check(WingMatchRewardBonus.DailyShardReward(int.MaxValue) == int.MaxValue, "Daily amount cannot overflow");
        Check(LoginStreakProgress.ShardRewardForDay(1) == 1, "Half-shard bonuses follow existing round-to-even behavior");

        UniversalCosmeticSelection.Slots[HeroId.Veyr] = UniversalHeroCosmetic.Voidfall;
        UniversalCosmeticSelection.Slots[HeroId.Rook] = UniversalHeroCosmetic.DeluxeWings;
        WingMatchRewardBonus.BeginMatch(HeroId.Veyr);
        PlayerHeroSelection.Selected = HeroId.Rook;
        Check(DailyQuestProgress.ShardReward(0) == 30, "Daily preview uses the selected hero rather than the last match snapshot");

        AccountProfilePrefs.Attach("wing-daily-quest", 1, Profile(0));
        ProfileTransaction.Commit(DailyQuestProgress.MatchWrites(50, 0, 0));
        Check(DailyQuestProgress.Claim(0) && PlayerWallet.EarnedBalance == 30,
            "Completed daily quest credits the boosted preview amount");
        Check(!DailyQuestProgress.Claim(0) && PlayerWallet.EarnedBalance == 30,
            "The boosted daily quest cannot pay twice");
        RewardedShardPayout.Enqueue(RewardedShardPayout.NewReceipt());
        Check(RewardedShardPayout.Collect() == 5 && PlayerWallet.EarnedBalance == 35,
            "Video payouts remain unboosted even when a wing is equipped");

        AccountProfilePrefs.Attach("wing-login", 1, Profile(0));
        AccountProfilePrefs.SetInt("DF_LoginStreakDay_v1", DailyCalendar.Yesterday);
        AccountProfilePrefs.SetInt("DF_LoginStreakIndex_v1", 1);
        Check(LoginStreakProgress.ShardRewardForDay(2) == 5 && LoginStreakProgress.ClaimForToday() == 5 &&
              PlayerWallet.EarnedBalance == 5, "Login claim pays the same boosted amount as its preview");
        Check(LoginStreakProgress.ClaimForToday() == 0 && PlayerWallet.EarnedBalance == 5,
            "The boosted login reward cannot pay twice");

        UniversalCosmeticSelection.Owned.Remove(UniversalHeroCosmetic.DeluxeWings);
        Check(DailyQuestProgress.ShardReward(0) == 20, "An unowned wing cannot grant a daily bonus");
        UniversalCosmeticSelection.Slots.Clear();
        UniversalCosmeticSelection.Owned.Clear();
        PlayerHeroSelection.Selected = HeroId.Veyr;
        Console.WriteLine("PASS: equipped wing daily login/quest rewards, rounding, claim-once, hero selection and unchanged ad payouts.");
    }

    static void CheckDailyAdQuest()
    {
        AccountProfilePrefs.Attach("daily-quest", 1, Profile(0));
        ProfileTransaction.Commit(DailyQuestProgress.MatchWrites(17, 3, 9, true, Alterverse.Match.ArenaVariant.Moon));
        Check(!DailyQuestProgress.Claim(4), "Cannot claim the ad quest before watching ads");
        for (int i = 1; i <= 5; i++)
        {
            var receipt = RewardedShardPayout.NewReceipt();
            RewardedShardPayout.Enqueue(receipt);
            Check(RewardedShardPayout.Collect() == 5, "Every completed video pays five shards");
            Check(DailyQuestProgress.Progress(4) == i, "Five separate videos advance the daily quest");
            Check(PlayerWallet.EarnedBalance == i * 5, "The quest bonus is separate from per-video rewards");
            Check(DailyQuestProgress.Progress(0) == 17 && DailyQuestProgress.Progress(1) == 3 &&
                DailyQuestProgress.Progress(2) == 9 && DailyQuestProgress.Progress(3) == 1,
                "Ad progress preserves today's combat and map progress");
            if (i < 5) Check(!DailyQuestProgress.Claim(4), "Four videos cannot claim the five-video quest");
        }
        Check(DailyQuestProgress.Claim(4) && PlayerWallet.EarnedBalance == 45, "Five videos pay 25 shards plus a claimable 20-shard bonus");
        Check(!DailyQuestProgress.Claim(4) && PlayerWallet.EarnedBalance == 45, "The daily bonus can only be claimed once");
        RewardedShardPayout.Enqueue(RewardedShardPayout.NewReceipt());
        RewardedShardPayout.Collect();
        Check(DailyQuestProgress.Progress(4) == 5 && PlayerWallet.EarnedBalance == 50 && DailyQuestProgress.Claimed(4),
            "Further videos still pay five but cannot repeat the daily bonus");
        ProfileTransaction.Commit(DailyQuestProgress.MatchWrites(1, 0, 0));
        Check(DailyQuestProgress.Progress(4) == 5 && DailyQuestProgress.Claimed(4), "Match rewards preserve ad progress and its claim flag");

        AccountProfilePrefs.SetInt("DF_DailyQuestDay_v2", DailyCalendar.Yesterday);
        RewardedShardPayout.Enqueue(RewardedShardPayout.NewReceipt());
        RewardedShardPayout.Collect();
        Check(DailyQuestProgress.Progress(4) == 1 && !DailyQuestProgress.Claimed(4), "First ad after the daily reset starts a new quest");
        Check(DailyQuestProgress.Progress(0) == 0 && DailyQuestProgress.Progress(3) == 0, "The daily reset also clears stale match progress");

        // Simulate durable receipts left over from yesterday and from the pre-quest build.
        foreach (int day in new[] { DailyCalendar.Yesterday, 0 })
        {
            string id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString("DF_CommerceRewardedAdsPending_v1",
                "{\"receipts\":[{\"id\":\"" + id + "\",\"account\":\"daily-quest\",\"day\":" + day + "}]}");
            Check(RewardedShardPayout.Collect() == 5 && DailyQuestProgress.Progress(4) == 1,
                "Old saved rewards pay currency without counting as today's videos");
        }
        AccountProfilePrefs.Attach("another-player", 1, Profile(0));
        Check(DailyQuestProgress.Progress(4) == 0 && !DailyQuestProgress.Claimed(4), "Daily ad progress is account-scoped");
        AccountProfilePrefs.Attach("daily-quest", 1, Profile(0));
        Check(DailyQuestProgress.Progress(4) == 1, "Daily ad progress survives restoring its account cache");
        Check(DailyQuestProgress.RewardedAdWrites(DailyCalendar.Yesterday).Length == 0,
            "Yesterday's completion cannot advance today's daily quest");
        AccountProfilePrefs.SetInt("DF_DailyQuestDay_v2", int.MaxValue);
        Check(DailyQuestProgress.RewardedAdWrites(DailyCalendar.Today).Length == 0,
            "A device clock rollback cannot reset quest claims");
        Console.WriteLine("PASS: five-video daily quest, 20-shard bonus, claims, match progress, daily reset, old receipts and account isolation.");
    }
}
