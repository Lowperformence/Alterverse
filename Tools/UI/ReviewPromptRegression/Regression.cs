using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using Alterverse.Profile;
using Alterverse.UI;

// Only engine, reward-economy and native SDK boundaries are faked. Scheduling,
// receipt handling, account isolation/cache and review UI are production sources.
namespace UnityEngine
{
    public class MonoBehaviour { }
    public struct Color { public Color(float r,float g,float b,float a=1){} public static Color white=>new(); }
    public struct Rect { public float x,y,width,height; public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;} }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 operator+(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);public static Vector2 operator*(Vector2 a,float b)=>new(a.x*b,a.y*b); }
    public static class Mathf { public const float PI=(float)Math.PI; public static int Max(int a,int b)=>Math.Max(a,b); public static int Clamp(int a,int b,int c)=>Math.Clamp(a,b,c);public static float Min(float a,float b)=>Math.Min(a,b);public static float Cos(float a)=>(float)Math.Cos(a);public static float Sin(float a)=>(float)Math.Sin(a); }
    public static class Time { static Stopwatch clock=Stopwatch.StartNew();public static float unscaledTime=100,Advance;public static float realtimeSinceStartup=>(float)clock.Elapsed.TotalSeconds+Advance; }
    public static class Screen { public static Rect safeArea=new(0,0,1920,1080); }
    public static class GUI { public static Color color;public static bool enabled; }
    public static class GUIUtility { public static int hotControl; }
    public static class Debug { public static void LogWarning(object value){} }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute:Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value){} }
    public class TextAsset { public string text; }
    public static class Resources { public static T Load<T>(string path) where T:class=>null; }
    public static class Application { public static string identifier="com.example.dimensionsfront";public static readonly List<string> Urls=new();public static void OpenURL(string url)=>Urls.Add(url); }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string,object> values=new();
        public static int GetInt(string key,int fallback=0)=>values.TryGetValue(key,out var n)&&n is int i?i:fallback;
        public static float GetFloat(string key,float fallback=0)=>values.TryGetValue(key,out var n)&&n is float i?i:fallback;
        public static string GetString(string key,string fallback="")=>values.TryGetValue(key,out var n)&&n is string s?s:fallback;
        public static bool HasKey(string key)=>values.ContainsKey(key);
        public static void SetInt(string key,int value)=>values[key]=value;
        public static void SetFloat(string key,float value)=>values[key]=value;
        public static void SetString(string key,string value)=>values[key]=value;
        public static void DeleteKey(string key)=>values.Remove(key);
        public static void Save(){}
    }
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions options=new(){IncludeFields=true,TypeInfoResolver=new DefaultJsonTypeInfoResolver{Modifiers={info=>{if(info.Kind==JsonTypeInfoKind.Object)info.CreateObject=()=>System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(info.Type);}}}};
        public static string ToJson(object value)=>JsonSerializer.Serialize(value,options);
        public static T FromJson<T>(string json)=>JsonSerializer.Deserialize<T>(json,options);
    }
    public class AndroidJavaObject:IDisposable
    {
        public static bool StoreUnavailable; public static int StoreOpens;
        public AndroidJavaObject(string name,params object[] args){}
        public T Call<T>(string name,params object[] args)=>(T)(object)new AndroidJavaObject(name);
        public void Call(string name,params object[] args){if(name=="startActivity"){if(StoreUnavailable)throw new Exception();StoreOpens++;}}
        public void Dispose(){}
    }
    public class AndroidJavaClass:AndroidJavaObject
    {
        public AndroidJavaClass(string name):base(name){}
        public T GetStatic<T>(string name)=>(T)(object)new AndroidJavaObject(name);
        public T CallStatic<T>(string name,params object[] args)=>(T)(object)new AndroidJavaObject(name);
    }
}
namespace Google.Play.Review
{
    public enum ReviewErrorCode { NoError, ErrorRequestingFlow, ErrorLaunchingFlow }
    public sealed class PlayReviewInfo { }
    public sealed class Operation<T> { public volatile bool IsDone=true;public ReviewErrorCode Error;public T GetResult()=>default; }
    public sealed class ReviewManager
    {
        public static Operation<PlayReviewInfo> Request=new(); public static Operation<object> Launch=new();
        public static int Requests,Launches;public static bool Throws;
        public Operation<PlayReviewInfo> RequestReviewFlow(){Requests++;if(Throws)throw new Exception();return Request;}
        public Operation<object> LaunchReviewFlow(PlayReviewInfo info){Launches++;return Launch;}
        public static void Reset(){Request=new();Launch=new();Requests=Launches=0;Throws=false;}
    }
}
namespace Alterverse.Match { public static class MatchExit{public static bool Leaving;}public static class MatchShardRewards{public static int Roll(bool won,int kills,int assists,int objectives)=>0;}public static class ArenaVariantRuntime{public static int Current=>0;}public static class WingMatchRewardBonus{public static Alterverse.Shop.UniversalHeroCosmetic MatchCosmetic=>0;public static float Percent(Alterverse.Shop.UniversalHeroCosmetic c)=>0;public static int CalculateBonus(int n,Alterverse.Shop.UniversalHeroCosmetic c)=>0;} }
namespace Alterverse.Multiplayer { public static class CoopAuthority{public static bool Active;} }
namespace Alterverse.Shop { public enum UniversalHeroCosmetic{Original} }
namespace Alterverse.AI { public enum BotDifficulty{Normal}public static class BotDifficultySettings{public static BotDifficulty Current=>0;public static int RewardBonusFor(int n,BotDifficulty d)=>0;public static void RecordSoloResult(bool won,string id){}} }
namespace Alterverse.Online { public static class GooglePlayLevelLeaderboard{public static bool IsAuthenticated=>false;public static void SubmitCurrentScore(){}} }
namespace Alterverse.Profile
{
    public static class SafeIdentityCatalog{public static bool IsPlayerName(string value)=>true;}
    public static class AccountServerBootstrap{public static bool CloudProfileReady=true;}
    public static class PlayerExperience{public static long TotalXp=>0;public const int MaxLevel=100;public static long XpForLevel(int level)=>100000;public static int CalculateMatchXp(bool w,int k,int a,int o)=>10;}
    public static class PlayerWallet{public static int EarnedBalance=>0;}
    public static class BattlePassProgress{public static void GrantMatchXp(int n,string id){}}
    public static class DailyQuestProgress{public static ProfileTransaction.Write[] MatchWrites(int a,int b,int c,bool won,int arena)=>Array.Empty<ProfileTransaction.Write>();}
}
namespace Alterverse.UI
{
    public static class RankedPanel{public static bool Visible;}
    internal sealed class FrontEndTheme
    {
        public object Title=new(),Heading=new(),Body=new(),Small=new();
        public string Click;public readonly List<(UnityEngine.Rect rect,string label)> Buttons=new();
        public static void Fill(UnityEngine.Rect r,UnityEngine.Color c){}public void Panel(UnityEngine.Rect r){}public void Label(UnityEngine.Rect r,string s,object style,bool muted=false){}
        public bool Action(UnityEngine.Rect rect,string text,bool primary=false,bool selected=false,bool enabled=true,bool locked=false,UnityEngine.Color? accent=null){Buttons.Add((rect,text));if(enabled&&Click==text){Click=null;return true;}return false;}
    }
    internal static class CombatGlyphs { public static int Segments;public static void Line(UnityEngine.Vector2 a,UnityEngine.Vector2 b,float w,UnityEngine.Color c)=>Segments++; }
    public sealed partial class MainMenuController:UnityEngine.MonoBehaviour
    {
        static MainMenuController instance;bool launching,choosingMatchMode;float pageChangedAt,menuWidth=1280;const float MenuHeight=720;
        readonly FrontEndTheme theme=new();public static bool IsVisible=>instance!=null;
        public MainMenuController(){instance=this;}
        public void Tick()=>UpdateReviewPrompt();public bool Open=>reviewPromptOpen;public bool Busy=>reviewRequestBusy;
        public void SetLaunching(bool value)=>launching=value;
        public void Click(string label){theme.Click=label;DrawReviewPrompt();}
        public void Rate()=>RequestStoreReview();
        public void CheckLayout(){theme.Buttons.Clear();CombatGlyphs.Segments=0;DrawReviewPrompt();Regression.Check(CombatGlyphs.Segments==50,"Five stars need ten segments each");foreach(var b in theme.Buttons)Regression.Check(b.rect.y>=82&&b.rect.y+b.rect.height<=646,"Control must fit the modal");}
    }
}
public static class Regression
{
    static int checks,serial;
    public static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
    static void Account(string name,AccountProfilePrefs.Entry[] remote=null,long revision=1){AccountProfilePrefs.Detach();AccountProfilePrefs.Attach(name,revision,remote??Array.Empty<AccountProfilePrefs.Entry>());}
    static void Finish(string receipt=null,bool won=false)=>MatchRewardService.GrantMatchReward(won,0,0,0,receipt??"round-"+(++serial));
    static MainMenuController Fresh(){Account("player-"+(++serial));return new();}
    static MainMenuController Due(){var menu=Fresh();Finish();Finish();menu.Tick();Check(menu.Open,"Second finished match should offer review");return menu;}
    static void Wait(Func<bool> done){for(int i=0;i<300&&!done();i++)Thread.Sleep(10);Check(done(),"Asynchronous review did not finish");}
    public static void Main()
    {
        var menu=Fresh();
        AccountProfilePrefs.SetInt("DF_FirstMatchReviewPrompt_v1",2);
        menu.Tick();Check(!menu.Open,"No prompt before a completed match");
        Finish("first",true);menu.Tick();Check(!menu.Open,"First match must not prompt");
        Finish("second");AccountServerBootstrap.CloudProfileReady=false;menu.Tick();Check(!menu.Open,"Wait for authoritative cloud profile");
        AccountServerBootstrap.CloudProfileReady=true;menu.SetLaunching(true);menu.Tick();Check(!menu.Open,"Never interrupt launching");
        menu.SetLaunching(false);menu.Tick();Check(menu.Open,"Old offered-once flag must not suppress match two");
        menu.CheckLayout();menu.Click("NOT NOW");menu.Tick();Check(!menu.Open,"Dismissal must not reopen in the same menu");
        menu=new();menu.Tick();Check(!menu.Open,"Recreating menu must not re-offer the same invitation");
        Finish("second");menu.Tick();Check(MatchRewardService.MatchesCompleted==2&&!menu.Open,"Duplicate receipt must not requeue or count a round");
        Finish("third");menu.Tick();Check(!menu.Open,"Match three must not prompt");
        Finish("fourth",true);menu.Tick();Check(menu.Open,"Wins and losses must both count toward match four");
        menu.Click("ALREADY RATED");menu.Tick();Check(ReviewPromptProgress.AlreadyRated&&!menu.Open,"Confirmation must close and suppress");
        string ratedAccount=AccountProfilePrefs.Account;var saved=AccountProfilePrefs.Capture();
        Check(AccountProfilePrefs.Dirty,"Confirmation must enter the cloud profile");
        Account("different-player");Finish();Finish();menu=new();menu.Tick();Check(menu.Open&&!ReviewPromptProgress.AlreadyRated,"Another account must retain its own invitation");
        Account(ratedAccount);Check(ReviewPromptProgress.AlreadyRated,"Confirmation must survive detach and cached reattach");
        Account(ratedAccount,saved,2);menu=new();Finish();Finish();menu.Tick();Check(ReviewPromptProgress.AlreadyRated&&!menu.Open,"Cloud-restored confirmation must suppress later matches");
        menu=Fresh();Finish();Finish();Finish();menu.Tick();Check(menu.Open,"A pending invitation survives delayed return to menu");menu.Click("NOT NOW");
        Check(!ReviewPromptProgress.ShouldOffer(3),"A delayed invitation is consumed only once");

#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        Google.Play.Review.ReviewManager.Reset();
        menu=Due();menu.Rate();Check(!menu.Busy&&menu.Open,"Native completion must offer explicit follow-up");
        Check(Google.Play.Review.ReviewManager.Requests==1&&Google.Play.Review.ReviewManager.Launches==1,"Use native request and launch APIs");
        Check(!ReviewPromptProgress.AlreadyRated&&UnityEngine.Application.Urls.Count==0&&UnityEngine.AndroidJavaObject.StoreOpens==0,"Native completion must not claim a rating or auto-redirect");
        menu.Click("NOT NOW");Finish();Finish();menu.Tick();Check(menu.Open,"A canceled or silently suppressed native flow must remain eligible");
        menu.Click("RATE ON GOOGLE PLAY");Check(!menu.Open&&UnityEngine.AndroidJavaObject.StoreOpens==1,"Explicit fallback should open Play Store");
        UnityEngine.AndroidJavaObject.StoreUnavailable=true;NativeStoreReview.OpenStore();Check(UnityEngine.Application.Urls.Count==1,"No Play Store must fall back to browser");UnityEngine.AndroidJavaObject.StoreUnavailable=false;

        Google.Play.Review.ReviewManager.Reset();Google.Play.Review.ReviewManager.Request.Error=Google.Play.Review.ReviewErrorCode.ErrorRequestingFlow;
        menu=Due();menu.Rate();Check(!menu.Busy&&menu.Open&&!ReviewPromptProgress.AlreadyRated&&Google.Play.Review.ReviewManager.Launches==0,"Failed request must preserve eligibility");
        Google.Play.Review.ReviewManager.Reset();Google.Play.Review.ReviewManager.Launch.Error=Google.Play.Review.ReviewErrorCode.ErrorLaunchingFlow;
        Check(NativeStoreReview.RequestAsync().GetAwaiter().GetResult()==StoreReviewResult.Failed,"Launch errors must be handled");
        Google.Play.Review.ReviewManager.Throws=true;Check(NativeStoreReview.RequestAsync().GetAwaiter().GetResult()==StoreReviewResult.Failed,"Native exceptions must be handled");

        Google.Play.Review.ReviewManager.Reset();Google.Play.Review.ReviewManager.Request.IsDone=false;
        menu=Due();menu.Rate();menu.Rate();menu.Click("ALREADY RATED");
        Check(menu.Busy&&!ReviewPromptProgress.AlreadyRated&&Google.Play.Review.ReviewManager.Requests==1,"Double tap and confirmation must be blocked while native request is busy");
        Account("switched-during-native");var switched=new MainMenuController();Google.Play.Review.ReviewManager.Request.IsDone=true;Wait(()=>!menu.Busy);
        switched.Tick();Check(!switched.Open&&!MainMenuController.ReviewPromptVisible&&!ReviewPromptProgress.AlreadyRated,"A stale native callback must not reopen or change another account");

        Google.Play.Review.ReviewManager.Reset();Google.Play.Review.ReviewManager.Request.IsDone=false;
        menu=Due();menu.Rate();UnityEngine.Time.Advance=20;Wait(()=>!menu.Busy);UnityEngine.Time.Advance=0;
        Check(menu.Open&&!ReviewPromptProgress.AlreadyRated,"Request timeout must return control without marking rated");
#else
        Check(NativeStoreReview.RequestAsync().GetAwaiter().GetResult()==StoreReviewResult.Unavailable,"Editor/server must not call Android SDK");
        menu=Due();menu.Rate();Check(menu.Open&&!menu.Busy&&!ReviewPromptProgress.AlreadyRated,"Unavailable platform must preserve explicit store fallback");
#endif
        Console.WriteLine(checks+" review scheduling, receipt, account persistence, UI and SDK-boundary checks passed.");
    }
}
