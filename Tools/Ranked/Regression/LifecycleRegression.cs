using System;
using System.Threading.Tasks;
using Alterverse.Match;
using Alterverse.Multiplayer;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Multiplayer;
partial class Regression
{
 static OnlineMatchPlan EndPlan(SessionMode mode=SessionMode.OnlineCasual)
 {
  var players=new System.Collections.Generic.List<OnlineMatchPlan.DedicatedMember>();
  for(int i=0;i<8;i++)players.Add(new(){PlayerId=i==0?"host":"p"+i,DisplayName="Player",TeamId=i<4?"red":"blue"});
  return OnlineMatchPlan.BuildDedicated("ended",ArenaVariant.Moon,Alterverse.AI.BotDifficulty.Hard,players,mode);
 }

 static async Task LifecycleCases()
 {
  HotzoneReplicationCases();
  WorldRegistryCases();
  await GuestSessionSearchCases();
  foreach(bool host in new[]{true,false})
  {
   ResetLobby();var party=Room(2,host:host);Put(OnlinePartyService.Instance,"session",party);
   await OnlinePartyService.Instance.SetMatchmakingLockedAsync(true);
   CoopAuthority.Active=true;CoopAuthority.ActivePlan=EndPlan();
   var front=new DimensionsFront{HasEnded=true};UnityEngine.Object.Found[typeof(DimensionsFront)]=front;
   MatchScoreboard.Instance=new(){ResultCommitted=false};CoopMatchResultSync.Instance=new(){ResultReady=true};
   var world=new GameObject("DF_MatchWorld");
   int shown=Alterverse.UI.OnlinePartyOverlay.Shows;
   await MatchExit.ReturnToOnlineLobbyAsync();
   Check(CoopAuthority.Active&&world.activeSelf&&Alterverse.UI.OnlinePartyOverlay.Shows==shown,"pending reward must not be discarded on disconnect or early exit");
   MatchScoreboard.Instance.ResultCommitted=true;
   await MatchExit.ReturnToOnlineLobbyAsync();
   Check(OnlinePartyService.Instance.InParty&&OnlinePartyService.Instance.JoinCode=="ABC123"&&party.PlayerCount==2,"result exit preserves group, code and members for host and guest");
   Check(party.Deletes==0&&party.Leaves==0&&!CoopAuthority.Active&&!MatchExit.Leaving,"result exit shuts down match without leaving party");
   Check(Alterverse.UI.OnlinePartyOverlay.Shows==shown+1&&!OnlinePartyService.Instance.LocalReady,"result exit opens matchmaking lobby and requires fresh ready");
   Check(!EdgegapOnlineFlow.Instance.IsSearching&&!EdgegapMatchmakerClient.Instance.HasMembership,"return never starts another search");
  }

  ResetLobby();Put(OnlinePartyService.Instance,"session",Room(1));
  await MatchExit.ReturnToMenuAsync();
  Check(!OnlinePartyService.Instance.InParty,"explicit leave still closes party");

  // Exercise the real online loop guard and protocol callbacks with old queued messages.
  foreach(var mode in new[]{SessionMode.OnlineCasual,SessionMode.OnlineRanked})
  {
   ResetLobby();CoopAuthority.Active=true;CoopAuthority.ActivePlan=EndPlan(mode);
   UnityEngine.Object.Found[typeof(DimensionsFront)]=new DimensionsFront{HasEnded=true};
   MatchScoreboard.Instance=new(){ResultCommitted=true};CoopMatchResultSync.Instance=new(){ResultReady=true};
   var loop=Component<NetworkRoundLoop>();
   Invoke(loop,"Update");
   Check(loop.Deadline<0&&!loop.Transitioning&&NetworkManager.Singleton.CustomMessagingManager.Sent.Count==0,"online completion never proposes or counts down another round");
   Check(!(bool)loop.GetType().GetMethod("Current",Flags).Invoke(loop,new object[]{"ended"}),"queued next-round messages rejected online");
   UnityEngine.Object.Found[typeof(Alterverse.Prototype.PrototypeBootstrap)]=new Alterverse.Prototype.PrototypeBootstrap();
   Put(loop,"proposed",CoopAuthority.ActivePlan);Invoke(loop,"Launch");
   Check(!loop.Transitioning,"queued online launch cannot restart battlefield");
  }

  ResetLobby();var partySolo=Room(1);Put(OnlinePartyService.Instance,"session",partySolo);
  CoopAuthority.Active=true;CoopAuthority.ActivePlan=EndPlan();CoopRosterBuilder.LocalSlot=0;
  Alterverse.UI.MainMenuController.IsVisible=false;
  UnityEngine.Object.Found[typeof(DimensionsFront)]=new DimensionsFront{HasEnded=true};
  MatchScoreboard.Instance=new(){ResultCommitted=false};CoopMatchResultSync.Instance=new(){ResultReady=true};
  var auto=Component<PostMatchAutoLoop>();Invoke(auto,"Update");Time.unscaledTime=30;Invoke(auto,"Update");
  Check(CoopAuthority.Active,"online countdown waits for reward transaction");
  MatchScoreboard.Instance.ResultCommitted=true;Invoke(auto,"Update");Time.unscaledTime=41;Invoke(auto,"Update");
  Check(CoopAuthority.Active,"result remains visible for twelve seconds after reward commit");
  Time.unscaledTime=42;Invoke(auto,"Update");
  await Until(()=>!CoopAuthority.Active,"online result automatically returns to lobby");
  Check(OnlinePartyService.Instance.InParty&&!EdgegapOnlineFlow.Instance.IsSearching,"automatic return keeps lobby without auto search");

  ResetLobby();MatchScoreboard.Instance=null;CoopMatchResultSync.Instance=null;
 }

