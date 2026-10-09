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
        public const BotDifficulty InitialDifficulty=BotDifficulty.Normal;
        public const BotDifficulty Lowest=BotDifficulty.VeryEasy;
        public const BotDifficulty Highest=BotDifficulty.Nightmare;
        public const BotDifficulty OnlineDifficulty=BotDifficulty.Hard;

        public static bool IsValid(BotDifficulty value)=>value>=Lowest&&value<=Highest;
        public static BotDifficulty Clamp(BotDifficulty value)=>(BotDifficulty)Mathf.Clamp((int)value,(int)Lowest,(int)Highest);
        public static BotDifficulty Cycle(BotDifficulty value,int direction)
        {
            int count=(int)Highest-(int)Lowest+1;
            int index=(int)Clamp(value)-(int)Lowest+System.Math.Sign(direction);
            return (BotDifficulty)((index%count+count)%count+(int)Lowest);
        }
        public static bool HoldAttackHelpAllowed(BotDifficulty value,bool multiplayer)
            =>value==BotDifficulty.VeryEasy&&!multiplayer;
        public static bool ShowHoldAttackHelp=>HoldAttackHelpAllowed(Current,
            Alterverse.Multiplayer.CoopAuthority.Active||Alterverse.Multiplayer.CoopAuthority.PreparedPlan!=null);

        private static bool hasMatchOverride;
        private static BotDifficulty matchOverride;
        public static BotDifficulty Current=>Alterverse.Multiplayer.CoopAuthority.ActivePlan is Alterverse.Multiplayer.OnlineMatchPlan
            ?OnlineDifficulty:hasMatchOverride?matchOverride:Selected;
        public static BotDifficulty Personal=>Selected;

        public static void BeginMatchOverride(BotDifficulty value)
        {
            matchOverride=IsValid(value)?value:BotDifficulty.Normal;
            hasMatchOverride=true;
        }

        public static void EndMatchOverride(){hasMatchOverride=false;matchOverride=BotDifficulty.Normal;}

        public static BotDifficulty Selected
        {
            get
            {
                // Use Normal only for a new profile or invalid data. Never reset a saved
                // tier/streak at app startup, match end, or when leaving an online override.
                int value=Alterverse.Profile.AccountProfilePrefs.GetInt(Key,(int)InitialDifficulty);
                return IsValid((BotDifficulty)value)?(BotDifficulty)value:InitialDifficulty;
            }
            set
            {
                if(!IsValid(value))value=BotDifficulty.Normal;
                Alterverse.Profile.AccountProfilePrefs.SetInt(Key,(int)value);Alterverse.Profile.AccountProfilePrefs.Save();
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
            if(string.IsNullOrEmpty(matchId)||Alterverse.Profile.AccountProfilePrefs.GetString(AdaptiveReceiptKey,"")==matchId)return false;

            BotDifficulty tier=Selected;
            int wins=won?Mathf.Min(StreakThreshold,Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(WinStreakKey,0))+1):0;
            int losses=won?0:Mathf.Min(StreakThreshold,Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(LossStreakKey,0))+1);
            bool changed=false;

            if(wins>=StreakThreshold)
            {
                if(tier<BotDifficulty.Nightmare){tier++;changed=true;}
                wins=0;losses=0;
            }
            else if(losses>=StreakThreshold)
            {
                if(tier>Lowest){tier--;changed=true;}
                wins=0;losses=0;
            }

            Alterverse.Profile.AccountProfilePrefs.SetInt(Key,(int)tier);
            Alterverse.Profile.AccountProfilePrefs.SetInt(WinStreakKey,wins);
            Alterverse.Profile.AccountProfilePrefs.SetInt(LossStreakKey,losses);
            Alterverse.Profile.AccountProfilePrefs.SetString(AdaptiveReceiptKey,matchId);
            Alterverse.Profile.AccountProfilePrefs.Save();
            return changed;
        }

        public static string Label(BotDifficulty value)=>value switch
        {
            BotDifficulty.VeryEasy=>"VERY EASY",
            BotDifficulty.Easy=>"EASY",
            BotDifficulty.Hard=>"HARD",
            BotDifficulty.Expert=>"EXPERT",
            BotDifficulty.Nightmare=>"NIGHTMARE",
            _=>"MEDIUM"
        };
    }
}


