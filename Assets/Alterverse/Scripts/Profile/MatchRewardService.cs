using System;
using Alterverse.Match;
using Alterverse.Shop;
using UnityEngine;
namespace Alterverse.Profile
{
    public readonly struct MatchRewardResult
    {
        public readonly int Shards,Xp,BaseShards,ShardBonus,BaseXp,XpBonus,BonusPercent;
        public readonly bool ShardDropped;
        public readonly UniversalHeroCosmetic BonusCosmetic;

        public MatchRewardResult(int shards,int xp)
        {
            Shards=shards;Xp=xp;BaseShards=shards;ShardBonus=0;BaseXp=xp;XpBonus=0;BonusPercent=0;
            ShardDropped=shards>0;BonusCosmetic=UniversalHeroCosmetic.Original;
        }

        public MatchRewardResult(int baseShards,int shardBonus,int baseXp,int xpBonus,UniversalHeroCosmetic bonusCosmetic)
        {
            BaseShards=Mathf.Max(0,baseShards);
            ShardBonus=Mathf.Max(0,shardBonus);
            BaseXp=Mathf.Max(0,baseXp);
            XpBonus=Mathf.Max(0,xpBonus);
            Shards=ProfileTransaction.Add(BaseShards,ShardBonus);
            Xp=ProfileTransaction.Add(BaseXp,XpBonus);
            BonusCosmetic=bonusCosmetic;
            BonusPercent=WingMatchRewardBonus.Percent(bonusCosmetic);
            ShardDropped=Shards>0;
        }
    }

    public static class MatchRewardService
    {
        private const string MatchesKey="DF_MatchesCompleted",WinsKey="DF_MatchesWon",KillsKey="DF_LifetimeKills",AssistsKey="DF_LifetimeAssists",ObjectivesKey="DF_LifetimeObjectives";
        private const string ReceiptKey="DF_LastRewardReceipt",ReceiptShards="DF_LastRewardShards",ReceiptXp="DF_LastRewardXP";
        private const string ReceiptBaseShards="DF_LastRewardBaseShards",ReceiptShardBonus="DF_LastRewardShardBonus";
        private const string ReceiptBaseXp="DF_LastRewardBaseXP",ReceiptXpBonus="DF_LastRewardXPBonus",ReceiptBonusCosmetic="DF_LastRewardBonusCosmetic";

        public static int MatchesCompleted=>Mathf.Max(0,PlayerPrefs.GetInt(MatchesKey,0));
        public static int MatchesWon=>Mathf.Clamp(PlayerPrefs.GetInt(WinsKey,0),0,MatchesCompleted);
        public static int LifetimeKills=>Mathf.Max(0,PlayerPrefs.GetInt(KillsKey,0));
        public static int LifetimeAssists=>Mathf.Max(0,PlayerPrefs.GetInt(AssistsKey,0));
        public static int LifetimeObjectives=>Mathf.Max(0,PlayerPrefs.GetInt(ObjectivesKey,0));

