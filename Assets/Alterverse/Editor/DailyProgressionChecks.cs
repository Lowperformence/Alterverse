using System;
using Alterverse.Match;
using Alterverse.Profile;
using UnityEditor;
using UnityEngine;

namespace Alterverse.Editor
{
    public static class DailyProgressionChecks
    {
        [MenuItem("DIMENSIONS FRONT/Validation/Daily quests and login streak")]
        public static void Run()
        {
            int mask = 0;
            mask = DailyQuestProgress.AddWonMap(mask, true, ArenaVariant.Moon);
            mask = DailyQuestProgress.AddWonMap(mask, true, ArenaVariant.Moon);
            Check(DailyQuestProgress.CountWonMaps(mask) == 1, "Repeated map must count once");
            mask = DailyQuestProgress.AddWonMap(mask, false, ArenaVariant.Mars);
            Check(DailyQuestProgress.CountWonMaps(mask) == 1, "Defeat must not count");
            foreach (ArenaVariant map in new[] { ArenaVariant.Mars, ArenaVariant.Earth, ArenaVariant.Kryon, ArenaVariant.Noxara })
                mask = DailyQuestProgress.AddWonMap(mask, true, map);
            Check(DailyQuestProgress.CountWonMaps(mask) == 5, "Five distinct wins");
            Check(DailyQuestProgress.AddWonMap(mask, true, (ArenaVariant)99) == mask, "Invalid map rejected");
            Check(DailyQuestProgress.CountWonMaps(int.MinValue) == 0, "Unknown mask bits rejected");
            Check(LoginStreakProgress.NextDay(20260102, 20260101, 20260102, 4) == 0, "Same-day repeat");
            Check(LoginStreakProgress.NextDay(20260102, 20260101, 20260103, 4) == 0, "Clock rollback");
            Check(LoginStreakProgress.NextDay(20260102, 20260101, 20251230, 4) == 1, "Missed day resets");
            Check(LoginStreakProgress.NextDay(20260102, 20260101, 20260101, 7) == 1, "Seven-day cycle resets");
            Check(LoginStreakProgress.NextDay(20260102, 20260101, 20260101, 99) == 1, "Corrupt streak resets");
            int[] rewards = { 1, 3, 6, 12, 12, 12, 12 };
            for (int i=0; i<rewards.Length; i++)
                Check(LoginStreakProgress.RewardForDay(i+1) == rewards[i], "Reward schedule");
            Check(LoginStreakProgress.RewardForDay(0) == 0 && LoginStreakProgress.RewardForDay(8) == 0, "Invalid reward day");
            Check(DailyCalendar.Stamp(DailyCalendar.BerlinDate(new DateTime(2026, 1, 1, 23, 0, 0, DateTimeKind.Utc))) == 20260102, "Winter midnight");
            Check(DailyCalendar.Stamp(DailyCalendar.BerlinDate(new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc))) == 20260702, "Summer midnight");
            Check(DailyCalendar.Stamp(DailyCalendar.BerlinDate(new DateTime(2026, 3, 29, 22, 0, 0, DateTimeKind.Utc))) == 20260330, "DST start");
            Check(DailyCalendar.Stamp(DailyCalendar.BerlinDate(new DateTime(2026, 10, 25, 22, 30, 0, DateTimeKind.Utc))) == 20261025, "DST end");
            Debug.Log("DIMENSIONS FRONT: daily quest and streak rule checks passed.");
        }

        private static void Check(bool valid, string scenario)
        {
            if (!valid) throw new InvalidOperationException("Daily progression check failed: " + scenario);
        }
    }
}
