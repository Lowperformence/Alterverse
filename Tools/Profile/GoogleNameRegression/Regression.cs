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
#else
  Check(!GooglePlayAccountSession.Supported&&!GooglePlayAccountSession.IsAuthenticated,"non-Android native code excluded");
  Check(GooglePlayAccountSession.DisplayName==null,"non-Android has no Google SDK name");
  Attach("desktop");Check(!await Verify(bootstrap)&&PlayerProfile.PlayerName=="SolarFox0000001","desktop retains fallback without verified link");
#endif
  Console.WriteLine(checks+" production Google-name account checks passed (controlled SDK, not an Android device).");
 }
}