 static void HotzoneReplicationCases()
 {
  ResetLobby();CoopAuthority.Active=true;CoopAuthority.ActivePlan=EndPlan();
  CoopAuthority.LocalIsHost=false;CoopAuthority.LocalHasServerAuthority=true;
  var server=NetworkManager.Singleton;server.IsClient=false;server.ConnectedClientsIds.Add(7);
  var zone=new HotZoneDirector();var sync=new CoopWorldEventSync();
  Put(sync,"messaging",server.CustomMessagingManager);
  Action<Vector3,int> opened=(p,n)=>Invoke(sync,"OnHostHotOpened",p,n);
  Action<int,float,float,int> resolved=(w,z,l,n)=>Invoke(sync,"OnHostHotResolved",w,z,l,n);
  HotZoneDirector.NetworkZoneOpened+=opened;HotZoneDirector.NetworkZoneResolved+=resolved;
  Alterverse.Combat.HotZoneTank.Spawns=0;
  try
  {
   Invoke(zone,"Open");
   Check(zone.IsActive&&server.CustomMessagingManager.Sent.Count==1,"dedicated server broadcasts Hotzone open without a local host player");
   var open=server.CustomMessagingManager.Sent[0];
   Check(open.Name=="DF_COOP_HOT_OPEN_V2"&&open.Id==7,"Hotzone uses real reliable world-event broadcast path");
   Invoke(sync,"OnHostHotProgress",0,12f,8f,3f,2,1);
   var progress=server.CustomMessagingManager.Sent[1];
   Put(zone,"zeroTime",8f);Put(zone,"lumenTime",3f);Invoke(zone,"Resolve");
   Check(Alterverse.Combat.HotZoneTank.Spawns==1&&server.CustomMessagingManager.Sent.Count==3,"server awards monster and broadcasts capture");
   var result=server.CustomMessagingManager.Sent[2];
   CoopAuthority.LocalHasServerAuthority=false;server.IsServer=server.IsHost=false;server.IsClient=true;
   var client=new HotZoneDirector();UnityEngine.Object.Found[typeof(HotZoneDirector)]=client;
   Invoke(sync,"OnHotOpen",99UL,new FastBufferReader(open.Values));
   Check(!client.IsActive,"non-server Hotzone events rejected");
   Invoke(sync,"OnHotOpen",0UL,new FastBufferReader(open.Values));
   Check(client.IsActive&&client.Visuals==1&&client.transform.position.x==3,"client opens zone at authoritative location");
   Invoke(sync,"OnHotProgress",0UL,new FastBufferReader(progress.Values));
   Check(client.Remaining==18&&client.ZeroControlSeconds==8&&client.ZeroInside==2,"online HUD receives capture clock, score and participant count");
   Invoke(sync,"OnHotResolve",0UL,new FastBufferReader(result.Values));
   Check(!client.IsActive&&Alterverse.Combat.HotZoneTank.Spawns==2,"client spawns replica of awarded Hotzone monster");
   Invoke(sync,"OnHotResolve",0UL,new FastBufferReader(result.Values));
   Invoke(sync,"OnHotOpen",0UL,new FastBufferReader(open.Values));
   Check(!client.IsActive&&Alterverse.Combat.HotZoneTank.Spawns==2,"old objective events cannot duplicate rewards or reopen resolved zone");
   CoopAuthority.LocalHasServerAuthority=true;client.ApplyNetworkOpen(new Vector3(),1);
   Check(!client.IsActive,"server ignores client presentation mutation");
  }
  finally
  {
   HotZoneDirector.NetworkZoneOpened-=opened;HotZoneDirector.NetworkZoneResolved-=resolved;
   CoopAuthority.LocalHasServerAuthority=false;UnityEngine.Object.Found.Clear();
  }
 }

