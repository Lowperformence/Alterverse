using UnityEngine;
namespace Alterverse.AI
{
    public static class BotDifficultySettings
    {
        private const string Key="DF_BotDifficulty";
        public static BotDifficulty Selected
        {
            get
            {
                int value=PlayerPrefs.GetInt(Key,(int)BotDifficulty.Normal);
                return value>=0&&value<=2?(BotDifficulty)value:BotDifficulty.Normal;
            }
            set
            {
                if(value<BotDifficulty.Easy||value>BotDifficulty.Hard)value=BotDifficulty.Normal;
                PlayerPrefs.SetInt(Key,(int)value);PlayerPrefs.Save();
            }
        }
        public static string Label(BotDifficulty value)=>value switch{BotDifficulty.Easy=>"EASY",BotDifficulty.Hard=>"HARD",_=>"NORMAL"};
    }
}
