using UnityEngine;
namespace Alterverse.AI
{
    public static class BotDifficultySettings
    {
        // Personal difficulty is intentionally separate from the per-match override. This lets
        // offline play use the local hidden skill tier while co-op/online matches can publish one
        // authoritative difficulty for the whole lobby.
        private const string Key="DF_BotAdaptiveTier";
        private const string WinStreakKey="DF_BotAdaptiveWinStreak";
        private const string LossStreakKey="DF_BotAdaptiveLossStreak";
        private const string AdaptiveReceiptKey="DF_BotAdaptiveLastReceipt";
        public const int StreakThreshold=3;

        private static bool hasMatchOverride;
        private static BotDifficulty matchOverride;
        public static BotDifficulty Current=>hasMatchOverride?matchOverride:Selected;
        public static BotDifficulty Personal=>Selected;

        public static void BeginMatchOverride(BotDifficulty value)
        {
            matchOverride=value<BotDifficulty.Easy||value>BotDifficulty.Nightmare?BotDifficulty.Normal:value;
            hasMatchOverride=true;
        }

        public static void EndMatchOverride(){hasMatchOverride=false;matchOverride=BotDifficulty.Normal;}

        public static BotDifficulty Selected
        {
            get
            {
                int value=PlayerPrefs.GetInt(Key,(int)BotDifficulty.Normal);
                return value>=(int)BotDifficulty.Easy&&value<=(int)BotDifficulty.Nightmare?(BotDifficulty)value:BotDifficulty.Normal;
            }
            set
            {
                if(value<BotDifficulty.Easy||value>BotDifficulty.Nightmare)value=BotDifficulty.Normal;
                PlayerPrefs.SetInt(Key,(int)value);PlayerPrefs.Save();
            }
        }

        public static int RewardBonusPercent(BotDifficulty value)
        {
            int tier=Mathf.Clamp((int)value,(int)BotDifficulty.Easy,(int)BotDifficulty.Nightmare);
            return tier*10;
        }

        public static int RewardBonusFor(int baseAmount,BotDifficulty value)
        {
            if(baseAmount<=0)return 0;
            return Mathf.Max(0,Mathf.RoundToInt(baseAmount*(RewardBonusPercent(value)/100f)));
        }

        // Called only for local solo matches. The receipt makes this safe if reward granting retries.
        // Three consecutive wins move up one tier; three consecutive losses move down one tier.
        public static bool RecordSoloResult(bool won,string matchId)
        {
            if(string.IsNullOrEmpty(matchId)||PlayerPrefs.GetString(AdaptiveReceiptKey,"")==matchId)return false;

            BotDifficulty tier=Selected;
            int wins=won?Mathf.Min(StreakThreshold,Mathf.Max(0,PlayerPrefs.GetInt(WinStreakKey,0))+1):0;
            int losses=won?0:Mathf.Min(StreakThreshold,Mathf.Max(0,PlayerPrefs.GetInt(LossStreakKey,0))+1);
            bool changed=false;

            if(wins>=StreakThreshold)
            {
                if(tier<BotDifficulty.Nightmare){tier++;changed=true;}
                wins=0;losses=0;
            }
            else if(losses>=StreakThreshold)
            {
                if(tier>BotDifficulty.Easy){tier--;changed=true;}
                wins=0;losses=0;
            }

            PlayerPrefs.SetInt(Key,(int)tier);
            PlayerPrefs.SetInt(WinStreakKey,wins);
            PlayerPrefs.SetInt(LossStreakKey,losses);
            PlayerPrefs.SetString(AdaptiveReceiptKey,matchId);
            PlayerPrefs.Save();
            return changed;
        }

        public static string Label(BotDifficulty value)=>value switch
        {
            BotDifficulty.Easy=>"EASY",
            BotDifficulty.Hard=>"HARD",
            BotDifficulty.Expert=>"EXPERT",
            BotDifficulty.Nightmare=>"NIGHTMARE",
            _=>"MEDIUM"
        };
    }
}

