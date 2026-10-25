using System;
using System.Threading.Tasks;
using Alterverse.Online;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

namespace UnityEngine {
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){}}
 public enum RuntimeInitializeLoadType{SubsystemRegistration}
 public static class Mathf{public static int Max(int a,int b)=>Math.Max(a,b);}
 public static class Resources{public static object Load(string name)=>new object();}
 public static class JsonUtility{
  public static string ToJson(object value)=>"{}";
  public static T FromJson<T>(string text){var value=Activator.CreateInstance(typeof(T),true);typeof(T).GetField("mAppId").SetValue(value,"1029475752167");return (T)value;}
 }
 public static class Debug{public static void LogWarning(object value){}}
}
namespace Alterverse.Profile {
 public static class PlayerProfile{public static string PlayerName=>"Pilot";}
 public static class MatchRewardService{public static int MatchesWon=12;}
 public static class AccountProfilePrefs{public static bool Attached=true,Dirty;public static string Account="account-a";}
 public sealed class AccountServerBootstrap{public static AccountServerBootstrap Instance=new();public bool GooglePlayAccountReady=true;}
 public static class GooglePlayAccountSession{
  public static string NativeConfigurationError=>"";public static string ConfigurationError=>"";
  public static Task<bool> AuthenticateAsync(bool interactive){PlayGamesPlatform.Instance.Authenticated=true;return Task.FromResult(true);}
 }
}
namespace Alterverse.Shop {
 public sealed class PaidShardCommerce{
  public static PaidShardCommerce Instance=new();public bool Ready=true;public string AccountHash="account-a";
  public Func<Task<bool>> Save;
  public Task<bool> FlushProfileAsync()=>Save?.Invoke()??Task.FromResult(true);
 }
}
namespace GooglePlayGames.BasicApi {
 public enum LeaderboardTimeSpan{AllTime}public enum LeaderboardStart{PlayerCentered}public enum LeaderboardCollection{Public}public enum UIStatus{Valid,UserClosedUI}
 public sealed class TestScore{public long value;public string formattedValue;}
 public sealed class LeaderboardScoreData{public bool Valid;public TestScore PlayerScore;}
}
namespace GooglePlayGames {
 public sealed class PlayGamesPlatform{
  public static PlayGamesPlatform Instance=new();public bool Authenticated=true;public sealed class User{public string id="google-a";}public User localUser=new();
  public bool IsAuthenticated()=>Authenticated;
  public Action<bool> SubmitCallback;public Action<LeaderboardScoreData> LoadCallback;
  public int Reports,Loads,Opens;public long Sent;
  public void ReportScore(long score,string id,Action<bool> callback){Sent=score;Reports++;SubmitCallback=callback;}
  public void LoadScores(string id,LeaderboardStart start,int rows,LeaderboardCollection collection,LeaderboardTimeSpan span,Action<LeaderboardScoreData> callback){Loads++;LoadCallback=callback;}
  public void ShowLeaderboardUI(string id,LeaderboardTimeSpan span,Action<UIStatus> callback){Opens++;callback(UIStatus.UserClosedUI);}
 }
}
public static class Regression {
 static int checks;
 static void Assert(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static async Task Wait(Func<bool> predicate){for(int i=0;i<2000;i++){if(predicate())return;await Task.Delay(1);}throw new Exception("Native fixture callback did not progress");}
 static PlayGamesPlatform Reset(){
  typeof(GooglePlayLevelLeaderboard).GetMethod("ResetRuntimeState",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
  PlayGamesPlatform.Instance=new();Alterverse.Profile.AccountServerBootstrap.Instance=new();Alterverse.Shop.PaidShardCommerce.Instance=new();
  Alterverse.Profile.AccountProfilePrefs.Attached=true;Alterverse.Profile.AccountProfilePrefs.Dirty=false;Alterverse.Profile.AccountProfilePrefs.Account="account-a";Alterverse.Profile.MatchRewardService.MatchesWon=12;
  return PlayGamesPlatform.Instance;
 }
 static LeaderboardScoreData Score(long value,string formatted=null)=>new(){Valid=true,PlayerScore=new(){value=value,formattedValue=formatted??value.ToString()}};
 public static async Task Main(){
  var native=Reset();GooglePlayLevelLeaderboard.ShowLeaderboard();
  Assert(native.Reports==1&&native.Sent==12&&native.Opens==0,"Do not open the native ranking while the new score is still uploading");
  native.SubmitCallback(true);await Wait(()=>native.LoadCallback!=null);
  Assert(native.Opens==0,"Wait for score readback before opening the native ranking");
  native.LoadCallback(Score(12));await Wait(()=>!GooglePlayLevelLeaderboard.IsBusy);
  Assert(native.Opens==1&&GooglePlayLevelLeaderboard.LastVerifiedScore==12,"Confirmed score then opens the requested ranking once");
  native=Reset();GooglePlayLevelLeaderboard.ShowLeaderboard();native.SubmitCallback(true);await Wait(()=>native.LoadCallback!=null);native.LoadCallback(Score(0));await Wait(()=>!GooglePlayLevelLeaderboard.IsBusy);
  Assert(GooglePlayLevelLeaderboard.State==GooglePlayLevelLeaderboard.ConnectionState.Error&&GooglePlayLevelLeaderboard.Status.Contains("Google still reports 0"),"A zero readback must not be presented as a successful win sync");
  native=Reset();GooglePlayLevelLeaderboard.ShowLeaderboard();native.SubmitCallback(true);await Wait(()=>native.LoadCallback!=null);native.LoadCallback(Score(12,"0"));await Wait(()=>!GooglePlayLevelLeaderboard.IsBusy);
  Assert(GooglePlayLevelLeaderboard.LastVerifiedScore==12&&GooglePlayLevelLeaderboard.LastGoogleFormattedScore=="0"&&GooglePlayLevelLeaderboard.Status.Contains("display: 0"),"Keep raw Google score separate from Console score formatting");
  native=Reset();Alterverse.Profile.AccountProfilePrefs.Dirty=true;var saved=new TaskCompletionSource<bool>();Alterverse.Shop.PaidShardCommerce.Instance.Save=()=>saved.Task;
  GooglePlayLevelLeaderboard.AuthenticateAndSync(true);Assert(native.Reports==0,"Dirty cloud profile must save before reporting wins");
  Alterverse.Profile.AccountProfilePrefs.Dirty=false;saved.SetResult(true);await Wait(()=>native.SubmitCallback!=null);native.SubmitCallback(true);await Wait(()=>native.LoadCallback!=null);native.LoadCallback(Score(12));await Wait(()=>!GooglePlayLevelLeaderboard.IsBusy);
  Assert(GooglePlayLevelLeaderboard.LastVerifiedScore==12,"Manual sync verifies the cloud-backed score");
  native=Reset();GooglePlayLevelLeaderboard.ShowLeaderboard();native.localUser.id="google-b";native.SubmitCallback(true);await Wait(()=>!GooglePlayLevelLeaderboard.IsBusy);
  Assert(native.Opens==0&&native.Loads==0&&GooglePlayLevelLeaderboard.LastSubmittedScore==-1,"An account switch cancels the old pending ranking operation");
  native=Reset();GooglePlayLevelLeaderboard.ShowLeaderboard();Alterverse.Profile.MatchRewardService.MatchesWon=14;native.SubmitCallback(true);await Wait(()=>native.Reports==2);
  Assert(native.Sent==14&&native.Opens==0,"A win arriving during upload must be coalesced before opening");native.SubmitCallback(true);await Wait(()=>native.LoadCallback!=null);native.LoadCallback(Score(14));await Wait(()=>!GooglePlayLevelLeaderboard.IsBusy);
  Assert(native.Reports==2&&GooglePlayLevelLeaderboard.LastVerifiedScore==14,"Late win is submitted exactly once");
  native=Reset();Alterverse.Profile.AccountServerBootstrap.Instance.GooglePlayAccountReady=false;GooglePlayLevelLeaderboard.ShowLeaderboard();
  Assert(native.Reports==0&&native.Opens==1,"Native browsing remains possible without uploading an unlinked cloud profile");
  native=Reset();GooglePlayLevelLeaderboard.ShowLeaderboard();native.SubmitCallback(false);await Wait(()=>!GooglePlayLevelLeaderboard.IsBusy);
  Assert(native.Opens==0&&GooglePlayLevelLeaderboard.State==GooglePlayLevelLeaderboard.ConnectionState.Error,"A failed upload is visible and leaves the native UI closed");
  native=Reset();Alterverse.Profile.AccountProfilePrefs.Dirty=true;Alterverse.Shop.PaidShardCommerce.Instance.Save=()=>Task.FromResult(false);GooglePlayLevelLeaderboard.AuthenticateAndSync(true);await Wait(()=>!GooglePlayLevelLeaderboard.IsBusy);
  Assert(native.Reports==0&&GooglePlayLevelLeaderboard.State==GooglePlayLevelLeaderboard.ConnectionState.Error,"A failed account save cannot upload stale wins");
  Console.WriteLine(checks+" Google leaderboard sequencing/account/readback checks passed (controlled native services).");
 }
}
