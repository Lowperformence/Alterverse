using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Profile
{
    public readonly struct BattlePassReward
    {
        public readonly int Shards;
        public readonly BattleItemType Item;
        public readonly int ItemAmount;
        public readonly int AvatarId;

        public BattlePassReward(int shards=0,BattleItemType item=BattleItemType.None,int itemAmount=0,int avatarId=-1)
        {
            Shards=Mathf.Max(0,shards);
            Item=item;
            ItemAmount=Mathf.Max(0,itemAmount);
            AvatarId=avatarId;
        }

        public static BattlePassReward Empty=>new BattlePassReward(0,BattleItemType.None,0,-1);
        public bool IsEmpty=>Shards<=0&&(Item==BattleItemType.None||ItemAmount<=0)&&AvatarId<0;

        public string Label
        {
            get
            {
                if(IsEmpty)return "—";
                List<string> parts=new();
                if(Shards>0)parts.Add(Shards+" SHARDS");
                if(Item!=BattleItemType.None&&ItemAmount>0)parts.Add(ItemAmount+"× "+BattleItemInventory.Name(Item));
                if(AvatarId>=0)parts.Add(AvatarId==BattlePassProgress.BetaCrownAvatarId?"BETA CROWN":"AVATAR");
                return string.Join(" + ",parts);
            }
        }
    }

    public enum BattlePassPurchaseResult
    {
        Unlocked,
        AlreadyOwned,
        InsufficientShards
    }

    public static class BattlePassProgress
    {
        public const string SeasonId="BETA_SEASON_01";
        public const string SeasonName="BETA SEASON 01";
        public const int MaxLevel=50;
        public const int PremiumPrice=1000;
        public const int PremiumShardReturn=800;
        public const int BetaCrownAvatarId=16;

        private const string CurrentSeasonKey="DF_BattlePassCurrentSeason";
        private const string Prefix="DF_BP_BETA01_";
        private const string XpKey=Prefix+"XP";
        private const string PremiumKey=Prefix+"Premium";
        private const string FreeClaimKey=Prefix+"FreeClaims";
        private const string PremiumClaimKey=Prefix+"PremiumClaims";
        private const string LastXpReceiptKey=Prefix+"LastXpReceipt";
        private const string LastSeenLevelKey=Prefix+"LastSeenLevel";

        public static int Xp
        {
            get
            {
                EnsureSeasonMarker();
                return Mathf.Clamp(PlayerPrefs.GetInt(XpKey,0),0,TotalXpRequired);
            }
        }

        public static int Level=>LevelForXp(Xp);
        public static int XpIntoLevel=>Level>=MaxLevel?0:Xp-TotalXpForLevel(Level);
        public static int XpToNextLevel=>Level>=MaxLevel?0:TotalXpForLevel(Level+1)-Xp;
        public static float LevelProgress=>Level>=MaxLevel?1f:(float)XpIntoLevel/XpToAdvance(Level);
        public static int TotalXpRequired=>TotalXpForLevel(MaxLevel);
        public static bool PremiumUnlocked=>PlayerPrefs.GetInt(PremiumKey,0)==1;
        public static bool BetaCrownUnlocked=>AvatarInventory.IsOwned(BetaCrownAvatarId);
        public static int LastSeenLevel=>Mathf.Clamp(PlayerPrefs.GetInt(LastSeenLevelKey,1),1,MaxLevel);
        public static bool HasUnseenLevel=>Level>LastSeenLevel;

        public static int XpToAdvance(int level)
        {
            if(level<1||level>=MaxLevel)return 0;
            // 160 XP for the first step, rising by 14 each tier.
            // Sum across 49 advances = 24,304 BP XP.
            return 160+14*(level-1);
        }

        public static int TotalXpForLevel(int level)
        {
            level=Mathf.Clamp(level,1,MaxLevel);
            int transitions=level-1;
            return transitions*160+14*transitions*(transitions-1)/2;
        }

        public static int LevelForXp(int xp)
        {
            xp=Mathf.Clamp(xp,0,TotalXpRequired);
            int low=1,high=MaxLevel;
            while(low<high)
            {
                int mid=(low+high+1)/2;
                if(TotalXpForLevel(mid)<=xp)low=mid;
                else high=mid-1;
            }
            return low;
        }

        public static int GrantMatchXp(int earnedAccountXp,string matchReceipt)
        {
            if(earnedAccountXp<=0||string.IsNullOrEmpty(matchReceipt))return 0;
            EnsureSeasonMarker();
            if(PlayerPrefs.GetString(LastXpReceiptKey,"")==matchReceipt)return 0;

            int before=Xp;
            int next=Mathf.Min(TotalXpRequired,ProfileTransaction.Add(before,earnedAccountXp));
            ProfileTransaction.Commit(
                new ProfileTransaction.Write(XpKey,next),
                new ProfileTransaction.Write(LastXpReceiptKey,matchReceipt));
            return next-before;
        }

        public static BattlePassPurchaseResult UnlockPremium()
        {
            EnsureSeasonMarker();
            if(PremiumUnlocked)return BattlePassPurchaseResult.AlreadyOwned;
            if(!PlayerWallet.CanAfford(PremiumPrice))return BattlePassPurchaseResult.InsufficientShards;

            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-PremiumPrice),
                new ProfileTransaction.Write(PremiumKey,1));
            return BattlePassPurchaseResult.Unlocked;
        }

        public static BattlePassReward FreeRewardFor(int level)
        {
            return level switch
            {
                1=>new BattlePassReward(item:BattleItemType.Heal,itemAmount:1),
                5=>new BattlePassReward(item:BattleItemType.Shield,itemAmount:1),
                10=>new BattlePassReward(item:BattleItemType.Speed,itemAmount:1),
                15=>new BattlePassReward(item:BattleItemType.Meteor,itemAmount:1),
                20=>new BattlePassReward(item:BattleItemType.Heal,itemAmount:1),
                25=>new BattlePassReward(item:BattleItemType.Shield,itemAmount:1),
                30=>new BattlePassReward(item:BattleItemType.Speed,itemAmount:1),
                35=>new BattlePassReward(item:BattleItemType.Meteor,itemAmount:1),
                40=>new BattlePassReward(item:BattleItemType.Heal,itemAmount:2),
                45=>new BattlePassReward(item:BattleItemType.Shield,itemAmount:2),
                50=>new BattlePassReward(item:BattleItemType.Speed,itemAmount:2),
                _=>BattlePassReward.Empty
            };
        }

        public static BattlePassReward PremiumRewardFor(int level)
        {
            if(level<1||level>MaxLevel)return BattlePassReward.Empty;

            int shards=level switch
            {
                10=>50,
                20=>75,
                30=>100,
                40=>100,
                45=>125,
                49=>150,
                50=>200,
                _=>0
            };

            BattleItemType item=(BattleItemType)(1+((level-1)%4));
            int amount=level>=41?2:1;
            if(level==15||level==25||level==35||level==45)amount=3;
            if(level==50)amount=3;
            int avatar=level==50?BetaCrownAvatarId:-1;
            return new BattlePassReward(shards,item,amount,avatar);
        }

        public static bool FreeClaimed(int level)=>Claimed(FreeClaimKey,level);
        public static bool PremiumClaimed(int level)=>Claimed(PremiumClaimKey,level);

        public static bool CanClaimFree(int level)
        {
            BattlePassReward reward=FreeRewardFor(level);
            return !reward.IsEmpty&&level<=Level&&!FreeClaimed(level);
        }

        public static bool CanClaimPremium(int level)
        {
            BattlePassReward reward=PremiumRewardFor(level);
            return PremiumUnlocked&&!reward.IsEmpty&&level<=Level&&!PremiumClaimed(level);
        }

        public static bool ClaimFree(int level)=>Claim(level,false);
        public static bool ClaimPremium(int level)=>Claim(level,true);

        public static int ClaimAllAvailable()
        {
            int claimed=0;
            for(int level=1;level<=Level;level++)
            {
                if(CanClaimFree(level)&&ClaimFree(level))claimed++;
                if(CanClaimPremium(level)&&ClaimPremium(level))claimed++;
            }
            return claimed;
        }

        public static int AvailableClaimCount()
        {
            int count=0;
            for(int level=1;level<=Level;level++)
            {
                if(CanClaimFree(level))count++;
                if(CanClaimPremium(level))count++;
            }
            return count;
        }

        public static int PremiumShardTotal()
        {
            int total=0;
            for(int level=1;level<=MaxLevel;level++)total+=PremiumRewardFor(level).Shards;
            return total;
        }

        public static int NextRewardLevel()
        {
            if(Level>=MaxLevel)return MaxLevel;
            return Level+1;
        }

        public static void AcknowledgeCurrentLevel()
        {
            PlayerPrefs.SetInt(LastSeenLevelKey,Level);
            PlayerPrefs.Save();
        }

        public static string Status(bool premium,int level)
        {
            BattlePassReward reward=premium?PremiumRewardFor(level):FreeRewardFor(level);
            if(reward.IsEmpty)return "NO REWARD";
            if(premium&&!PremiumUnlocked)return "PREMIUM";
            bool claimed=premium?PremiumClaimed(level):FreeClaimed(level);
            if(claimed)return "CLAIMED";
            return level<=Level?"AVAILABLE":"LOCKED";
        }

        private static bool Claim(int level,bool premium)
        {
            if(level<1||level>MaxLevel)return false;
            BattlePassReward reward=premium?PremiumRewardFor(level):FreeRewardFor(level);
            if(reward.IsEmpty||level>Level)return false;
            if(premium&&!PremiumUnlocked)return false;

            string maskKey=premium?PremiumClaimKey:FreeClaimKey;
            ulong mask=ReadMask(maskKey);
            ulong bit=1UL<<(level-1);
            if((mask&bit)!=0)return false;

            List<ProfileTransaction.Write> writes=new();
            if(reward.Shards>0)
                writes.Add(new ProfileTransaction.Write("DF_Shards",ProfileTransaction.Add(PlayerWallet.Balance,reward.Shards)));
            if(reward.Item!=BattleItemType.None&&reward.ItemAmount>0)
                writes.Add(new ProfileTransaction.Write(BattleItemInventory.StorageKey(reward.Item),
                    ProfileTransaction.Add(BattleItemInventory.Count(reward.Item),reward.ItemAmount)));
            if(reward.AvatarId>=0)
                writes.Add(new ProfileTransaction.Write(AvatarInventory.StorageKey(reward.AvatarId),1));

            mask|=bit;
            writes.Add(new ProfileTransaction.Write(maskKey,mask.ToString()));
            ProfileTransaction.Commit(writes.ToArray());

            if(reward.AvatarId>=0)PlayerProfile.Avatar=reward.AvatarId;
            return true;
        }

        private static bool Claimed(string key,int level)
        {
            if(level<1||level>MaxLevel)return false;
            return (ReadMask(key)&(1UL<<(level-1)))!=0;
        }

        private static ulong ReadMask(string key)
        {
            string raw=PlayerPrefs.GetString(key,"0");
            return ulong.TryParse(raw,out ulong value)?value:0UL;
        }

        private static void EnsureSeasonMarker()
        {
            if(PlayerPrefs.GetString(CurrentSeasonKey,"")==SeasonId)return;
            PlayerPrefs.SetString(CurrentSeasonKey,SeasonId);
            PlayerPrefs.Save();
        }
    }
}
