using Unity.Netcode;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
 public class Object {public static T FindAnyObjectByType<T>()where T:class=>null;}
 public enum RuntimeInitializeLoadType {SubsystemRegistration,AfterSceneLoad}
 public sealed class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){}}
 public static class Application {public static bool isFocused=true,isMobilePlatform;public static int targetFrameRate=60;}
 public static class Debug {public static int Logs;public static void Log(object value){Logs++;}}
 public class Component:Object
 {
  public GameObject gameObject;public Transform transform=>gameObject.transform;
  public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
 }
 public class MonoBehaviour:Component {}
 public class GameObject:Object
 {
  public string name;public Transform transform=new();readonly Dictionary<Type,object> components=new();
  public GameObject(string name="actor"){this.name=name;}
  public void SetActive(bool value){}
  public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components[typeof(T)]=c;return c;}
  public T GetComponent<T>() where T:class=>components.TryGetValue(typeof(T),out var c)?c as T:null;
 }
 public class Transform
 {
  public Vector3 position,eulerAngles,localScale;public Quaternion rotation;public GameObject gameObject;
  public void SetPositionAndRotation(Vector3 p,Quaternion r){position=p;rotation=r;}
 }
 public struct Vector2
 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
  public float magnitude=>(float)Math.Sqrt(x*x+y*y);
  public static Vector2 operator *(Vector2 a,float b)=>new(a.x*b,a.y*b);
 }
 public struct Vector3
 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new();public static Vector3 up=>new(0,1,0);
  public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public Vector3 normalized=>magnitude>0?this*(1/magnitude):zero;
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
  public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
 }
 public struct Quaternion {public float yaw;public static Quaternion Euler(float x,float y,float z)=>new(){yaw=y};}
 public static class Time {public static float unscaledTime,time,timeScale=1,unscaledDeltaTime;}
 public static class Mathf
 {
  public static float Abs(float a)=>Math.Abs(a);
  public static int Clamp(int v,int a,int b)=>Math.Clamp(v,a,b);
  public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);
  public static float Min(float a,float b)=>Math.Min(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
  public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);public static float Repeat(float v,float n)=>v-(float)Math.Floor(v/n)*n;
 }
}
namespace Unity.Collections
{
 public enum Allocator {Temp}
 public struct FixedString128Bytes {readonly string value;public FixedString128Bytes(string value){this.value=value;}public override string ToString()=>value;}
}
namespace Unity.Netcode
{
 public enum NetworkDelivery {UnreliableSequenced,ReliableSequenced}
 public class FastBufferWriter:IDisposable
 {
  public readonly List<object> Values=new();public FastBufferWriter(int size,Unity.Collections.Allocator a){}
  public void WriteValueSafe<T>(T value)=>Values.Add(value);public void Dispose(){}
 }
 public struct FastBufferReader
 {
  readonly object[] values;int index;public FastBufferReader(object[] values){this.values=values;index=0;}
  public bool TryBeginRead(int size)=>index<values.Length;
  public void ReadValueSafe<T>(out T value){value=(T)values[index++];}
 }
 public class CustomMessagingManager
 {
  public readonly List<(string Name,ulong Client,object[] Values,NetworkDelivery Delivery)> Sent=new();
  public void SendNamedMessage(string name,ulong client,FastBufferWriter writer,NetworkDelivery delivery)=>Sent.Add((name,client,writer.Values.ToArray(),delivery));
 }
 public class NetworkManager
 {
  public static NetworkManager Singleton;public const ulong ServerClientId=0;
  public bool IsListening=true;public CustomMessagingManager CustomMessagingManager=new();public bool IsServer;public readonly List<ulong> ConnectedClientsIds=new(){0,7};
 }
}
namespace Alterverse.Match
{
 public sealed class PlanetBuffReceiver:Component {public float MoveSpeedMultiplier=1;}
 public sealed class HeroRespawn:Component {public bool IsRespawning;public float RemainingSeconds;}
 // Only render/reward code is excluded. The scoreboard accessors under test are extracted
 // verbatim from MatchScoreboard.cs into the other partial declaration by run.py.
 public sealed partial class MatchScoreboard
 {
  public static MatchScoreboard Instance;private sealed class Entry {public int Kills,Deaths,Assists;}
  readonly Dictionary<Alterverse.Combat.Health,Entry> entries=new();
  void RefreshRoster(){foreach(var h in Registered)if(!entries.ContainsKey(h))entries[h]=new();}
  public readonly List<Alterverse.Combat.Health> Registered=new();
  public void Seed(Alterverse.Combat.Health h,int k,int d,int a){Registered.Add(h);entries[h]=new(){Kills=k,Deaths=d,Assists=a};}
  public (int,int,int) Read(Alterverse.Combat.Health h)=>entries.TryGetValue(h,out var e)?(e.Kills,e.Deaths,e.Assists):(-1,-1,-1);
 }
}
namespace Alterverse.Combat
{
 public sealed class LaneMinion:Component {public static readonly List<LaneMinion> Active=new();}
 public sealed class HotZoneTank:Component {public static readonly List<HotZoneTank> Active=new();public Health TankHealth;}
 public sealed class Health:Component
 {
  public float Current=100,Shield,Max=100;public bool IsDead;public float Normalized=>Current/Max;public void Heal(float value){Current=Math.Min(Max,Current+value);}
  public void ApplyNetworkSnapshot(float hp,float shield,bool dead){Current=hp;Shield=shield;IsDead=dead;}
 }
 public sealed class BasicAttack:Component {public float RemainingCooldown;public void ApplyNetworkCooldown(float value,uint acknowledged=0){RemainingCooldown=value;}}
}
namespace Alterverse.Characters
{
 public enum HeroId {Other,Timmy}
 public sealed class HeroIdentity:Component {public HeroId Hero;}
 public sealed class HeroCombatProfile:Component {public float MoveSpeed=6.5f;}
 public sealed class HeroMotor:Component {public Vector3 SmoothedVelocity;}
 public sealed class HeroLocomotion:Component
 {
  public bool IsDashing,IsForcedMoving,IsVortexPulled;
  // The epoch implementation is extracted verbatim from HeroLocomotion.cs by run.py.
  public float SpeedMultiplier=1,VortexRadius;public Vector3 VortexCenter;
  public void SetRemoteRunVelocity(Vector3 p){}public void SetVortexPull(Vector3 p,float r,float seconds){VortexCenter=p;VortexRadius=r;IsVortexPulled=seconds>0;}
  public void Move(Vector3 delta,float x,float z){transform.position=Alterverse.Combat.GameplayCollision.ClampToRoundedArena(transform.position+delta,x,z);}
 }
 public sealed class PlayerHeroAbilities:Component {public float RemainingFor(int i)=>0;public void ApplyNetworkCooldown(int i,float value,uint acknowledged=0){}}
}
namespace Alterverse.Visuals
{
 public sealed class HeroVisualMotion:Component {public Vector3 PresentedPosition;public float SampledAt;public void ReceivePresentationSnapshot(Vector3 p,Quaternion q,bool reset,bool forced,float sampledAt=float.NaN){SampledAt=sampledAt;}}
}
namespace Alterverse.Multiplayer
{
 public static class DedicatedServerNetworkRuntime {public static bool Requested;}
 public sealed class CoopHeroSlot:Component {public int Slot;public string PlayerId="player";public bool IsLocalHuman;}
 public sealed class CoopRespawnReplica:Component {public void Apply(bool active,float remaining){}}
 public sealed class CoopHeroReplicaPresentation:Component {public void Apply(bool dead){}}
 public static class MatchSession {public const int MaxPlayers=8;}
 public sealed class MatchPlayerSlot {public bool IsBot,IsHost;public string PlayerId="player";}
 public sealed class Session {public MatchPlayerSlot[] Slots=new MatchPlayerSlot[8];}
 public sealed class CoopMatchPlan {public Session Session=new();}
 public static class CoopAuthority {public static bool Active=true,LocalHasServerAuthority,LocalIsHost;public static CoopMatchPlan ActivePlan=new();public static bool CanSimulateHostGameplay=>LocalHasServerAuthority;}
 public static class CoopPlanHandshake {public static uint Round=123;public static int Calls;public static uint Fingerprint(CoopMatchPlan plan){Calls++;return Round;}}
 public static class CoopGameplayCommands {public static uint ProcessedCommand(int slot)=>0;}
 public static class MatchNetworkProtocol
 {
  public static bool AllowOwner=true;
  public static bool Authorized(ulong id,string player)=>AllowOwner;
  public static bool ReadRound(ref Unity.Netcode.FastBufferReader reader){reader.ReadValueSafe(out uint round);return round==CoopPlanHandshake.Round;}
  public static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
 }
 // Non-transport VFX handlers are excluded; actual transport methods compile unchanged.
 public sealed partial class CoopHeroStateSync:MonoBehaviour
 {
  private Unity.Netcode.CustomMessagingManager messaging=new();
  private readonly CoopHeroSlot[] slots=new CoopHeroSlot[8];
  private readonly Dictionary<ulong,string> senderOwners=new();
  private readonly Dictionary<ulong,float> senderLastPoseAt=new();
  private int localSlot=-1;private string localPlayerId="player";
  private float nextOwnerSend,nextHostSnapshot,nextRefresh;
  private void RefreshSlots(bool immediate){}
  private void Attach(Unity.Netcode.CustomMessagingManager value){}
  // Cosmetic traffic has its own production regression fixture.
  private void TickHeadwear(Unity.Netcode.NetworkManager value){}
  public void Tick()=>Update();
  public Unity.Netcode.CustomMessagingManager Messages=>messaging;
  internal OwnerPoseHistory History=>ownerPoseHistory;
  public void Slot(CoopHeroSlot actor,bool local=false){slots[actor.Slot]=actor;if(local){localSlot=actor.Slot;localPlayerId=actor.PlayerId;}}
  public void Send(Unity.Netcode.NetworkManager m)=>SendOwnerPose(m);
  public void ReceiveOwner(ulong id,object[] values)=>OnOwnerPose(id,new Unity.Netcode.FastBufferReader(values));
  public void Broadcast(Unity.Netcode.NetworkManager m)=>BroadcastHostSnapshot(m);
  public void ReceiveSnapshot(ulong id,object[] values)=>OnHeroSnapshot(id,new Unity.Netcode.FastBufferReader(values));
 }
}

