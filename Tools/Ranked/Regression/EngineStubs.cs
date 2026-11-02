// Test-only engine/service substitutes. Never included in Unity Assets.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
namespace UnityEngine {
 public class Object { public static void Destroy(Object o){if(o is GameObject g)GameObject.All.Remove(g);} public static void DontDestroyOnLoad(Object o){} public static readonly Dictionary<Type,object> Found=new(); public static T FindAnyObjectByType<T>() where T:class=>Found.TryGetValue(typeof(T),out var o)?o as T:null; }
 public class MonoBehaviour:Object { public GameObject gameObject=new GameObject(); public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();public bool isActiveAndEnabled=true;public Transform transform=>gameObject.transform; public void StartCoroutine(IEnumerator e){} public void StopAllCoroutines(){} }
 public class GameObject:Object {
  public static readonly List<GameObject> All=new();public string name;public Transform transform=new();readonly Dictionary<Type,object> components=new();
  public bool activeSelf=true;public void SetActive(bool active){activeSelf=active;}public GameObject(string n=""){name=n;All.Add(this);}public static GameObject Find(string n)=>All.Find(g=>g.name==n);
  public T GetComponent<T>() where T:class=>components.TryGetValue(typeof(T),out var o)?o as T:null;
  public T AddComponent<T>() where T:new(){var o=new T();components[typeof(T)]=o;if(o is MonoBehaviour b)b.gameObject=this;typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.Invoke(o,null);return o;}
 }
 public class Transform {public Vector3 position,localScale,eulerAngles;public void SetParent(Transform t,bool world){}}
 public class CapsuleCollider {public float height,radius;}
 public class SerializeField:Attribute {}
 public static class Random {public static int TestIndex;public static int Range(int min,int max)=>Math.Max(min,Math.Min(max-1,min+TestIndex));}
 public class TextAsset {public string text;}
 public static class Resources {
  public static TextAsset AccountConfig, RankedConfig;
  public static T Load<T>(string s) where T:class => (s=="Social/SafeIdentityCatalog" ? new TextAsset{text=System.IO.File.ReadAllText(Environment.GetEnvironmentVariable("DF_TEST_IDENTITY_CATALOG"))} : s=="Commerce/LegacyProfileKeys" ? null : s=="RankedAvailability" ? RankedConfig : AccountConfig) as T;
 }
 public static class PlayerPrefs {
  static readonly Dictionary<string,object> values=new();
  public static bool HasKey(string key)=>values.ContainsKey(key);
  public static string GetString(string key,string fallback="")=>values.TryGetValue(key,out var v)&&v is string s?s:fallback;
  public static int GetInt(string key,int fallback=0)=>values.TryGetValue(key,out var v)&&v is int n?n:fallback;
  public static float GetFloat(string key,float fallback=0)=>values.TryGetValue(key,out var v)&&v is float n?n:fallback;
  public static void SetString(string key,string value)=>values[key]=value;
  public static void SetInt(string key,int value)=>values[key]=value;
  public static void SetFloat(string key,float value)=>values[key]=value;
  public static void DeleteKey(string key)=>values.Remove(key);
  public static void Save(){}
 }
 public static class JsonUtility {
#if MONO_FIXTURE
 public static string ToJson(object o){using var stream=new System.IO.MemoryStream();new System.Runtime.Serialization.Json.DataContractJsonSerializer(o.GetType()).WriteObject(stream,o);return System.Text.Encoding.UTF8.GetString(stream.ToArray());}
 // Unity defaults missing fields; Serializable DataContract JSON would require
 // every field. Read JSON through Mono's XML bridge and populate public fields.
 public static T FromJson<T>(string s){using var reader=System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(System.Text.Encoding.UTF8.GetBytes(s),System.Xml.XmlDictionaryReaderQuotas.Max);var doc=new System.Xml.XmlDocument();doc.Load(reader);return (T)ReadNode(typeof(T),doc.DocumentElement);}
 static object ReadNode(Type type,System.Xml.XmlElement node){if(node.GetAttribute("type")=="null")return null;if(type==typeof(string))return node.InnerText;if(type.IsEnum)return Enum.Parse(type,node.InnerText);if(type.IsPrimitive)return Convert.ChangeType(node.InnerText,type,System.Globalization.CultureInfo.InvariantCulture);if(type.IsArray){var array=Array.CreateInstance(type.GetElementType(),node.ChildNodes.Count);for(int i=0;i<array.Length;i++)array.SetValue(ReadNode(type.GetElementType(),(System.Xml.XmlElement)node.ChildNodes[i]),i);return array;}var value=Activator.CreateInstance(type,true);foreach(var field in type.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)){var child=node[field.Name];if(child!=null)field.SetValue(value,ReadNode(field.FieldType,child));}return value;}
#else
 static System.Text.Json.JsonSerializerOptions opts=new(){IncludeFields=true}; public static string ToJson(object o)=>System.Text.Json.JsonSerializer.Serialize(o,o.GetType(),opts); public static T FromJson<T>(string s)=>System.Text.Json.JsonSerializer.Deserialize<T>(s,opts);
#endif
 }
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int n){}}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public enum RuntimeInitializeLoadType {BeforeSceneLoad,AfterSceneLoad,SubsystemRegistration}
 public static class Time {public static int frameCount;public static float unscaledTime,realtimeSinceStartup,deltaTime,timeScale=1f;}
 public static class Debug {public static readonly List<string> Messages=new();public static void Log(object o){Messages.Add(o?.ToString()??"");}public static void LogWarning(object o){Log(o);}public static void LogError(object o){Log(o);}}
 public static class Mathf {public static int CeilToInt(float v)=>(int)Math.Ceiling(v);public static float Clamp(float v,float lo,float hi)=>Math.Max(lo,Math.Min(hi,v));public static int Clamp(int n,int lo,int hi)=>Math.Max(lo,Math.Min(hi,n));public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Clamp01(float a)=>Math.Max(0,Math.Min(1,a));}
 public struct Vector2 {}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new(); public static Vector3 operator*(Vector3 v,float f)=>v;}
 public struct Quaternion {public static Quaternion identity=>new();}
 public struct Matrix4x4 {public static Matrix4x4 TRS(Vector3 v,Quaternion q,Vector3 s)=>new();}
 public struct Rect {public float x,y,width,height;public float yMax=>y+height; public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}}
 public static class Screen {public static Rect safeArea=>new(0,0,1280,720);public static int height=720;}
 public enum TextAnchor {MiddleCenter}public enum FontStyle {Bold}public class GUIStyle {public GUIStyle(){}public GUIStyle(GUIStyle o){}public TextAnchor alignment;public FontStyle fontStyle;}
 public static class GUI {public static Matrix4x4 matrix;public static int depth;public static Vector2 BeginScrollView(Rect r,Vector2 s,Rect inner)=>s;public static void EndScrollView(){}}
}
namespace UnityEngine.Networking {
 public class DownloadHandlerBuffer {public string text="{}";}
 public class UploadHandlerRaw {public readonly byte[] data;public UploadHandlerRaw(byte[] b){data=b;}}
 public class UnityWebRequestAsyncOperation {public volatile bool isDone=true;}
 public class UnityWebRequest:IDisposable {public static Func<UnityWebRequest,UnityWebRequestAsyncOperation> Handler;public readonly string url,method;public readonly Dictionary<string,string> Headers=new();public enum Result {Success,ConnectionError};public Result result=Result.Success;public long responseCode=200;public string error;public int timeout;public UploadHandlerRaw uploadHandler;public DownloadHandlerBuffer downloadHandler;public UnityWebRequest(string u,string m){url=u;method=m;}public static UnityWebRequest Delete(string u)=>new(u,"DELETE");public static string EscapeURL(string s)=>Uri.EscapeDataString(s);public void SetRequestHeader(string n,string v){Headers[n]=v;}public UnityWebRequestAsyncOperation SendWebRequest()=>Handler?.Invoke(this)??new();public void Dispose(){}}
}
namespace Unity.Services.Authentication {
 public class PlayerInfo {public string Id,Username,GooglePlayGamesId;public string GetGooglePlayGamesId()=>GooglePlayGamesId;}
 public class AuthenticationService {public static AuthenticationService Instance=new();public bool IsAuthorized=true;public bool IsSignedIn=true;public bool SessionTokenExists=true;public Task SignInAnonymouslyAsync(){IsSignedIn=true;return Task.CompletedTask;}public string PlayerId="user",AccessToken="jwt";public Func<Task<PlayerInfo>> PlayerInfoRequest;public Task<PlayerInfo> GetPlayerInfoAsync()=>PlayerInfoRequest?.Invoke()??Task.FromResult(new PlayerInfo{Id=PlayerId,GooglePlayGamesId="linked-google"});}
}
namespace Unity.Collections {
 public enum Allocator {Temp}
 public struct FixedString128Bytes {string s;public FixedString128Bytes(string v){s=v;}public override string ToString()=>s;}
 public struct FixedString64Bytes {string s;public FixedString64Bytes(string v){s=v;}public override string ToString()=>s;}
 public struct FixedString4096Bytes {string s;public FixedString4096Bytes(string v){s=v;}public override string ToString()=>s;}
}
namespace Unity.Netcode {
 public enum NetworkDelivery {ReliableSequenced,UnreliableSequenced}
 public class FastBufferWriter:IDisposable {public readonly List<object> Values=new();public FastBufferWriter(int n,Unity.Collections.Allocator a){}public void WriteValueSafe<T>(T v){Values.Add(v);}public void Dispose(){}}
 public class FastBufferReader {readonly Queue<object> values;public FastBufferReader(params object[] data){values=new(data);}public bool TryBeginRead(int n)=>true;public void ReadValueSafe<T>(out T v){v=values.Count>0?(T)values.Dequeue():default;}}
 public class CustomMessagingManager {public readonly List<(string Name,ulong Id,object[] Values)> Sent=new();public void RegisterNamedMessageHandler(string n,Action<ulong,FastBufferReader> a){}public void UnregisterNamedMessageHandler(string n){}public void SendNamedMessage(string n,ulong id,FastBufferWriter w,NetworkDelivery d){Sent.Add((n,id,w.Values.ToArray()));}}
 public class NetworkManager {public static NetworkManager Singleton;public const ulong ServerClientId=0;public class Clock {public double Time;}public Clock ServerTime=new();public ulong LocalClientId=1;public event Action<ulong> OnClientDisconnectCallback;public bool IsServer,IsClient,IsHost,IsListening,IsConnectedClient;public List<ulong> ConnectedClientsIds=new();public CustomMessagingManager CustomMessagingManager=new();public void DisconnectClient(ulong i){ConnectedClientsIds.Remove(i);}}
}
namespace Alterverse.AI {public enum BotDifficulty {Easy,Medium,Hard} public static class BotDifficultySettings {public const BotDifficulty OnlineDifficulty=BotDifficulty.Hard;public static BotDifficulty Selected;public static BotDifficulty Clamp(BotDifficulty b)=>b;public static BotDifficulty Cycle(BotDifficulty b,int n)=>b;public static string Label(BotDifficulty b)=>b.ToString();}}
namespace Alterverse.Characters {public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok}public static class PlayerHeroSelection {public static HeroId Selected;public static bool Select(HeroId h){Selected=h;return true;}}
 public class HeroIdentity:MonoBehaviour {public const float HeroCollisionHeight=2,HeroCollisionRadius=.5f;public HeroId Hero;public void Configure(HeroId h){Hero=h;}}
 public class HeroCombatProfile:MonoBehaviour {public float Health=100;public void Configure(HeroId h){}}
 public class HeroAbilityKit:MonoBehaviour {public void Configure(HeroId h){}}
}
namespace Alterverse.Combat {public class Health:MonoBehaviour {public static readonly List<Health> Active=new();public static uint RegistryVersion=>(uint)Active.Count;public static int Lookups;public new T GetComponent<T>() where T:class{Lookups++;return gameObject.GetComponent<T>();}public float Current,Shield;public bool IsDead;public void Configure(float n){}public void ApplyNetworkSnapshot(float hp,float shield,bool dead){Current=hp;Shield=shield;IsDead=dead;}}public class CombatFeedback:MonoBehaviour {}}
namespace Alterverse.Visuals {public class StylizedHeroVisual:MonoBehaviour {public void Build(){}}}
namespace Alterverse.AI {
 public class BotPersonality:MonoBehaviour {public enum Style {One,Two,Three,Four,Five,Six}public float Aggression,Caution;public void Configure(Style s){}}
 public class BotTargetSensor:MonoBehaviour {}public class MobaBotBrain:MonoBehaviour {public void Initialize(int t,float f,float a,float c){}public void RefreshPersonality(){}}
 public class BotLanePlan:MonoBehaviour {public void Initialize(int t,float f){}}public class MobaBotMover:MonoBehaviour {}public class MobaBotCombat:MonoBehaviour {}public class HeroBotAbilities:MonoBehaviour {}public class MobaBotRespawn:MonoBehaviour {}
}
namespace Alterverse.Match {public enum ArenaVariant{Moon=0,Mars=1,GreenPlanet=2,Noxara=3,Kryon=4,Earth=5,Pyraxis=6,Neptune=7,Dark00=8,DiamondCore=9,GJ504b=10,Wasp76b=11,Trappist1e=12,Toi715b=13}public static class ArenaVariantRuntime {public static ArenaVariant Selected;public static bool ShuffleEnabled;public static ArenaVariant RandomOwnedArena(ArenaVariant a)=>a;}public class DimensionsFront {public bool HasEnded;public event Action<int> MatchEnded;}}
namespace Alterverse.Profile {public enum BattleItemType{None,Shield}public static class BattleItemInventory {public static BattleItemType Equipped;}public static class GooglePlayAccountSession {public static bool Supported;public static string DisplayName=>null;}public class AccountServerBootstrap {public static AccountServerBootstrap Instance;public static bool CloudProfileReady=>!GooglePlayAccountSession.Supported||Instance!=null;public bool Busy;public Task<bool> EnsureAuthenticatedAsync()=>Task.FromResult(true);}}
namespace Alterverse.UI {public sealed partial class MainMenuController {public static bool IsVisible=true;public static int PartyReturns,HomeReturns;public static void ShowCoopAfterMatch(string s){PartyReturns++;IsVisible=true;}public static void ShowHome(){HomeReturns++;IsVisible=true;}public static void ShowLaunchFailure(string s){}public bool launching,choosingMatchMode;public string launchMessage;bool PrepareSelectedMatch()=>true;}public class FrontEndTheme {public static object Background,Surface,Zero,Positive,Accent,Warning,Danger,Raised,Pixel;public GUIStyle Title=new(),Heading=new(),Body=new(),Small=new();public static void Fill(UnityEngine.Rect r,object c){}public void Panel(UnityEngine.Rect r){}public void StatusBadge(UnityEngine.Rect r,string s,object c){}public void Label(UnityEngine.Rect r,string s,GUIStyleShim t,bool muted=false){}public void Label(UnityEngine.Rect r,string s,UnityEngine.GUIStyle t,bool muted=false){}public bool Action(UnityEngine.Rect r,string s,bool primary=false,bool enabled=true)=>false;} public class GUIStyleShim {} }
namespace Alterverse.Multiplayer {
 public static class CoopAuthority {public static bool Active,LocalIsHost,LocalHasServerAuthority;public static bool CanSimulateHostGameplay=>!Active||LocalHasServerAuthority;public static CoopMatchPlan PreparedPlan,ActivePlan;public static bool BeginPreparedMatch(bool b)=>true;public static void Prepare(CoopMatchPlan p){PreparedPlan=p;}public static void ClearPrepared(){PreparedPlan=null;}public static void EndMatch(){Active=false;ActivePlan=PreparedPlan=null;}}
 public static class CoopRosterBuilder {public static int LocalSlot;public static int FindLocalHumanSlot(CoopMatchPlan p)=>LocalSlot;}
 public static class DedicatedServerNetworkRuntime {public static bool Requested;public static void ReleaseMatchBootstrap(){}}
 public static class EdgegapInjectedMatchContext {public static bool IsEdgegapDeployment;public static int AssignedTicketCount;public static string InjectedMatchId;public static bool TryValidateTicket(string t,string c,string m,out string team,out string match,out string reason){team=c;match=m;reason="";return true;}}
 public static class CoopNetworkBootstrap {public static Unity.Netcode.NetworkManager Manager;public static string DirectServerStatus="";public static bool IsListening=>Manager?.IsListening==true;public static bool IsLinkReadyForPlayers(int n)=>Manager?.IsListening==true&&(Manager.IsHost?Manager.ConnectedClientsIds.Count>=n:Manager.IsConnectedClient);public static string LinkStateForPlayers(int n)=>"CONNECTED";public static int Shutdowns;public static void ShutdownLocalNetwork(){Shutdowns++;if(Manager!=null)Manager.IsListening=false;}public static int Connects,Cancels;public static bool ConnectToDedicatedServer(string s,ushort p){Connects++;return true;}public static void CancelDedicatedConnection(){Cancels++;}}
 public static class OnlineMatchRuntime {public static bool Enter(OnlineMatchPlan p)=>true;}
}

