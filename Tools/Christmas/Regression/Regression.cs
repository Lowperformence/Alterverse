using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Alterverse.Match;
using Alterverse.Profile;
using Alterverse.Multiplayer;
using Alterverse.Combat;
using Alterverse.Characters;
using UnityEngine;
using Unity.Netcode;

static class Regression
{
 static int checks;
 static void Check(bool v,string message){if(!v)throw new Exception(message);checks++;}
 static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
 static int Count(ChristmasSnowmanEvent e)=>((IDictionary)Field(e,"pickups")).Count;
 static void Tick(ChristmasSnowmanEvent e,float dt){Time.deltaTime=dt;Time.time+=dt;Time.realtimeSinceStartup+=dt;Call(e,"Update");}
 static void Reset(){UnityEngine.Object.All.Clear();Health.Active.Clear();CoopAuthority.EndMatch();NetworkManager.Singleton=null;AccountProfilePrefs.Reset();Time.time=Time.realtimeSinceStartup=0;Time.timeScale=1;Physics.Blocked=false;Alterverse.Visuals.LunarSurfaceField.Steep=false;}
 static ChristmasSnowmanEvent Event(){new GameObject("front").AddComponent<DimensionsFront>();var e=new GameObject("event").AddComponent<ChristmasSnowmanEvent>();Call(e,"Awake");return e;}
 static CoopMatchPlan Network(bool server,bool client,bool local){var plan=new OnlineMatchPlan();plan.Session.Slots[0]=new(){PlayerId="host",IsLocal=server&&client&&local,IsHost=true};plan.Session.Slots[1]=new(){PlayerId="guest",IsLocal=!server&&local};plan.Session.Slots[2]=new(){PlayerId="bot",IsBot=true};NetworkManager.Singleton=new(){IsServer=server,IsClient=client,IsConnectedClient=client};CoopAuthority.Prepare(plan);CoopAuthority.BeginPreparedMatch(server&&client);return plan;}
 static Health Actor(Vector3 p,int slot=-1,bool bot=false,bool connected=true){var g=new GameObject("actor");g.transform.position=p;g.AddComponent<HeroIdentity>();var h=g.AddComponent<Health>();Health.Active.Add(h);if(slot>=0){var data=CoopAuthority.ActivePlan.Session.Slots[slot];data.Connected=connected;g.AddComponent<CoopHeroSlot>().Configure(slot,data,data.IsLocal);}else g.AddComponent<HeroMotor>();if(bot)g.AddComponent<Alterverse.AI.MobaBotBrain>();return h;}
 static Vector3 Ground(ChristmasSnowmanEvent e){var d=(IDictionary)Field(e,"pickups");object p=d.Values.Cast<object>().First();return (Vector3)p.GetType().GetField("Ground").GetValue(p);}
 static byte[] Payload(params object[] values){using var w=new FastBufferWriter(64,Unity.Collections.Allocator.Temp);foreach(object v in values){if(v is uint u)w.WriteValueSafe(u);else if(v is int i)w.WriteValueSafe(i);else w.WriteValueSafe((float)v);}return w.Bytes();}
 static void Send(string name,ulong sender,params object[] values)=>NetworkManager.Singleton.CustomMessagingManager.Deliver(name,sender,Payload(values));

