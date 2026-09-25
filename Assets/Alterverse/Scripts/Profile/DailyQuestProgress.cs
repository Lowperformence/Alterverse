using System;
using Alterverse.Content;
using UnityEngine;

namespace Alterverse.Profile
{
    // Three repeatable UTC-day quests. Progress is committed with the match receipt,
    // so retrying the same match cannot count it twice.
    public static class DailyQuestProgress
    {
        private const string DayKey="DF_DailyQuestDay_v1";
        private const string MatchesKey="DF_DailyQuestMatches_v1";
        private const string KillsKey="DF_DailyQuestKills_v1";
        private const string ObjectivesKey="DF_DailyQuestObjectives_v1";
        private const string ClaimedKey="DF_DailyQuestClaimed_v1";
        private static int Today=>int.Parse(DateTime.UtcNow.ToString("yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture),System.Globalization.CultureInfo.InvariantCulture);
        private static bool Current=>PlayerPrefs.GetInt(DayKey,0)==Today;
        public static int Target(int index)=>index==0?2:index==1?10:5;
        public static int Progress(int index)
        {
            if(index<0||index>=PlayerChallengeContent.Daily.Length||!Current)return 0;
            string key=index==0?MatchesKey:index==1?KillsKey:ObjectivesKey;
            return Mathf.Clamp(PlayerPrefs.GetInt(key,0),0,Target(index));
        }
        public static bool Claimed(int index)=>index>=0&&index<PlayerChallengeContent.Daily.Length&&Current&&
            (PlayerPrefs.GetInt(ClaimedKey,0)&(1<<index))!=0;
        public static ProfileTransaction.Write[] MatchWrites(int kills,int objectives)
        {
            int today=Today;
            // Never move an existing day backward if a device clock is rolled back.
            int stored=PlayerPrefs.GetInt(DayKey,0);
            if(stored>today)return Array.Empty<ProfileTransaction.Write>();
            bool same=stored==today;
            return new[]
            {
                new ProfileTransaction.Write(DayKey,today),
                new ProfileTransaction.Write(MatchesKey,Mathf.Min(2,(same?PlayerPrefs.GetInt(MatchesKey,0):0)+1)),
                new ProfileTransaction.Write(KillsKey,Mathf.Min(10,(same?PlayerPrefs.GetInt(KillsKey,0):0)+Mathf.Max(0,kills))),
                new ProfileTransaction.Write(ObjectivesKey,Mathf.Min(5,(same?PlayerPrefs.GetInt(ObjectivesKey,0):0)+Mathf.Max(0,objectives))),
                new ProfileTransaction.Write(ClaimedKey,same?PlayerPrefs.GetInt(ClaimedKey,0):0)
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
