using System;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace UnityEngine
{
 public class Object
 {
  public bool destroyed;
  public static void Destroy(Object obj){if(obj!=null)obj.destroyed=true;}
  public static bool operator ==(Object a,Object b){bool an=ReferenceEquals(a,null)||a.destroyed,bn=ReferenceEquals(b,null)||b.destroyed;return an?bn:!bn&&ReferenceEquals(a,b);}
  public static bool operator !=(Object a,Object b)=>!(a==b);
  public override bool Equals(object o)=>this==(o as Object);public override int GetHashCode()=>base.GetHashCode();
 }
 public class GameObject:Object
 {
  static readonly Dictionary<string,GameObject> Objects=new();readonly Dictionary<Type,object> parts=new();
  public Transform transform=new();public string name;
  public GameObject(string n=""){name=n;Objects[n]=this;}
  public void Put<T>(T value)=>parts[typeof(T)]=value;
  public T GetComponent<T>() where T:class=>parts.TryGetValue(typeof(T),out object v)?(T)v:null;
  public static GameObject Find(string n)=>Objects.TryGetValue(n,out var g)?g:null;
 }
 public class MonoBehaviour:Object
 {
  public GameObject gameObject=new();
  public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
  protected static void Destroy(GameObject obj){if(!ReferenceEquals(obj,null))obj.destroyed=true;}
 }
 public class Transform {public Vector3 position;}
 public struct Vector3
 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 up=>new(0,1,0);
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
 }
 public static class Mathf
 {
  public static int FloorToInt(float f)=>(int)Math.Floor(f);public static int CeilToInt(float f)=>(int)Math.Ceiling(f);
  public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);
  public static int Clamp(int x,int lo,int hi)=>Math.Min(hi,Math.Max(lo,x));
 }
 public static class Time {public static float time,timeScale=1,unscaledTime;}
 public enum QueryTriggerInteraction {Ignore}
 public static class Physics {public static bool Blocked;public static bool CheckSphere(Vector3 p,float r,int m,QueryTriggerInteraction q)=>Blocked;}
}
namespace Unity.Collections {public enum Allocator {Temp}}
namespace Unity.Netcode
{
 public enum NetworkDelivery {ReliableSequenced}
 public struct FastBufferWriter:IDisposable
 {
  public List<object> Data;public FastBufferWriter(int size,Allocator a){Data=new();}
  public void WriteValueSafe(int n)=>Data.Add(n);public void WriteValueSafe(uint n)=>Data.Add(n);public void WriteValueSafe(float n)=>Data.Add(n);public void Dispose(){}
 }
 public struct FastBufferReader
 {
  public List<object> Data;public int Index;public FastBufferReader(List<object> v){Data=v;Index=0;}
  public bool TryBeginRead(int n)=>Data!=null&&Data.Count>Index;
  public void ReadValueSafe(out int n)=>n=(int)Data[Index++];public void ReadValueSafe(out uint n)=>n=(uint)Data[Index++];public void ReadValueSafe(out float n)=>n=(float)Data[Index++];
 }
 public class CustomMessagingManager
 {
  public List<object> Last;
  public void RegisterNamedMessageHandler(string n,Action<ulong,FastBufferReader> handler){}
  public void UnregisterNamedMessageHandler(string n){}
  public void SendNamedMessage(string n,ulong target,FastBufferWriter w,NetworkDelivery d){Last=new List<object>(w.Data);}
 }
 public class NetworkManager
 {
  public static NetworkManager Singleton;public const ulong ServerClientId=0;
  public bool IsListening=true,IsServer,IsClient;public CustomMessagingManager CustomMessagingManager=new();public List<ulong> ConnectedClientsIds=new(){0,1};
 }
}
namespace Alterverse.Profile
{
 public enum BattleItemType {None,Heal,Shield,Speed,TeamHeal}
 public static class BattleItemInventory
 {
  public static BattleItemType Equipped;public static int Count=2,Consumes;
  public static bool TryConsumeEquipped(BattleItemType type){if(Equipped!=type||Count<=0)return false;Count--;Consumes++;Equipped=BattleItemType.None;return true;}
 }
}
namespace Alterverse.Characters {}
namespace Alterverse.Match {public class DimensionsFront {public float ElapsedSeconds;public bool HasEnded;}}
namespace Alterverse.UI {public static class MainMenuController {public static bool IsVisible;}}
namespace Alterverse.Visuals {public static class LunarSurfaceField {public static Vector3 WorldSurface(float x,float z)=>new(x,0,z);}}
namespace Alterverse.Combat
{
 public class TeamMember {public int Team;}
 public static class GameplayCollision {public const int SightMask=1;}
 public class BattleItemPickup:UnityEngine.Object
 {
  public static int Created;public bool IsAirborne=true,Authoritative;public float Age;public BattleItemType Type;public GameObject gameObject=new();
  public static BattleItemPickup Create(BattleItemType i,int t,Vector3 p,bool airdrop,bool authoritative=true){Created++;return new(){Type=i,Authoritative=authoritative};}
  public void SetReplicaAge(float age)=>Age=age;
  public static bool operator ==(BattleItemPickup a,BattleItemPickup b){bool an=ReferenceEquals(a,null)||a.gameObject.destroyed,bn=ReferenceEquals(b,null)||b.gameObject.destroyed;return an?bn:!bn&&ReferenceEquals(a,b);}
  public static bool operator !=(BattleItemPickup a,BattleItemPickup b)=>!(a==b);public override bool Equals(object o)=>this==(o as BattleItemPickup);public override int GetHashCode()=>base.GetHashCode();
 }
 public static class BattleItemRuntime
 {
  public static bool TryDropEquipped(BattleItemType item,int team,Vector3 p,out BattleItemPickup drop)
  {drop=BattleItemPickup.Create(item,team,p,true);return BattleItemInventory.TryConsumeEquipped(item);}
 }
}
namespace Alterverse.Multiplayer
{
 public class MatchPlayerSlot {public bool IsBot,IsLocal;public int Team;public BattleItemType BattleItem;}
 public class MatchSession {public const int TeamSize=4,MaxPlayers=8;public MatchPlayerSlot[] Slots=new MatchPlayerSlot[MaxPlayers];}
 public class CoopMatchPlan {public MatchSession Session=new();}
 public static class CoopAuthority {public static bool Active,LocalHasServerAuthority;public static CoopMatchPlan ActivePlan;}
 public static class CoopPlanHandshake {public static uint Fingerprint(CoopMatchPlan p)=>123;}
 public static class MatchNetworkProtocol
 {
  public static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
  public static bool ReadRound(ref FastBufferReader r){if(!r.TryBeginRead(4))return false;r.ReadValueSafe(out uint fp);return fp==123;}
 }
}
static class Regression
{
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
 static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static BattleItemAirdropSchedule Schedule(DimensionsFront front)
 {var schedule=new BattleItemAirdropSchedule();schedule.gameObject.Put(front);Call(schedule,"Awake");return schedule;}
 static void Offline()
 {
  CoopAuthority.Active=false;BattleItemInventory.Equipped=BattleItemType.Heal;BattleItemInventory.Count=2;BattleItemInventory.Consumes=0;BattleItemPickup.Created=0;
  new GameObject("AV_Player").Put(new TeamMember{Team=0});var front=new DimensionsFront();var schedule=Schedule(front);
  Time.time=1;front.ElapsedSeconds=119;Call(schedule,"Update");Check(BattleItemPickup.Created==0,"no early offline drop");
  Time.time=2;front.ElapsedSeconds=120;Call(schedule,"Update");Check(BattleItemPickup.Created==1&&BattleItemInventory.Consumes==1,"first drop consumes once");
  Time.time=3;front.ElapsedSeconds=240;Call(schedule,"Update");Check(BattleItemPickup.Created==1,"uncollected supply prevents duplicate");
  var first=(BattleItemPickup)Field(schedule,"drop");first.gameObject.destroyed=true;
  Time.time=4;front.ElapsedSeconds=359;Call(schedule,"Update");Check(BattleItemPickup.Created==1,"wait for next scheduled tick");
  Time.time=5;front.ElapsedSeconds=360;Call(schedule,"Update");Check(BattleItemPickup.Created==2&&BattleItemInventory.Consumes==1,"collected drop respawns without extra inventory charge");
  ((BattleItemPickup)Field(schedule,"drop")).gameObject.destroyed=true;Physics.Blocked=true;
  Time.time=6;front.ElapsedSeconds=480;Call(schedule,"Update");Check(BattleItemPickup.Created==2,"blocked landing does not spawn");
  Physics.Blocked=false;Time.time=8;front.ElapsedSeconds=482;Call(schedule,"Update");Check(BattleItemPickup.Created==3,"blocked landing retries promptly");
 }
 static CoopMatchPlan Plan(bool firstLocal)
 {
  var p=new CoopMatchPlan();p.Session.Slots[0]=new(){Team=0,IsLocal=firstLocal,BattleItem=BattleItemType.Heal};
  p.Session.Slots[4]=new(){Team=1,IsLocal=!firstLocal,BattleItem=BattleItemType.Shield};return p;
 }
 static void Online()
 {
  BattleItemInventory.Equipped=BattleItemType.Heal;BattleItemInventory.Count=2;BattleItemInventory.Consumes=0;BattleItemPickup.Created=0;
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=true;CoopAuthority.ActivePlan=Plan(true);
  var server=new NetworkManager{IsServer=true};NetworkManager.Singleton=server;var sf=new DimensionsFront{ElapsedSeconds=120};var host=Schedule(sf);Time.time=20;Time.unscaledTime=20;Call(host,"Update");
  Check(BattleItemPickup.Created==2&&BattleItemInventory.Consumes==1&&server.CustomMessagingManager.Last!=null,"server creates both supplies and sends snapshot");
  var packet=server.CustomMessagingManager.Last;var hostDrops=(BattleItemPickup[])Field(host,"networkDrops");
  CoopAuthority.LocalHasServerAuthority=false;CoopAuthority.ActivePlan=Plan(false);BattleItemInventory.Equipped=BattleItemType.Shield;BattleItemInventory.Count=2;BattleItemInventory.Consumes=0;
  var client=new NetworkManager{IsClient=true};NetworkManager.Singleton=client;var cf=new DimensionsFront{ElapsedSeconds=120};var replica=Schedule(cf);Time.time=21;Call(replica,"Update");
  int before=BattleItemPickup.Created;Check(before==2,"client does not create pickups independently");
  Call(replica,"OnSnapshot",(ulong)0,new FastBufferReader(packet));var clientDrops=(BattleItemPickup[])Field(replica,"networkDrops");
  Check(BattleItemPickup.Created==4&&!clientDrops[0].Authoritative&&!clientDrops[4].Authoritative&&BattleItemInventory.Consumes==1,"client mirrors host and consumes only its own item once");
  Call(replica,"OnSnapshot",(ulong)0,new FastBufferReader(packet));Check(BattleItemPickup.Created==4&&BattleItemInventory.Consumes==1,"snapshot replay is idempotent");
  CoopAuthority.LocalHasServerAuthority=true;CoopAuthority.ActivePlan=Plan(true);NetworkManager.Singleton=server;sf.ElapsedSeconds=240;Time.time=30;Time.unscaledTime=30;Call(host,"Update");
  Check(BattleItemPickup.Created==4,"host skips occupied supplies on tick");
  hostDrops[0].gameObject.destroyed=true;sf.ElapsedSeconds=360;Time.time=40;Time.unscaledTime=40;Call(host,"Update");
  Check(BattleItemPickup.Created==5,"host respawns only collected slot");packet=server.CustomMessagingManager.Last;
  CoopAuthority.LocalHasServerAuthority=false;CoopAuthority.ActivePlan=Plan(false);NetworkManager.Singleton=client;cf.ElapsedSeconds=360;Call(replica,"OnSnapshot",(ulong)0,new FastBufferReader(packet));
  Check(BattleItemPickup.Created==6&&clientDrops[0]!=null&&clientDrops[4]!=null&&BattleItemInventory.Consumes==1,"client receives new generation without extra charge");
  CoopAuthority.LocalHasServerAuthority=true;CoopAuthority.ActivePlan=Plan(true);NetworkManager.Singleton=server;
  hostDrops[0].gameObject.destroyed=true;hostDrops[4].gameObject.destroyed=true;
  sf.ElapsedSeconds=361;Time.time=41;Time.unscaledTime=41;Call(host,"Update");packet=server.CustomMessagingManager.Last;
  CoopAuthority.LocalHasServerAuthority=false;CoopAuthority.ActivePlan=Plan(false);NetworkManager.Singleton=client;cf.ElapsedSeconds=361;
  Call(replica,"OnSnapshot",(ulong)0,new FastBufferReader(packet));
  Check(clientDrops[0]==null&&clientDrops[4]==null&&replica.Status.Contains("119s"),"server pickup removes both replicas and preserves countdown");
 }
 static void Main(){Offline();Online();Console.WriteLine("PASS: two-minute supply recurrence, occupied slots, retry, one-time charge and authoritative snapshots.");}
}
