// Controlled SDK/service boundaries. Production account and profile classes compile unchanged.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
namespace UnityEngine
{
 public class Object {public static void Destroy(Object o){}public static void DontDestroyOnLoad(Object o){}}
 public class GameObject:Object
 {
  public GameObject(string name=""){}public static GameObject Find(string name)=>null;
  public T AddComponent<T>() where T:new(){var value=new T();typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(value,null);return value;}
 }
 public class MonoBehaviour:Object {public GameObject gameObject=new();}
 public enum RuntimeInitializeLoadType {AfterSceneLoad,SubsystemRegistration}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){}}
 public static class Application {public static bool isBatchMode;}
 public class TextAsset {public string text;}
 public static class Resources
 {
  public class Settings {public string mAppId="123",mWebClientId="web-client";}
  public static object Load(string path)=>new Settings();
  public static T Load<T>(string path) where T:class=>path=="Social/SafeIdentityCatalog"?new TextAsset{text=System.IO.File.ReadAllText(Environment.GetEnvironmentVariable("DF_TEST_IDENTITY_CATALOG"))} as T:null;
 }
 public static class JsonUtility
 {
  static readonly System.Text.Json.JsonSerializerOptions options=new(){IncludeFields=true};
  public static string ToJson(object value)=>System.Text.Json.JsonSerializer.Serialize(value,value.GetType(),options);
  public static T FromJson<T>(string text)=>System.Text.Json.JsonSerializer.Deserialize<T>(text,options);
 }
 public static class Debug {public static void LogWarning(string value){}}
 public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static float Clamp01(float f)=>Math.Clamp(f,0,1);}
 public static class PlayerPrefs
 {
  static readonly Dictionary<string,object> values=new();public static int Saves;
  public static bool HasKey(string k)=>values.ContainsKey(k);
  public static string GetString(string k,string fallback="")=>values.TryGetValue(k,out var v)&&v is string s?s:fallback;
  public static int GetInt(string k,int fallback=0)=>values.TryGetValue(k,out var v)&&v is int n?n:fallback;
  public static float GetFloat(string k,float fallback=0)=>values.TryGetValue(k,out var v)&&v is float f?f:fallback;
  public static void SetString(string k,string v)=>values[k]=v;public static void SetInt(string k,int v)=>values[k]=v;
  public static void SetFloat(string k,float v)=>values[k]=v;public static void DeleteKey(string k)=>values.Remove(k);
  public static void Save()=>Saves++;
 }
}
namespace Unity.Services.Core
{
 public enum ServicesInitializationState {Uninitialized,Initialized}
 public static class UnityServices {public static ServicesInitializationState State=ServicesInitializationState.Initialized;public static Task InitializeAsync(){State=ServicesInitializationState.Initialized;return Task.CompletedTask;}}
}
namespace Unity.Services.Authentication
{
 public class AuthenticationException:Exception {public int ErrorCode;}
 public static class AuthenticationErrorCodes {public const int AccountAlreadyLinked=1;}
 public class SignInOptions {public bool CreateAccount=true;}
 public class PlayerInfo {public string Google;public string GetGooglePlayGamesId()=>Google;}
 public class AuthenticationService
 {
  public static AuthenticationService Instance=new();public bool IsAuthorized=true,IsSignedIn=true,SessionTokenExists=true;
  public string PlayerId="unity-a",AccessToken="token",Provider="google-a";public Func<Task<PlayerInfo>> Info;
  public event Action SignedIn,SignedOut,Expired;
  public Task<PlayerInfo> GetPlayerInfoAsync()=>Info?.Invoke()??Task.FromResult(new PlayerInfo{Google=Provider});
  public Task SignInAnonymouslyAsync(){IsSignedIn=IsAuthorized=true;SignedIn?.Invoke();return Task.CompletedTask;}
  public Task SignInWithGooglePlayGamesAsync(string code,SignInOptions options=null)=>SignInAnonymouslyAsync();
  public Task SignInWithUsernamePasswordAsync(string user,string password)=>SignInAnonymouslyAsync();
  public Task AddUsernamePasswordAsync(string user,string password)=>Task.CompletedTask;
  public Task LinkWithGooglePlayGamesAsync(string code)=>Task.CompletedTask;
  public void SignOut(){IsSignedIn=IsAuthorized=false;SignedOut?.Invoke();}
 }
}
namespace GooglePlayGames.BasicApi {public enum SignInStatus {Success,Failed}}
namespace GooglePlayGames
{
 public class PlayGamesPlatform
 {
  public class User {public string id="google-a";}
  public static PlayGamesPlatform Instance=new();public User localUser=new();public bool Authenticated=true;
  public string Name="Chris Ä龍🎮";public bool ThrowName;public static void Activate(){}
  public bool IsAuthenticated()=>Authenticated;public string GetUserDisplayName()=>ThrowName?throw new Exception():Name;
  public void Authenticate(Action<BasicApi.SignInStatus> callback)=>callback(Authenticated?BasicApi.SignInStatus.Success:BasicApi.SignInStatus.Failed);
  public void ManuallyAuthenticate(Action<BasicApi.SignInStatus> callback)=>Authenticate(callback);
  public void RequestServerSideAccess(bool refresh,Action<string> callback)=>callback("one-use-code");
 }
}
namespace Alterverse.Shop
{
 public static class AvatarInventory {public static bool IsOwned(int id)=>id==0;}
 public static class FounderEntitlement {public static bool Owned;}
 public static class FounderPackCatalog {public const string FounderTitle="FOUNDER";}
 public class GooglePlayShardStore {public static GooglePlayShardStore Instance;public bool Busy,PendingRecovery;public void Reconnect(){}}
 public class PaidShardCommerce
 {
  public static PaidShardCommerce Instance;public static bool Spending;public bool Ready=true,HasPaidHistory;public int FlushCalls;public string AccountHash;
  public static event Action EntitlementsChanged;public static void Raise()=>EntitlementsChanged?.Invoke();
  public static void Connect(string endpoint,Func<string> token,string account){Instance=new(){AccountHash=account};}
  public static void Disconnect(){Instance=null;Alterverse.Profile.AccountProfilePrefs.Detach();Raise();}
  public void Refresh(){}public Task<bool> FlushProfileAsync(){FlushCalls++;return Task.FromResult(true);}
 }
}
namespace Alterverse.Profile
{
 public static class ProfileTransaction {public static void Recover(){}}
 public static class SocialService {public static void Connect(string endpoint,Func<string> token){}public static void Disconnect(){}}
}
namespace Alterverse.Online {public static class GooglePlayLevelLeaderboard {public static void SubmitCurrentScore(){}}}
namespace Alterverse.Multiplayer
{
 public class RankedService {public static RankedService Instance;public bool Searching,Busy;}
 public class CoopLobbyService {public static CoopLobbyService Instance;public bool Busy,InLobby;}
 public class OnlinePartyService {public static OnlinePartyService Instance;public bool Busy,InParty;}
 public class EdgegapOnlineFlow {public static EdgegapOnlineFlow Instance;public bool Busy;}
 public static class CoopAuthority {public static bool Active;}
 public static class DedicatedServerNetworkRuntime {public static bool Requested;}
}
