using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Color { public Color(float r,float g,float b,float a=1){} }
    public static class Debug { public static void Log(object message)=>Console.WriteLine(message); }
    public static class Mathf
    {
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static float Floor(float a)=>(float)Math.Floor(a);
        public static int FloorToInt(float a)=>(int)Math.Floor(a);
        public static int CeilToInt(float a)=>(int)Math.Ceiling(a);
        public static int RoundToInt(float a)=>(int)Math.Round(a,MidpointRounding.ToEven);
        public static int Clamp(int a,int min,int max)=>Math.Min(Math.Max(a,min),max);
        public static float Clamp(float a,float min,float max)=>Math.Min(Math.Max(a,min),max);
    }
}
namespace UnityEditor { public sealed class MenuItem:Attribute { public MenuItem(string label){} } }
namespace Alterverse.Characters
{
    public enum HeroId {Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy,VoidKnight}
    public static class HeroIdentity { public const int HeroCount=10; }
    public static class PlayerHeroSelection { public static HeroId Selected;public static void Select(HeroId hero)=>Selected=hero; }
}
namespace Alterverse.Profile
{
    public static class ProfileTransaction
    {
        public readonly struct Write { public readonly string key;public readonly int number;public Write(string k,int n){key=k;number=n;} }
        public static void Recover(){}
        public static void Commit(params Write[] writes)=>throw new Exception("Live profile writes are forbidden in this fixture.");
    }
    public static class AccountProfilePrefs
    {
        public static bool HasKey(string key)=>false;
        public static int GetInt(string key,int fallback=0)=>fallback;
    }
    public static class PlayerWallet { public static int Balance=>0;public static int EarnedBalance=>0;public static bool BeginServerPurchase(string sku)=>false; }
}
namespace Alterverse.Shop
{
    public enum AvatarPurchaseResult {InvalidItem,AlreadyOwned,InsufficientShards,Pending,Purchased}
    public class PaidShardCommerce { public static PaidShardCommerce Instance;public bool Ready;public static bool OwnsVoidfall=>false; }
}
namespace Alterverse.Audio
{
    public enum DimensionSound {Purchase}
    public static class DimensionAudio {public static void Play(DimensionSound sound,float volume){}}
}
