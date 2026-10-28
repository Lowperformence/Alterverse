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
  var info=new TaskCompletionSource<PlayerInfo>();auth.Info=()=>info.Task;
  string localName=PlayerPrefs.GetString("DF_PlayerName","");
  var first=b.EnsureAuthenticatedAsync();var second=b.EnsureAuthenticatedAsync();
  Check(ReferenceEquals(first,second)&&auth.GoogleSignIns==1,"concurrent startup callers share one Google sign-in");
  Check(auth.AnonymousSignIns==0&&auth.LastCreateAccount,"fresh Android uses Google directly, never an anonymous substitute");
  Check(PaidShardCommerce.Connections==0&&!AccountProfilePrefs.Attached,"SignedIn cannot start wallet/profile sync before provider verification");
  Check(PlayerProfile.PlayerName=="CONNECTING..."&&PlayerPrefs.GetString("DF_PlayerName","")==localName,
   "waiting for verification never displays or persists a generated identity");
  PaidShardCommerce.OnConnect=account=>Check(b.GooglePlayAccountReady&&account==Hash(auth.PlayerId),
   "only the verified Unity/Google binding can connect its wallet");
  info.SetResult(new PlayerInfo{Google=sdk.localUser.id});
  Check(await first&&await second&&PaidShardCommerce.Connections==1,"verified startup connects exactly one backend account");
  Check(!AccountProfilePrefs.Attached,"native success does not fabricate a cloud profile");
  Attach(auth.PlayerId);
  Check(AccountProfilePrefs.GetString(PlayerProfile.PlayGamesNameKey,"")==""&&PlayerProfile.PlayerName=="CONNECTING...",
   "first cloud frame waits for the verified Play alias instead of displaying the generated server name");
  sdk.Name=null;
  Check(PlayerProfile.PlayerName=="CONNECTING...","missing Google name never falls back to the server-generated name");
  sdk.Name="Only Play Player";PaidShardCommerce.Raise();
  Check(PlayerProfile.PlayerName==sdk.Name&&AccountProfilePrefs.GetInt("DF_Shards")==777,"Google-only display preserves the account's progress");

  b=NewStartup();auth=AuthenticationService.Instance;auth.SessionTokenExists=true;
  auth.GooglePlayerId="existing-google-account";
  Check(await b.EnsureAuthenticatedAsync()&&auth.PlayerId=="existing-google-account"&&
   !auth.LastCreateAccount&&auth.AnonymousSignIns==0&&PaidShardCommerce.Connections==1,
   "cached legacy session cannot replace an existing Google account or create another identity");

  b=NewStartup();auth=AuthenticationService.Instance;auth.IsSignedIn=auth.IsAuthorized=true;
  auth.PlayerId="legacy-in-place";auth.Provider=null;
  Check(await b.EnsureAuthenticatedAsync()&&auth.PlayerId=="legacy-in-place"&&auth.Links==1&&auth.GoogleSignIns==0&&auth.AnonymousSignIns==0,
   "an already signed-in legacy identity is linked in place instead of creating a second account");
  Check(PaidShardCommerce.Connections==1&&PaidShardCommerce.Instance.AccountHash==Hash("legacy-in-place"),
   "legacy profile connects only once after its Google link is verified");

  b=NewStartup();auth=AuthenticationService.Instance;auth.SessionTokenExists=true;auth.FailGoogle=true;
  Check(await b.EnsureAuthenticatedAsync()&&auth.AnonymousSignIns==1&&auth.AnonymousCreates==0&&auth.Links==1,
   "legacy migration restores only an existing token after native Google authorization, then links the same identity");
  Check(PaidShardCommerce.Connections==1&&b.GooglePlayAccountReady,"legacy restore cannot expose an unverified guest wallet");

  b=NewStartup();auth=AuthenticationService.Instance;auth.SessionTokenExists=true;
  PlayGamesPlatform.Instance.Authenticated=false;
  Check(!await b.EnsureAuthenticatedAsync()&&auth.AnonymousSignIns==0&&auth.GoogleSignIns==0&&PaidShardCommerce.Connections==0,
   "failed native Google login cannot restore or create a background guest account");
  Check(!AccountProfilePrefs.Attached&&PlayerProfile.PlayerName=="SIGN IN REQUIRED",
   "failed login has no active local profile or generated display identity");

  b=NewStartup();auth=AuthenticationService.Instance;auth.FailGoogle=true;
  Check(!await b.EnsureAuthenticatedAsync()&&auth.AnonymousSignIns==0&&PaidShardCommerce.Connections==0,
   "a fresh Google service failure never falls back to anonymous account creation");

  b=NewStartup();auth=AuthenticationService.Instance;auth.IsSignedIn=auth.IsAuthorized=true;
  auth.Provider="different-google";
  Check(!await b.EnsureAuthenticatedAsync()&&PaidShardCommerce.Connections==0&&auth.Links==0,
   "a cached Unity identity linked to another Google player never connects its profile in the background");
  Check(!await b.SignInAsync("other-user","password",false)&&!await b.SignInAsync("other-user","password",true)&&
   auth.UsernameSignIns==0,"Android compatibility APIs cannot switch to a second username account");
  Check(await b.ConnectGoogleAsync(true)&&auth.PlayerId==auth.GooglePlayerId&&!auth.LastCreateAccount&&
   auth.AnonymousSignIns==0&&PaidShardCommerce.Connections==1,
   "manual restore can recover the existing Google account after an unverified cached provider mismatch");

  b=NewStartup();auth=AuthenticationService.Instance;
  Check(await b.EnsureAuthenticatedAsync(),"prepare a verified cloud account for an explicit Google-account change");
  Attach(auth.PlayerId);var oldBackend=PaidShardCommerce.Instance;oldBackend.SaveOnFlush=true;
  PlayGamesPlatform.Instance.localUser.id="google-b";auth.GooglePlayerId="unity-play-b";
  Check(await b.ConnectGoogleAsync(true)&&oldBackend.FlushCalls>=2&&auth.PlayerId=="unity-play-b"&&
   !auth.LastCreateAccount&&b.GooglePlayAccountReady,
   "restoring from an active cloud profile still flushes existing progress before selecting the other verified Google account");
  Resources.AccountConfigured=false;PaidShardCommerce.OnConnect=null;
  Console.WriteLine("PASS: Google-only startup, verified profile activation, safe in-place migration, failure isolation and no anonymous creation.");
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
