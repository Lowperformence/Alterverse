using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.Networking;

partial class Regression
{
 static void Drop(object component) {if(component!=null)Invoke(component,"OnDestroy");}
 static void ResetLobby()
 {
  Drop(EdgegapOnlineFlow.Instance);Drop(OnlinePartyService.Instance);Drop(CoopLobbyService.Instance);Drop(RankedService.Instance);Drop(OnlinePlanHandshake.Instance);
  typeof(EdgegapMatchmakerClient).GetProperty("Instance").SetValue(null,null);
  MultiplayerService.Instance=new();UnityServices.Initialize=null;UnityWebRequest.Handler=null;
  CoopAuthority.Active=false;CoopAuthority.PreparedPlan=CoopAuthority.ActivePlan=null;
  Time.unscaledTime=Time.realtimeSinceStartup=0;NetworkRoundLoop.IsBetweenRounds=false;PlayerHeroSelection.Selected=HeroId.Veyr;
  var manager=new NetworkManager{IsHost=true,IsServer=true,IsClient=true,IsListening=true,IsConnectedClient=true};manager.ConnectedClientsIds.Add(0);
  NetworkManager.Singleton=CoopNetworkBootstrap.Manager=manager;CoopNetworkBootstrap.Connects=0;
  Environment.SetEnvironmentVariable(EdgegapMatchmakerClient.TokenEnvironmentVariable,"fixture-token");
  Component<OnlinePartyService>();Component<CoopLobbyService>();Component<EdgegapOnlineFlow>();Component<EdgegapMatchmakerClient>();
 }
 static Player LobbyPlayer(string id,int hero,bool ready=true)=>new(){Id=id,Properties=new(){
  {"hero",new PlayerProperty(hero.ToString(),VisibilityPropertyOptions.Member)},
  {"ready",new PlayerProperty(ready?"1":"0",VisibilityPropertyOptions.Member)},
  {"displayName",new PlayerProperty("Player",VisibilityPropertyOptions.Member)}}};
 static FakeSession Room(int count,int capacity=8,bool host=true)
 {
  var room=new FakeSession{MaxPlayers=capacity};
  for(int i=0;i<count;i++)room.Players.Add(LobbyPlayer(i==0?"host":"p"+i,i));
  room.CurrentPlayer=(Player)room.Players[host?0:1];
  room.SetProperty("phase",new SessionProperty("lobby",VisibilityPropertyOptions.Member));
  return room;
 }
 static async Task Finish(Task task,string reason)
 {
  Check(await Task.WhenAny(task,Task.Delay(3000))==task,reason+" completes without a real timeout");
  await task;
 }
 static async Task Until(Func<bool> ready,string reason)
 {
  var deadline=DateTime.UtcNow.AddSeconds(3);
  while(!ready()&&DateTime.UtcNow<deadline)await Task.Delay(10);
  Check(ready(),reason);
 }
 static string Membership(string group="g1",string member="m1")=>"{\"group_id\":\""+group+"\",\"member_id\":\""+member+"\",\"is_ready\":true,\"ticket_id\":\"t1\",\"status\":\"SEARCHING\"}";

