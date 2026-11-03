using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object value){}
        public static void DontDestroyOnLoad(Object value){}
        public static T[] FindObjectsByType<T>(FindObjectsInactive flags) where T:class
            =>GameObject.All.SelectMany(o=>o.Components).OfType<T>().ToArray();
    }
    public enum FindObjectsInactive{Exclude}
    public class Component:Object
    {
        public GameObject gameObject;
        public Transform transform=>gameObject.transform;
        public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
        public void GetComponentsInChildren<T>(bool inactive,List<T> values)=>values.AddRange(gameObject.Components.OfType<T>());
    }
    public class MonoBehaviour:Component{}
    public class GameObject:Object
    {
        public static List<GameObject> All=new();
        public readonly List<object> Components=new();
        public Transform transform=new();
        public GameObject(string name=""){this.name=name;All.Add(this);}
        public T GetComponent<T>() where T:class=>Components.OfType<T>().FirstOrDefault();
        public T AddComponent<T>() where T:Component,new()
        {var value=new T{gameObject=this};Components.Add(value);Invoke(value,"Awake");return value;}
        public static void Invoke(object value,string method,params object[] args)
            =>value.GetType().GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(value,args);
    }
    public class Transform{public Vector3 position;public void SetParent(Transform parent,bool world){}}
    public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
    public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);}
    public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
    public static class Time{public static float time,unscaledTime,realtimeSinceStartup;}
    public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);}
    public static class Application{public static bool isBatchMode=true;}
    public enum RuntimeInitializeLoadType{AfterSceneLoad}
    public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){}}
    public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int order){}}
    public class Shader:Object{public bool isSupported=true;}
    public class Texture:Object{}
    public class Material:Object
    {
        private readonly Dictionary<string,float> floats=new();private readonly Dictionary<string,Color> colors=new();
        public Shader shader;public int renderQueue;public Material(Shader shader){this.shader=shader;colors["_BaseColor"]=new Color(1,1,1,1);colors["_Color"]=new Color(1,1,1,1);}
        public Material(Material other){shader=other.shader;name=other.name;foreach(var x in other.colors)colors[x.Key]=x.Value;foreach(var x in other.floats)floats[x.Key]=x.Value;}
        public bool HasProperty(string name)=>true;public void SetFloat(string name,float value)=>floats[name]=value;
        public float GetFloat(string name)=>floats.TryGetValue(name,out var value)?value:0;
        public Color GetColor(string name)=>colors.TryGetValue(name,out var value)?value:new Color(1,1,1,1);
        public void SetColor(string name,Color value)=>colors[name]=value;
        public Texture GetTexture(string name)=>null;public void SetTexture(string name,Texture value){}
        public Vector2 GetTextureScale(string name)=>new Vector2(1,1);public Vector2 GetTextureOffset(string name)=>new Vector2();
        public void SetTextureScale(string name,Vector2 value){}public void SetTextureOffset(string name,Vector2 value){}
        public void SetOverrideTag(string name,string value){}public void EnableKeyword(string name){}public void DisableKeyword(string name){}
    }
    public class Renderer:Component{public bool forceRenderingOff,enabled=true;private Material[] materials=Array.Empty<Material>();public Material[] sharedMaterials{get=>(Material[])materials.Clone();set=>materials=(Material[])value.Clone();}}
    public class MeshRenderer:Renderer{}public class SkinnedMeshRenderer:Renderer{}
    public class ParticleSystemRenderer:Renderer{public Material sharedMaterial;public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;}
    public enum ParticleSystemShapeType{Circle}
    public enum ParticleSystemStopBehavior{StopEmittingAndClear}
    public class ParticleSystem:Component
    {
        public class Main{public bool playOnAwake,loop;public float duration,startLifetime,startSpeed,startSize;public int maxParticles;public MinMaxGradient startColor;}
        public class Emission{public float rateOverTime;}
        public class Shape{public ParticleSystemShapeType shapeType;public float radius;public Vector3 rotation;}
        public Main main=new();public Emission emission=new();public Shape shape=new();
        public struct MinMaxGradient{public MinMaxGradient(Color a,Color b){}}
        public void Stop(bool children,ParticleSystemStopBehavior behavior){}
        public void Simulate(float seconds,bool children,bool restart){}public void Play(){}
    }
    public static class Resources{public static T Load<T>(string path) where T:class=>path=="CombatVfx/VoidKnightStealth"?new Shader{name="DimensionsFront/VoidKnightStealth"} as T:null;}
}
namespace UnityEngine.Rendering{public enum ShadowCastingMode{Off}public enum BlendMode{SrcAlpha=5,OneMinusSrcAlpha=10}}
namespace UnityEngine.Android
{
    public class PermissionCallbacks
    {public event Action<string> PermissionGranted,PermissionDenied,PermissionDeniedAndDontAskAgain;public void Answer(bool yes){if(yes)PermissionGranted?.Invoke("microphone");else PermissionDenied?.Invoke("microphone");}}
    public static class Permission
    {
        public const string Microphone="microphone";public static bool Granted=true,Answer=true;public static int Requests;
        public static bool HasUserAuthorizedPermission(string value)=>Granted;
        public static void RequestUserPermission(string value,PermissionCallbacks callbacks){Requests++;Granted=Answer;callbacks.Answer(Answer);}
    }
}
namespace Unity.Collections{public enum Allocator{Temp}}
namespace Unity.Netcode
{
    public enum NetworkDelivery{ReliableSequenced}
    public class FastBufferWriter:IDisposable
    {public List<object> Values=new();public FastBufferWriter(int capacity,Unity.Collections.Allocator allocator){}public void WriteValueSafe<T>(T value)=>Values.Add(value);public void Dispose(){}}
    public class FastBufferReader
    {private Queue<object> values;public int Length=16;public FastBufferReader(params object[] values){this.values=new(values);}public void ReadValueSafe<T>(out T value)=>value=(T)values.Dequeue();}
    public class CustomMessagingManager
    {
        public Dictionary<string,Action<ulong,FastBufferReader>> Handlers=new();
        public List<object[]> Sent=new();
        public void RegisterNamedMessageHandler(string name,Action<ulong,FastBufferReader> handler)=>Handlers[name]=handler;
        public void UnregisterNamedMessageHandler(string name)=>Handlers.Remove(name);
        public void SendNamedMessage(string name,ulong id,FastBufferWriter writer,NetworkDelivery delivery)=>Sent.Add(writer.Values.ToArray());
    }
    public class NetworkManager
    {public static NetworkManager Singleton;public const ulong ServerClientId=0;public bool IsServer,IsListening=true;public class Clock{public double Time;}public Clock ServerTime=new();public List<ulong> ConnectedClientsIds=new(){0,7};public CustomMessagingManager CustomMessagingManager=new();}
}
namespace Alterverse.Characters
{
    public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy,VoidKnight,Bolt7}
    public class HeroIdentity:UnityEngine.MonoBehaviour{public HeroId Hero;}
    public class VoidKnightState:UnityEngine.MonoBehaviour{public bool Cloaked;public static bool HiddenFrom(UnityEngine.GameObject observer,Alterverse.Combat.Health target)=>false;}
    public static class LocalHeroContext{public static Alterverse.Combat.TeamMember Member;}
}
namespace Alterverse.Combat
{
    public class LaneMinion:UnityEngine.MonoBehaviour{public int Team;}public class LaneTower:UnityEngine.MonoBehaviour{public int Team;}
    public class Health:UnityEngine.MonoBehaviour{public bool IsDead;}
    public class TeamMember:UnityEngine.MonoBehaviour{public int Team;}
}
namespace Alterverse.Multiplayer
{
    public class MatchPlan{public string MatchId="fixture";}
    public static class CoopAuthority{public static bool Active;public static bool CanSimulateHostGameplay=true;public static MatchPlan ActivePlan;}
    public class CoopHeroSlot:UnityEngine.MonoBehaviour{public int Slot;}
    public static class MatchSession{public const int TeamSize=4,MaxPlayers=8;}
    public class OnlinePartyService{public static OnlinePartyService Instance=new();public string VoiceRoomId="",JoinCode="";public bool InParty,MatchmakingLocked,IsPartyFull;}
}
namespace Unity.Services.Authentication
{
    public class AuthenticationService{public static AuthenticationService Instance=new();public bool IsSignedIn=true;}
}
namespace Unity.Services.Vivox
{
    public enum ChatCapability{AudioOnly}
    public class LoginOptions{public bool EnableTTS;}
    public class VivoxService
    {
        public static VivoxService Instance=new();
        public bool IsLoggedIn;public int Initializations,Unmutes,Logouts;public bool Muted=true,Fail;
        public TaskCompletionSource<bool> JoinPending;public string Channel;
        public Task InitializeAsync(){Initializations++;if(Fail)throw new Exception("fixture unavailable");return Task.CompletedTask;}
        public Task LoginAsync(LoginOptions options){IsLoggedIn=true;return Task.CompletedTask;}
        public async Task JoinGroupChannelAsync(string channel,ChatCapability capability){if(Fail)throw new Exception("fixture unavailable");Channel=channel;if(JoinPending!=null)await JoinPending.Task;}
        public Task LogoutAsync(){IsLoggedIn=false;Channel=null;Logouts++;return Task.CompletedTask;}
        public void MuteInputDevice()=>Muted=true;
        public void UnmuteInputDevice(){Muted=false;Unmutes++;}
    }
}