 static void WorldRegistryCases()
 {
  ResetLobby();CoopAuthority.Active=true;CoopAuthority.ActivePlan=EndPlan();CoopAuthority.LocalHasServerAuthority=false;
  NetworkManager.Singleton.IsServer=NetworkManager.Singleton.IsHost=false;NetworkManager.Singleton.IsClient=true;
  Alterverse.Combat.Health.Active.Clear();
  for(int i=0;i<110;i++)
  {
   var g=new GameObject();var h=g.AddComponent<Alterverse.Combat.Health>();
   g.AddComponent<CoopWorldEntityKey>().Key="entity"+i;Alterverse.Combat.Health.Active.Add(h);
  }
  var sync=new CoopWorldStateSync();Time.frameCount=1;Alterverse.Combat.Health.Lookups=0;
  uint round=CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan);
  for(int i=0;i<19;i++)Invoke(sync,"OnState",0UL,new FastBufferReader(round,0,0));
  Check(Alterverse.Combat.Health.Lookups==220,"nineteen world chunks scan 110 entities once per frame instead of nineteen times");
  Time.frameCount++;Invoke(sync,"OnState",0UL,new FastBufferReader(round,0,0));
  Check(Alterverse.Combat.Health.Lookups==440,"lookup cache refreshes on next frame");
  var spawned=new GameObject();var health=spawned.AddComponent<Alterverse.Combat.Health>();
  spawned.AddComponent<CoopWorldEntityKey>().Key="fresh";Alterverse.Combat.Health.Active.Add(health);
  Invoke(sync,"OnState",0UL,new FastBufferReader(round,1,new Unity.Collections.FixedString128Bytes("fresh"),2f,0f,3f,0f,42f,5f,false,0));
  Check(health.Current==42&&health.Shield==5,"new entity in same frame is discovered and receives fresh HP/shield");
  int before=Alterverse.Combat.Health.Lookups;Time.frameCount++;
  Invoke(sync,"OnState",0UL,new FastBufferReader(round+1,0,0));
  Check(Alterverse.Combat.Health.Lookups==before,"old round packets do not trigger world scan");
  Console.WriteLine("World lookup fixture: 19 chunks / 110 entities: 4180 -> 220 component lookups in one frame. Not a device FPS measurement.");
  Alterverse.Combat.Health.Active.Clear();
 }

 static async Task GuestSessionSearchCases()
 {
  ResetLobby();var party=Room(2,host:false);Put(OnlinePartyService.Instance,"session",party);
  NetworkManager.Singleton.IsServer=NetworkManager.Singleton.IsHost=NetworkManager.Singleton.IsListening=false;
  var flow=EdgegapOnlineFlow.Instance;int joins=0;string group="after-match";
  UnityEngine.Networking.UnityWebRequest.Handler=r=>{if(r.method=="POST")joins++;return Reply(r,Membership(group,"guest"));};
  party.SetProperty("edgegapSearch",new SessionProperty("1",VisibilityPropertyOptions.Member));
  party.SetProperty("edgegapGroup",new SessionProperty(group,VisibilityPropertyOptions.Member));
  Invoke(flow,"Update");
  await Until(()=>flow.IsSearching,"guest joins shared matchmaking group without Relay");
  Check(joins==1&&OnlinePartyService.Instance.LocalJoinedGroupId==group,"guest publishes receipt for exact current group");
  party.SetProperty("edgegapSearch",new SessionProperty("0",VisibilityPropertyOptions.Member));Invoke(flow,"Update");
  await Until(()=>!flow.Busy&&!flow.IsSearching,"guest receives leader cancel through session state");
  Check(OnlinePartyService.Instance.InParty&&!EdgegapMatchmakerClient.Instance.HasMembership,"guest cancellation retains friend lobby");
  party.SetProperty("edgegapSearch",new SessionProperty("1",VisibilityPropertyOptions.Member));Invoke(flow,"Update");
  Check(joins==1,"returning guest cannot rejoin stale completed group");
  group="next-search";party.SetProperty("edgegapGroup",new SessionProperty(group,VisibilityPropertyOptions.Member));Invoke(flow,"Update");
  await Until(()=>flow.IsSearching,"guest can join a fresh search from same lobby");
  Check(joins==2&&OnlinePartyService.Instance.LocalJoinedGroupId==group,"fresh search receives a fresh guest acknowledgement");
  await flow.CompleteMatchAsync();
  Check(OnlinePartyService.Instance.InParty,"completed match cleanup keeps guest session");
 }
}