 static async Task LobbyCases()
 {
  ResetLobby();
  var menu=new Alterverse.UI.MainMenuController();int http=0;
  UnityWebRequest.Handler=r=>{http++;return Reply(r,Membership());};
  await (Task)menu.GetType().GetMethod("BeginOnlineMatchmakingAsync",Flags).Invoke(menu,null);
  Check(Alterverse.UI.OnlinePartyOverlay.Shows>0,"Play Online opens invitation choices");
  Check(MultiplayerService.Instance.Creates==0&&http==0,"opening invites never starts matchmaking");
  Check(await OnlinePartyService.Instance.CreatePrivatePartyAsync(),"create invite party");
  Check(MultiplayerService.Instance.LastOptions.MaxPlayers==8&&MultiplayerService.Instance.LastOptions.IsPrivate,"invite room has correct relay capacity and privacy");
  Check(OnlinePartyService.Instance.JoinCode=="ABC123","created invite code remains available");
  await (Task)menu.GetType().GetMethod("BeginOnlineMatchmakingAsync",Flags).Invoke(menu,null);
  Check(http==0,"reopening an existing party does not start search");
  await OnlinePartyService.Instance.LeavePartyAsync();
  Check(!OnlinePartyService.Instance.InParty,"host can close invite party");

  for(int count=1;count<=4;count++)
  {
   ResetLobby();var room=Room(count);Put(OnlinePartyService.Instance,"session",room);
   Check(OnlinePartyService.Instance.PartyPlayerCount==count&&OnlinePartyService.Instance.PartyReady,"private party readiness for "+count);
   Check(await OnlinePartyService.Instance.SetMatchmakingLockedAsync(true)&&room.IsLocked,"matchmaking locks invite room");
   Check(!await OnlinePartyService.Instance.SetLocalHeroAsync(HeroId.Nerok),"hero cannot change after group snapshot");
   Check(!await OnlinePartyService.Instance.SetLocalReadyAsync(false),"ready cannot change after group snapshot");
   Check(await OnlinePartyService.Instance.SetMatchmakingLockedAsync(false)&&!room.IsLocked,"cancel unlocks invite room");
   Check(await OnlinePartyService.Instance.SetLocalReadyAsync(false)&&!OnlinePartyService.Instance.LocalReady,"party editing restored");
  }

  foreach(bool coop in new[]{false,true})
  {
   ResetLobby();var room=Room(2,coop?4:8,false);
   MultiplayerService.Instance.Join=(code,options)=>Task.FromResult<ISession>(room);
   bool joined=coop?await CoopLobbyService.Instance.JoinLobbyAsync(" ab-12 c "):await OnlinePartyService.Instance.JoinByCodeAsync(" ab-12 c ");
   Check(joined&&MultiplayerService.Instance.LastCode=="AB12C","normalized invite joins correct service");
   room.Leave=()=>Task.FromException(new Exception("outage"));
   if(coop)await CoopLobbyService.Instance.LeaveLobbyAsync();else await OnlinePartyService.Instance.LeavePartyAsync();
   Check(coop?CoopLobbyService.Instance.InLobby:OnlinePartyService.Instance.InParty,"failed leave preserves visible membership for retry");
   room.Leave=null;
   if(coop)await CoopLobbyService.Instance.LeaveLobbyAsync();else await OnlinePartyService.Instance.LeavePartyAsync();
   Check(!(coop?CoopLobbyService.Instance.InLobby:OnlinePartyService.Instance.InParty),"leave retry clears membership");
  }
  foreach(int count in new[]{2,5})
  {
   ResetLobby();var room=Room(count,count==2?10:8,false);
   MultiplayerService.Instance.Join=(code,o)=>Task.FromResult<ISession>(room);
   Check(!await OnlinePartyService.Instance.JoinByCodeAsync("ABCD"),"incompatible/full friend invite rejected");
   Check(room.Leaves==1&&!OnlinePartyService.Instance.InParty,"rejected join cleans up guest membership");
  }
  ResetLobby();var locked=Room(2,8,false);locked.SetProperty("edgegapSearch",new SessionProperty("1",VisibilityPropertyOptions.Member));
  MultiplayerService.Instance.Join=(c,o)=>Task.FromResult<ISession>(locked);
  Check(!await OnlinePartyService.Instance.JoinByCodeAsync("ABCD")&&locked.Leaves==1,"late invite cannot join searching party");

  for(int count=1;count<=4;count++)
  {
   ResetLobby();var room=Room(count,4);Put(CoopLobbyService.Instance,"session",room);Invoke(CoopLobbyService.Instance,"AttachSession",room);
   for(ulong id=1;id<(ulong)count;id++)NetworkManager.Singleton.ConnectedClientsIds.Add(id);
   Check(CoopLobbyService.Instance.CanStageMatch,"co-op staged readiness with "+count+" humans");
   Check(await CoopLobbyService.Instance.BeginMatchStagingAsync()&&room.IsLocked,"co-op staging closes invite room");
   Check(CoopAuthority.PreparedPlan?.HumanCount==count&&CoopAuthority.PreparedPlan.Session.IsFull,"co-op stage publishes complete 4v4 plan");
   Check(!await CoopLobbyService.Instance.SetLocalHeroAsync(HeroId.Nerok)&&!await CoopLobbyService.Instance.SetLocalReadyAsync(false),"staged co-op choices frozen");
   Check(!await CoopLobbyService.Instance.CycleHostArenaAsync(1)&&!await CoopLobbyService.Instance.CycleHostDifficultyAsync(1),"staged host settings frozen");
   Check(await CoopLobbyService.Instance.CancelMatchStagingAsync()&&!room.IsLocked&&CoopAuthority.PreparedPlan==null,"co-op cancel restores editable lobby");
  }
  ResetLobby();var conflict=Room(2,4);Put(CoopLobbyService.Instance,"session",conflict);conflict.Players[1].Properties["hero"].Value="0";
  Check(!CoopLobbyService.Instance.CanStageMatch&&!await CoopLobbyService.Instance.BeginMatchStagingAsync(),"duplicate co-op heroes cannot stage");
  conflict.Players[1].Properties["hero"].Value="1";
  Check(!CoopLobbyService.Instance.CanStageMatch,"missing relay peer cannot stage");

  // Cancel while the POST /groups response is delayed. No phantom group may survive.
  ResetLobby();var party=Room(1);Put(OnlinePartyService.Instance,"session",party);
  UnityWebRequest creation=null;var held=new UnityWebRequestAsyncOperation{isDone=false};int deleted=0;
  UnityWebRequest.Handler=r=>{if(r.method=="POST"){creation=r;return held;}if(r.method=="DELETE")deleted++;return Reply(r,Membership());};
  var search=EdgegapOnlineFlow.Instance.BeginSearchAsync();
  Check(creation!=null&&EdgegapOnlineFlow.Instance.IsSearching,"search is visible while group creation is pending");
  var cancel=EdgegapOnlineFlow.Instance.CancelSearchAsync();
  Check(!cancel.IsCompleted&&EdgegapOnlineFlow.Instance.Busy,"cancel waits for pending membership and blocks new search");
  creation.downloadHandler.text=Membership();held.isDone=true;
  await Finish(Task.WhenAll(search,cancel),"cancel during group creation");
  Check(!await search&&await cancel&&deleted==1&&!EdgegapMatchmakerClient.Instance.HasMembership,"late-created group is deleted exactly once");
  Check(!party.IsLocked&&!OnlinePartyService.Instance.MatchmakingLocked,"cancel returns party to invite mode");
  Check(CoopNetworkBootstrap.Connects==0,"cancelled search never connects");

  for(int humans=2;humans<=4;humans++)
  {
   ResetLobby();party=Room(humans);Put(OnlinePartyService.Instance,"session",party);
   for(ulong id=1;id<(ulong)humans;id++)NetworkManager.Singleton.ConnectedClientsIds.Add(id);
   var current=EdgegapOnlineFlow.Instance;Invoke(current,"Attach",NetworkManager.Singleton.CustomMessagingManager);
   int readyCalls=0;
   UnityWebRequest.Handler=r=>{if(r.method=="PATCH")readyCalls++;return Reply(r,Membership());};
   var pendingSearch=current.BeginSearchAsync();
   Check(!pendingSearch.IsCompleted&&readyCalls==0,"leader stays unready until all "+humans+" friends acknowledge");
   for(ulong id=1;id<(ulong)humans;id++)Invoke(current,"OnJoinAck",id,new FastBufferReader(new FixedString128Bytes("g1")));
   await Finish(pendingSearch,"friend party start");
   Check(await pendingSearch&&readyCalls==1&&current.IsSearching,"complete friend group starts matchmaking exactly once");
   await current.CancelSearchAsync();
   Check(!party.IsLocked&&!EdgegapMatchmakerClient.Instance.HasMembership,"friend-party cancel preserves invite room");
  }

  // Count alone cannot detect a changed hero/readiness snapshot while the HTTP request is pending.
  ResetLobby();party=Room(1);Put(OnlinePartyService.Instance,"session",party);
  creation=null;held=new UnityWebRequestAsyncOperation{isDone=false};int readyAfterChange=0;
  UnityWebRequest.Handler=r=>{if(r.method=="POST"){creation=r;return held;}if(r.method=="PATCH")readyAfterChange++;return Reply(r,Membership());};
  search=EdgegapOnlineFlow.Instance.BeginSearchAsync();party.CurrentPlayer.Properties["hero"].Value="1";
  creation.downloadHandler.text=Membership();held.isDone=true;await Finish(search,"changed party snapshot");
  Check(!await search&&readyAfterChange==0&&!party.IsLocked&&!EdgegapMatchmakerClient.Instance.HasMembership,"changed snapshot never starts an outdated group");

  // An expired member response must close the group and unlock the party for a retry.
  ResetLobby();party=Room(1);Put(OnlinePartyService.Instance,"session",party);deleted=0;
  UnityWebRequest.Handler=r=>{if(r.method=="DELETE"){deleted++;r.responseCode=404;return Reply(r,"{}");}if(r.method=="GET"){r.responseCode=410;return Reply(r,"expired");}return Reply(r,Membership());};
  Check(await EdgegapOnlineFlow.Instance.BeginSearchAsync(),"ready solo starts matchmaking");
  await Until(()=>!EdgegapOnlineFlow.Instance.IsSearching&&!EdgegapOnlineFlow.Instance.Busy,"expired search finishes cleanup");
  Check(deleted==1&&!EdgegapMatchmakerClient.Instance.HasMembership&&!party.IsLocked,"expired search cannot leave ghost membership or locked invites");

  // Edgegap cannot delete a group after MATCH_FOUND (409); it removes it automatically.
  // That terminal response must not strand a connected player's next search locally.
  foreach(int code in new[]{409,404,410})
  {
   ResetLobby();var terminalClient=EdgegapMatchmakerClient.Instance;int deletes=0;
   UnityWebRequest.Handler=r=>Reply(r,Membership("previous"));await terminalClient.CreateGroupAsync();
   UnityWebRequest.Handler=r=>{deletes++;r.responseCode=code;return Reply(r,"group already matched or removed");};
   Check(await terminalClient.CancelGroupAsync(),"terminal group cleanup succeeds for HTTP "+code);
   Check(deletes==1&&!terminalClient.HasMembership&&terminalClient.MatchmakerStatus=="IDLE"&&terminalClient.LastError=="","terminal cleanup releases membership exactly once");
  }
  ResetLobby();party=Room(1);Put(OnlinePartyService.Instance,"session",party);
  UnityWebRequest.Handler=r=>Reply(r,Membership("matched"));await EdgegapMatchmakerClient.Instance.CreateGroupAsync();
  int replacementGroups=0;
  UnityWebRequest.Handler=r=>{
   if(r.method=="DELETE"){r.responseCode=409;return Reply(r,"group already matched");}
   if(r.method=="POST")replacementGroups++;
   if(r.method=="GET"){r.responseCode=410;return Reply(r,"expired");}
   return Reply(r,Membership("replacement"));
  };
  Check(await EdgegapOnlineFlow.Instance.BeginSearchAsync()&&replacementGroups==1,"matched previous group no longer blocks the next search");
  await Until(()=>!EdgegapOnlineFlow.Instance.IsSearching&&!EdgegapOnlineFlow.Instance.Busy,"replacement search cleanup");

  // A permission failure is not an expired group; keep membership for a real retry.
  foreach(int code in new[]{400,401,403})
  {
   ResetLobby();var rejectedClient=EdgegapMatchmakerClient.Instance;
   UnityWebRequest.Handler=r=>Reply(r,Membership("retained"));await rejectedClient.CreateGroupAsync();
   UnityWebRequest.Handler=r=>{r.responseCode=code;return Reply(r,"request rejected");};
   Check(!await rejectedClient.CancelGroupAsync()&&rejectedClient.GroupId=="retained"&&rejectedClient.LastError.Contains(code.ToString()),"non-terminal cleanup failure remains visible for HTTP "+code);
  }
  ResetLobby();var guestClient=EdgegapMatchmakerClient.Instance;
  UnityWebRequest.Handler=r=>Reply(r,Membership("friends","guest"));await guestClient.JoinGroupAsync("friends");
  UnityWebRequest.Handler=r=>{r.responseCode=409;return Reply(r,"membership conflict");};
  Check(!await guestClient.LeaveGroupAsync()&&guestClient.HasMembership,"group-delete exception does not mask unrelated member conflicts");

  // Assigned groups are immutable and auto-expire. Their local handles must be released
  // even if the matchmaker is unreachable, rejects DELETE, or the cached token expired.
  foreach(bool guest in new[]{false,true})
  {
   ResetLobby();var assignedClient=EdgegapMatchmakerClient.Instance;
   assignedClient.AcceptRankedAssignment("{\"group_id\":\"assigned\",\"member_id\":\"member\",\"ticket_id\":\"ticket\",\"assignment\":{\"fqdn\":\"host.invalid\",\"ports\":{\"gameport\":{\"external\":7777}}}}");
   Check(assignedClient.HasAssignment&&assignedClient.HasMembership,"assigned cleanup fixture is complete");
   UnityWebRequest.Handler=r=>throw new Exception("Immutable assigned group must not make a cleanup request");
   Environment.SetEnvironmentVariable(EdgegapMatchmakerClient.TokenEnvironmentVariable,"");
   Check(guest?await assignedClient.LeaveGroupAsync():await assignedClient.CancelGroupAsync(),"assigned owner/guest can release without a matchmaker request");
   Check(!assignedClient.HasAssignment&&!assignedClient.HasMembership&&assignedClient.LastError=="","assigned cleanup resets all stale local handles");
  }

  // A stale ready response cannot replace a newer group after cancellation.
  ResetLobby();var client=EdgegapMatchmakerClient.Instance;UnityWebRequest readyRequest=null;var readyHeld=new UnityWebRequestAsyncOperation{isDone=false};
  UnityWebRequest.Handler=r=>r.method=="PATCH"?(readyRequest=r,readyHeld).Item2:Reply(r,Membership("old"));
  Check(await client.CreateGroupAsync(),"initial group created");var oldReady=client.SetReadyAsync(true);
  Check(await client.CancelGroupAsync(),"old group cancelled");
  UnityWebRequest.Handler=r=>Reply(r,Membership("new"));Check(await client.CreateGroupAsync(),"new group created");
  readyRequest.downloadHandler.text=Membership("old");readyHeld.isDone=true;await Finish(oldReady,"late ready response");
  Check(!await oldReady&&client.GroupId=="new","late ready response cannot resurrect previous group");

  ResetLobby();client=EdgegapMatchmakerClient.Instance;creation=null;held=new UnityWebRequestAsyncOperation{isDone=false};string staleDeletion=null;
  UnityWebRequest.Handler=r=>{creation=r;return held;};
  var staleCreate=client.CreateGroupAsync();await client.CancelGroupAsync();
  UnityWebRequest.Handler=r=>{if(r.method=="DELETE")staleDeletion=r.url;return Reply(r,Membership("current"));};
  await client.CreateGroupAsync();creation.downloadHandler.text=Membership("abandoned");held.isDone=true;
  await Finish(staleCreate,"stale group creation");
  Check(!await staleCreate&&client.GroupId=="current"&&staleDeletion.EndsWith("/groups/abandoned"),"late create cleans only its abandoned group");

  // A guest leaving while its group POST is pending must delete only that guest membership.
  ResetLobby();party=Room(2,8,false);Put(OnlinePartyService.Instance,"session",party);
  NetworkManager.Singleton.IsHost=NetworkManager.Singleton.IsServer=false;
  creation=null;held=new UnityWebRequestAsyncOperation{isDone=false};string deletion=null;
  UnityWebRequest.Handler=r=>{if(r.method=="POST"){creation=r;return held;}if(r.method=="DELETE")deletion=r.url;return Reply(r,"{}");};
  var join=(Task<bool>)EdgegapOnlineFlow.Instance.GetType().GetMethod("JoinFriendGroupAsync",Flags).Invoke(EdgegapOnlineFlow.Instance,new object[]{"friends"});
  var leave=EdgegapOnlineFlow.Instance.LeavePartyAsync();
  Check(!leave.IsCompleted,"guest leave waits for in-flight membership");
  creation.downloadHandler.text=Membership("friends","guest");held.isDone=true;
  await Finish(Task.WhenAll(join,leave),"guest leave during join");
  Check(deletion.EndsWith("/groups/friends/members/guest")&&!OnlinePartyService.Instance.InParty,"leaving guest removes its ticket and exits room");

  // A delayed GET may not reconnect after cancel, nor replace a new group's state.
  ResetLobby();client=EdgegapMatchmakerClient.Instance;UnityWebRequest poll=null;var pollHeld=new UnityWebRequestAsyncOperation{isDone=false};
  UnityWebRequest.Handler=r=>r.method=="GET"?(poll=r,pollHeld).Item2:Reply(r,Membership("first"));
  await client.CreateGroupAsync();var waiting=client.WaitForAssignmentAndConnectAsync();
  await client.CancelGroupAsync();UnityWebRequest.Handler=r=>Reply(r,Membership("second"));await client.CreateGroupAsync();
  poll.downloadHandler.text="{\"group_id\":\"first\",\"member_id\":\"m1\",\"assignment\":{\"fqdn\":\"old.example.invalid\",\"ports\":{\"gameport\":{\"external\":7777}}}}";pollHeld.isDone=true;
  await Finish(waiting,"cancelled assignment polling");
  Check(!await waiting&&client.GroupId=="second"&&!client.HasAssignment&&CoopNetworkBootstrap.Connects==0,"late assignment cannot reconnect or overwrite next search");
  int cancelsBeforeOldWait=CoopNetworkBootstrap.Cancels;
  var obsoleteWait=(Task<bool>)client.GetType().GetMethod("WaitForDedicatedConnectionAsync",Flags).Invoke(client,new object[]{-1});
  await Finish(obsoleteWait,"obsolete dedicated connection wait");
  Check(!await obsoleteWait&&CoopNetworkBootstrap.Cancels==cancelsBeforeOldWait,"old connection waiter cannot shut down a newer connection");

  // Relay room disappearance is expected after an assigned dedicated-server handoff.
  ResetLobby();party=Room(1);Put(OnlinePartyService.Instance,"session",party);
  flowForHandoff();
  await OnlineStartCases();

  foreach(bool disconnect in new[]{false,true})
  {
   ResetLobby();party=Room(2);Put(OnlinePartyService.Instance,"session",party);NetworkManager.Singleton.ConnectedClientsIds.Add(1);
   var legacy=Component<OnlinePlanHandshake>();
   Check(await OnlinePlanHandshake.StartOnlineMatchAsync()&&OnlinePlanHandshake.Preparing,"Relay online compatibility starts plan verification");
   if(disconnect)party.Remove("p1");else Time.unscaledTime=21;
   Invoke(legacy,"Update");
   Check(!OnlinePlanHandshake.Preparing&&CoopAuthority.PreparedPlan==null,"Relay plan aborts on missing player or ACK timeout");
  }

  // Session creation and Ranked search are mutually exclusive, including during authentication.
  ResetLobby();var init=new TaskCompletionSource<bool>();UnityServices.Initialize=()=>init.Task;
  var creating=OnlinePartyService.Instance.CreatePrivatePartyAsync();
  Resources.RankedConfig=new TextAsset{text="{\"enabled\":true}"};var ranked=Component<RankedService>();
  Check(!await ranked.StartSearchAsync(),"Ranked cannot race an in-flight invitation room creation");
  init.SetResult(true);await creating;

  ResetLobby();var flow=EdgegapOnlineFlow.Instance;Put(flow,"activeGroupId","g1");Invoke(flow,"Attach",NetworkManager.Singleton.CustomMessagingManager);
  Invoke(flow,"OnJoinAck",99UL,new FastBufferReader(new FixedString128Bytes("g1")));
  Check(((HashSet<ulong>)Get(flow,"joinedGuestClients")).Count==0,"disconnected client cannot acknowledge party membership");
  NetworkManager.Singleton.ConnectedClientsIds.Add(1);Invoke(flow,"OnJoinAck",1UL,new FastBufferReader(new FixedString128Bytes("g1")));
  Check(((HashSet<ulong>)Get(flow,"joinedGuestClients")).Count==1,"connected guest acknowledgement accepted");
  Drop(flow);Drop(OnlinePartyService.Instance);Drop(CoopLobbyService.Instance);Drop(OnlinePlanHandshake.Instance);Drop(RankedService.Instance);
  typeof(EdgegapMatchmakerClient).GetProperty("Instance").SetValue(null,null);UnityWebRequest.Handler=null;
  Environment.SetEnvironmentVariable(EdgegapMatchmakerClient.TokenEnvironmentVariable,null);
 }

