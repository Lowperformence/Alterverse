using System;
using Alterverse.Content;
using UnityEngine;

namespace Alterverse.Profile
{
    // Progress is committed with the match receipt so retrying that receipt cannot count twice.
    // The reset follows the player's established Europe/Berlin daily boundary, including DST.
    public static class DailyQuestProgress
    {
        private const string DayKey = "DF_DailyQuestDay_v2";
        private const string KillsKey = "DF_DailyQuestKills_v2";
        private const string HotZonesKey = "DF_DailyQuestHotZones_v2";
        private const string SoldierKillsKey = "DF_DailyQuestSoldierKills_v2";
        private const string ClaimedKey = "DF_DailyQuestClaimed_v2";

        private static DateTime BerlinNow
        {
            get
            {
                DateTime utc = DateTime.UtcNow;
                int year = utc.Year;
                DateTime dstStart = LastSunday(year, 3).AddHours(1);  // 01:00 UTC
                DateTime dstEnd = LastSunday(year, 10).AddHours(1);   // 01:00 UTC
                return utc.AddHours(utc >= dstStart && utc < dstEnd ? 2 : 1);
            }
        }

        private static DateTime LastSunday(int year, int month)
        {
            DateTime last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            return last.AddDays(-(int)last.DayOfWeek);
        }

        private static int Today
        {
            get
            {
                DateTime local = BerlinNow;
                return local.Year * 10000 + local.Month * 100 + local.Day;
            }
        }

        private static bool Current => Alterverse.Profile.AccountProfilePrefs.GetInt(DayKey, 0) == Today;

        public static int Target(int index) => index switch
        {
            0 => 50,
            1 => 20,
            2 => 60,
            _ => 0
        };

        public static int Progress(int index)
        {
            if (index < 0 || index >= PlayerChallengeContent.Daily.Length || !Current)
                return 0;

            string key = index switch
            {
                0 => KillsKey,
                1 => HotZonesKey,
                _ => SoldierKillsKey
            };
            return Mathf.Clamp(Alterverse.Profile.AccountProfilePrefs.GetInt(key, 0), 0, Target(index));
        }

        public static bool Claimed(int index) =>
            index >= 0 && index < PlayerChallengeContent.Daily.Length &&
            Current && (Alterverse.Profile.AccountProfilePrefs.GetInt(ClaimedKey, 0) & (1 << index)) != 0;

        public static ProfileTransaction.Write[] MatchWrites(int kills, int hotZoneCaptures, int soldierKills)
        {
            int today = Today;
            int stored = Alterverse.Profile.AccountProfilePrefs.GetInt(DayKey, 0);
            if (stored > today)
                return Array.Empty<ProfileTransaction.Write>();

            bool sameDay = stored == today;
            int priorKills = sameDay ? Alterverse.Profile.AccountProfilePrefs.GetInt(KillsKey, 0) : 0;
            int priorHotZones = sameDay ? Alterverse.Profile.AccountProfilePrefs.GetInt(HotZonesKey, 0) : 0;
            int priorSoldierKills = sameDay ? Alterverse.Profile.AccountProfilePrefs.GetInt(SoldierKillsKey, 0) : 0;
            int claimed = sameDay ? Alterverse.Profile.AccountProfilePrefs.GetInt(ClaimedKey, 0) : 0;

            return new[]
            {
                new ProfileTransaction.Write(DayKey, today),
                new ProfileTransaction.Write(KillsKey, Mathf.Min(Target(0), priorKills + Mathf.Max(0, kills))),
                new ProfileTransaction.Write(HotZonesKey, Mathf.Min(Target(1), priorHotZones + Mathf.Max(0, hotZoneCaptures))),
                new ProfileTransaction.Write(SoldierKillsKey, Mathf.Min(Target(2), priorSoldierKills + Mathf.Max(0, soldierKills))),
                new ProfileTransaction.Write(ClaimedKey, claimed)
            };
        }

        public static bool Claim(int index)
        {
            ProfileTransaction.Recover();
            if (index < 0 || index >= PlayerChallengeContent.Daily.Length ||
                Progress(index) < Target(index) || Claimed(index))
                return false;

            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",
                    ProfileTransaction.Add(PlayerWallet.EarnedBalance, PlayerChallengeContent.Daily[index].Shards)),
                new ProfileTransaction.Write(ClaimedKey,
                    Alterverse.Profile.AccountProfilePrefs.GetInt(ClaimedKey, 0) | (1 << index)));
            return true;
        }
    }
}

