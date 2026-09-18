using System;
using Alterverse.Match;
using UnityEngine;
namespace Alterverse.Profile
{
    public readonly struct MatchRewardResult
    {
        public readonly int Shards,Xp;public readonly bool ShardDropped;
        public MatchRewardResult(int shards,int xp){Shards=shards;Xp=xp;ShardDropped=shards>0;}
    }
    public static class MatchRewardService
    {
        private const string MatchesKey="DF_MatchesCompleted",WinsKey="DF_MatchesWon",KillsKey="DF_LifetimeKills",AssistsKey="DF_LifetimeAssists",ObjectivesKey="DF_LifetimeObjectives";
        private const string ReceiptKey="DF_LastRewardReceipt",ReceiptShards="DF_LastRewardShards",ReceiptXp="DF_LastRewardXP";
        public static int MatchesCompleted=>Mathf.Max(0,PlayerPrefs.GetInt(MatchesKey,0));
        public static int MatchesWon=>Mathf.Clamp(PlayerPrefs.GetInt(WinsKey,0),0,MatchesCompleted);
        public static int LifetimeKills=>Mathf.Max(0,PlayerPrefs.GetInt(KillsKey,0));
        public static int LifetimeAssists=>Mathf.Max(0,PlayerPrefs.GetInt(AssistsKey,0));
        public static int LifetimeObjectives=>Mathf.Max(0,PlayerPrefs.GetInt(ObjectivesKey,0));
        public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives,string matchId=null)
        {
            ProfileTransaction.Recover();
            matchId=matchId??Guid.NewGuid().ToString("N");
            if(PlayerPrefs.GetString(ReceiptKey,"")==matchId)
                return new MatchRewardResult(PlayerPrefs.GetInt(ReceiptShards),PlayerPrefs.GetInt(ReceiptXp));
            kills=Mathf.Max(0,kills);assists=Mathf.Max(0,assists);objectives=Mathf.Max(0,objectives);
            int shards=MatchShardRewards.Roll(won,kills,assists,objectives);
            long before=PlayerExperience.TotalXp;
            long next=Math.Min(PlayerExperience.XpForLevel(PlayerExperience.MaxLevel),before+PlayerExperience.CalculateMatchXp(won,kills,assists,objectives));
            int xp=(int)(next-before);
            int wins=ProfileTransaction.Add(MatchesWon,won?1:0);
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",ProfileTransaction.Add(PlayerWallet.Balance,shards)),
                new ProfileTransaction.Write("DF_AccountXP",next.ToString()),
                new ProfileTransaction.Write(MatchesKey,ProfileTransaction.Add(MatchesCompleted,1)),
                new ProfileTransaction.Write(WinsKey,wins),
                new ProfileTransaction.Write(KillsKey,ProfileTransaction.Add(LifetimeKills,kills)),
                new ProfileTransaction.Write(AssistsKey,ProfileTransaction.Add(LifetimeAssists,assists)),
                new ProfileTransaction.Write(ObjectivesKey,ProfileTransaction.Add(LifetimeObjectives,objectives)),
                new ProfileTransaction.Write(ReceiptKey,matchId),
                new ProfileTransaction.Write(ReceiptShards,shards),new ProfileTransaction.Write(ReceiptXp,xp));
            return new MatchRewardResult(shards,xp);
        }
    }
}