        public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives,string matchId=null,int hotZoneCaptures=0,int soldierKills=0)
        {
            ProfileTransaction.Recover();
            matchId=matchId??Guid.NewGuid().ToString("N");

            if(PlayerPrefs.GetString(ReceiptKey,"")==matchId)
            {
                int storedShards=Mathf.Max(0,PlayerPrefs.GetInt(ReceiptShards,0));
                int storedXp=Mathf.Max(0,PlayerPrefs.GetInt(ReceiptXp,0));
                int storedBaseShards=PlayerPrefs.HasKey(ReceiptBaseShards)?Mathf.Max(0,PlayerPrefs.GetInt(ReceiptBaseShards,0)):storedShards;
                int storedShardBonus=PlayerPrefs.HasKey(ReceiptShardBonus)?Mathf.Max(0,PlayerPrefs.GetInt(ReceiptShardBonus,0)):Mathf.Max(0,storedShards-storedBaseShards);
                int storedBaseXp=PlayerPrefs.HasKey(ReceiptBaseXp)?Mathf.Max(0,PlayerPrefs.GetInt(ReceiptBaseXp,0)):storedXp;
                int storedXpBonus=PlayerPrefs.HasKey(ReceiptXpBonus)?Mathf.Max(0,PlayerPrefs.GetInt(ReceiptXpBonus,0)):Mathf.Max(0,storedXp-storedBaseXp);
                int rawCosmetic=PlayerPrefs.GetInt(ReceiptBonusCosmetic,0);
                UniversalHeroCosmetic storedCosmetic=rawCosmetic>=0&&rawCosmetic<=3?(UniversalHeroCosmetic)rawCosmetic:UniversalHeroCosmetic.Original;

                // Battle Pass intentionally receives BASE account XP only; its own receipt keeps this idempotent.
                BattlePassProgress.GrantMatchXp(storedBaseXp,matchId);
                return new MatchRewardResult(storedBaseShards,storedShardBonus,storedBaseXp,storedXpBonus,storedCosmetic);
            }

            kills=Mathf.Max(0,kills);assists=Mathf.Max(0,assists);objectives=Mathf.Max(0,objectives);

            UniversalHeroCosmetic cosmetic=WingMatchRewardBonus.MatchCosmetic;
            int baseShards=MatchShardRewards.Roll(won,kills,assists,objectives);
            int shardBonus=WingMatchRewardBonus.CalculateBonus(baseShards,cosmetic);
            int shards=ProfileTransaction.Add(baseShards,shardBonus);

            long before=PlayerExperience.TotalXp;
            long cap=PlayerExperience.XpForLevel(PlayerExperience.MaxLevel);
            int calculatedBaseXp=PlayerExperience.CalculateMatchXp(won,kills,assists,objectives);
            int baseXp=(int)Math.Max(0L,Math.Min(calculatedBaseXp,cap-before));
            int requestedXpBonus=WingMatchRewardBonus.CalculateBonus(baseXp,cosmetic);
            long next=Math.Min(cap,before+(long)baseXp+requestedXpBonus);
            int xp=(int)Math.Max(0L,next-before);
            int xpBonus=Math.Max(0,xp-baseXp);

            int wins=ProfileTransaction.Add(MatchesWon,won?1:0);
            var writes=new System.Collections.Generic.List<ProfileTransaction.Write>
            {
                new ProfileTransaction.Write("DF_Shards",ProfileTransaction.Add(PlayerWallet.Balance,shards)),
                new ProfileTransaction.Write("DF_AccountXP",next.ToString()),
                new ProfileTransaction.Write(MatchesKey,ProfileTransaction.Add(MatchesCompleted,1)),
                new ProfileTransaction.Write(WinsKey,wins),
                new ProfileTransaction.Write(KillsKey,ProfileTransaction.Add(LifetimeKills,kills)),
                new ProfileTransaction.Write(AssistsKey,ProfileTransaction.Add(LifetimeAssists,assists)),
                new ProfileTransaction.Write(ObjectivesKey,ProfileTransaction.Add(LifetimeObjectives,objectives)),
                new ProfileTransaction.Write(ReceiptKey,matchId),
                new ProfileTransaction.Write(ReceiptShards,shards),
                new ProfileTransaction.Write(ReceiptXp,xp),
                new ProfileTransaction.Write(ReceiptBaseShards,baseShards),
                new ProfileTransaction.Write(ReceiptShardBonus,shardBonus),
                new ProfileTransaction.Write(ReceiptBaseXp,baseXp),
                new ProfileTransaction.Write(ReceiptXpBonus,xpBonus),
                new ProfileTransaction.Write(ReceiptBonusCosmetic,(int)cosmetic)
            };
            writes.AddRange(DailyQuestProgress.MatchWrites(kills,hotZoneCaptures,soldierKills));
            ProfileTransaction.Commit(writes.ToArray());

            // Wing bonuses accelerate account progression only, never Battle Pass progression.
            BattlePassProgress.GrantMatchXp(baseXp,matchId);
            return new MatchRewardResult(baseShards,shardBonus,baseXp,xpBonus,cosmetic);
        }
    }
}