namespace Alterverse.Shop {
 public readonly struct ArenaShopItem {public readonly Alterverse.Match.ArenaVariant Variant;public readonly int Price;public string Name=>Variant.ToString();public ArenaShopItem(Alterverse.Match.ArenaVariant variant,int price){Variant=variant;Price=price;}}
 public static class ArenaShopCatalog {public static readonly ArenaShopItem[] Items={
  new(Alterverse.Match.ArenaVariant.Moon,0),
  new(Alterverse.Match.ArenaVariant.Mars,0),
  new(Alterverse.Match.ArenaVariant.Earth,900)
 };public static int IndexOf(Alterverse.Match.ArenaVariant arena)=>Array.FindIndex(Items,item=>item.Variant==arena);}
 public static class ArenaUnlockInventory {
  public static readonly HashSet<Alterverse.Match.ArenaVariant> Unlocked=new();
  public static bool Owned(Alterverse.Match.ArenaVariant arena)=>Unlocked.Contains(arena);
 }
 public static class AvatarInventory {public static bool IsOwned(int id)=>id==0;}
 public static class FounderEntitlement {public static bool Owned=>false;}
 public static class FounderPackCatalog {public const string FounderTitle="FOUNDER";}
 public class PaidShardCommerce {
  public class Snapshot {public string accountHash;public Alterverse.Profile.AccountProfilePrefs.Entry[] profile;}
  public static PaidShardCommerce Instance;public bool Ready;public string AccountHash;public int SessionVersion;
  public System.Threading.Tasks.Task<bool> FlushProfileAsync()=>System.Threading.Tasks.Task.FromResult(true);
  public bool TryBeginSocialSpend()=>true;
  public void EndSocialSpend(int session,Snapshot wallet,Alterverse.Profile.AccountProfilePrefs.Entry[] entries){}
  public void Refresh(){}
 }
}



