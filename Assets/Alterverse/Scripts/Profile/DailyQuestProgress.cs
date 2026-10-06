using System;
using Alterverse.Content;
using Alterverse.Match;
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

        private const string MapsKey = "DF_DailyQuestWonMaps_v1";
        private static int Today => DailyCalendar.Today;

        private static bool Current => Alterverse.Profile.AccountProfilePrefs.GetInt(DayKey, 0) == Today;

        public static int Target(int index) => index switch
        {
            0 => 50,
            1 => 20,
            2 => 60,
            3 => 5,
            _ => 0
        };

        public static int Progress(int index)
        {
            if (index < 0 || index >= PlayerChallengeContent.Daily.Length || !Current)
                return 0;

            if (index == 3) return Mathf.Min(5, CountWonMaps(Alterverse.Profile.AccountProfilePrefs.GetInt(MapsKey, 0)));

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

        public static ProfileTransaction.Write[] MatchWrites(int kills, int hotZoneCaptures, int soldierKills, bool won = false, ArenaVariant arena = ArenaVariant.Moon)
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

            int maps = sameDay ? Alterverse.Profile.AccountProfilePrefs.GetInt(MapsKey, 0) : 0;
            maps = AddWonMap(maps, won, arena);

            return new[]
            {
                new ProfileTransaction.Write(DayKey, today),
                new ProfileTransaction.Write(KillsKey, Mathf.Min(Target(0), ProfileTransaction.Add(priorKills, Mathf.Max(0, kills)))),
                new ProfileTransaction.Write(HotZonesKey, Mathf.Min(Target(1), ProfileTransaction.Add(priorHotZones, Mathf.Max(0, hotZoneCaptures)))),
                new ProfileTransaction.Write(SoldierKillsKey, Mathf.Min(Target(2), ProfileTransaction.Add(priorSoldierKills, Mathf.Max(0, soldierKills)))),
                new ProfileTransaction.Write(ClaimedKey, claimed),
                new ProfileTransaction.Write(MapsKey, maps)
            };
        }

        public static int AddWonMap(int mask, bool won, ArenaVariant arena)
        {
            int valid = 0;
            foreach (ArenaVariant map in Enum.GetValues(typeof(ArenaVariant)))
                if ((int)map >= 0 && (int)map < 31) valid |= 1 << (int)map;
            mask &= valid;
            if (won && Enum.IsDefined(typeof(ArenaVariant), arena) && (int)arena >= 0 && (int)arena < 31)
                mask |= 1 << (int)arena;
            return mask;
        }

        public static int CountWonMaps(int mask)
        {
            mask = AddWonMap(mask, false, ArenaVariant.Moon);
            int count = 0;
            while (mask != 0) { mask &= mask - 1; count++; }
            return count;
        }

        public static bool Claim(int index)
        {
            ProfileTransaction.Recover();
            if (!Current || index < 0 || index >= PlayerChallengeContent.Daily.Length ||
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


