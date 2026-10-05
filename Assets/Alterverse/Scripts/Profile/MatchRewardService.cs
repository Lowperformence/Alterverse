using System;
using Alterverse.AI;
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

        public static int MatchesCompleted=>Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(MatchesKey,0));
        public static int MatchesWon=>Mathf.Clamp(Alterverse.Profile.AccountProfilePrefs.GetInt(WinsKey,0),0,MatchesCompleted);
        public static int LifetimeKills=>Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(KillsKey,0));
        public static int LifetimeAssists=>Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(AssistsKey,0));
        public static int LifetimeObjectives=>Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(ObjectivesKey,0));

        public static MatchRewardResult GrantMatchReward(bool won,int kills,int assists,int objectives,string matchId=null,int enemyKills=-1,int hotZoneCaptures=0,int soldierKills=0,bool adaptiveDifficulty=true)
        {
            ProfileTransaction.Recover();
            matchId=matchId??Guid.NewGuid().ToString("N");

            if(Alterverse.Profile.AccountProfilePrefs.GetString(ReceiptKey,"")==matchId)
            {
                int storedShards=Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(ReceiptShards,0));
                int storedXp=Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(ReceiptXp,0));
                int storedBaseShards=Alterverse.Profile.AccountProfilePrefs.HasKey(ReceiptBaseShards)?Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(ReceiptBaseShards,0)):storedShards;
                int storedShardBonus=Alterverse.Profile.AccountProfilePrefs.HasKey(ReceiptShardBonus)?Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(ReceiptShardBonus,0)):Mathf.Max(0,storedShards-storedBaseShards);
                int storedBaseXp=Alterverse.Profile.AccountProfilePrefs.HasKey(ReceiptBaseXp)?Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(ReceiptBaseXp,0)):storedXp;
                int storedXpBonus=Alterverse.Profile.AccountProfilePrefs.HasKey(ReceiptXpBonus)?Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(ReceiptXpBonus,0)):Mathf.Max(0,storedXp-storedBaseXp);
                int rawCosmetic=Alterverse.Profile.AccountProfilePrefs.GetInt(ReceiptBonusCosmetic,0);
                UniversalHeroCosmetic storedCosmetic=rawCosmetic>=0&&rawCosmetic<=3?(UniversalHeroCosmetic)rawCosmetic:UniversalHeroCosmetic.Original;

                // Battle Pass intentionally receives BASE account XP only; its own receipt keeps this idempotent.
                BattlePassProgress.GrantMatchXp(storedBaseXp,matchId);
                if(adaptiveDifficulty)BotDifficultySettings.RecordSoloResult(won,matchId);
                return new MatchRewardResult(storedBaseShards,storedShardBonus,storedBaseXp,storedXpBonus,storedCosmetic);
            }

            kills=Mathf.Max(0,kills);assists=Mathf.Max(0,assists);objectives=Mathf.Max(0,objectives);

            // Freeze reward scaling to the difficulty that actually governed this match. In network
            // matches Current is the authoritative lobby override; in solo it is the local hidden tier.
            BotDifficulty rewardDifficulty=BotDifficultySettings.Current;
            UniversalHeroCosmetic cosmetic=WingMatchRewardBonus.MatchCosmetic;
            int baseShards=MatchShardRewards.Roll(won,kills,assists,objectives);
            int difficultyShardBonus=BotDifficultySettings.RewardBonusFor(baseShards,rewardDifficulty);
            int wingShardBase=ProfileTransaction.Add(baseShards,difficultyShardBonus);
            int wingShardBonus=WingMatchRewardBonus.CalculateBonus(wingShardBase,cosmetic);
            int shardBonus=ProfileTransaction.Add(difficultyShardBonus,wingShardBonus);
            int shards=ProfileTransaction.Add(baseShards,shardBonus);

            long before=PlayerExperience.TotalXp;
            long cap=PlayerExperience.XpForLevel(PlayerExperience.MaxLevel);
            int calculatedBaseXp=PlayerExperience.CalculateMatchXp(won,kills,assists,objectives);
            int baseXp=(int)Math.Max(0L,Math.Min(calculatedBaseXp,cap-before));
            int difficultyXpBonus=BotDifficultySettings.RewardBonusFor(baseXp,rewardDifficulty);
            int wingXpBase=ProfileTransaction.Add(baseXp,difficultyXpBonus);
            int wingXpBonus=WingMatchRewardBonus.CalculateBonus(wingXpBase,cosmetic);
            int requestedXpBonus=ProfileTransaction.Add(difficultyXpBonus,wingXpBonus);
            long next=Math.Min(cap,before+(long)baseXp+requestedXpBonus);
            int xp=(int)Math.Max(0L,next-before);
            int xpBonus=Math.Max(0,xp-baseXp);

            int wins=ProfileTransaction.Add(MatchesWon,won?1:0);
            var writes=new System.Collections.Generic.List<ProfileTransaction.Write>
            {
                new ProfileTransaction.Write("DF_Shards",ProfileTransaction.Add(PlayerWallet.EarnedBalance,shards)),
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
            writes.AddRange(DailyQuestProgress.MatchWrites(enemyKills<0?kills:enemyKills,hotZoneCaptures,soldierKills));
            ProfileTransaction.Commit(writes.ToArray());

            // Persist the win before reporting it. Co-op can stay on its own result/lobby
            // flow without visiting MainMenu.ShowHome, so menu-only sync misses these wins.
            if(won&&Alterverse.Online.GooglePlayLevelLeaderboard.IsAuthenticated)
                Alterverse.Online.GooglePlayLevelLeaderboard.SubmitCurrentScore();

            // Difficulty reward bonus accelerates account progression only. Battle Pass keeps its
            // existing base-XP economy, and solo adaptive progression is receipt-safe.
            BattlePassProgress.GrantMatchXp(baseXp,matchId);
            if(adaptiveDifficulty)BotDifficultySettings.RecordSoloResult(won,matchId);
            return new MatchRewardResult(baseShards,shardBonus,baseXp,xpBonus,cosmetic);
        }
    }
}