namespace Alterverse.Performance
{
 public enum MobileQuality {Battery,Balanced,Performance}
 public static class MobilePerformanceProfile {public static MobileQuality Current=MobileQuality.Balanced;}
}

namespace Alterverse.Combat
{
 public sealed partial class CentralHealingPickup:MonoBehaviour
 {
  public static event Action<bool,float> NetworkAvailabilityChanged;
  private float radius=1.5f,healPercent=.286f,respawnSeconds=22f/1.3f,readyAt;private bool available=true;
  private Transform plus;public readonly System.Collections.Generic.List<Alterverse.Characters.TeamMember> Members=new();
  private T[] FindObjectsByType<T>()=>Members.Cast<T>().ToArray();
  private void AnimateAvailable(){}public void Tick()=>Update();public bool Available=>available;
 }
}
namespace Alterverse.Characters {public sealed class TeamMember:Component {}}
namespace Alterverse.Multiplayer
{
 public sealed class CoopWorldEntityKey:Component {public string Key;}
 public sealed partial class CoopWorldStateSync
 {
  private const string StateMessage="DF_COOP_WORLD_STATE_V3";
  private readonly Dictionary<string,CoopWorldEntityKey> entities=new();
  private readonly HashSet<string> previousHostKeys=new(),currentHostKeys=new();
  private readonly List<string> tombstones=new();
  private readonly List<CoopWorldEntityKey> live=new(128);
  private float nextSend,nextMovingSend,nextRefresh;public int RefreshCalls;
  private readonly WorldSnapshotHistory snapshotHistory=new();
  private CoopMatchPlan snapshotPlan;
  private void Attach(CustomMessagingManager value){}
  public void Tick()=>Update();
  private readonly CustomMessagingManager messaging=new();
  public readonly List<CoopWorldEntityKey> Live=new();
  private void RefreshEntities(){RefreshCalls++;entities.Clear();foreach(var e in Live)entities.Add(e.Key,e);}
  public void Broadcast(NetworkManager manager)=>SendState(manager);public CustomMessagingManager Messages=>messaging;
 }
}


