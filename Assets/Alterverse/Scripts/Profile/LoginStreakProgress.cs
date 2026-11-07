using System;
using Alterverse.Match;

namespace Alterverse.Profile
{
    public static class LoginStreakProgress
    {
        private const string DayKey = "DF_LoginStreakDay_v1";
        private const string StreakKey = "DF_LoginStreakIndex_v1";
        public const int CycleDays = 7;
        public static int RewardForDay(int day) => day == 1 ? 1 : day == 2 ? 3 : day == 3 ? 6 : day >= 4 && day <= CycleDays ? day + 8 : 0;
        public static int ShardRewardForDay(int day) => WingMatchRewardBonus.DailyShardReward(RewardForDay(day));
        public static int CurrentDay
        {
            get
            {
                DateTime date = DailyCalendar.BerlinDate(DateTime.UtcNow);
                return DayForDisplay(DailyCalendar.Stamp(date), DailyCalendar.Stamp(date.AddDays(-1)),
                    AccountProfilePrefs.GetInt(DayKey, 0), AccountProfilePrefs.GetInt(StreakKey, 0));
            }
        }

        // Show today's position while the player has not claimed today's reward yet.
        // Keep the saved claim markers unchanged until the atomic reward transaction.
        public static int DayForDisplay(int today, int yesterday, int previous, int streak)
        {
            int next = NextDay(today, yesterday, previous, streak);
            return next == 0 ? Math.Max(1, Math.Min(CycleDays, streak)) : next;
        }
        public static bool ReceivedToday => AccountProfilePrefs.GetInt(DayKey, 0) == DailyCalendar.Today;
        public static bool ClockBehind => AccountProfilePrefs.GetInt(DayKey, 0) > DailyCalendar.Today;
        public static bool CanClaimToday => AccountProfilePrefs.GetInt(DayKey, 0) < DailyCalendar.Today;

        // Absolute values are committed together: recovery cannot add a reward a second time.
        // Call only in response to the player's Claim action, never on login or menu update.
        public static int ClaimForToday()
        {
            ProfileTransaction.Recover();
            DateTime date = DailyCalendar.BerlinDate(DateTime.UtcNow);
            int today = DailyCalendar.Stamp(date);
            int previous = AccountProfilePrefs.GetInt(DayKey, 0);
            int priorStreak = AccountProfilePrefs.GetInt(StreakKey, 0);
            int next = NextDay(today, DailyCalendar.Stamp(date.AddDays(-1)), previous, priorStreak);
            if (next == 0) return 0;
            int reward = ShardRewardForDay(next);
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards", ProfileTransaction.Add(PlayerWallet.EarnedBalance, reward)),
                new ProfileTransaction.Write(DayKey, today),
                new ProfileTransaction.Write(StreakKey, next));
            return reward;
        }

        public static int NextDay(int today, int yesterday, int previous, int streak)
        {
            if (previous >= today) return 0; // duplicate visit or a clock moved backwards
            return previous == yesterday && streak >= 1 && streak <= CycleDays ? streak % CycleDays + 1 : 1;
        }
    }
}