 static void flowForHandoff()
 {
  var flow=EdgegapOnlineFlow.Instance;Put(flow,"searching",true);Put(flow,"searchOwner",true);Put(flow,"expectedPartyPlayers",1);
  EdgegapMatchmakerClient.Instance.AcceptRankedAssignment("{\"group_id\":\"g\",\"member_id\":\"m\",\"assignment\":{\"fqdn\":\"server.example.invalid\",\"ports\":{\"gameport\":{\"external\":7777}}}}");
  Put(OnlinePartyService.Instance,"session",null);Invoke(flow,"Update");
  Check(flow.IsSearching,"assigned dedicated handoff is not cancelled when Relay room closes");
 }

 static async Task OnlineStartCases()
 {
  const string assigned="{\"group_id\":\"g\",\"member_id\":\"m\",\"ticket_id\":\"t\",\"team_id\":\"red\",\"status\":\"HOST_ASSIGNED\",\"assignment\":{\"public_ip\":\"192.0.2.1\",\"ports\":{\"gameport\":{\"external\":7777}}}}";
  // Exercise the complete production polling continuation after a successful transport link.
  foreach(string outcome in new[]{"disconnect","timeout","launch","cancel"})
  {
   ResetLobby();var flow=EdgegapOnlineFlow.Instance;var party=Room(1);Put(OnlinePartyService.Instance,"session",party);
   party.IsLocked=true;Put(flow,"searching",true);Put(flow,"searchOwner",true);
   var manager=NetworkManager.Singleton;manager.IsHost=manager.IsServer=false;
   int deletes=0;
   UnityWebRequest.Handler=r=>{if(r.method=="DELETE")deletes++;return Reply(r,assigned);};
   await EdgegapMatchmakerClient.Instance.CreateGroupAsync();
   Task polling=(Task)flow.GetType().GetMethod("PollAssignmentAndConnectAsync",Flags).Invoke(flow,new object[]{0});
   await Until(()=>CoopNetworkBootstrap.Connects==1,"dedicated transport connects");
   await Task.Delay(30);
   Check(!polling.IsCompleted&&flow.IsSearching,"connected transport is still monitored until launch");
   Check(flow.Status.Contains("WAITING FOR MATCH START"),"connected player sees launch phase, not opponent search");
   if(outcome=="disconnect")manager.IsConnectedClient=false;
   if(outcome=="timeout")Time.realtimeSinceStartup=96;
   if(outcome=="launch")EdgegapOnlineFlow.MarkMatchStarted();
   if(outcome=="cancel")await flow.LeavePartyAsync();
   await Finish(polling,"dedicated start monitor "+outcome);
   Check(!flow.IsSearching&&!flow.Busy,"terminal start outcome releases search state");
   if(outcome=="launch")
    Check(deletes==0&&flow.LastError=="","successful launch keeps server link and assignment intact");
   else if(outcome=="cancel")
    Check(deletes==0&&flow.LastError==""&&!OnlinePartyService.Instance.InParty&&!EdgegapMatchmakerClient.Instance.HasMembership,"assigned cancel releases locally without spurious failure or immutable-group deletion");
   else
    Check(deletes==0&&!EdgegapMatchmakerClient.Instance.HasMembership&&flow.LastError.Contains(outcome=="timeout"?"TIMED OUT":"DISCONNECTED"),"failed assigned match releases membership locally and retains clear error");
  }

  // A failed dedicated handoff leaves the lobby membership but shuts down Relay.
  // Retrying solo must not wait for a host transport that was intentionally replaced.
  ResetLobby();var retryFlow=EdgegapOnlineFlow.Instance;var retryParty=Room(1);
  Put(OnlinePartyService.Instance,"session",retryParty);
  NetworkManager.Singleton.IsHost=NetworkManager.Singleton.IsServer=NetworkManager.Singleton.IsListening=NetworkManager.Singleton.IsConnectedClient=false;
  int retryGroups=0;
  UnityWebRequest.Handler=r=>
  {
   if(r.method=="POST"&&r.url.EndsWith("/groups"))retryGroups++;
   return Reply(r,Membership().Replace("SEARCHING", "EXPIRED"));
  };
  var retry=retryFlow.BeginSearchAsync();
  await Finish(retry,"solo retry without old Relay");
  Check(await retry&&retryGroups==1,"solo retry creates a fresh group without the former Relay host");
  await Until(()=>!retryFlow.IsSearching&&!retryFlow.Busy,"solo retry completes terminal cleanup");
  Check(!retryFlow.LastError.Contains("RELAY"),"solo retry does not report a friend Relay dependency");

  // Multi-person parties still require an actual live host link to carry the group id.
  ResetLobby();var friendFlow=EdgegapOnlineFlow.Instance;var friendParty=Room(2);
  Put(OnlinePartyService.Instance,"session",friendParty);Put(friendFlow,"expectedPartyPlayers",2);
  Put(friendFlow,"expectedRoster",OnlinePartyService.Instance.ReadyRosterKey);
  NetworkManager.Singleton.IsHost=NetworkManager.Singleton.IsListening=false;
  var relayWait=(Task<bool>)friendFlow.GetType().GetMethod("WaitForFriendRelayAsync",Flags).Invoke(friendFlow,new object[]{2,2f,0});
  Check(!relayWait.IsCompleted,"multi-person retry still waits for Relay");
  Time.realtimeSinceStartup=3;await Finish(relayWait,"missing friend Relay deadline");
  Check(!await relayWait,"missing Relay cannot silently strand friends");
  Time.realtimeSinceStartup=0;NetworkManager.Singleton.IsHost=NetworkManager.Singleton.IsListening=true;NetworkManager.Singleton.ConnectedClientsIds.Add(1);
  relayWait=(Task<bool>)friendFlow.GetType().GetMethod("WaitForFriendRelayAsync",Flags).Invoke(friendFlow,new object[]{2,2f,0});
  Check(await relayWait,"connected friend Relay still passes readiness");
  relayWait=(Task<bool>)friendFlow.GetType().GetMethod("WaitForFriendRelayAsync",Flags).Invoke(friendFlow,new object[]{1,2f,99});
  Check(!await relayWait,"cancelled solo generation cannot bypass readiness");

  ResetLobby();var progress=EdgegapOnlineFlow.Instance;Put(progress,"searching",true);
  foreach(var phase in new[]{("SEARCHING","SEARCHING FOR OPPONENTS"),("TEAM_FOUND","TEAM FOUND"),("MATCH_FOUND","STARTING GAME SERVER")})
  {
   Invoke(EdgegapMatchmakerClient.Instance,"SetStatus",phase.Item1);
   Check(progress.Status.Contains(phase.Item2),"actual matchmaker phase is visible: "+phase.Item1);
  }

  foreach(string invalid in new[]{
   "{\"group_id\":\"g\",\"member_id\":\"m\",\"status\":\"HOST_ASSIGNED\"}",
   assigned.Replace("\"ticket_id\":\"t\",",""),
   assigned.Replace("\"team_id\":\"red\",","")})
  {
   ResetLobby();var client=EdgegapMatchmakerClient.Instance;
   UnityWebRequest.Handler=r=>Reply(r,invalid);await client.CreateGroupAsync();
   Task<bool> waiting=client.WaitForAssignmentAndConnectAsync();
   await Finish(waiting,"malformed assignment rejected promptly");
   Check(!await waiting&&CoopNetworkBootstrap.Connects==0&&client.LastError.Contains("ASSIGNMENT"),"unusable host/ticket/team cannot silently enter handshake");
  }

  ResetLobby();progress=EdgegapOnlineFlow.Instance;Put(progress,"searching",true);Put(progress,"searchOwner",true);
  UnityWebRequest.Handler=r=>{if(r.method=="GET")throw new Exception("fixture connection failure");return Reply(r,Membership());};
  await EdgegapMatchmakerClient.Instance.CreateGroupAsync();
  Task faulted=(Task)progress.GetType().GetMethod("PollAssignmentAndConnectAsync",Flags).Invoke(progress,new object[]{0});
  await Finish(faulted,"unexpected poll failure cleanup");
  Check(!progress.IsSearching&&progress.LastError.Contains("fixture connection failure"),"unexpected poll exceptions are visible and release search");

  ResetLobby();var diagnostics=EdgegapMatchmakerClient.Instance;Debug.Messages.Clear();
  string response=Membership();UnityWebRequest.Handler=r=>Reply(r,response);
  Check(await diagnostics.CreateGroupAsync(),"diagnostic membership created");
  Check(Debug.Messages.Contains("DIMENSIONS FRONT ONLINE: SEARCHING"),"phase from real membership response reaches the console");
  foreach(string phase in new[]{"TEAM_FOUND","MATCH_FOUND","SEARCHING"})
  {
   response=Membership().Replace("SEARCHING",phase);
   int before=Debug.Messages.Count;
   await diagnostics.RefreshMembershipAsync();
   Check(Debug.Messages.Count==before+1,"actual response phase is logged once: "+phase);
   int after=Debug.Messages.Count;await diagnostics.RefreshMembershipAsync();
   Check(Debug.Messages.Count==after,"unchanged membership phase does not spam logs");
  }

  ResetLobby();var readinessParty=Room(1);Put(OnlinePartyService.Instance,"session",readinessParty);int readinessDeletes=0;
  UnityWebRequest.Handler=r=>
  {
   if(r.method=="DELETE")readinessDeletes++;
   return Reply(r,r.method=="PATCH"?Membership().Replace("\"is_ready\":true","\"is_ready\":false"):Membership());
  };
  Check(!await EdgegapOnlineFlow.Instance.BeginSearchAsync(),"unconfirmed ready response cannot start a silent search");
  Check(readinessDeletes==1&&!readinessParty.IsLocked&&!EdgegapOnlineFlow.Instance.IsSearching,"unconfirmed ready cleans membership and unlocks invitations");
  Check(EdgegapOnlineFlow.Instance.LastError.Contains("READY"),"server readiness rejection remains visible");
 }
}

