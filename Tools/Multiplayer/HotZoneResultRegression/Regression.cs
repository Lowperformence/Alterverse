using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Match;

namespace UnityEngine {
 public enum PrimitiveType{Capsule}public enum FindObjectsInactive{Exclude}
 public class Object {
  public static T[] FindObjectsByType<T>(FindObjectsInactive mode)where T:class=>GameObject.All.Where(g=>g.active).Select(g=>g.GetComponent<T>()).Where(x=>x!=null).ToArray();
  public static void Destroy(Object o){if(o is GameObject g){g.active=false;var h=g.GetComponent<Health>();if(h!=null)Health.Active.Remove(h);}}
 }
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();}
 public class MonoBehaviour:Component {}
 public class GameObject:Object {
  public static List<GameObject> All=new();public string name;public bool active=true;public Transform transform=new();Dictionary<Type,Component> components=new();
  public GameObject(string name="actor"){this.name=name;All.Add(this);}
  public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};components[typeof(T)]=c;if(c is Health h)Health.Active.Add(h);if(c is HotZoneTank tank)HotZoneTank.Register(tank);return c;}
  public T GetComponent<T>()where T:class=>components.Values.OfType<T>().FirstOrDefault();
  public static GameObject CreatePrimitive(PrimitiveType type){var g=new GameObject();g.AddComponent<Renderer>();return g;}
  public static GameObject Find(string name)=>All.FirstOrDefault(g=>g.active&&g.name==name);
 }
 public class Transform {public Vector3 position,localScale;public void SetParent(Transform parent,bool preserve){}}
 public class Renderer:Component{public bool enabled=true;}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public float sqrMagnitude=>x*x+y*y+z*z;public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);}
 public static class Mathf {public static int Clamp(int x,int min,int max)=>Math.Clamp(x,min,max);public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);}
 public static class Time {public static int frameCount;}
}
namespace Unity.Collections {public readonly struct FixedString128Bytes {readonly string value;public FixedString128Bytes(string value){this.value=value;}public override string ToString()=>value;}}
namespace Unity.Netcode {
 public struct FastBufferReader {readonly object[] data;int index;public FastBufferReader(object[] data){this.data=data;index=0;}public void ReadValueSafe<T>(out T value)=>value=(T)data[index++];}
 public class NetworkManager {public static NetworkManager Singleton=new();public const ulong ServerClientId=0;public bool IsClient=true;}
}
namespace Alterverse.Characters {public class HeroIdentity:Component{}}
namespace Alterverse.Combat {
 public class Health:Component {public static List<Health> Active=new();public float Current=476,Shield;public bool IsDead,isActiveAndEnabled=true;public void Configure(float hp,bool b){Current=hp;}public void ApplyNetworkSnapshot(float hp,float shield,bool dead){Current=hp;Shield=shield;IsDead=dead;}}
 public class TeamMember:Component {public int Team;public void Initialize(int team){Team=team;}}
 public class DimensionPlanet:Component {public void ApplyNetworkAvailability(bool alive){}}
 public sealed partial class HotZoneTank:MonoBehaviour {
  const float MaxHp=476;static int coopSpawnSerial;static readonly List<HotZoneTank> active=new();Health health;
  public Health TankHealth=>health;public static IReadOnlyList<HotZoneTank> Active=>active;
  public static void Register(HotZoneTank tank)=>active.Add(tank);
  public static void Reset()=>active.Clear();
  // Rendering/AI initialization is a fixture; spawn decisions and identity are production code.
  void Initialize(int team,Vector3 p,bool s,bool a,bool spider,bool bear){health=GetComponent<Health>();}
 }
}
namespace Alterverse.Match {
 public static class ArenaVariantRuntime {public static bool IsMars,IsGreenPlanet,IsNoxara,IsKryon,IsExoplanet,IsGJ504b;public static int Current;}
 public static class ExoplanetArenaProfile {public static string MonsterName(int i)=>"variant";}
 public class MatchTelemetry {public class HeroStats {public int Kills,Deaths,Assists,Objectives,HotZoneCaptures,SoldierKills;public float DamageDealt,DamageTaken,Healing,FrontContribution;}
 public static MatchTelemetry Instance=new();public HeroStats Stats=new();public HeroStats GetStats(Health health)=>Stats;}
 public sealed partial class MatchScoreboard {
  public static MatchScoreboard Instance=new();class Entry{public int Kills,Deaths,Assists;}Dictionary<Health,Entry> entries=new();void RefreshRoster(){}
  public void Seed(Health health,int k,int d,int a){entries[health]=new(){Kills=k,Deaths=d,Assists=a};}
 }
}
namespace Alterverse.Multiplayer {
 public class CoopMatchPlan {public string MatchId="round";}
 public static class CoopAuthority {public static bool Active=true,LocalHasServerAuthority;public static bool CanSimulateHostGameplay=>LocalHasServerAuthority;public static CoopMatchPlan ActivePlan=new();}
 public class CoopWorldEntityKey:Component {public string Key;public bool Hidden;public void Configure(string key=null){if(key!=null)Key=key;if(Key==null)Key=gameObject.name;}public void ApplyTarget(Vector3 position,float yaw,bool immediate){transform.position=position;}public void ApplyAlivePresentation(bool alive){Hidden=!alive;}}
 public class CoopHeroSlot:Component {public int Slot;}
 public static class MatchSession {public const int MaxPlayers=10;}
 public static class MatchNetworkProtocol {public static bool ReadRound(ref FastBufferReader reader){reader.ReadValueSafe(out string round);return round==CoopAuthority.ActivePlan.MatchId;}public static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);}
 public sealed partial class CoopWorldStateSync:MonoBehaviour {
  readonly Dictionary<string,CoopWorldEntityKey> entities=new();readonly HashSet<string> removedKeys=new();CoopMatchPlan replicaPlan;int lastRefreshFrame=-1,lastHealthCount=-1;
  public void Receive(object[] data,ulong sender=0)=>OnState(sender,new FastBufferReader(data));
  public CoopWorldEntityKey Find(string key)=>entities.TryGetValue(key,out var entity)?entity:null;
 }
 public sealed partial class CoopMatchResultSync:MonoBehaviour {
  readonly Dictionary<int,SlotResult> results=new();int winningTeam;string matchId;bool resultReady;
  public void Capture(int winner)=>CaptureHostResult(winner);public SlotResult Read(int slot)=>results[slot];
 }
}
class Regression {
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Reset(){GameObject.All.Clear();Health.Active.Clear();HotZoneTank.Reset();Time.frameCount++;CoopAuthority.ActivePlan=new();CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;}
 static object[] Pose(string key,float hp=476,bool dead=false,float x=12)=>new object[]{"round",1,new FixedString128Bytes(key),x,1f,3f,45f,hp,0f,dead,0};
 static void Main(){
  foreach(int team in new[]{0,1})foreach(int occurrence in new[]{0,1,2,20,int.MaxValue}){
   string key=HotZoneRewardKey.Create(occurrence,team);Check(HotZoneRewardKey.TryParse(key,out int o,out int t)&&o==occurrence&&t==team,"stable event/team identity");
  }
  foreach(string bad in new[]{null,"","HOTZONE:0","HOTZONE:0:T2","HOTZONE:-1:T0","HOTZONE:1:T00","HOTZONE:2147483648:T0","HOTZONE: 1:T0","other"})Check(!HotZoneRewardKey.TryParse(bad,out _,out _),"reject malformed identity");
  Reset();Check(!HotZoneTank.Spawn(0,0)&&HotZoneTank.Active.Count==0,"client resolve cannot invent a reward");
  var sync=new GameObject().AddComponent<CoopWorldStateSync>();string first=HotZoneRewardKey.Create(0,0);
  sync.Receive(Pose(first));var replica=sync.Find(first);Check(replica!=null&&HotZoneTank.Active.Count==1,"missing resolve event recovered by snapshot");
  Check(replica.GetComponent<TeamMember>().Team==0&&replica.transform.position.x==12,"snapshot restores team and live pose");
  sync.Receive(Pose(first,120,false,14));Check(HotZoneTank.Active.Count==1&&replica.GetComponent<Health>().Current==120&&replica.transform.position.x==14,"repeated snapshot moves/damages same replica");
  sync.Receive(Pose(first,0,true));Check(replica.GetComponent<Health>().IsDead&&replica.Hidden,"authoritative death hides replica");
  string second=HotZoneRewardKey.Create(1,1);sync.Receive(Pose(second));sync.Receive(Pose(HotZoneRewardKey.Create(2,0)));
  Check(sync.Find(second)?.GetComponent<TeamMember>().Team==1&&HotZoneTank.Active.Count==3,"client cache cap cannot suppress real host reward");
  sync.Receive(new object[]{"round",0,1,new FixedString128Bytes(second)});Check(sync.Find(second)==null,"host removal removes replica");
  sync.Receive(Pose(second));Check(sync.Find(second)==null,"late movement packet cannot resurrect tombstoned reward");
  CoopAuthority.ActivePlan=new(){MatchId="round2"};var newRound=Pose(second);newRound[0]="round2";sync.Receive(newRound);Check(sync.Find(second)!=null,"new round clears tombstone history");
  Reset();sync=new GameObject().AddComponent<CoopWorldStateSync>();sync.Receive(Pose(first),55);Check(HotZoneTank.Active.Count==0,"reject nonserver spawn");sync.Receive(Pose(first,476,false,float.NaN));Check(HotZoneTank.Active.Count==0,"reject malformed spawn position");
  Reset();CoopAuthority.LocalHasServerAuthority=true;Check(HotZoneTank.Spawn(0,0)&&HotZoneTank.Spawn(1,1)&&!HotZoneTank.Spawn(0,2),"host retains two living rewards cap");Check(HotZoneTank.EnsureNetworkReplica(first)==null,"host cannot consume replica spawn path");
  Reset();CoopAuthority.LocalHasServerAuthority=true;var hero=new GameObject();hero.AddComponent<CoopHeroSlot>().Slot=3;var health=hero.AddComponent<Health>();MatchScoreboard.Instance=new();MatchScoreboard.Instance.Seed(health,2,1,4);MatchTelemetry.Instance.Stats=new(){Kills=19,Deaths=1,Assists=4,SoldierKills=16,HotZoneCaptures=2};
  var result=new GameObject().AddComponent<CoopMatchResultSync>();result.Capture(1);var score=result.Read(3);
  Check(score.Kills==2&&score.Deaths==1&&score.Assists==4,"final KDA exactly matches hero-only live scoreboard");
  Check(score.QuestKills==19&&score.SoldierKills==16&&score.HotZoneCaptures==2,"quest totals remain separate and intact");
  Console.WriteLine($"PASS {checks} reward lifecycle/result checks; production spawn, snapshot receive and result capture. Native art/physics/NGO are fixtures.");
 }
}
