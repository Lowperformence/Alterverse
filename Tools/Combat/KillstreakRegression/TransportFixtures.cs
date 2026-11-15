using System;
using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
    public enum RuntimeInitializeLoadType {BeforeSceneLoad}
    public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
    public class Object {public static void Destroy(Object o){}public static void DontDestroyOnLoad(Object o){}}
    public class MonoBehaviour:Object {public GameObject gameObject;}
    public static class Time {public static float timeScale=1,unscaledTime;}
    public class GameObject:Object
    {
        private readonly Dictionary<Type,object> components=new();
        public GameObject(){}public GameObject(string name){}
        public T GetComponent<T>() where T:class=>components.TryGetValue(typeof(T),out object o)?(T)o:null;
        public void Put<T>(T obj) where T:class {components[typeof(T)]=obj;}
        public T AddComponent<T>() where T:MonoBehaviour,new(){var c=new T{gameObject=this};Put(c);typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(c,null);return c;}
    }
}
namespace Unity.Collections
{
    public enum Allocator {Temp}
    public struct FixedString128Bytes
    {private readonly string value;public FixedString128Bytes(string text){value=text;}public override string ToString()=>value??"";}
}
namespace Unity.Netcode
{
    public enum NetworkDelivery {ReliableSequenced}
    public class FastBufferWriter:IDisposable
    {
        public readonly List<object> Values=new();
        public FastBufferWriter(int size,Allocator a){}
        public void WriteValueSafe<T>(T value){Values.Add(value);}
        public void Dispose(){}
    }
    public struct FastBufferReader
    {
        private readonly List<object> values;private int position;
        public FastBufferReader(List<object> values){this.values=values;position=0;}
        private static int Size(object value)=>value is bool?1:value is FixedString128Bytes s?2+s.ToString().Length:4;
        public bool TryBeginRead(int size){int available=0;for(int i=position;i<values.Count;i++)available+=Size(values[i]);return available>=size;}
        public void ReadValueSafe<T>(out T value){value=(T)values[position++];}
    }
    public class CustomMessagingManager
    {
        public string LastName;public ulong LastRecipient;public List<object> LastValues;public int Sent;
        public void SendNamedMessage(string name,ulong id,FastBufferWriter writer,NetworkDelivery delivery)
        {LastName=name;LastRecipient=id;LastValues=new List<object>(writer.Values);Sent++;}
        public void RegisterNamedMessageHandler(string name,Action<ulong,FastBufferReader> handler){}
        public void UnregisterNamedMessageHandler(string name){}
    }
    public class NetworkManager
    {
        public static NetworkManager Singleton;public const ulong ServerClientId=0;
        public bool IsServer,IsClient,IsConnectedClient,IsListening=true;
        public CustomMessagingManager CustomMessagingManager;
        public List<ulong> ConnectedClientsIds=new(){0,9};
    }
}
namespace Alterverse.Combat
{
    public class Health
    {
        public static readonly List<Health> Active=new();
        public GameObject gameObject;public bool IsDead,isActiveAndEnabled=true;
        public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
    }
    public static class AimGeometry
    {public static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);}
}
namespace Alterverse.Characters {}
namespace Alterverse.Multiplayer
{
    public class CoopHeroSlot
    {public int Slot;public string PlayerId;public bool IsLocalHuman;public GameObject gameObject;}
    public static class MatchSession {public const int MaxPlayers=8;}
    public class MatchPlayerSlot {public bool IsBot,IsHost;public string PlayerId;}
    public class Session {public MatchPlayerSlot[] Slots=new MatchPlayerSlot[8];}
    public class CoopMatchPlan {public Session Session=new();}
    public static class CoopAuthority {public static bool Active=true,LocalHasServerAuthority;public static CoopMatchPlan ActivePlan=new();}
    public static class CoopPlanHandshake {public static uint Fingerprint(CoopMatchPlan plan)=>123;}
    public static class MatchNetworkProtocol
    {
        public static bool Authorized(ulong clientId,string playerId)=>clientId==9&&playerId=="player";
        public static bool ReadRound(ref FastBufferReader r){if(!r.TryBeginRead(4))return false;r.ReadValueSafe(out uint round);return round==123;}
    }
    public class CoopGameplayCommands
    {
        public static CoopGameplayCommands Instance;
        private readonly Dictionary<ulong,string> senderOwners=new();
        public CoopGameplayCommands(){Instance=this;}
        public bool TryAuthorizeTurretPlacement(ulong id,ref FastBufferReader reader,out GameObject actor)=>TryReadAuthorizedActor(id,ref reader,out actor);
        /*AUTH_METHOD*/
    }
}
namespace Alterverse.UI
{
    public static class CombatFeedbackHUD
    {
        public static int DeployCalls,StatusCalls;
        public static bool DeployNetworkTurret(GameObject actor,Vector3 position)
        {if(!AimGeometry.Finite(position))return false;DeployCalls++;return true;}
        public static bool GetNetworkReward(int slot,out int normal,out int mobile,out bool eligible,out int streak)
        {normal=slot==0?1:0;mobile=slot==1?1:0;eligible=slot==0;streak=slot;return true;}
        public static void ApplyNetworkReward(int slot,int normal,int mobile,bool eligible,int streak){StatusCalls++;}
    }
}
static class TransportRegression
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static List<object> Command(uint round,string player,uint sequence,float x=1)=>new(){round,0,new FixedString128Bytes(player),sequence,x,0f,2f};
    static void Call(KillstreakNetworkSync sync,string method,params object[] args)=>typeof(KillstreakNetworkSync).GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(sync,args);
    static void Place(KillstreakNetworkSync sync,ulong id,List<object> v)=>Call(sync,"OnPlacement",id,new FastBufferReader(v));
    static void Status(KillstreakNetworkSync sync,ulong id,List<object> v)=>Call(sync,"OnRewards",id,new FastBufferReader(v));
    static void Main()
    {
        var msg=new CustomMessagingManager();
        var manager=new NetworkManager{IsServer=true,CustomMessagingManager=msg};NetworkManager.Singleton=manager;CoopAuthority.LocalHasServerAuthority=true;
        new CoopGameplayCommands();var transport=new GameObject().AddComponent<KillstreakNetworkSync>();Call(transport,"Update");
        var actor=new GameObject();var slot=new CoopHeroSlot{Slot=0,PlayerId="player",IsLocalHuman=true,gameObject=actor};
        var hp=new Health{gameObject=actor};actor.Put(slot);actor.Put(hp);Health.Active.Add(hp);
        CoopAuthority.ActivePlan.Session.Slots[0]=new MatchPlayerSlot{PlayerId="player"};
        Place(transport,9,Command(123,"player",1));Check(CombatFeedbackHUD.DeployCalls==1,"authorized placement reaches host");
        Place(transport,9,Command(123,"player",1));Check(CombatFeedbackHUD.DeployCalls==1,"duplicate sequence ignored");
        Place(transport,8,Command(123,"player",2));Check(CombatFeedbackHUD.DeployCalls==1,"other client cannot place");
        Place(transport,9,Command(999,"player",2));Check(CombatFeedbackHUD.DeployCalls==1,"old round rejected");
        Place(transport,9,Command(123,"wrong",2));Check(CombatFeedbackHUD.DeployCalls==1,"wrong player rejected");
        hp.IsDead=true;Place(transport,9,Command(123,"player",2));Check(CombatFeedbackHUD.DeployCalls==1,"dead actor rejected");hp.IsDead=false;
        Place(transport,9,Command(123,"player",2,float.NaN));Check(CombatFeedbackHUD.DeployCalls==1,"nonfinite location rejected");
        Place(transport,9,Command(123,"player",3));Check(CombatFeedbackHUD.DeployCalls==2,"new valid sequence accepted");
        Call(transport,"SendRewards",manager);Check(msg.LastName=="DF_COOP_TURRET_REWARDS_V1"&&msg.LastValues.Count==33,"server publishes full round-scoped rewards");
        var status=msg.LastValues;
        manager.IsServer=false;manager.IsClient=true;manager.IsConnectedClient=true;CoopAuthority.LocalHasServerAuthority=false;
        Status(transport,8,status);Check(CombatFeedbackHUD.StatusCalls==0,"status from non-server rejected");
        Status(transport,0,status);Check(CombatFeedbackHUD.StatusCalls==8,"client receives all slot snapshots");
        Check(KillstreakNetworkSync.TrySendPlacement(actor,new Vector3(1,0,2))&&msg.LastName=="DF_COOP_TURRET_PLACE_V1", "client sends placement command");
        hp.IsDead=true;Check(!KillstreakNetworkSync.TrySendPlacement(actor,new Vector3(1,0,2)),"dead player cannot send placement");
        Console.WriteLine("PASS authorized killstreak transport, snapshot and duplicate checks");
    }
}
