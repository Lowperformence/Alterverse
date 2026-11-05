using System;
using System.Collections.Generic;
using System.Text.Json;

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public class Object { public static void Destroy(Object value) { } public static void DontDestroyOnLoad(Object value) { } }
    public class MonoBehaviour : Object { public GameObject gameObject; }
    public class GameObject : Object
    {
        public GameObject(string name) { }
        public T AddComponent<T>() where T : MonoBehaviour, new()
        {
            var component = new T { gameObject = this };
            typeof(T).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(component, null);
            return component;
        }
    }
    public struct Vector2 { public float x,y; public Vector2(float x,float y) {this.x=x;this.y=y;} public static Vector2 zero => new Vector2(); }
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float x,float y,float width,float height) {this.x=x;this.y=y;this.width=width;this.height=height;}
        public float xMax => x+width; public float yMax => y+height;
        public Vector2 center => new Vector2(x+width/2,y+height/2);
    }
    public struct Color
    {
        public Color(float r,float g,float b,float a) { }
        public static Color operator *(Color a,Color b) => a;
    }
    public enum FontStyle { Normal,Bold,Italic,BoldAndItalic }
    public enum TextureFormat { RGBA32 }
    public enum HideFlags { HideAndDontSave }
    public enum FilterMode { Bilinear }
    public enum ScaleMode { StretchToFill }
    public enum EventType { Repaint,Layout }
    public enum TextAnchor { UpperLeft,UpperCenter,UpperRight,MiddleLeft,MiddleCenter,MiddleRight,LowerLeft,LowerCenter,LowerRight }
    public class Event { public static Event current = new Event(); public EventType type = EventType.Repaint; }
    public class GUIStyleState { public Color textColor; }
    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) {fontSize=other.fontSize;wordWrap=other.wordWrap;fontStyle=other.fontStyle;alignment=other.alignment;}
        public int fontSize=18; public bool wordWrap=true; public FontStyle fontStyle; public TextAnchor alignment;
        public GUIStyleState normal=new GUIStyleState();
    }
    public static class GUI
    {
        public static Matrix4x4 matrix;
        public static bool enabled=true; public static Color color; public static string NextText;
        public static void FocusControl(string value) { }
        public static string TextField(Rect r,string value,int limit,GUIStyle style) {var text=NextText??value;NextText=null;return text;}
        public static Vector2 BeginScrollView(Rect r,Vector2 scroll,Rect view) => scroll;
        public static void EndScrollView() { }
        public static void DrawTexture(Rect r,Texture2D t,ScaleMode scale,bool alpha) { }
    }
    public class Texture2D : Object
    {
        public int width,height; public HideFlags hideFlags; public FilterMode filterMode;
        public Texture2D(int w,int h,TextureFormat format,bool mip) {width=w;height=h;}
    }
    public static class ImageConversion { public static bool LoadImage(Texture2D t,byte[] image,bool unreadable) {t.width=400;t.height=100;return true;} }
    public class AndroidJavaClass : IDisposable
    {
        public static int Calls;
        public AndroidJavaClass(string name) { }
        public T CallStatic<T>(string method,params object[] args) {Calls++;return (T)(object)new byte[]{1};}
        public void Dispose() { }
    }
    public static class Time { public static float unscaledTime; public static int frameCount=10; }
    public struct Matrix4x4 { public static Matrix4x4 identity=>new Matrix4x4(); }
    public static class Screen { public static int height=1080; public static Rect safeArea=new Rect(0,0,1920,1080); }
    public static class Mathf
    {
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static int Clamp(int value,int min,int max)=>Math.Max(min,Math.Min(max,value));
        public static int CeilToInt(float value)=>(int)Math.Ceiling(value);
        public static int RoundToInt(float value)=>(int)Math.Round(value);
    }
    public enum SystemLanguage { English,German,French,Spanish,Portuguese,Russian,ChineseSimplified,ChineseTraditional,Japanese,Unknown }
    public static class Application
    {
        public static SystemLanguage systemLanguage=SystemLanguage.English;
        public static event Action lowMemory;
        public static event Action<bool> focusChanged;
    }
    public static class PlayerPrefs
    {
        public static readonly Dictionary<string,string> Values = new Dictionary<string,string>();
        public static int Saves;
        public static bool HasKey(string key)=>Values.ContainsKey(key);
        public static string GetString(string key,string fallback)=>Values.TryGetValue(key,out var value)?value:fallback;
        public static void SetString(string key,string value)=>Values[key]=value;
        public static void Save()=>Saves++;
    }
    public class TextAsset { public string text; }
    public static class Resources
    {
        public static string Catalog;
        public static T Load<T>(string path) where T:class => path=="Localization/InterfaceLanguages" ? new TextAsset{text=Catalog} as T : null;
    }
    public static class JsonUtility { public static T FromJson<T>(string value)=>JsonSerializer.Deserialize<T>(value,new JsonSerializerOptions{IncludeFields=true}); }
    public static class Debug { public static void LogWarning(string value) { } }
}
namespace Alterverse.UI
{
    internal class FrontEndTheme
    {
        public static string Click; public static int Actions;
        public UnityEngine.GUIStyle Small=new UnityEngine.GUIStyle(),Heading=new UnityEngine.GUIStyle(),Field=new UnityEngine.GUIStyle();
        public static void Fill(UnityEngine.Rect r,UnityEngine.Color c) { }
        public void Panel(UnityEngine.Rect r) { }
        public void FitLabel(UnityEngine.Rect r,string value,UnityEngine.GUIStyle style,bool muted=false) { }
        public void Label(UnityEngine.Rect r,string value,UnityEngine.GUIStyle style,bool muted=false) { }
        public bool Action(UnityEngine.Rect r,string label,bool primary=false,bool selected=false,bool enabled=true)
        { Actions++; if(!UnityEngine.GUI.enabled||!enabled||Click!=label)return false;Click=null;return true; }
    }
    public sealed partial class MainMenuController
    {
        enum Page { Home,Other }
        static readonly MainMenuController instance=new MainMenuController();
        Page page; bool launching;
        readonly LanguagePicker languagePicker=new LanguagePicker();
        readonly FrontEndTheme theme=new FrontEndTheme();
        public static bool MockVisible=true;
        public static bool IsVisible=>MockVisible;
        public static bool ReviewPromptVisible {get;set;}
    }
    public static class OnlinePartyOverlay { public static bool IsOpen; }
    public static class RankedPanel { public static bool Visible; }
    public static class OnlineMatchLoadingScreen { public static bool IsVisible; }
}
namespace Alterverse.Shop
{
    public sealed class RewardedShardAds
    {
        public static readonly RewardedShardAds Instance=new RewardedShardAds();
        public bool ReadyForRequests; public int PrivacyRevision;
        public string Status="Checking ad privacy preferences...";
        public static RewardedShardAds Ensure()=>Instance;
    }
}
namespace GoogleMobileAds.Api
{
    public sealed class AdRequest { }
    public sealed class LoadAdError
    {
        public int GetCode()=>2;
        public string GetMessage()=>"Network unavailable";
    }
    public sealed class AdSize
    {
        public readonly int Width,Height;
        public static readonly AdSize Banner=new AdSize(320,50);
        public AdSize(int width,int height) {Width=width;Height=height;}
    }
    public static class MobileAds { public static class Utils { public static float GetDeviceScale()=>3; } }
    public sealed class BannerView
    {
        public static readonly List<BannerView> All=new List<BannerView>();
        public string Unit; public AdSize Size; public int X,Y,Loads; public bool Visible,Destroyed;
        public event Action OnBannerAdLoaded;
        public event Action<LoadAdError> OnBannerAdLoadFailed;
        public BannerView(string unit,AdSize size,int x,int y) {Unit=unit;Size=size;X=x;Y=y;All.Add(this);}
        public void LoadAd(AdRequest request)=>Loads++;
        public void SetPosition(int x,int y) {X=x;Y=y;}
        public void Show()=>Visible=true;
        public void Hide()=>Visible=false;
        public void Destroy() {Destroyed=true;Visible=false;}
        public void Complete()=>OnBannerAdLoaded?.Invoke();
        public void Fail()=>OnBannerAdLoadFailed?.Invoke(new LoadAdError());
    }
}