namespace Alterverse.Profile {public static class GameLanguage{public static string Text(string value)=>value;}}
namespace UnityEngine {
 public static class GUIUtility{public static string systemCopyBuffer;}
 public delegate void AndroidJavaRunnable();
 public class AndroidJavaObject:System.IDisposable {
  public static System.Collections.Generic.Queue<AndroidJavaRunnable> Ui=new();
  public static AndroidJavaObject Activity;public static int Started;public static string Shared;public bool Disposed;
  public string Text;public AndroidJavaObject(string name,params object[] args){}
  public T Call<T>(string name,params object[] args) where T:class {
   if(Disposed)throw new System.ObjectDisposedException("JNI reference");
   if(name=="putExtra"&&(string)args[0]=="android.intent.extra.TEXT")Text=(string)args[1];
   return new AndroidJavaObject("return"){Text=Text} as T;
  }
  public void Call(string name,params object[] args) {
   if(Disposed)throw new System.ObjectDisposedException("JNI reference");
   if(name=="runOnUiThread")Ui.Enqueue((AndroidJavaRunnable)args[0]);
   if(name=="startActivity"){Started++;Shared=((AndroidJavaObject)args[0]).Text;}
  }
  public void Dispose(){Disposed=true;}
 }
 public class AndroidJavaClass:AndroidJavaObject {
  public AndroidJavaClass(string name):base(name){}
  public T GetStatic<T>(string name) where T:class {Activity=new AndroidJavaObject("activity");return Activity as T;}
  public T CallStatic<T>(string name,params object[] args) where T:class=>new AndroidJavaObject("chooser"){Text=((AndroidJavaObject)args[0]).Text} as T;
 }
}

namespace Alterverse.Visuals {public class VoidKnightStealth:UnityEngine.MonoBehaviour {public bool Applied;}}
