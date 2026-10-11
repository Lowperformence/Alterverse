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
  UnityWebRequest.Handler=null;CoopAuthority.Active=false;CoopAuthority.PreparedPlan=null;
  CoopNetworkBootstrap.Connects=CoopNetworkBootstrap.Cancels=0;CoopNetworkBootstrap.DirectServerStatus="";
  typeof(EdgegapMatchmakerClient).GetProperty("Instance").SetValue(null,null);
  AuthenticationService.Instance=new AuthenticationService();AccountServerBootstrap.Instance=new();
  Resources.AccountConfig=new TextAsset{text="{\"httpsEndpoint\":\"https://account.example.invalid\"}"};
  DedicatedServerNetworkRuntime.Requested=true;EdgegapInjectedMatchContext.IsEdgegapDeployment=true;
  EdgegapInjectedMatchContext.InjectedMatchId="fixture-match";EdgegapInjectedMatchContext.AssignedTicketCount=10;
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
  for(int i=0;i<10;i++)
  {
   ulong id=(ulong)(i+1);string ticket="t"+i;var human=Activator.CreateInstance(humanType,true);
   foreach(var pair in new[]{("TicketId",ticket),("PlayerId",ticket),("TeamId",i<5?"red":"blue"),("MatchId","fixture-match"),("Admission","signed-"+ticket)})Put(human,pair.Item1,pair.Item2);
   Put(human,"ClientId",id);pending.Add(id,human);authorized.Add(id,ticket);manager.ConnectedClientsIds.Add(id);
   humans.Add(new(){PlayerId=ticket,TeamId=i<5?"red":"blue",Hero=HeroId.Veyr});
  }
  CoopAuthority.PreparedPlan=OnlineMatchPlan.BuildDedicated("fixture-match",ArenaVariant.Moon,BotDifficulty.Hard,humans,SessionMode.OnlineRanked);
  Put(handshake,"planSent",true);Put(handshake,"planSentAt",0f);return handshake;
 }
 static async Task FailureCases()
 {
  int failures=0;
  async Task Case(string name,Func<Task> run){try{await run();}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.GetBaseException().Message);}}
  await Case("missing ACK",()=>
  {
   var handshake=PendingRanked(out var manager,out var reporter);int voids=0;
   UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/void"))voids++;return Reply(r,"{}");};
   for(ulong i=1;i<=9;i++)((HashSet<ulong>)Get(handshake,"planAcks")).Add(i);
   Time.unscaledTime=21;Invoke(handshake,"UpdateDedicatedServer",manager);
   Check((bool)Get(handshake,"launched"),"ranked missing ACK aborts rather than rebuilding a nine-player match");
   Check(voids==1,"missing ACK produces exactly one backend tombstone");
   Check(manager.ConnectedClientsIds.Count==0,"all ranked clients are disconnected on failed confirmation");
   Check(CoopAuthority.PreparedPlan==null,"aborted plan is cleared");return Task.CompletedTask;
  });
  await Case("disconnect before start",()=>
  {
   var handshake=PendingRanked(out var manager,out var reporter);int voids=0;
   UnityWebRequest.Handler=r=>{if(r.url.EndsWith("/void"))voids++;return Reply(r,"{}");};
   manager.ConnectedClientsIds.Remove(10);Invoke(handshake,"UpdateDedicatedServer",manager);
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
   for(ulong i=1;i<=9;i++)((HashSet<ulong>)Get(handshake,"planAcks")).Add(i);
   Time.unscaledTime=21;Invoke(handshake,"UpdateDedicatedServer",manager);
   Check(!(bool)Get(handshake,"launched"),"casual waits for rebuilt ACKs");
   Check(manager.ConnectedClientsIds.Count==9,"casual only evicts missing ACK");
   Check(CoopAuthority.PreparedPlan.Session.Mode==SessionMode.OnlineCasual&&CoopAuthority.PreparedPlan.HumanCount==9,"casual rebuild retains bot fill");return Task.CompletedTask;
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
    var players=new RankedMatchReporter.Player[10];for(int i=0;i<10;i++)players[i]=new(){ticket="t"+i,admission="signed",team=i/5};
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
