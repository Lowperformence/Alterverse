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
   var c=Actor(i);client.Slot(c);clientActors[i]=c;clientBoard.Registered.Add(c.GetComponent<Health>());
  }
  CoopAuthority.LocalHasServerAuthority=true;MatchScoreboard.Instance=serverBoard;server.Broadcast(serverManager);
  Check((int)server.Messages.Sent[^1].Values[1]==8,"A complete 4v4 roster is serialized");
  int bytes=0;foreach(var field in server.Messages.Sent[^1].Values)
   bytes+=field is bool?1:field is int||field is uint||field is float?4:throw new Exception("Unknown snapshot field type");
  Check(bytes<1200,"Full roster snapshot stays below a conservative 1200-byte application payload budget");
  int projectedTenBytes=8+(bytes-8)/8*10;
  Check(projectedTenBytes<1200,"Projected ten-hero record payload fits the same budget; this does not enable ten-player admission");
  Console.WriteLine($"Hero snapshot application bytes: eight={bytes}, projected ten={projectedTenBytes}; transport framing is additional.");
  CoopAuthority.LocalHasServerAuthority=false;MatchScoreboard.Instance=clientBoard;client.ReceiveSnapshot(0,server.Messages.Sent[^1].Values);
  for(int i=1;i<8;i++)
  {
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
   Check(12+count*(128+6*4+1)<=1024,"Maximum-key state packet fits the writer and unfragmented budget");
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
    (float)first.Values[7]==37&&(float)first.Values[8]==12&&!(bool)first.Values[9]&&(int)first.Values[10]==0,
    "World snapshot preserves identity, position, HP, shield, alive state and tombstone count");
  object buffer=typeof(CoopWorldStateSync).GetField("live",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sync);
  for(int i=1;i<=60;i++){Time.unscaledTime=i/60f;sync.Tick();}
  Check(sync.RefreshCalls==sync.Messages.Sent.Count,"No server entity discovery on nonsend frames");
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
 static void Main(){HistoryCases();TransportCases();DashTransportCases();HealingAndStatusCases();WorldPacketCases();SnapshotWorkCases();Console.WriteLine("PASS: "+checks+" movement, transport, scoreboard, healing and status checks (Unity/network substitutes)");}
}
