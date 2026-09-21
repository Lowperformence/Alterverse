using UnityEngine;
namespace Alterverse.AI
{
    public static class BotDifficultySettings
    {
        private const string Key="DF_BotDifficulty";
        private static bool hasMatchOverride;
        private static BotDifficulty matchOverride;
        public static BotDifficulty Current=>hasMatchOverride?matchOverride:Selected;

        public static void BeginMatchOverride(BotDifficulty value)
        {
            matchOverride=value<BotDifficulty.Easy||value>BotDifficulty.Expert?BotDifficulty.Normal:value;
            hasMatchOverride=true;
        }

        public static void EndMatchOverride(){hasMatchOverride=false;matchOverride=BotDifficulty.Normal;}

        public static BotDifficulty Selected
        {
            get
            {
                int value=PlayerPrefs.GetInt(Key,(int)BotDifficulty.Normal);
                return value>=0&&value<=3?(BotDifficulty)value:BotDifficulty.Normal;
            }
            set
            {
                if(value<BotDifficulty.Easy||value>BotDifficulty.Expert)value=BotDifficulty.Normal;
                PlayerPrefs.SetInt(Key,(int)value);PlayerPrefs.Save();
            }
        }
        public static string Label(BotDifficulty value)=>value switch
        {
            BotDifficulty.Easy=>"EASY",
            BotDifficulty.Hard=>"HARD",
            BotDifficulty.Expert=>"EXPERT",
            _=>"MEDIUM"
        };
    }
}
