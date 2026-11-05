using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Alterverse.Profile;
using Alterverse.Shop;
using GooglePlayGames;
using Unity.Services.Authentication;
using UnityEngine;
static class Regression
{
 static int checks;
 static void Check(bool valid,string message){checks++;if(!valid)throw new Exception(message);}
 static object Invoke(object obj,string method)=>obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null);
 static Task<bool> Verify(AccountServerBootstrap b)=>(Task<bool>)Invoke(b,"VerifyGoogleLinkAsync");
 static string Hash(string id)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant();
 static void Attach(string player,long revision=0)=>AccountProfilePrefs.Attach(Hash(player),revision,new[]{
  new AccountProfilePrefs.Entry{key="DF_PlayerName",kind=1,text="SolarFox0000001"},
  new AccountProfilePrefs.Entry{key="DF_Shards",kind=0,number=777},
  new AccountProfilePrefs.Entry{key="DF_AccountXP",kind=1,text="12345"}});
 static AccountServerBootstrap NewStartup()
 {
  if(AccountServerBootstrap.Instance!=null)Invoke(AccountServerBootstrap.Instance,"OnDestroy");
  AccountProfilePrefs.Detach();PaidShardCommerce.Instance=null;PaidShardCommerce.Connections=0;PaidShardCommerce.OnConnect=null;
  Resources.AccountConfigured=true;PlayerPrefs.DeleteKey("DF_KnownUnityAccountOnDevice_v1");
  AuthenticationService.Instance=new(){IsSignedIn=false,IsAuthorized=false,SessionTokenExists=false};
  PlayGamesPlatform.Instance=new(){Name="Only Play Player"};
  return new GameObject("startup-account").AddComponent<AccountServerBootstrap>();
 }
 static async Task StartupCases()
 {
  var b=NewStartup();var auth=AuthenticationService.Instance;var sdk=PlayGamesPlatform.Instance;
  auth.IsSignedIn=auth.IsAuthorized=auth.SessionTokenExists=true;
  auth.PlayerId="old-generated-account";auth.Provider=null;auth.GooglePlayerId="verified-google-delayed";
  PlayerPrefs.SetInt("DF_KnownUnityAccountOnDevice_v1",1);
  PlayerPrefs.SetString("DF_PlayerName","OldGeneratedPlayer");
  PlayerPrefs.SetInt("DF_TestHero",1);PlayerPrefs.SetInt("DF_Shards",9999);
  sdk.Authenticated=false;sdk.DelayAuthentication=true;
  var info=new TaskCompletionSource<PlayerInfo>();auth.Info=()=>info.Task;
  var menu=new Alterverse.UI.MainMenuController();
  Alterverse.Characters.PlayerHeroSelection.Reads=0;
  var first=b.EnsureAuthenticatedAsync();var second=b.EnsureAuthenticatedAsync();
  Check(ReferenceEquals(first,second),"delayed startup callers share one native Google sign-in");
  Check(!menu.PrepareForTest()&&Alterverse.Characters.PlayerHeroSelection.Reads==0&&menu.SelectedForTest==-1,
   "cold menu never reads or initializes the old device profile before native Google sign-in");
  menu.DrawStartupForTest();
  Check(!string.Join(" ",Alterverse.UI.FrontEndTheme.Labels).Contains("OldGeneratedPlayer"),
   "startup presentation contains no generated-account identity");
  Check(auth.GoogleSignIns==0&&auth.AnonymousSignIns==0&&PaidShardCommerce.Connections==0,
   "native authorization is required before any backend sign-in");
  sdk.Authenticated=true;sdk.PendingAuthentication(GooglePlayGames.BasicApi.SignInStatus.Success);
  Check(auth.GoogleSignIns==1&&auth.PlayerId=="verified-google-delayed"&&auth.Links==0&&auth.AnonymousSignIns==0&&!auth.LastCreateAccount,
   "an existing installation selects its Google provider directly without restoring or linking the cached guest");
  Check(PaidShardCommerce.Connections==0&&!menu.PrepareForTest()&&Alterverse.Characters.PlayerHeroSelection.Reads==0,
   "provider verification remains a separate gate before profile activation");
  info.SetResult(new PlayerInfo{Google=sdk.localUser.id});
  Check(await first&&await second&&PaidShardCommerce.Connections==1,
   "verified Google startup connects exactly one backend account");
  Check(!AccountProfilePrefs.Attached&&!menu.PrepareForTest()&&Alterverse.Characters.PlayerHeroSelection.Reads==0,
   "native sign-in success cannot expose local progress while the cloud save is loading");
  var backend=PaidShardCommerce.Instance;
  backend.Synchronizing=true;backend.Ready=false;backend.Status="Account server not configured.";
  Alterverse.UI.FrontEndTheme.Actions.Clear();menu.DrawStartupForTest();
  Check(Alterverse.UI.FrontEndTheme.Actions.Count==1&&
   Alterverse.UI.FrontEndTheme.Actions[0].Text=="SIGNING IN  •  PLEASE WAIT…"&&
   !Alterverse.UI.FrontEndTheme.Actions[0].Enabled,
   "verified Google login with pending cloud HTTP shows a disabled wait action despite stale status text");
  backend.Synchronizing=false;backend.Status="Account connection interrupted. Saved operations will retry.";
  Alterverse.UI.FrontEndTheme.Actions.Clear();menu.DrawStartupForTest();
  Check(Alterverse.UI.FrontEndTheme.Actions.Count==1&&Alterverse.UI.FrontEndTheme.Actions[0].Text=="TRY AGAIN"&&
   Alterverse.UI.FrontEndTheme.Actions[0].Enabled,"completed failed cloud sync offers retry");
  backend.Synchronizing=true;
  Alterverse.UI.FrontEndTheme.Actions.Clear();menu.DrawStartupForTest();
  Check(Alterverse.UI.FrontEndTheme.Actions[0].Text=="SIGNING IN  •  PLEASE WAIT…"&&
   !Alterverse.UI.FrontEndTheme.Actions[0].Enabled,"a retry in flight hides TRY AGAIN even with stale error text");
  backend.Synchronizing=false;backend.Ready=true;
  Attach(auth.PlayerId,1);
  AccountProfilePrefs.SetInt("DF_TestHero",2);AccountProfilePrefs.SetInt("DF_TestArena",3);
  AccountProfilePrefs.SetFloat("DF_MasterVolume",.25f);
  AccountProfilePrefs.SetString(PlayerProfile.PlayGamesNameKey,"Old saved alias");
  Check(menu.PrepareForTest()&&menu.SelectedForTest==2&&menu.ArenaForTest==3&&menu.MasterForTest==.25f,
   "the first visible account menu initializes selections and audio from the verified cloud owner");
  Check(PlayerProfile.PlayerName==sdk.Name&&AccountProfilePrefs.GetInt("DF_Shards")==777,
   "native Google name and cloud balance appear together instead of the old cached alias/balance");
  int reads=Alterverse.Characters.PlayerHeroSelection.Reads;
  Check(menu.PrepareForTest()&&Alterverse.Characters.PlayerHeroSelection.Reads==reads,
   "a stable menu account is initialized once instead of reloading preferences each frame");
  Check(PlayerPrefs.GetString("DF_PlayerName")=="OldGeneratedPlayer"&&PlayerPrefs.GetInt("DF_Shards")==9999,
   "ignoring the legacy profile does not delete its stored progress");
  AccountProfilePrefs.Detach();
  Check(!menu.PrepareForTest(),"losing the verified cloud attachment immediately hides account presentation");

  b=NewStartup();auth=AuthenticationService.Instance;
  Check(await b.EnsureAuthenticatedAsync()&&auth.GoogleSignIns==1&&auth.LastCreateAccount&&auth.AnonymousSignIns==0&&auth.Links==0,
   "a fresh player creates only a Google-backed identity");
  b=NewStartup();auth=AuthenticationService.Instance;auth.SessionTokenExists=true;
  PlayerPrefs.SetInt("DF_KnownUnityAccountOnDevice_v1",1);auth.GooglePlayerId="existing-google-account";
  Check(await b.EnsureAuthenticatedAsync()&&auth.PlayerId=="existing-google-account"&&!auth.LastCreateAccount&&auth.AnonymousSignIns==0,
   "a previously connected installation restores the existing Google identity without replacement creation");
  b=NewStartup();auth=AuthenticationService.Instance;auth.IsSignedIn=auth.IsAuthorized=true;
  auth.PlayerId="legacy-unlinked";auth.Provider=null;
  Check(await b.EnsureAuthenticatedAsync()&&auth.PlayerId==auth.GooglePlayerId&&auth.GoogleSignIns==1&&auth.Links==0&&auth.AnonymousSignIns==0,
   "an already signed-in unlinked legacy account is not revived or linked automatically");

  b=NewStartup();auth=AuthenticationService.Instance;auth.SessionTokenExists=true;auth.FailGoogle=true;
  Check(!await b.EnsureAuthenticatedAsync()&&auth.AnonymousSignIns==0&&auth.Links==0&&PaidShardCommerce.Connections==0&&!b.GooglePlayAccountReady,
   "a failed Google provider sign-in cannot fall back to legacy session recovery");
  b=NewStartup();auth=AuthenticationService.Instance;auth.SessionTokenExists=true;sdk=PlayGamesPlatform.Instance;sdk.Authenticated=false;
  Check(!await b.EnsureAuthenticatedAsync()&&auth.AnonymousSignIns==0&&auth.GoogleSignIns==0&&PaidShardCommerce.Connections==0,
   "a failed native sign-in cannot restore or create any account");
  Check(!AccountProfilePrefs.Attached&&PlayerProfile.PlayerName=="SIGN IN REQUIRED",
   "failed login has no visible local identity");

  b=NewStartup();auth=AuthenticationService.Instance;auth.IsSignedIn=auth.IsAuthorized=true;
  auth.PlayerId=auth.GooglePlayerId="cached-google-player";sdk=PlayGamesPlatform.Instance;
  sdk.Authenticated=false;sdk.ManualSucceeds=true;
  Check(!await b.EnsureAuthenticatedAsync()&&PaidShardCommerce.Connections==0,
   "failed silent login leaves the cached Unity session inactive");
  Check(await b.ConnectGoogleAsync()&&sdk.ManualSignIns==1&&b.GooglePlayAccountReady&&auth.PlayerId=="cached-google-player",
   "interactive retry selects the actual Google identity");
  Check(auth.GoogleSignIns==1&&auth.AnonymousSignIns==0&&auth.Links==0&&PaidShardCommerce.Connections==1,
   "interactive recovery cannot revive or link an old guest account");

  b=NewStartup();auth=AuthenticationService.Instance;auth.IsSignedIn=auth.IsAuthorized=true;
  sdk=PlayGamesPlatform.Instance;sdk.Authenticated=false;
  Check(!await b.ConnectGoogleAsync()&&sdk.ManualSignIns==1&&PaidShardCommerce.Connections==0&&auth.GoogleSignIns==0&&auth.Links==0,
   "cancelled native login cannot activate the cached account");
  b=NewStartup();auth=AuthenticationService.Instance;auth.IsSignedIn=auth.IsAuthorized=true;
  auth.Provider="different-google";
  Check(await b.EnsureAuthenticatedAsync()&&auth.PlayerId==auth.GooglePlayerId&&auth.Links==0&&PaidShardCommerce.Connections==1,
   "a stale cached provider cannot override the Google account selected at startup");
  Check(!await b.SignInAsync("other-user","password",false)&&!await b.SignInAsync("other-user","password",true)&&auth.UsernameSignIns==0,
   "Android compatibility APIs cannot create or select a username account");

  b=NewStartup();auth=AuthenticationService.Instance;
  Check(await b.EnsureAuthenticatedAsync(),"prepare a verified cloud account for explicit account switching");
  Attach(auth.PlayerId);var oldBackend=PaidShardCommerce.Instance;oldBackend.SaveOnFlush=true;
  PlayGamesPlatform.Instance.localUser.id="google-b";auth.GooglePlayerId="unity-play-b";
  Check(await b.ConnectGoogleAsync(true)&&oldBackend.FlushCalls>=2&&auth.PlayerId=="unity-play-b"&&!auth.LastCreateAccount&&b.GooglePlayAccountReady,
   "explicit active-account restore saves the existing cloud profile before selecting the other Google identity");

  b=NewStartup();auth=AuthenticationService.Instance;auth.GooglePlayerId="saved-before-failed-restore";
  Check(await b.EnsureAuthenticatedAsync(),"prepare account for failed restore");
  Attach(auth.PlayerId);oldBackend=PaidShardCommerce.Instance;oldBackend.SaveOnFlush=true;
  string savedHash=Hash(auth.PlayerId);
  PlayGamesPlatform.Instance.localUser.id="google-c";auth.FailGoogle=true;
  Check(!await b.ConnectGoogleAsync(true)&&!auth.IsSignedIn&&auth.AnonymousSignIns==0&&!AccountProfilePrefs.Attached,
   "failed explicit restore stays signed out instead of recovering an old account anonymously");
  Check(oldBackend.FlushCalls>=2&&PlayerPrefs.GetString("DF_AccountProfileCache_"+savedHash).Contains("777"),
   "failed restore preserves the previous account's saved cloud-profile cache");
  Resources.AccountConfigured=false;PaidShardCommerce.OnConnect=null;
  Console.WriteLine("PASS: delayed Google-only startup, hidden legacy presentation, correct cloud selections, retry/restore isolation and no Android guest recovery.");
 }
 static async Task Main()
 {
  var bootstrap=new GameObject("account").AddComponent<AccountServerBootstrap>();
#if UNITY_ANDROID && !UNITY_EDITOR
  var sdk=PlayGamesPlatform.Instance;var auth=AuthenticationService.Instance;
  Check(GooglePlayAccountSession.Supported,"Android native path compiled");
  sdk.Authenticated=false;Check(GooglePlayAccountSession.DisplayName==null,"signed-out SDK has no display name");
  Check(!await Verify(bootstrap),"native sign-out cannot adopt name");sdk.Authenticated=true;
  sdk.ThrowName=true;Check(GooglePlayAccountSession.DisplayName==null,"SDK display failure is contained");sdk.ThrowName=false;
  Check(GooglePlayAccountSession.DisplayName==sdk.Name,"SDK getter supplies Google Play Games name");
  Attach(auth.PlayerId);auth.Provider="another-google";
  Check(!await Verify(bootstrap)&&PlayerProfile.PlayerName=="CONNECTING..."&&
        AccountProfilePrefs.GetString(PlayerProfile.PlayGamesNameKey,"")=="","wrong Google provider cannot rename account or display a provisional identity");
  auth.Provider="google-a";
  var pending=new TaskCompletionSource<PlayerInfo>();auth.Info=()=>pending.Task;
  var verifying=Verify(bootstrap);sdk.localUser.id="google-b";pending.SetResult(new PlayerInfo{Google="google-a"});
  Check(!await verifying&&PlayerProfile.PlayerName=="CONNECTING...","native account change during verification rejected");
  sdk.localUser.id="google-a";pending=new();auth.Info=()=>pending.Task;
  verifying=Verify(bootstrap);auth.PlayerId="unity-b";pending.SetResult(new PlayerInfo{Google="google-a"});
  Check(!await verifying&&PlayerProfile.PlayerName=="CONNECTING...","Unity account change during verification rejected");
  auth.PlayerId="unity-a";auth.Info=null;AccountProfilePrefs.Detach();
  Check(await Verify(bootstrap)&&bootstrap.GooglePlayAccountReady,"matching provider is verified before cloud load");
  Check(!AccountProfilePrefs.Attached,"verification does not create a substitute profile");
  PaidShardCommerce.Instance=new(){AccountHash=Hash(auth.PlayerId)};Attach(auth.PlayerId);PaidShardCommerce.Raise();
  Check(PlayerProfile.PlayerName==sdk.Name,"late cloud-load event adopts Google name");
  Check(PaidShardCommerce.Instance.FlushCalls==1,"adopted alias immediately schedules cloud persistence");
  Check(AccountProfilePrefs.GetString("DF_PlayerName")=="SolarFox0000001"&&AccountProfilePrefs.GetInt("DF_Shards")==777&&AccountProfilePrefs.GetString("DF_AccountXP")=="12345","canonical identity, currency and XP preserved");
  var submitted=AccountProfilePrefs.Capture();AccountProfilePrefs.ApplyRemote(1,submitted,submitted);
  int saves=PlayerPrefs.Saves;PaidShardCommerce.Raise();PaidShardCommerce.Raise();
  Check(PlayerPrefs.Saves==saves&&!AccountProfilePrefs.Dirty,"unchanged name produces no profile write loop");
  Check(PaidShardCommerce.Instance.FlushCalls==1,"unchanged name produces no cloud request loop");
  sdk.Name="Neuer Google Name";PaidShardCommerce.Raise();
  Check(PlayerProfile.PlayerName==sdk.Name&&AccountProfilePrefs.Dirty,"next verified sync refreshes changed Google name");
  sdk.Name=null;PaidShardCommerce.Raise();Check(PlayerProfile.PlayerName=="Neuer Google Name","empty SDK reply keeps cached account alias");
  Attach("unity-b");sdk.Name="Wrong Owner";PaidShardCommerce.Raise();
  Check(PlayerProfile.PlayerName=="CONNECTING..."&&AccountProfilePrefs.GetString(PlayerProfile.PlayGamesNameKey,"")=="","late cloud event cannot write to a different attached owner");
  Attach("unity-a",1);auth.IsAuthorized=false;PaidShardCommerce.Raise();
  Check(PlayerProfile.PlayerName=="CONNECTING..."&&AccountProfilePrefs.GetString(PlayerProfile.PlayGamesNameKey)=="Neuer Google Name","expired authorization cannot replace alias");auth.IsAuthorized=true;
  Invoke(bootstrap,"OnSignedOut");Attach("unity-c");PaidShardCommerce.Raise();
  Check(PlayerProfile.PlayerName=="CONNECTING..."&&!bootstrap.GooglePlayAccountReady,"sign-out clears verified binding");
  AccountProfilePrefs.Detach();Attach("unity-a");sdk.Name="Direct verified name";
  Check(await Verify(bootstrap)&&AccountProfilePrefs.GetString(PlayerProfile.PlayGamesNameKey)==sdk.Name,"already-loaded profile persists verified name even before wallet readiness");
  await StartupCases();
#else
  Check(!GooglePlayAccountSession.Supported&&!GooglePlayAccountSession.IsAuthenticated,"non-Android native code excluded");
  Check(GooglePlayAccountSession.DisplayName==null,"non-Android has no Google SDK name");
  Attach("desktop");Check(!await Verify(bootstrap)&&PlayerProfile.PlayerName=="SolarFox0000001","desktop retains fallback without verified link");
#endif
  Console.WriteLine(checks+" production Google-name account checks passed (controlled SDK, not an Android device).");
 }
}


