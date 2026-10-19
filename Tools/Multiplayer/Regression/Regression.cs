using System;
using System.Reflection;
using Alterverse.Combat;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Unity.Netcode;
using UnityEngine;

static class Regression
{
 static int checks;
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
 static Vector3 P(float x)=>new(x,0,0);
 static void Position(Vector3 actual,Vector3 expected,string reason)=>Check((actual-expected).sqrMagnitude<.000001f,reason);
 static CoopHeroSlot Actor(int index,bool local=false)
 {
  var g=new GameObject();var slot=g.AddComponent<CoopHeroSlot>();slot.Slot=index;slot.IsLocalHuman=local;
  g.AddComponent<Health>();g.AddComponent<HeroLocomotion>();g.AddComponent<HeroCombatProfile>();g.AddComponent<HeroIdentity>();
  return slot;
 }
 static void HistoryCases()
 {
  var h=new OwnerPoseHistory();uint a=h.Record(P(1));h.Record(P(2));h.Record(P(3));
  Check(h.TryReconcile(a,P(1),P(4),out var result),"Acknowledgement resolves a recorded prediction");
  Position(result,P(4),"Network latency alone must not pull newer owner movement backwards");
  Check(!h.TryReconcile(a,P(1),P(5),out result),"Repeated acknowledgement cannot correct twice");
  Position(result,P(5),"Repeated snapshots preserve current owner movement");
  h.Clear(true);a=h.Record(P(1));uint b=h.Record(P(2));uint c=h.Record(P(3));
  h.TryReconcile(a,P(.5f),P(4),out result);Position(result,P(3.5f),"A real server collision rejection corrects the owner");
  h.TryReconcile(b,P(1.5f),result,out result);Position(result,P(3.5f),"An already applied correction rebases later predictions");
  Check(!h.TryReconcile(a,P(9),result,out _),"Out-of-order acknowledgement cannot rewind state");
  h.TryReconcile(c,P(2.5f),result,out result);Position(result,P(3.5f),"Later acknowledgement retains pending movement");
  Check(h.Count==0,"Acknowledged history is retired");
  for(int i=0;i<1000;i++)h.Record(P(i));Check(h.Count==OwnerPoseHistory.Capacity,"Packet loss cannot grow history without bound");
  Check(!h.TryReconcile(0,P(0),P(2),out _),"Missing acknowledgement is not a correction");
  Check(!h.TryReconcile(100000,P(0),P(2),out _),"Unknown sequence cannot invent a prediction");
  h.Clear();Check(h.Count==0,"Relocation discards outstanding predictions");
  uint following=h.Record(P(3));Check(following>1000,"Relocation keeps the owner sequence increasing");
  Check(!h.TryReconcile(following,new(float.NaN,0,0),P(3),out _),"Non-finite server correction is rejected");
  var f=BindingFlags.NonPublic|BindingFlags.Instance;
  h.Clear(true);typeof(OwnerPoseHistory).GetField("nextSequence",f).SetValue(h,uint.MaxValue-1);
  typeof(OwnerPoseHistory).GetField("lastAcknowledged",f).SetValue(h,uint.MaxValue-2);
  uint last=h.Record(P(1)),wrapped=h.Record(P(2));Check(last==uint.MaxValue&&wrapped==1,"Sequence wrap skips reserved zero");
  Check(h.TryReconcile(last,P(1),P(3),out _)&&h.TryReconcile(wrapped,P(2),P(3),out _),"Acknowledgements survive unsigned sequence wrap");
 }
 static void TransportCases()
 {
  CoopAuthority.Active=true;CoopAuthority.ActivePlan=new();CoopAuthority.ActivePlan.Session.Slots[0]=new();
  MatchNetworkProtocol.AllowOwner=true;Time.unscaledTime=0;Time.timeScale=1;
  var server=new CoopHeroStateSync();var client=new CoopHeroStateSync();
  var serverActor=Actor(0);var clientActor=Actor(0,true);server.Slot(serverActor);client.Slot(clientActor,true);
  // A missing Health must not make the announced snapshot count disagree with the payload.
  var empty=new GameObject().AddComponent<CoopHeroSlot>();empty.Slot=1;server.Slot(empty);
  var serverManager=new NetworkManager{IsServer=true};var clientManager=new NetworkManager();
  var serverBoard=new MatchScoreboard();serverBoard.Seed(serverActor.GetComponent<Health>(),3,1,2);
  var clientBoard=new MatchScoreboard();clientBoard.Registered.Add(clientActor.GetComponent<Health>());
  for(int i=1;i<=5;i++)
  {
   clientActor.transform.position=P(i*.325f);CoopAuthority.LocalHasServerAuthority=false;
   client.Send(clientManager);
  }
  Check(client.Messages.Sent.Count==5,"Owner sends movement predictions through the production writer");
  CoopAuthority.LocalHasServerAuthority=true;NetworkManager.Singleton=serverManager;Time.unscaledTime=.05f;
  server.ReceiveOwner(7,client.Messages.Sent[0].Values);Position(serverActor.transform.position,P(.325f),"Server validates and accepts normal movement");
  serverActor.GetComponent<Health>().Current=62;serverActor.GetComponent<Health>().Shield=18;
  MatchScoreboard.Instance=serverBoard;server.Broadcast(serverManager);
  var snapshot=server.Messages.Sent[^1].Values;
  Check((int)snapshot[1]==1,"Snapshot count includes only actors with serializable health");
  // Physics may have advanced after processing the owner packet. Only the pose at
  // acknowledgement time is comparable to that packet's client-side prediction.
  snapshot[3]=.5f;
  CoopAuthority.LocalHasServerAuthority=false;NetworkManager.Singleton=clientManager;MatchScoreboard.Instance=clientBoard;
  client.ReceiveSnapshot(0,snapshot);
  Position(clientActor.transform.position,P(1.625f),"Full transport preserves four newer movement predictions in flight");
  Check(clientActor.GetComponent<Health>().Current==62&&clientActor.GetComponent<Health>().Shield==18,"Server health and shield loss reaches the client's real snapshot reader");
  Check(clientBoard.Read(clientActor.GetComponent<Health>())==(3,1,2),"Live K/D/A reaches the scoreboard before the match ends");
  client.ReceiveSnapshot(0,snapshot);Position(clientActor.transform.position,P(1.625f),"Duplicate server snapshot does not double-correct");
  var hp=clientActor.GetComponent<Health>();hp.Current=77;client.ReceiveSnapshot(99,snapshot);
  Check(hp.Current==77,"A non-server cannot publish hero health or scores");
  object[] otherRound=(object[])snapshot.Clone();otherRound[0]=999u;client.ReceiveSnapshot(0,otherRound);
  Check(hp.Current==77,"A previous round cannot overwrite hero state");
  CoopAuthority.LocalHasServerAuthority=true;NetworkManager.Singleton=serverManager;MatchScoreboard.Instance=serverBoard;
  Time.unscaledTime=.10f;server.ReceiveOwner(7,client.Messages.Sent[1].Values);
  Vector3 accepted=serverActor.transform.position;server.ReceiveOwner(7,client.Messages.Sent[0].Values);
  Position(serverActor.transform.position,accepted,"Out-of-order owner packet cannot overwrite accepted movement");
  object[] cheat=(object[])client.Messages.Sent[4].Values.Clone();cheat[5]=100f;Time.unscaledTime=.15f;server.ReceiveOwner(7,cheat);
  Check(serverActor.transform.position.x-accepted.x<1,"Sequence acknowledgement does not bypass the server speed limit");
  object[] untrusted=(object[])client.Messages.Sent[4].Values.Clone();untrusted[3]=6u;untrusted[5]=200f;
  accepted=serverActor.transform.position;MatchNetworkProtocol.AllowOwner=false;server.ReceiveOwner(7,untrusted);
  Position(serverActor.transform.position,accepted,"Unauthorized owner cannot move another player");MatchNetworkProtocol.AllowOwner=true;
  untrusted[5]=float.NaN;server.ReceiveOwner(7,untrusted);Position(serverActor.transform.position,accepted,"Non-finite movement cannot poison server state");
  // Forced movement remains authoritative and invalidates pre-dash predictions.
  serverActor.GetComponent<HeroLocomotion>().IsForcedMoving=true;serverActor.transform.position=P(9);server.Broadcast(serverManager);
  CoopAuthority.LocalHasServerAuthority=false;MatchScoreboard.Instance=clientBoard;
  client.ReceiveSnapshot(0,server.Messages.Sent[^1].Values);
  Position(clientActor.transform.position,P(9),"Authoritative forced movement still relocates the owner");Check(client.History.Count==0,"Forced relocation discards old movement predictions");
  // Applying a score snapshot never grants a reward or invents combat events.
  clientBoard.ApplyNetworkScore(hp,-1,-2,-3);Check(clientBoard.Read(hp)==(0,0,0),"Negative counters are clamped");
  CoopAuthority.LocalHasServerAuthority=true;clientBoard.ApplyNetworkScore(hp,999,999,999);
  Check(clientBoard.Read(hp)==(0,0,0),"Client snapshots cannot overwrite authoritative scoreboard entries");
  CoopAuthority.LocalHasServerAuthority=false;CoopAuthority.Active=false;clientBoard.ApplyNetworkScore(hp,999,999,999);
  Check(clientBoard.Read(hp)==(0,0,0),"Network score snapshots do not change offline matches");CoopAuthority.Active=true;
  var clientActors=new CoopHeroSlot[8];clientActors[0]=clientActor;
  for(int i=1;i<8;i++)
  {
   var s=Actor(i);s.transform.position=new Vector3(i*2,0,i*.5f);server.Slot(s);
   serverBoard.Seed(s.GetComponent<Health>(),i*2,i,i+1);
   var c=Actor(i);c.gameObject.AddComponent<Alterverse.Visuals.HeroVisualMotion>();client.Slot(c);clientActors[i]=c;clientBoard.Registered.Add(c.GetComponent<Health>());
  }
  CoopAuthority.LocalHasServerAuthority=true;MatchScoreboard.Instance=serverBoard;server.Broadcast(serverManager);
  Check((int)server.Messages.Sent[^1].Values[1]==8,"A complete 4v4 roster is serialized");
  float snapshotTime=Time.unscaledTime;
  int bytes=0;foreach(var field in server.Messages.Sent[^1].Values)
   bytes+=field is bool?1:field is int||field is uint||field is float?4:throw new Exception("Unknown snapshot field type");
  Check(bytes<1200,"Full roster snapshot stays below a conservative 1200-byte application payload budget");
  int projectedTenBytes=8+(bytes-8)/8*10;
  Check(projectedTenBytes<1200,"Projected ten-hero record payload fits the same budget; this does not enable ten-player admission");
  Console.WriteLine($"Hero snapshot application bytes: eight={bytes}, projected ten={projectedTenBytes}; transport framing is additional.");
  CoopAuthority.LocalHasServerAuthority=false;MatchScoreboard.Instance=clientBoard;client.ReceiveSnapshot(0,server.Messages.Sent[^1].Values);
  for(int i=1;i<8;i++)
  {
   Check(clientActors[i].GetComponent<Alterverse.Visuals.HeroVisualMotion>().SampledAt==snapshotTime,"hero source timestamp reaches renderer through actual transport");
   Check(clientBoard.Read(clientActors[i].GetComponent<Health>())==(i*2,i,i+1),"Full-roster live score for slot "+i);
   Position(clientActors[i].transform.position,new Vector3(i*2,0,i*.5f),"Full-roster remote pose for slot "+i);
  }
  CoopPlanHandshake.Round=124;clientActor.GetComponent<Health>().IsDead=false;
  clientActor.transform.position=P(.325f);client.Send(clientManager);
  var nextRoundPacket=client.Messages.Sent[^1].Values;
  Check((uint)nextRoundPacket[0]==124&&(uint)nextRoundPacket[3]==1,"A new round resets prediction sequencing before the first owner packet");
  hp.Current=17;client.ReceiveSnapshot(0,snapshot);Check(hp.Current==17,"An old-round snapshot cannot consume new-round prediction history");
  Check(client.History.Count==1,"Old-round snapshot retains the new round's outstanding prediction");
  CoopAuthority.LocalHasServerAuthority=true;serverActor.GetComponent<HeroLocomotion>().IsForcedMoving=false;
  serverActor=Actor(0);server.Slot(serverActor);serverActor.transform.position=Vector3.zero;server.ReceiveOwner(7,nextRoundPacket);
  Position(serverActor.transform.position,P(.325f),"Server accepts restarted owner sequencing in the new round");
  CoopPlanHandshake.Round=123;
 }
 static void DashTransportCases()
 {
  foreach(bool hook in new[]{false,true})foreach(bool loseStart in new[]{false,true})
  {
   CoopPlanHandshake.Round++;CoopAuthority.ActivePlan=new();CoopAuthority.ActivePlan.Session.Slots[0]=new();
   CoopAuthority.Active=true;Time.timeScale=1;Time.unscaledTime=0;MatchNetworkProtocol.AllowOwner=true;
   var server=new CoopHeroStateSync();var client=new CoopHeroStateSync();
   var a=Actor(0);var b=Actor(0,true);server.Slot(a);client.Slot(b,true);
   var sm=new NetworkManager{IsServer=true};var cm=new NetworkManager();
   CoopAuthority.LocalHasServerAuthority=false;NetworkManager.Singleton=cm;client.Send(cm);
   var stale=client.Messages.Sent[^1].Values;
   var body=a.GetComponent<HeroLocomotion>();
   if(hook)body.IsForcedMoving=true;else body.IsDashing=true;
   CoopAuthority.LocalHasServerAuthority=true;NetworkManager.Singleton=sm;
   a.transform.position=P(2);server.Broadcast(sm);
   Check(body.MovementRevision==1,"Beginning authoritative displacement advances the production epoch");
   if(!loseStart)
   {
    CoopAuthority.LocalHasServerAuthority=false;NetworkManager.Singleton=cm;client.ReceiveSnapshot(0,server.Messages.Sent[^1].Values);
    Position(b.transform.position,P(2),"Owner receives actual dash/hook movement while in progress");
    Check(client.History.Count==0,"Dash clears incompatible owner predictions");
   }
   CoopAuthority.LocalHasServerAuthority=true;NetworkManager.Singleton=sm;
   a.transform.position=P(6);body.IsDashing=body.IsForcedMoving=false;
   server.ReceiveOwner(7,stale);
   Position(a.transform.position,P(6),"Pre-dash owner packet cannot undo the final server endpoint");
   Check(body.MovementRevision==2,"Finishing displacement advances a second epoch");
   server.Broadcast(sm);var endpoint=server.Messages.Sent[^1].Values;
   // Drop final snapshot once, then recover from the next ordinary snapshot.
   server.Broadcast(sm);
   CoopAuthority.LocalHasServerAuthority=false;NetworkManager.Singleton=cm;
   client.ReceiveSnapshot(0,server.Messages.Sent[^1].Values);
   Position(b.transform.position,P(6),"Endpoint survives lost start/end packets, even when movement is no longer active");
   client.ReceiveSnapshot(0,endpoint);Position(b.transform.position,P(6),"Repeated endpoint snapshot is idempotent");
   b.transform.position=P(6.325f);client.Send(cm);
   CoopAuthority.LocalHasServerAuthority=true;NetworkManager.Singleton=sm;Time.unscaledTime=.05f;
   server.ReceiveOwner(7,client.Messages.Sent[^1].Values);
   Position(a.transform.position,P(6.325f),"Movement resumes only after owner acknowledges the final movement epoch");
  }
  CoopPlanHandshake.Round=123;
 }
 static void HealingAndStatusCases()
 {
  CoopAuthority.Active=true;CoopAuthority.LocalIsHost=false;CoopAuthority.LocalHasServerAuthority=true;
  Time.time=0;Time.timeScale=1;
  var pickup=new GameObject().AddComponent<CentralHealingPickup>();
  var member=new GameObject().AddComponent<TeamMember>();var health=member.gameObject.AddComponent<Health>();health.Current=40;
  pickup.Members.Add(member);int changes=0;bool available=true;
  Action<bool,float> changed=(value,delay)=>{changes++;available=value;};
  CentralHealingPickup.NetworkAvailabilityChanged+=changed;
  pickup.Tick();Check(health.Current>40&&!pickup.Available,"Dedicated server consumes and heals");
  Check(changes==1&&!available,"Dedicated server publishes consumed healing despite LocalIsHost false");
  Time.time=30;pickup.Tick();Check(changes==2&&available,"Dedicated server publishes healing respawn");
  CoopAuthority.LocalHasServerAuthority=false;health.Current=40;pickup.Tick();
  Check(health.Current==40&&changes==2,"Client cannot heal or broadcast availability");
  pickup.ApplyNetworkAvailability(false,10);Check(!pickup.Available,"Client accepts authoritative healing availability");
  CentralHealingPickup.NetworkAvailabilityChanged-=changed;
  var status=member.gameObject.AddComponent<HeroCombatStatus>();var source=new UnityEngine.Object();
  Time.time=100;Check(status.TryRoot(source,2,true)&&status.Rooted&&status.ActionLocked&&!status.Stunned,"Source-owned root locks movement/actions without becoming stun");
  status.ReleaseRoot(new UnityEngine.Object());Check(status.Rooted,"Other sources cannot release root");
  status.ReleaseRoot(source);Check(!status.Rooted&&!status.ActionLocked,"Root release restores actions");
  status.Stun(.5f);Check(status.Stunned&&status.MoveMultiplier==0,"Stun stops movement");
  Time.time=101;Check(!status.Stunned,"Stun expires");
  status.ApplyNetworkSnapshot(false,0);Check(status.Rooted&&!status.Stunned,"Network root stops movement without stun");
  status.Clear();Check(!status.Rooted&&!status.Stunned&&status.MoveMultiplier==1,"Respawn clears status overrides");
  CoopAuthority.LocalHasServerAuthority=true;CoopAuthority.ActivePlan=new();CoopAuthority.ActivePlan.Session.Slots[0]=new();
  var server=new CoopHeroStateSync();var actor=Actor(0);server.Slot(actor);var combat=actor.gameObject.AddComponent<HeroCombatStatus>();
  NetworkManager.Singleton=new NetworkManager{IsServer=true};Time.time=200;combat.Stun(.5f);
  object[] packet={CoopPlanHandshake.Round,0,new Unity.Collections.FixedString128Bytes("player"),1u,0u,5f,0f,0f,0f,0f,0f};
  server.ReceiveOwner(7,packet);Position(actor.transform.position,P(0),"Owner packets cannot move a stunned server actor through movement slack");
  Time.time=202;combat.TryRoot(source,2);packet[3]=2u;server.ReceiveOwner(7,packet);
  Position(actor.transform.position,P(0),"Owner packets cannot move a rooted server actor");
 }
 static void WorldPacketCases()
 {
  var sync=new CoopWorldStateSync();var manager=new NetworkManager{IsServer=true};
  for(int i=0;i<110;i++)
  {
   var entity=new GameObject().AddComponent<CoopWorldEntityKey>();entity.Key=i.ToString().PadRight(120,'x');
   entity.gameObject.AddComponent<Health>();sync.Live.Add(entity);
  }
  CoopPlanHandshake.Calls=0;sync.Broadcast(manager);int delivered=0;
  Check(CoopPlanHandshake.Calls==1,"One round hash covers all 19 world chunks instead of hashing per chunk");
  foreach(var message in sync.Messages.Sent)
  {
   int count=(int)message.Values[1];delivered+=count;
   Check(count<=6,"World states are bounded to six entities per independent packet");
   // Conservative full-size FixedString storage (actual NGO may encode fewer bytes).
   Check(12+count*(128+7*4+1)<=1024,"Maximum-key state packet fits the writer and unfragmented budget");
   Check(message.Delivery==NetworkDelivery.UnreliableSequenced,"Repeated world motion stays on unreliable delivery");
  }
  Check(delivered==110,"World snapshots do not silently omit objects beyond the former 96 cap");
  sync.Messages.Sent.Clear();sync.Live.Clear();sync.Broadcast(manager);int removed=0;
  foreach(var message in sync.Messages.Sent)
  {
   int count=(int)message.Values[2];removed+=count;
   Check((int)message.Values[1]==0&&count<=6,"Removal chunks retain the existing receiver wire format");
   Check(message.Delivery==NetworkDelivery.ReliableSequenced,"One-time tombstones survive packet loss");
  }
  Check(removed==110,"All removals are delivered beyond the former 64 cap");
  sync.Messages.Sent.Clear();sync.Broadcast(manager);Check(sync.Messages.Sent.Count==0,"Removed world objects are retired after reliable send");
 }
 static void SnapshotWorkCases()
 {
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=true;
  NetworkManager.Singleton=new NetworkManager{IsServer=true};Time.unscaledTime=0;
  var sync=new CoopWorldStateSync();var e=new GameObject().AddComponent<CoopWorldEntityKey>();e.Key="live";
  var health=e.gameObject.AddComponent<Health>();health.Current=37;health.Shield=12;e.transform.position=new Vector3(3,2,1);
  sync.Live.Add(e);sync.Tick();
  Check(sync.RefreshCalls==1,"Server discovers world entities once on a send frame, not twice");
  var first=sync.Messages.Sent[0];
  Check((uint)first.Values[0]==CoopPlanHandshake.Round&&(int)first.Values[1]==1,"World header remains compatible");
  Check(((Unity.Collections.FixedString128Bytes)first.Values[2]).ToString()=="live"&&
    (float)first.Values[3]==3&&(float)first.Values[4]==2&&(float)first.Values[5]==1&&
    (float)first.Values[7]==37&&(float)first.Values[8]==12&&!(bool)first.Values[9]&&(float)first.Values[10]==0&&(int)first.Values[11]==0,
    "World snapshot preserves identity, position, HP, shield, alive state and tombstone count");
  object buffer=typeof(CoopWorldStateSync).GetField("live",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sync);
  for(int i=1;i<=60;i++){Time.unscaledTime=i/60f;sync.Tick();}
  Check(sync.RefreshCalls<=9,"Full-world discovery stays bounded to eight updates per second plus initial state");
  Check(sync.Messages.Sent.Count==2,"Unchanged stationary world state uses a one-second recovery heartbeat");
  Check(ReferenceEquals(buffer,typeof(CoopWorldStateSync).GetField("live",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sync)),"Live snapshot buffer survives subsequent sends");
  health.Current=21;health.IsDead=true;sync.Broadcast(NetworkManager.Singleton);
  var changed=sync.Messages.Sent[^1];Check((float)changed.Values[7]==21&&(bool)changed.Values[9],"Reused buffers read current state rather than stale values");
  sync.Live.Clear();sync.Broadcast(NetworkManager.Singleton);
  Check((int)sync.Messages.Sent[^1].Values[2]==1&&sync.Messages.Sent[^1].Delivery==NetworkDelivery.ReliableSequenced,"Reused buffer cannot retain a removed entity");
  int previous=sync.RefreshCalls;CoopAuthority.LocalHasServerAuthority=false;Time.unscaledTime=2;sync.Tick();
  Check(sync.RefreshCalls==previous+1,"Client keeps its periodic entity discovery");
  CoopAuthority.LocalHasServerAuthority=true;
  var hero=new CoopHeroStateSync();hero.Slot(Actor(0));CoopPlanHandshake.Calls=0;hero.Broadcast(NetworkManager.Singleton);
  Check(CoopPlanHandshake.Calls==1,"Hero snapshot reuses the identity checked by EnsurePoseRound");
  var owner=new CoopHeroStateSync();owner.Slot(Actor(0,true),true);NetworkManager.Singleton=new NetworkManager();
  CoopAuthority.LocalHasServerAuthority=false;CoopPlanHandshake.Calls=0;owner.Send(NetworkManager.Singleton);
  Check(CoopPlanHandshake.Calls==1,"Owner movement reuses the current round identity");
 }
 static void EnlargedBoundaryCases()
 {
  foreach(var pair in new[]{
   (new Vector3(49,0,0),new Vector3(.2f,0,0)),
   (new Vector3(-49,0,0),new Vector3(-.2f,0,0)),
   (new Vector3(0,0,34),new Vector3(0,0,.2f)),
   (new Vector3(0,0,-34),new Vector3(0,0,-.2f))})
  {
   CoopPlanHandshake.Round++;CoopAuthority.ActivePlan=new();CoopAuthority.ActivePlan.Session.Slots[0]=new();
   CoopAuthority.Active=true;Time.timeScale=1;Time.unscaledTime=.05f;MatchNetworkProtocol.AllowOwner=true;
   var server=new CoopHeroStateSync();var client=new CoopHeroStateSync();
   var a=Actor(0);var b=Actor(0,true);server.Slot(a);client.Slot(b,true);
   a.transform.position=pair.Item1;b.transform.position=pair.Item1+pair.Item2;
   var sm=new NetworkManager{IsServer=true};var cm=new NetworkManager();
   CoopAuthority.LocalHasServerAuthority=false;NetworkManager.Singleton=cm;client.Send(cm);
   Vector3 sent=b.transform.position;
   b.transform.position+=pair.Item2;Vector3 advanced=b.transform.position;
   CoopAuthority.LocalHasServerAuthority=true;NetworkManager.Singleton=sm;
   server.ReceiveOwner(7,client.Messages.Sent[^1].Values);
   Position(a.transform.position,sent,"Server accepts ordinary movement in the enlarged owner area");
   MatchScoreboard.Instance=new MatchScoreboard();server.Broadcast(sm);
   CoopAuthority.LocalHasServerAuthority=false;NetworkManager.Singleton=cm;
   client.ReceiveSnapshot(0,server.Messages.Sent[^1].Values);
   Position(b.transform.position,advanced,"Acknowledgement at enlarged edge does not create an invisible wall or rewind in-flight movement");
  }
  float width=ArenaLayout.MovementHalfWidth,height=ArenaLayout.MovementHalfHeight;
  Position(GameplayCollision.ClampToRoundedArena(new Vector3(1000,0,0),width,height),
   new Vector3(ArenaLayout.HalfLength-ArenaLayout.LengthScale,0,0),"Movement retains the existing scaled floor margin");
  Position(GameplayCollision.ClampToRoundedArena(new Vector3(0,0,-1000),width,height),
   new Vector3(0,0,-ArenaLayout.HalfBreadth+1),"Movement remains inside the visible floor breadth");
  foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
  {
   var corner=GameplayCollision.ClampToRoundedArena(new Vector3(x*1000,0,z*1000),width,height);
   Check(Math.Abs(corner.x)<width*ArenaLayout.LengthScale&&Math.Abs(corner.z)<height,"Rounded corners remain enforced");
   Position(GameplayCollision.ClampToRoundedArena(corner,width,height),corner,"Corner clamping is stable across repeated acknowledgements");
  }
 }
 static void CadenceCases()
 {
  foreach(int fps in new[]{30,60,90,120})foreach(bool host in new[]{false,true})
  {
   CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=host;
   CoopAuthority.ActivePlan=new();Time.unscaledTime=0;
   var sync=new CoopHeroStateSync();sync.Slot(Actor(0,!host),!host);
   NetworkManager.Singleton=new NetworkManager{IsServer=host};
   MatchScoreboard.Instance=new MatchScoreboard();
   for(int i=0;i<fps*10;i++){Time.unscaledTime=i/(float)fps;sync.Tick();}
   Check(Math.Abs(sync.Messages.Sent.Count-200)<=1,$"Production {(host?"server":"owner")} maintains 20 Hz at {fps} FPS, got {sync.Messages.Sent.Count/10f}");
   Console.WriteLine($"Production {(host?"server":"owner")} at {fps} FPS: {sync.Messages.Sent.Count/10f} updates/s.");
   int before=sync.Messages.Sent.Count;Time.unscaledTime+=3;sync.Tick();sync.Tick();
   Check(sync.Messages.Sent.Count==before+1,"A stall sends only one current pose, with no catch-up burst");
  }
  // Full snapshots must not restart the moving-creature clock.
  foreach(int fps in new[]{30,60,120})
  {
   Time.unscaledTime=0;CoopAuthority.LocalHasServerAuthority=true;
   NetworkManager.Singleton=new NetworkManager{IsServer=true};
   var world=new CoopWorldStateSync();var entity=new GameObject().AddComponent<CoopWorldEntityKey>();entity.Key="moving";
   var hp=entity.gameObject.AddComponent<Health>();var tank=entity.gameObject.AddComponent<HotZoneTank>();tank.TankHealth=hp;
   HotZoneTank.Active.Clear();HotZoneTank.Active.Add(tank);world.Live.Add(entity);
   for(int i=0;i<fps*10;i++){Time.unscaledTime=i/(float)fps;world.Tick();}
   Check(Math.Abs(world.Messages.Sent.Count-200)<=1,"Moving world packets retain 20 Hz across 8 Hz full snapshots");
   Check(world.RefreshCalls<=81,"World discovery does not run at movement frequency");
   HotZoneTank.Active.Clear();
  }
 }
 static void WorldDeltaCases()
 {
  Time.unscaledTime=0;var sync=new CoopWorldStateSync();var manager=new NetworkManager{IsServer=true};
  for(int i=0;i<110;i++){var e=new GameObject().AddComponent<CoopWorldEntityKey>();e.Key="static"+i;e.gameObject.AddComponent<Health>();sync.Live.Add(e);}
  sync.Broadcast(manager);Check(sync.Messages.Sent.Count==19,"Initial full world reaches clients");
  sync.Messages.Sent.Clear();
  for(int i=1;i<8;i++){Time.unscaledTime=i*.125f;sync.Broadcast(manager);}
  Check(sync.Messages.Sent.Count==0,"Seven unchanged world scans produce zero redundant static packets");
  Time.unscaledTime=1;sync.Broadcast(manager);
  Check(sync.Messages.Sent.Count==19,"One-second full-state heartbeat repairs packet loss and late joins");
  sync.Messages.Sent.Clear();Time.unscaledTime=1.125f;
  var hp=sync.Live[0].GetComponent<Health>();hp.Current=29;hp.Shield=7;hp.IsDead=true;
  sync.Broadcast(manager);
  Check(sync.Messages.Sent.Count==1&&(int)sync.Messages.Sent[0].Values[1]==1,"Health/shield/death change bypasses static heartbeat immediately");
  sync.Messages.Sent.Clear();sync.Live[1].transform.position=P(9);sync.Broadcast(manager);
  Check(sync.Messages.Sent.Count==1,"A stationary-object relocation is never suppressed");
  sync.Messages.Sent.Clear();var removed=sync.Live[2];sync.Live.RemoveAt(2);sync.Broadcast(manager);
  Check(sync.Messages.Sent.Count==1&&sync.Messages.Sent[0].Delivery==NetworkDelivery.ReliableSequenced,"Filtering preserves reliable removal");
  sync.Messages.Sent.Clear();sync.Live.Add(removed);sync.Broadcast(manager);
  Check(sync.Messages.Sent.Count==1&&(int)sync.Messages.Sent[0].Values[1]==1,"Recreated key is not hidden by retired history");
  var history=new WorldSnapshotHistory();history.ShouldSend("a",P(0),0,100,0,false,false,10);
  Check(history.ShouldSend("a",P(0),0,100,0,false,false,0),"Clock reset forces fresh state");
  Check(history.ShouldSend("a",P(0),0,100,0,false,true,.01f),"Stationary creature still supplies interpolation samples");
  history.Clear();Check(history.ShouldSend("a",P(0),0,100,0,false,false,.02f),"Reconnect clears history");
  sync.Messages.Sent.Clear();CoopAuthority.ActivePlan=new();sync.Broadcast(manager);
  Check(sync.Messages.Sent.Count==19,"New match plan forces all current world state through the cache");
  long before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++)history.ShouldSend("a",P(0),0,100,0,false,false,.02f);
  Check(GC.GetAllocatedBytesForCurrentThread()==before,"Unchanged snapshot comparisons allocate no temporary managed memory");
  Console.WriteLine("110 unchanged stationary objects: 19 packets/second instead of 152; moving actors stay at 20 Hz.");
 }
 static void TimingCases()
 {
  var window=new Alterverse.Performance.FrameTimingWindow();bool report=false;
  for(int i=0;i<600;i++)report|=window.Observe(1f/60f);
  Check(report&&Math.Abs(window.Fps-60)<.01f&&Math.Abs(window.P95Milliseconds-1000f/60)<.01f,"Server pacing window measures 60 FPS and frame percentile");
  window.Reset();report=false;
  for(int i=0;i<580;i++)report|=window.Observe(.016f);
  report|=window.Observe(1f);
  Check(report&&window.Fps<60&&window.MaxMilliseconds==1000&&window.P95Milliseconds<17,"Rare stall is retained in max even when p95 stays low");
  window.Reset();Check(!window.Observe(float.NaN)&&!window.Observe(float.PositiveInfinity)&&!window.Observe(0),"Invalid frame samples cannot poison the diagnostics");
  for(int i=0;i<10000;i++)window.Observe(.001f);
  Check(window.Fps>999&&window.P95Milliseconds<1.01f,"Diagnostics stay bounded above the sample capacity");
  long before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++)window.Observe(.001f);
  Check(GC.GetAllocatedBytesForCurrentThread()==before,"Timing collection and reporting reuse their sample storage");
  var monitor=new Alterverse.Performance.RuntimePerformanceMonitor();
  var update=(Action)Delegate.CreateDelegate(typeof(Action),monitor,typeof(Alterverse.Performance.RuntimePerformanceMonitor).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic));
  DedicatedServerNetworkRuntime.Requested=true;Application.isFocused=false;Time.timeScale=0;Time.unscaledDeltaTime=1f/60;
  int logs=Debug.Logs;for(int i=0;i<600;i++)update();
  Check(Debug.Logs==logs+1&&Math.Abs(Alterverse.Performance.RuntimePerformanceMonitor.SmoothedFps-60)<.01f,"Headless paused/unfocused server emits actual frame diagnostics");
  DedicatedServerNetworkRuntime.Requested=false;Application.isFocused=true;Time.timeScale=1;
  for(int i=0;i<120;i++)update();
  Check(Debug.Logs==logs+1,"Client performance monitoring does not emit dedicated-server logs");
 }
 static void OwnerBurstProbe()
 {
  foreach(float speed in new[]{4.77f,6.5f,8.0f})
  {
   CoopPlanHandshake.Round++;CoopAuthority.ActivePlan=new();CoopAuthority.ActivePlan.Session.Slots[0]=new();
   CoopAuthority.Active=true;Time.timeScale=1;Time.unscaledTime=0;MatchNetworkProtocol.AllowOwner=true;
   var server=new CoopHeroStateSync();var actor=Actor(0);server.Slot(actor);actor.GetComponent<HeroCombatProfile>().MoveSpeed=speed;
   CoopAuthority.LocalHasServerAuthority=true;NetworkManager.Singleton=new NetworkManager{IsServer=true};
   float worst=0;
   for(int i=1;i<=40;i++)
   {
    // Four legitimate 20Hz poses arrive together after a 200ms network/receive stall.
    Time.unscaledTime=(float)Math.Ceiling(i/4f)*.2f;
    float position=i*.05f*speed;
    object[] packet={CoopPlanHandshake.Round,0,new Unity.Collections.FixedString128Bytes("player"),(uint)i,0u,position,0f,0f,0f,speed,0f};
    server.ReceiveOwner(7,packet);
    worst=Math.Max(worst,position-actor.transform.position.x);
   }
   Console.WriteLine($"BURST PROBE speed={speed:F2} units/s: largest valid-pose rejection={worst:F4} units");
  }
 }
 static void Main(){OwnerBurstProbe();HistoryCases();TransportCases();DashTransportCases();HealingAndStatusCases();WorldPacketCases();SnapshotWorkCases();EnlargedBoundaryCases();CadenceCases();WorldDeltaCases();TimingCases();Console.WriteLine("PASS: "+checks+" movement, transport, scoreboard, healing and status checks (Unity/network substitutes)");}
}


