// Production async/handshake paths with controlled HTTP completion, never live services.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Alterverse.Multiplayer;
using Alterverse.Characters;
using Alterverse.AI;
using Alterverse.Match;
using Alterverse.Profile;
using Unity.Services.Authentication;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;
partial class Regression
{
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Put(object o,string field,object value)=>o.GetType().GetField(field,Flags).SetValue(o,value);
 static object Get(object o,string field)=>o.GetType().GetField(field,Flags).GetValue(o);
 static void Invoke(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
 static T Component<T>() where T:new(){var o=new T();Invoke(o,"Awake");return o;}
 static UnityWebRequestAsyncOperation Reply(UnityWebRequest r,string json){r.downloadHandler.text=json;return new();}
 static void ResetFailures()
 {
  var reporter=RankedMatchReporter.Instance;if(reporter!=null)Invoke(reporter,"OnDestroy");
  var handshake=DedicatedOnlineMatchHandshake.Instance;if(handshake!=null)Invoke(handshake,"OnDestroy");
  var service=RankedService.Instance;if(service!=null)Invoke(service,"OnDestroy");
  Resources.RankedConfig=new TextAsset{text="{\"enabled\":true}"};
  UnityWebRequest.Handler=null;CoopAuthority.Active=false;CoopAuthority.PreparedPlan=null;
  CoopNetworkBootstrap.Connects=CoopNetworkBootstrap.Cancels=0;CoopNetworkBootstrap.DirectServerStatus="";
  typeof(EdgegapMatchmakerClient).GetProperty("Instance").SetValue(null,null);
  AuthenticationService.Instance=new AuthenticationService();AccountServerBootstrap.Instance=new();
  Resources.AccountConfig=new TextAsset{text="{\"httpsEndpoint\":\"https://account.example.invalid\"}"};
  DedicatedServerNetworkRuntime.Requested=true;EdgegapInjectedMatchContext.IsEdgegapDeployment=true;
  EdgegapInjectedMatchContext.InjectedMatchId="fixture-match";EdgegapInjectedMatchContext.AssignedTicketCount=MatchSession.MaxPlayers;
  Time.unscaledTime=Time.realtimeSinceStartup=0;
  Environment.SetEnvironmentVariable("DF_RANKED_SERVER","1");
  Environment.SetEnvironmentVariable("DF_RANKED_ACCOUNT_URL","https://account.example.invalid");
  Environment.SetEnvironmentVariable("DF_RANKED_SERVER_KEY",new string('s',32));
 }
 static DedicatedOnlineMatchHandshake PendingRanked(out NetworkManager manager,out RankedMatchReporter reporter)
 {
  ResetFailures();reporter=Component<RankedMatchReporter>();var handshake=Component<DedicatedOnlineMatchHandshake>();
  manager=new NetworkManager{IsServer=true,IsListening=true};NetworkManager.Singleton=manager;
  Put(handshake,"messaging",manager.CustomMessagingManager);
  var humans=new List<OnlineMatchPlan.DedicatedMember>();
  Type humanType=typeof(DedicatedOnlineMatchHandshake).GetNestedType("PendingHuman",BindingFlags.NonPublic);
  var pending=(IDictionary)Get(handshake,"pending");var authorized=(IDictionary)Get(handshake,"authorizedPlayers");
  for(int i=0;i<MatchSession.MaxPlayers;i++)
  {
   ulong id=(ulong)(i+1);string ticket="t"+i;var human=Activator.CreateInstance(humanType,true);
   foreach(var pair in new[]{("TicketId",ticket),("PlayerId",ticket),("TeamId",i<MatchSession.TeamSize?"red":"blue"),("MatchId","fixture-match"),("Admission","signed-"+ticket)})Put(human,pair.Item1,pair.Item2);
   Put(human,"ClientId",id);pending.Add(id,human);authorized.Add(id,ticket);manager.ConnectedClientsIds.Add(id);
   humans.Add(new(){PlayerId=ticket,TeamId=i<MatchSession.TeamSize?"red":"blue",Hero=HeroId.Veyr});
  }
  CoopAuthority.PreparedPlan=OnlineMatchPlan.BuildDedicated("fixture-match",ArenaVariant.Moon,BotDifficulty.Hard,humans,SessionMode.OnlineRanked);
  Put(handshake,"planSent",true);Put(handshake,"planSentAt",0f);return handshake;
 }
 static async Task FailureCases()
 {
  {
   ResetFailures(); var service=Component<RankedService>(); int requests=0;
   UnityWebRequest.Handler=r=>{requests++;return Reply(r,"{}");};
   Resources.RankedConfig=null;
   Check(!RankedAvailability.Enabled,"missing release config locks Ranked");
   Resources.RankedConfig=new TextAsset{text="{\"enabled\":false}"};
   Check(!await service.StartSearchAsync(),"locked Ranked rejects direct queue calls");
   Check(requests==0 && !service.Searching && !service.Busy,"locked queue makes no HTTP calls or state changes");
   Alterverse.UI.RankedPanel.Show();
   Check(!Alterverse.UI.RankedPanel.Visible,"locked Ranked cannot open its panel");
   Resources.RankedConfig=new TextAsset{text="{\"enabled\":true}"};
   Check(RankedAvailability.Enabled,"single release setting enables Ranked");
  }
  int failures=0;
  async Task Case(string name,Func<Task> run){try{await run();}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.GetBaseException().Message);}}
  await Case("two-player casual dedicated start",()=>
  {
   ResetFailures();Environment.SetEnvironmentVariable("DF_RANKED_SERVER","0");
   EdgegapInjectedMatchContext.AssignedTicketCount=2;
   var handshake=Component<DedicatedOnlineMatchHandshake>();
   var manager=new NetworkManager{IsServer=true,IsListening=true};NetworkManager.Singleton=manager;
   Put(handshake,"messaging",manager.CustomMessagingManager);
   for(ulong id=1;id<=2;id++)
   {
    manager.ConnectedClientsIds.Add(id);string ticket="casual-"+id;
    Invoke(handshake,"OnClientHello",id,new FastBufferReader(4,new Unity.Collections.FixedString128Bytes(ticket),
     new Unity.Collections.FixedString128Bytes(id==1?"red":"blue"),new Unity.Collections.FixedString128Bytes("fixture-match"),
     new Unity.Collections.FixedString128Bytes(ticket),new Unity.Collections.FixedString64Bytes("Player"),
     (int)HeroId.Veyr,(int)BattleItemType.None,new Unity.Collections.FixedString4096Bytes("")));
   }
   Invoke(handshake,"UpdateDedicatedServer",manager);
   var plan=(OnlineMatchPlan)CoopAuthority.PreparedPlan;
   Check(plan!=null&&plan.HumanCount==2&&plan.Session.IsFull,"two assigned humans produce full 4v4 plan without waiting for eight humans");
   int bots=0;foreach(var slot in plan.Session.Slots)if(slot.IsBot)bots++;
   Check(bots==6,"two-human match adds exactly six server bots");
   uint fingerprint=(uint)Get(handshake,"planFingerprint");
   for(ulong id=1;id<=2;id++)
    Invoke(handshake,"OnPlanAck",id,new FastBufferReader(4,true,fingerprint,new Unity.Collections.FixedString128Bytes("casual-"+id)));
   Invoke(handshake,"UpdateDedicatedServer",manager);
   Check((bool)Get(handshake,"launched"),"two-player Casual launches once both plans are acknowledged");
   return Task.CompletedTask;
  });
  await Case("4v4 dedicated admission limits",()=>
  {
   ResetFailures();Environment.SetEnvironmentVariable("DF_RANKED_SERVER","0");
   var handshake=Component<DedicatedOnlineMatchHandshake>();
   var manager=new NetworkManager{IsServer=true,IsListening=true};NetworkManager.Singleton=manager;
   void Hello(ulong id,string team,int version=4)
   {
    string ticket="hello-"+id;manager.ConnectedClientsIds.Add(id);
    var reader=new FastBufferReader(version,new Unity.Collections.FixedString128Bytes(ticket),
     new Unity.Collections.FixedString128Bytes(team),new Unity.Collections.FixedString128Bytes("fixture-match"),
     new Unity.Collections.FixedString128Bytes(ticket),new Unity.Collections.FixedString64Bytes("Player"),
     (int)HeroId.Veyr,(int)BattleItemType.None,new Unity.Collections.FixedString4096Bytes(""));
    Invoke(handshake,"OnClientHello",id,reader);
   }
   Hello(90,"red",3);
   Check(manager.ConnectedClientsIds.Count==0,"old 5v5 protocol rejected");
   for(ulong id=1;id<=4;id++)Hello(id,"red");
   Hello(5,"red");
   Check(manager.ConnectedClientsIds.Count==4&&!manager.ConnectedClientsIds.Contains(5),"fifth teammate cannot enter handshake roster");
   for(ulong id=6;id<=9;id++)Hello(id,"blue");
   Hello(10,"blue");
   Check(manager.ConnectedClientsIds.Count==8&&!manager.ConnectedClientsIds.Contains(10),"ninth human cannot enter handshake roster");
   Check(((IDictionary)Get(handshake,"pending")).Count==8,"all eight valid players remain admitted");
   return Task.CompletedTask;
  });
  await Case("unlinked guest cannot earn unrecoverable rank",async()=>
  {
   ResetFailures();var service=Component<RankedService>();int requests=0;
   AuthenticationService.Instance.PlayerInfoRequest=()=>Task.FromResult(new PlayerInfo{Id="user"});
   UnityWebRequest.Handler=r=>{requests++;return Reply(r,Assignment("signed"));};
   Check(!await service.StartSearchAsync(),"guest must link a recoverable account before Ranked");
   Check(requests==0&&CoopNetworkBootstrap.Connects==0,"guest rejection creates no queue or admission");
   Check(!service.Busy&&!service.Searching,"guest rejection leaves the account-link controls usable");
   Check(service.Status.Contains("PROFILE")&&service.Status.Contains("reinstall"),"guest is told how to preserve rank");
  });
  await Case("Google and existing password accounts remain playable",async()=>
  {
   foreach(bool google in new[]{true,false})
   {
    ResetFailures();var service=Component<RankedService>();Component<EdgegapMatchmakerClient>();
    AuthenticationService.Instance.PlayerInfoRequest=()=>Task.FromResult(new PlayerInfo{Id="user",GooglePlayGamesId=google?"google-id":null,Username=google?null:"ExistingUsername"});
    UnityWebRequest.Handler=r=>Reply(r,Assignment("signed"));
    Check(await service.StartSearchAsync(),"recoverable sign-in can search: "+service.Status);
    Check(CoopNetworkBootstrap.Connects==1,"verified account reaches existing connection path");service.MatchStarted();
   }
  });
  await Case("account verification failure",async()=>
  {
   ResetFailures();var service=Component<RankedService>();int requests=0;
   AuthenticationService.Instance.PlayerInfoRequest=()=>Task.FromException<PlayerInfo>(new Exception("Account verification unavailable"));
   UnityWebRequest.Handler=r=>{requests++;return Reply(r,Assignment("signed"));};
   Check(!await service.StartSearchAsync()&&requests==0,"failed identity verification cannot start or cancel a queue");
   Check(!service.Busy&&!service.Searching,"verification failure is retryable");
  });
  await Case("account changes during recovery verification",async()=>
  {
   ResetFailures();var service=Component<RankedService>();var info=new TaskCompletionSource<PlayerInfo>();int requests=0;
   AuthenticationService.Instance.PlayerInfoRequest=()=>info.Task;
   UnityWebRequest.Handler=r=>{requests++;return Reply(r,Assignment("signed"));};
   Task<bool> search=service.StartSearchAsync();AuthenticationService.Instance.PlayerId="new-user";
   info.SetResult(new PlayerInfo{Id="user",GooglePlayGamesId="old-google"});
   Check(!await search&&requests==0,"previous account's link cannot authorize a new account, even before Update");
   Check(!service.Busy&&!service.Searching,"account switch during verification resets search state");
  });
  await Case("cancel during recovery verification",async()=>
  {
   ResetFailures();var service=Component<RankedService>();var info=new TaskCompletionSource<PlayerInfo>();int searches=0;
   AuthenticationService.Instance.PlayerInfoRequest=()=>info.Task;
   UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/search"))searches++;return Reply(r,"{\"fulfilled\":true,\"ranked\":{}}");};
   Task<bool> search=service.StartSearchAsync();await service.CancelAsync();
   info.SetResult(new PlayerInfo{Id="user",GooglePlayGamesId="google"});
   Check(!await search&&searches==0,"late verified identity cannot restart a cancelled search");
   Check(!service.Busy&&!service.Searching,"verification cancellation restores menu controls");
  });
  await Case("fresh client restores rank from same authenticated account",async()=>
  {
   ResetFailures();int statuses=0,leaders=0;string restoredAuthorization=null;
   UnityWebRequest.Handler=r=>
   {
    restoredAuthorization=r.Headers["Authorization"];
    if(r.url.EndsWith("/status")){statuses++;return Reply(r,"{\"fulfilled\":true,\"ranked\":{\"season\":\"S1\",\"rank\":\"Gold 1\",\"points\":1375,\"games\":12,\"wins\":9,\"placementsRemaining\":0,\"cooldownUntil\":123456,\"lastResult\":{\"matchId\":\"saved-match\",\"delta\":-10,\"left\":true}}}");}
    leaders++;return Reply(r,"{\"fulfilled\":true,\"ranked\":{\"leaders\":[{\"displayName\":\"Saved Hero\",\"rank\":\"Gold 1\",\"points\":1375,\"games\":12,\"wins\":9}]}}");
   };
   var original=Component<RankedService>();await original.RefreshAsync();Invoke(original,"OnDestroy");
   AuthenticationService.Instance=new AuthenticationService{PlayerId="user",AccessToken="new-install-token"};
   var fresh=Component<RankedService>();
   Check(fresh.Current==null&&fresh.Leaderboard.Length==0,"new installation begins without a local ranked snapshot");
   await fresh.RefreshAsync();
   Check(statuses==2&&leaders==2&&restoredAuthorization=="Bearer new-install-token","restoration uses the new valid session against the backend");
   Check(fresh.Current.points==1375&&fresh.Current.games==12&&fresh.Current.wins==9&&fresh.Current.placementsRemaining==0,"rank and completed placements are restored");
   Check(fresh.Current.cooldownUntil==123456&&fresh.Current.lastResult.left&&fresh.Current.lastResult.matchId=="saved-match","uninstall does not clear penalties or saved results");
   Check(fresh.Leaderboard.Length==1&&fresh.Leaderboard[0].displayName=="Saved Hero","season leaderboard is independent of the local installation");
  });
  await Case("all eight players confirm and launch",async()=>
  {
   var handshake=PendingRanked(out var manager,out var reporter);int registrations=0,voids=0;
   UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/register"))registrations++;if(r.url.EndsWith("/void"))voids++;return Reply(r,"{}");};
   for(ulong i=1;i<=MatchSession.MaxPlayers;i++)((HashSet<ulong>)Get(handshake,"planAcks")).Add(i);
   var plan=(OnlineMatchPlan)CoopAuthority.PreparedPlan;
   await (Task)handshake.GetType().GetMethod("RegisterRankedAsync",Flags).Invoke(handshake,new object[]{manager,plan});
   Check(registrations==1&&reporter.Registered,"eight confirmed humans register with backend");
   Check((bool)Get(handshake,"launched")&&DedicatedOnlineMatchHandshake.Status.Contains("MATCH LIVE"),"eight-player ranked match launches after registration");
   Check(voids==0&&manager.ConnectedClientsIds.Count==8,"successful launch keeps all eight clients connected");
  });
  await Case("missing ACK",()=>
  {
   var handshake=PendingRanked(out var manager,out var reporter);int voids=0;
   UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/void"))voids++;return Reply(r,"{}");};
   for(ulong i=1;i<MatchSession.MaxPlayers;i++)((HashSet<ulong>)Get(handshake,"planAcks")).Add(i);
   Time.unscaledTime=21;Invoke(handshake,"UpdateDedicatedServer",manager);
   Check((bool)Get(handshake,"launched"),"ranked missing ACK aborts rather than rebuilding a incomplete match");
   Check(voids==1,"missing ACK produces exactly one backend tombstone");
   Check(manager.ConnectedClientsIds.Count==0,"all ranked clients are disconnected on failed confirmation");
   Check(CoopAuthority.PreparedPlan==null,"aborted plan is cleared");return Task.CompletedTask;
  });
  await Case("disconnect before start",()=>
  {
   var handshake=PendingRanked(out var manager,out var reporter);int voids=0;
   UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/void"))voids++;return Reply(r,"{}");};
   manager.ConnectedClientsIds.Remove((ulong)MatchSession.MaxPlayers);Invoke(handshake,"UpdateDedicatedServer",manager);
   Check((bool)Get(handshake,"launched"),"pre-start ranked disconnect terminates the attempt immediately");
   Check(voids==1,"disconnect preserves match id for tombstone");return Task.CompletedTask;
  });
  await Case("delayed registration after void",async()=>
  {
   ResetFailures();var reporter=Component<RankedMatchReporter>();UnityWebRequest held=null;var pending=new UnityWebRequestAsyncOperation{isDone=false};int voids=0;
   UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/register")){held=r;return pending;}voids++;return Reply(r,"{}");};
   var registration=reporter.RegisterAsync("fixture-match",Array.Empty<RankedMatchReporter.Player>());
   await reporter.VoidAsync("fixture-match");await reporter.VoidAsync("fixture-match");
   held.downloadHandler.text="{}";pending.isDone=true;
   Check(!await registration,"late successful registration cannot resurrect a voided reporter");
   Check(!reporter.Registered&&!reporter.ResultPending,"voided reporter stays inactive");
   Check(voids==1,"void retries share one immutable operation");
  });
  await Case("account switch during result",async()=>
  {
   ResetFailures();DedicatedServerNetworkRuntime.Requested=false;var service=Component<RankedService>();
   UnityWebRequest held=null;var pending=new UnityWebRequestAsyncOperation{isDone=false};int calls=0;
   UnityWebRequest.Handler=r=>{calls++;held=r;return pending;};
   var result=service.RefreshResultAsync("fixture-match");
   AuthenticationService.Instance.PlayerId="another-account";Invoke(service,"Update");
   held.downloadHandler.text="{\"fulfilled\":true,\"ranked\":{\"points\":2000}}";pending.isDone=true;
   Check(await Task.WhenAny(result,Task.Delay(1000))==result,"old result polling stops immediately after account switch");
   Check(service.Current==null&&service.ResultText=="","old result cannot populate new account UI");
   Check(calls==1,"old result never requests another account");
  });
  await Case("cancel during creation",async()=>
  {
   ResetFailures();var service=Component<RankedService>();Component<EdgegapMatchmakerClient>();
   UnityWebRequest held=null;var pending=new UnityWebRequestAsyncOperation{isDone=false};int cancels=0;
   UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/search")){held=r;return pending;}cancels++;return Reply(r,"{\"fulfilled\":true,\"ranked\":{\"status\":\"CANCELLED\"}}");};
   Task<bool> search=service.StartSearchAsync();Task cancel=service.CancelAsync();
   Check(cancels==0,"cancel waits for original queue creation");
   held.downloadHandler.text=Assignment("signed-A");pending.isDone=true;
   Check(!await search,"cancelled delayed assignment is discarded");await cancel;
   Check(cancels==1&&CoopNetworkBootstrap.Connects==0,"late assignment cannot connect after cancellation");
   Check(!service.Busy&&!service.Searching&&service.Admission=="","cancel resets searchable UI");
  });
  await Case("account switch during rank refresh",async()=>
  {
   ResetFailures();var service=Component<RankedService>();
   UnityWebRequest status=null,queue=null;var old=new UnityWebRequestAsyncOperation{isDone=false};var fresh=new UnityWebRequestAsyncOperation{isDone=false};
   UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/status")){status=r;return old;}queue=r;return fresh;};
   Task refresh=service.RefreshAsync();AuthenticationService.Instance.PlayerId="new-user";Invoke(service,"Update");
   Task<bool> search=service.StartSearchAsync();Check(service.Busy,"new account search owns busy state");
   status.downloadHandler.text="{\"fulfilled\":true,\"ranked\":{\"points\":9000}}";old.isDone=true;await refresh;
   Check(service.Busy&&service.Searching&&service.Current==null,"old refresh cannot unlock or populate new search");
   Check(service.Status.Contains("SEARCHING"),"old refresh cannot overwrite current search status");
   var cancel=service.CancelAsync();queue.downloadHandler.text="{\"fulfilled\":true,\"ranked\":{}}";fresh.isDone=true;
   await search;await cancel;
  });
  await Case("account switch during cancel",async()=>
  {
   ResetFailures();var service=Component<RankedService>();Component<EdgegapMatchmakerClient>();
   UnityWebRequest oldRequest=null;var old=new UnityWebRequestAsyncOperation{isDone=false};int cancels=0;
   UnityWebRequest.Handler=r=>
   {
    if(r.url.EndsWith("/cancel")){cancels++;return Reply(r,"{\"fulfilled\":true,\"ranked\":{}}");}
    if(r.Headers["Authorization"]=="Bearer jwt"){oldRequest=r;return old;}
    return Reply(r,Assignment("signed-B"));
   };
   Task<bool> oldSearch=service.StartSearchAsync();Task oldCancel=service.CancelAsync();
   AuthenticationService.Instance.PlayerId="new-user";AuthenticationService.Instance.AccessToken="jwt-B";Invoke(service,"Update");
   Check(await service.StartSearchAsync(),"new account can search after ownership changes: "+service.Status);
   oldRequest.downloadHandler.text=Assignment("signed-A");old.isDone=true;await oldSearch;await oldCancel;
   Check(cancels==0,"old cancel never cancels new account queue");
   Check(service.Searching&&service.Admission=="signed-B"&&!service.Busy,"old finally cannot wipe new assignment");
   Check(CoopNetworkBootstrap.Connects==1,"only new account connects");service.MatchStarted();
  });
  await Case("ranked start and invalid port",async()=>
  {
   foreach(int port in new[]{7777,70000})
   {
    ResetFailures();var service=Component<RankedService>();var matchmaker=Component<EdgegapMatchmakerClient>();int cancels=0;
    UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/cancel")){cancels++;return Reply(r,"{\"fulfilled\":true,\"ranked\":{}}");}return Reply(r,Assignment("signed",port));};
    bool started=await service.StartSearchAsync();Check(started==(port==7777),"valid external port required: "+service.Status);
    Check(CoopNetworkBootstrap.Connects==(port==7777?1:0),"invalid port never reaches transport");
    if(started){Check(service.Searching&&service.Admission=="signed"&&matchmaker.TicketId=="t0","assignment retains admission until handshake");service.MatchStarted();Check(!service.Searching&&service.Admission=="","launch completes ranked search");}
    else Check(cancels==1&&!service.Searching&&!service.Busy,"bad assignment cleans backend queue");
   }
  });
  await Case("casual ACK behavior",()=>
  {
   var handshake=PendingRanked(out var manager,out var reporter);Environment.SetEnvironmentVariable("DF_RANKED_SERVER","0");
   for(ulong i=1;i<MatchSession.MaxPlayers;i++)((HashSet<ulong>)Get(handshake,"planAcks")).Add(i);
   Time.unscaledTime=21;Invoke(handshake,"UpdateDedicatedServer",manager);
   Check(!(bool)Get(handshake,"launched"),"casual waits for rebuilt ACKs");
   Check(manager.ConnectedClientsIds.Count==MatchSession.MaxPlayers-1,"casual only evicts missing ACK");
   Check(CoopAuthority.PreparedPlan.Session.Mode==SessionMode.OnlineCasual&&CoopAuthority.PreparedPlan.HumanCount==MatchSession.MaxPlayers-1,"casual rebuild retains bot fill");return Task.CompletedTask;
  });
  await Case("old ranked queue response cannot unlock a new search",async()=>
  {
   ResetFailures();var service=Component<RankedService>();Component<EdgegapMatchmakerClient>();
   UnityWebRequest oldQueue=null,newQueue=null;
   var oldPending=new UnityWebRequestAsyncOperation{isDone=false};var newPending=new UnityWebRequestAsyncOperation{isDone=false};
   UnityWebRequest.Handler=r=>
   {
    if(r.url.EndsWith("/cancel"))return Reply(r,"{}");
    if(r.Headers["Authorization"]=="Bearer jwt"){oldQueue=r;return oldPending;}
    newQueue=r;return newPending;
   };
   var oldSearch=service.StartSearchAsync();
   AuthenticationService.Instance.PlayerId="new-ranked-account";AuthenticationService.Instance.AccessToken="jwt-B";Invoke(service,"Update");
   var newSearch=service.StartSearchAsync();
   Check(oldQueue!=null&&newQueue!=null&&service.Busy,"new account queue request is pending");
   oldQueue.downloadHandler.text="{\"fulfilled\":true,\"ranked\":{}}";oldPending.isDone=true;
   await oldSearch;
   Check(service.Busy&&service.Searching,"old search response cannot clear new queue's busy state");
   var cancel=service.CancelAsync();newQueue.downloadHandler.text="{\"fulfilled\":true,\"ranked\":{}}";newPending.isDone=true;
   await Task.WhenAll(newSearch,cancel);
  });
  await Case("leaver receipt and retry",async()=>
  {
   foreach(bool longAbsence in new[]{false,true})
   {
    var handshake=PendingRanked(out var manager,out var reporter);int results=0;string first=null;
    UnityWebRequest.Handler=r=>
    {
     Check(r.Headers["Authorization"]=="Bearer "+new string('s',32),"reporter uses dedicated server credential");
     if(r.url.EndsWith("/result"))
     {
      string body=System.Text.Encoding.UTF8.GetString(r.uploadHandler.data);var receipt=JsonUtility.FromJson<ReceiptFixture>(body);
      Check(receipt.matchId=="fixture-match"&&receipt.winner==0,"immutable result identifies match and winner");
      Check(receipt.leavers.Length==(longAbsence?1:0),"60 second leaver threshold applies on final frame");
      if(longAbsence)Check(receipt.leavers[0]=="t0","only disconnected authorized ticket is penalized");
      if(results++==0){first=body;r.result=UnityWebRequest.Result.ConnectionError;r.responseCode=503;}else Check(first==body,"receipt retry preserves identical payload");
     }
     return Reply(r,"{}");
    };
    var players=new RankedMatchReporter.Player[MatchSession.MaxPlayers];for(int i=0;i<MatchSession.MaxPlayers;i++)players[i]=new(){ticket="t"+i,admission="signed",team=i/MatchSession.TeamSize};
    Check(await reporter.RegisterAsync("fixture-match",players),"reporter registration succeeds");
    CoopAuthority.Active=true;manager.ConnectedClientsIds.Remove(1);Invoke(reporter,"Update");
    Time.realtimeSinceStartup=longAbsence?60:59.9f;
    Task settle=reporter.SettleAsync(0);Check(reporter.ResultPending,"self-stop waits while result receipt retries");
    await settle;await reporter.SettleAsync(1);
    Check(results==2&&!reporter.ResultPending,"one result with retry, duplicate finish is ignored");
   }
  });
  ResetFailures();if(failures>0)throw new Exception(failures+" ranked failure-path cases failed");
 }
 static string Assignment(string admission,int port=7777)=>"{\"fulfilled\":true,\"ranked\":{\"ticket_id\":\"t0\",\"group_id\":\"g0\",\"member_id\":\"u0\",\"team_id\":\"red\",\"match_id\":\"fixture-match\",\"admission\":\""+admission+"\",\"assignment\":{\"fqdn\":\"game.example.invalid\",\"ports\":{\"gameport\":{\"external\":"+port+"}}}}}";
 [Serializable]class ReceiptFixture {public string matchId;public int winner;public string[] leavers;}
}