 static void Rules()
 {
  var r=new ChristmasSnowmanRules();Check(!r.Advance(119.5f),"no early spawn");Check(r.Advance(.5f),"first spawn at 120s");int id=r.Spawned();
  Check(!r.Advance(0)&&!r.Advance(float.NaN)&&!r.Advance(float.PositiveInfinity),"paused/invalid time does not advance");
  Check(!r.TryCollect(id,2,false,out _),"ineligible collector leaves snowman available");
  Check(r.TryCollect(id,0,true,out int n)&&n==1,"human gets one pickup");Check(!r.TryCollect(id,1,true,out _),"two players cannot collect same snowman");
  Check(r.CollectedBy(1)==0,"losing collector has no reward");Check(!r.TryCollect(-1,0,true,out _),"unknown pickup rejected");
  for(int i=0;i<10;i++){Check(!r.Advance(119),"subsequent timer holds");Check(r.Advance(1),"subsequent two-minute spawn");r.Spawned();}
  Check(!ChristmasSnowmanRules.Eligible(false,true,true,0,0),"bot rejected");Check(!ChristmasSnowmanRules.Eligible(true,false,true,0,0),"dead player rejected");
  Check(!ChristmasSnowmanRules.Eligible(true,true,false,0,0),"disconnected player rejected");Check(!ChristmasSnowmanRules.Eligible(true,true,true,100,0),"distant player rejected");
  Check(!ChristmasSnowmanRules.Eligible(true,true,true,0,3),"vertical separation rejected");Check(!ChristmasSnowmanRules.Eligible(true,true,true,float.NaN,0),"nonfinite position rejected");
  Check(ChristmasSnowmanRules.Eligible(true,true,true,1,1),"living nearby human eligible");
 }
 static void Receipts()
 {
  AccountProfilePrefs.Reset();Check(ChristmasSnowmanRewards.Apply("round",1,"account-a")==1&&PlayerWallet.EarnedBalance==1,"exactly one shard");
  Check(ChristmasSnowmanRewards.Apply("round",1,"account-a")==0&&PlayerWallet.EarnedBalance==1,"duplicate reward/reconnect idempotent");
  Check(ChristmasSnowmanRewards.Apply("round",3,"account-a")==2&&PlayerWallet.EarnedBalance==3,"cumulative resync recovers missed rewards");
  Check(ChristmasSnowmanRewards.Apply("round",2,"account-a")==0,"out-of-order reward never replays");
  AccountProfilePrefs.Account="account-b";Check(ChristmasSnowmanRewards.Apply("round",3,"account-a")==0&&PlayerWallet.EarnedBalance==0,"account switch cannot credit another account");
  Check(ChristmasSnowmanRewards.Apply("round",1,"account-b")==1,"receipt is scoped to account");AccountProfilePrefs.Account="account-a";
  Check(ChristmasSnowmanRewards.Apply("next-round",1,"account-a")==1&&PlayerWallet.EarnedBalance==4,"next match independent");
  AccountProfilePrefs.FailPrefix="DF_HalloweenPumpkins_";
  try{ChristmasSnowmanRewards.Apply("crash-round",1,"account-a");throw new Exception("expected interrupted write");}catch(IOException){}
  Check(PlayerWallet.EarnedBalance==5,"interruption occurred after balance write");ProfileTransaction.Recover();
  Check(ChristmasSnowmanRewards.Apply("crash-round",1,"account-a")==0&&PlayerWallet.EarnedBalance==5,"journal recovers balance and receipt atomically");
 }
 static void Offline()
 {
  Reset();var e=Event();Tick(e,119);Check(Count(e)==0,"offline timer waits");Time.timeScale=0;Tick(e,10);Check(Count(e)==0,"pause never spawns");Time.timeScale=1;
  Tick(e,1);Check(Count(e)==1,"offline actual director spawns");var ground=Ground(e);
  Check(GameplayCollision.ContainsRoundedArena(ground,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight,2),"spawn inside real rounded map bounds");
  Actor(ground,bot:true);Tick(e,.2f);Check(Count(e)==1&&PlayerWallet.EarnedBalance==0,"actual bot cannot consume pickup");
  var h=Actor(ground);h.IsDead=true;Tick(e,.2f);Check(Count(e)==1,"dead human cannot collect");h.IsDead=false;
  Tick(e,.2f);Check(Count(e)==0&&PlayerWallet.EarnedBalance==1,"actual local player collects and persists one shard");
  Tick(e,120);Check(Count(e)==1,"next interval creates one more snowman");
  UnityEngine.Object.FindAnyObjectByType<DimensionsFront>().HasEnded=true;Tick(e,120);Check(Count(e)==1,"match result stops spawning");
  Reset();e=Event();Physics.Blocked=true;Tick(e,120);Check(Count(e)==0,"no spawn in occupied geometry");Physics.Blocked=false;Tick(e,1);Check(Count(e)==1,"valid position retried without losing interval");
  Reset();e=Event();Alterverse.Visuals.LunarSurfaceField.Steep=true;Tick(e,120);Check(Count(e)==0,"steep relief rejected");
 }
 static void Dedicated()
 {
  Reset();Network(true,false,false);var e=Event();Tick(e,120);Check(Count(e)==1,"dedicated server spawns without local host player");Vector3 p=Ground(e);
  Actor(p,2);Tick(e,.2f);Check(Count(e)==1,"planned bot rejected even without brain component");
  var guest=Actor(p,1,connected:false);Tick(e,.2f);Check(Count(e)==1,"disconnected roster human rejected");CoopAuthority.ActivePlan.Session.Slots[1].Connected=true;
  Tick(e,.2f);Check(Count(e)==0&&PlayerWallet.EarnedBalance==0,"server grants ownership without touching server wallet");
  Check(((ChristmasSnowmanRules)Field(e,"rules")).CollectedBy(1)==1,"dedicated reward assigned to correct human slot");
  Check(NetworkManager.Singleton.CustomMessagingManager.Sent.Any(s=>s.Name=="DF_HALLOWEEN_CLAIM_V1"),"claim replicated reliably");
  int before=NetworkManager.Singleton.CustomMessagingManager.Sent.Count;Send("DF_HALLOWEEN_SYNC_REQUEST_V1",99,123u);Check(NetworkManager.Singleton.CustomMessagingManager.Sent.Count==before,"unbound sender cannot request event state");
  Send("DF_HALLOWEEN_SYNC_REQUEST_V1",7,123u);Check(NetworkManager.Singleton.CustomMessagingManager.Sent.Any(s=>s.Name=="DF_HALLOWEEN_SYNC_BEGIN_V1"),"bound human receives state and cumulative receipt");
 }
 static void Client()
 {
  Reset();Network(false,true,true);var e=Event();Tick(e,120);Check(Count(e)==0,"client cannot invent a local spawn");
  Send("DF_HALLOWEEN_SPAWN_V1",99,123u,1,0f,.08f,0f);Check(Count(e)==0,"forged spawn sender rejected");
  Send("DF_HALLOWEEN_SPAWN_V1",0,456u,1,0f,.08f,0f);Check(Count(e)==0,"old round spawn rejected");
  Send("DF_HALLOWEEN_SPAWN_V1",0,123u,1,0f,.08f,0f);Check(Count(e)==1,"valid server spawn visible");
  Send("DF_HALLOWEEN_CLAIM_V1",99,123u,1,1,1);Check(PlayerWallet.EarnedBalance==0&&Count(e)==1,"forged reward rejected");
  Send("DF_HALLOWEEN_CLAIM_V1",0,123u,1,0,1);Check(PlayerWallet.EarnedBalance==0,"another player's pickup never credits local account");
  Send("DF_HALLOWEEN_CLAIM_V1",0,123u,2,1,1);Check(PlayerWallet.EarnedBalance==1,"own server reward credits one shard");
  Send("DF_HALLOWEEN_CLAIM_V1",0,123u,2,1,1);Check(PlayerWallet.EarnedBalance==1,"duplicate claim message harmless");
  Send("DF_HALLOWEEN_SYNC_BEGIN_V1",0,123u,1u,1,3);Send("DF_HALLOWEEN_SYNC_ITEM_V1",0,123u,1u,4,2f,.08f,2f);Send("DF_HALLOWEEN_SYNC_END_V1",0,123u,1u);
  Check(Count(e)==1&&PlayerWallet.EarnedBalance==3,"snapshot restores active snowmen and missed credits");
  Send("DF_HALLOWEEN_SYNC_BEGIN_V1",0,123u,2u,1,3);Send("DF_HALLOWEEN_SYNC_END_V1",0,123u,2u);
  Check(Count(e)==0&&PlayerWallet.EarnedBalance==3,"snapshot removes stale snowmen without repeated payment");
  Send("DF_HALLOWEEN_CLAIM_V1",0,456u,4,1,100);Send("DF_HALLOWEEN_CLAIM_V1",0,123u,4);Check(PlayerWallet.EarnedBalance==3,"old-round and truncated claims rejected");
  Call(e,"OnDisable");Check(NetworkManager.Singleton.CustomMessagingManager.Handlers.Count==0,"handlers removed with match world");
 }
 static void Host()
 {
  Reset();Network(true,true,true);var e=Event();Tick(e,120);Actor(Ground(e),0);Tick(e,.2f);
  Check(Count(e)==0&&PlayerWallet.EarnedBalance==1,"relay host's real local player gets one shard");
 }
 static void Presentation()
 {
  Reset();
  var first=Alterverse.Visuals.ChristmasSnowmanVisual.Create(new GameObject("parent").transform,new Vector3(1,2,3),1);
  Check(first.name=="DF_ChristmasSnowman_1"&&first.transform.position.y==2,"snowman appears at authoritative ground");
  Check(!UnityEngine.Object.All.OfType<Collider>().Any(),"snowman decorations never block movement or pickup scans");
  string[] parts={"SnowBase","SnowBody","SnowHead","CarrotNose","LeftCoalEye","RightCoalEye","RedScarf","ScarfTail","SantaHat","SantaHatBrim","SantaHatPom"};
  Check(parts.All(n=>UnityEngine.Object.All.OfType<GameObject>().Any(g=>g.name==n)),"complete snowman silhouette and Christmas accessories");
  var baseMesh=UnityEngine.Object.All.OfType<GameObject>().First(g=>g.name=="SnowBase").GetComponent<MeshFilter>().sharedMesh;
  var noseMesh=UnityEngine.Object.All.OfType<GameObject>().First(g=>g.name=="CarrotNose").GetComponent<MeshFilter>().sharedMesh;
  Check(baseMesh.vertices.Length<400&&noseMesh.vertices.Length<200,"shared snowman geometry stays lightweight");
  foreach(var mesh in new[]{baseMesh,noseMesh})
  {
   Check(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"mesh has finite vertices");
   Check(mesh.triangles.All(i=>i>=0&&i<mesh.vertices.Length),"mesh indices valid");
   bool outward=true;
   for(int i=0;i<mesh.triangles.Length;i+=3)
   {
    var a=mesh.vertices[mesh.triangles[i]];var b=mesh.vertices[mesh.triangles[i+1]];var c=mesh.vertices[mesh.triangles[i+2]];
    var u=b-a;var v=c-a;var n=new Vector3(u.y*v.z-u.z*v.y,u.z*v.x-u.x*v.z,u.x*v.y-u.y*v.x);
    var center=(a+b+c)*(1f/3);if(mesh==noseMesh)center.y-=.25f;
    if(n.x*center.x+n.y*center.y+n.z*center.z < -.00001f)outward=false;
   }
   Check(outward,"mesh winding visible from outside");
  }
  int meshCount=UnityEngine.Object.All.OfType<MeshFilter>().Select(m=>m.sharedMesh).Distinct().Count();
  Alterverse.Visuals.ChristmasSnowmanVisual.Create(first.transform,new Vector3(0,0,0),2);
  Check(UnityEngine.Object.All.OfType<MeshFilter>().Select(m=>m.sharedMesh).Distinct().Count()==meshCount,"multiple pickups share meshes");
  var menu=new Alterverse.UI.MainMenuController();Call(menu,"DrawHomeNews");
  Check(menu.theme.Labels.Contains("CHRISTMAS EVENT")&&menu.theme.Labels.Contains("Snowmen on Map"),"menu promotes Christmas snowmen");
  Check(menu.theme.Labels.Contains("Every 2 min")&&menu.theme.Labels.Contains("+1 SHARD"),"menu reflects production cadence and reward");
  var icon=GUI.LastTexture;Check(icon.name=="DF_ChristmasNewsSnowman"&&icon.width==128&&icon.height==128,"snowman news icon generated");
  Check(icon.Pixels[31*128+64].r>180&&icon.Pixels[31*128+64].b>220,"icon has a white snowball");
  Check(icon.Pixels[114*128+65].r>180&&icon.Pixels[114*128+65].g<30,"icon has a red Christmas hat");
  Check(icon.Pixels[0].a==0,"news icon background transparent");
  Call(menu,"DrawHomeNews");Check(ReferenceEquals(icon,GUI.LastTexture),"news icon reused across frames");
  string preview=Environment.GetEnvironmentVariable("DF_CHRISTMAS_ICON_PREVIEW");
  if(!string.IsNullOrEmpty(preview))
  {
   using var stream=File.Create(preview);byte[] header=System.Text.Encoding.ASCII.GetBytes("P6\n128 128\n255\n");stream.Write(header);
   for(int y=127;y>=0;y--)for(int x=0;x<128;x++)
   {var p=icon.Pixels[y*128+x];float a=p.a/255f;stream.WriteByte((byte)(p.r*a+16*(1-a)));stream.WriteByte((byte)(p.g*a+24*(1-a)));stream.WriteByte((byte)(p.b*a+40*(1-a)));}
  }
 }
 static void Main(){Rules();Receipts();Offline();Dedicated();Client();Host();Presentation();Console.WriteLine("PASS: "+checks+" Christmas cadence, human-only pickup, wallet journal, sender, round, snapshot, host/dedicated/client, snowman mesh and news UI checks (engine/transport fixtures).");}
}

