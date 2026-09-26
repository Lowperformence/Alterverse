using System;
using Alterverse.Content;
using UnityEngine;

namespace Alterverse.Profile
{
    /// <summary>Three claim-once daily challenges, reset by the Europe/Berlin calendar day.</summary>
    public static class DailyQuestProgress
    {
        private const string DayKey="DF_DailyQuestDay_v2";
        private const string KillsKey="DF_DailyQuestKills_v2";
        private const string HotZonesKey="DF_DailyQuestHotZones_v2";
        private const string SoldiersKey="DF_DailyQuestSoldiers_v2";
        private const string ClaimedKey="DF_DailyQuestClaimed_v2";

        private static DateTime BerlinToday
        {
            get
            {
                DateTime utc=DateTime.UtcNow;
                // Berlin switches at 01:00 UTC on the last Sunday of March and October.
                DateTime start=LastSunday(utc.Year,3).AddHours(1);
                DateTime end=LastSunday(utc.Year,10).AddHours(1);
                return utc.AddHours(utc>=start&&utc<end?2:1).Date;
            }
        }
        private static int Today=>BerlinToday.Year*10000+BerlinToday.Month*100+BerlinToday.Day;
        private static DateTime LastSunday(int year,int month)
        {
            DateTime last=new DateTime(year,month,DateTime.DaysInMonth(year,month));
            return last.AddDays(-(int)last.DayOfWeek);
        }
        private static bool Current=>PlayerPrefs.GetInt(DayKey,0)==Today;
        public static int Target(int index)=>index==0?50:index==1?20:index==2?60:0;
        public static int Progress(int index)
        {
            if(index<0||index>=PlayerChallengeContent.Daily.Length||!Current)return 0;
            string key=index==0?KillsKey:index==1?HotZonesKey:SoldiersKey;
            return Mathf.Clamp(PlayerPrefs.GetInt(key,0),0,Target(index));
        }
        public static bool Claimed(int index)=>index>=0&&index<PlayerChallengeContent.Daily.Length&&Current&&
            (PlayerPrefs.GetInt(ClaimedKey,0)&(1<<index))!=0;

        public static ProfileTransaction.Write[] MatchWrites(int heroKills,int soldierKills,int hotZoneWins)
        {
            int today=Today,stored=PlayerPrefs.GetInt(DayKey,0);
            // A rolled-back device clock must not reopen an earlier reward day.
            if(stored>today)return Array.Empty<ProfileTransaction.Write>();
            bool same=stored==today;
            int priorKills=same?PlayerPrefs.GetInt(KillsKey,0):0;
            int priorZones=same?PlayerPrefs.GetInt(HotZonesKey,0):0;
            int priorSoldiers=same?PlayerPrefs.GetInt(SoldiersKey,0):0;
            int claimed=same?PlayerPrefs.GetInt(ClaimedKey,0):0;
            return new[]
            {
                new ProfileTransaction.Write(DayKey,today),
                new ProfileTransaction.Write(KillsKey,Mathf.Min(50,priorKills+Mathf.Max(0,heroKills)+Mathf.Max(0,soldierKills))),
                new ProfileTransaction.Write(HotZonesKey,Mathf.Min(20,priorZones+Mathf.Max(0,hotZoneWins))),
                new ProfileTransaction.Write(SoldiersKey,Mathf.Min(60,priorSoldiers+Mathf.Max(0,soldierKills))),
                new ProfileTransaction.Write(ClaimedKey,claimed)
            };
        }

        public static bool Claim(int index)
        {
            ProfileTransaction.Recover();
            if(index<0||index>=PlayerChallengeContent.Daily.Length||Progress(index)<Target(index)||Claimed(index))return false;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",ProfileTransaction.Add(PlayerWallet.Balance,PlayerChallengeContent.Daily[index].Shards)),
                new ProfileTransaction.Write(ClaimedKey,PlayerPrefs.GetInt(ClaimedKey,0)|(1<<index)));
            return true;
        }
    }
}
