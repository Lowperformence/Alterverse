using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { BeforeSceneLoad, SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
    public static class Mathf
    {
        public static int Max(int a,int b) => Math.Max(a,b);
        public static int Clamp(int value,int min,int max) => Math.Max(min,Math.Min(max,value));
    }
    public static class Debug { public static void LogWarning(string message) { } }
    // The fixture retains journal objects; production Unity owns their JSON serialization.
    public static class JsonUtility
    {
        static readonly Dictionary<string,object> objects=new Dictionary<string,object>();
        public static string ToJson(object value)
        { string key=Guid.NewGuid().ToString("N"); objects[key]=value; return key; }
        public static T FromJson<T>(string key) => (T)objects[key];
    }
}
namespace Alterverse.Profile
{
    public static class AccountProfilePrefs
    {
        static readonly Dictionary<string,Dictionary<string,object>> accounts=new Dictionary<string,Dictionary<string,object>>();
        public static string Account="offline";
        public static int FailAfterSaves=-1;
        static Dictionary<string,object> Values
        {
            get
            {
                Dictionary<string,object> values;
                if(!accounts.TryGetValue(Account,out values)) accounts[Account]=values=new Dictionary<string,object>();
                return values;
            }
        }
        public static int GetInt(string key,int fallback=0) => Values.ContainsKey(key)?(int)Values[key]:fallback;
        public static string GetString(string key,string fallback="") => Values.ContainsKey(key)?(string)Values[key]:fallback;
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static void SetInt(string key,int value) { Values[key]=value; }
        public static void SetString(string key,string value) { Values[key]=value; }
        public static void DeleteKey(string key) { Values.Remove(key); }
        public static void Save()
        { if(FailAfterSaves>0&&--FailAfterSaves==0)throw new InvalidOperationException("Simulated interrupted save"); }
    }
    public static class PlayerWallet { public static int EarnedBalance=>AccountProfilePrefs.GetInt("DF_Shards"); }
    public static class PlayerExperience
    {
        public const int MaxLevel=100;
        public static long TotalXp=>long.Parse(AccountProfilePrefs.GetString("DF_AccountXP","0"));
        public static long XpForLevel(int level)=>1000000;
        public static int CalculateMatchXp(bool won,int kills,int assists,int objectives)=>10;
    }
    public static class BattlePassProgress { public static void GrantMatchXp(int xp,string receipt) { } }
    public static class DailyQuestProgress
    {
        public static IEnumerable<ProfileTransaction.Write> MatchWrites(int kills,int captures,int soldiers,bool won,int arena)
        { return new ProfileTransaction.Write[0]; }
    }
}
namespace Alterverse.AI
{
    public enum BotDifficulty { Normal }
    public static class BotDifficultySettings
    {
        public static BotDifficulty Current=>BotDifficulty.Normal;
        public static int RewardBonusFor(int amount,BotDifficulty difficulty)=>0;
        public static void RecordSoloResult(bool won,string receipt) { }
    }
}
namespace Alterverse.Shop { public enum UniversalHeroCosmetic { Original } }
namespace Alterverse.Match
{
    public static class ArenaVariantRuntime { public static int Current=>0; }
    public static class MatchShardRewards { public static int Roll(bool won,int kills,int assists,int objectives)=>1; }
    public static class WingMatchRewardBonus
    {
        public static Alterverse.Shop.UniversalHeroCosmetic MatchCosmetic=>Alterverse.Shop.UniversalHeroCosmetic.Original;
        public static int Percent(Alterverse.Shop.UniversalHeroCosmetic cosmetic)=>0;
        public static int CalculateBonus(int amount,Alterverse.Shop.UniversalHeroCosmetic cosmetic)=>0;
    }
}
namespace Alterverse.Multiplayer { public static class CoopAuthority { public static bool Active; } }
namespace Alterverse.Online
{
    public static class GooglePlayLevelLeaderboard
    {
        public static bool IsAuthenticated=>false;
        public static void SubmitCurrentScore() { }
    }
}
