// Minimal engine/transport substitutes. These tests do not simulate a running Unity/NGO match.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Runtime.CompilerServices;

namespace UnityEngine
{
 public enum RuntimeInitializeLoadType { SubsystemRegistration,BeforeSceneLoad }
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType v){} }
 public class SerializeField:Attribute {} public class DisallowMultipleComponent:Attribute {}
 public class Object {
  public string name;public static readonly List<Object> All=new();
  public static T FindAnyObjectByType<T>() where T:Object=>All.OfType<T>().FirstOrDefault();
  public static void Destroy(Object o){All.Remove(o);}
 }
 public class Component:Object { public GameObject gameObject;public bool enabled=true;public bool isActiveAndEnabled=>enabled;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>(); }
 public class MonoBehaviour:Component {}
 public class GameObject:Object {
  readonly Dictionary<Type,Component> parts=new();public Transform transform;
  public GameObject(string n=""){name=n;All.Add(this);transform=AddComponent<Transform>();}
  public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};parts[typeof(T)]=c;All.Add(c);return c;}
  public T GetComponent<T>() where T:Component=>parts.Values.OfType<T>().FirstOrDefault();
  public static GameObject CreatePrimitive(PrimitiveType t){var g=new GameObject();g.AddComponent<Collider>();g.AddComponent<MeshRenderer>();return g;}
 }
 public class Transform:Component { public Vector3 position,localPosition,localScale;public Quaternion localRotation;public void SetParent(Transform p,bool keep){} }
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public float magnitude=>(float)Math.Sqrt(x*x+y*y);public static Vector2 operator*(Vector2 v,float n)=>new(v.x*n,v.y*n);public static Vector2 operator+(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);}
 public struct Vector3 {
  public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 up=>new(0,1,0);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator*(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
 }
 public struct Quaternion { public static Quaternion Euler(float x,float y,float z)=>default; }
 public struct Color { public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color clear=>new(0,0,0,0);public static Color operator*(Color a,float v)=>new(a.r*v,a.g*v,a.b*v,a.a);}
 public struct Color32 {public byte r,g,b,a;public static implicit operator Color32(Color c)=>new(){r=(byte)(Math.Clamp(c.r,0,1)*255),g=(byte)(Math.Clamp(c.g,0,1)*255),b=(byte)(Math.Clamp(c.b,0,1)*255),a=(byte)(Math.Clamp(c.a,0,1)*255)};}
 public struct Rect {public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}}
 public enum FontStyle {Normal,Bold} public enum ScaleMode {ScaleToFit} public enum TextureFormat {RGBA32}
 public enum HideFlags {HideAndDontSave} public enum TextureWrapMode {Clamp} public enum FilterMode {Bilinear}
 public class GUIStyleState {public Color textColor;}
 public class GUIStyle {public int fontSize;public FontStyle fontStyle;public GUIStyleState normal=new();public GUIStyle(){}public GUIStyle(GUIStyle source){}}
 public class Texture2D:Object {public int width,height;public Color32[] Pixels;public HideFlags hideFlags;public TextureWrapMode wrapMode;public FilterMode filterMode;public Texture2D(int w,int h,TextureFormat f,bool mip){width=w;height=h;}public void SetPixels32(Color32[] p){Pixels=p;}public void Apply(bool mip,bool release){}}
 public static class GUI {public static Texture2D LastTexture;public static void DrawTexture(Rect r,Texture2D t,ScaleMode m,bool alpha){LastTexture=t;}}
 public static class Mathf {public const float PI=(float)Math.PI,Deg2Rad=PI/180;
  public static float Abs(float v)=>Math.Abs(v);public static float Sin(float v)=>(float)Math.Sin(v);public static float Cos(float v)=>(float)Math.Cos(v);
  public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);
  public static int Clamp(int v,int a,int b)=>Math.Clamp(v,a,b);public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);
  public static float Clamp01(float v)=>Math.Clamp(v,0,1);
 }
 public static class Random {static System.Random r=new(42);public static float Range(float a,float b)=>a+(float)r.NextDouble()*(b-a);}
 public static class Time {public static float time,deltaTime,realtimeSinceStartup,timeScale=1;}
 public enum PrimitiveType {Cylinder} public enum QueryTriggerInteraction {Ignore}
 public class Collider:Component {} public static class Physics {public static bool Blocked;public static bool CheckSphere(Vector3 p,float r,int m,QueryTriggerInteraction q)=>Blocked;public static void IgnoreLayerCollision(int a,int b,bool v){} }
 public class Shader:Object { public static Shader Find(string s)=>new(); }
 public class Material:Object { public Color color;public Material(Shader s){}public bool HasProperty(string s)=>true;public void SetColor(string s,Color c){}public void SetFloat(string s,float f){}public void EnableKeyword(string s){} }
 public class Mesh:Object {public Vector3[] vertices;public int[] triangles;public void RecalculateNormals(){}public void RecalculateBounds(){}public void SetVertices(List<Vector3> v){vertices=v.ToArray();}public void SetTriangles(List<int> t,int s){triangles=t.ToArray();}}
 public class MeshFilter:Component {public Mesh sharedMesh;}public class Renderer:Component {public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;}public class MeshRenderer:Renderer {}
 public static class Debug {public static void LogWarning(string s){} }
 public static class JsonUtility {
  static readonly JsonSerializerOptions Options=Make();
  static JsonSerializerOptions Make(){var r=new DefaultJsonTypeInfoResolver();r.Modifiers.Add(i=>{if(i.Kind==JsonTypeInfoKind.Object&&i.Type.IsClass)i.CreateObject=()=>RuntimeHelpers.GetUninitializedObject(i.Type);});return new(){IncludeFields=true,TypeInfoResolver=r};}
  public static string ToJson<T>(T v)=>JsonSerializer.Serialize(v,Options);public static T FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,Options);
 }
}
namespace UnityEngine.Rendering {public enum ShadowCastingMode {Off}}
namespace Unity.Collections {public enum Allocator {Temp}}
namespace Unity.Netcode
{
 public enum NetworkDelivery {ReliableSequenced}
 public sealed class NetworkManager {
  public static NetworkManager Singleton;public const ulong ServerClientId=0;
  public bool IsListening=true,IsServer,IsClient,IsConnectedClient;public bool IsHost=>IsServer&&IsClient;
  public List<ulong> ConnectedClientsIds=new(){0,7};public CustomMessagingManager CustomMessagingManager=new();
 }
 public struct FastBufferWriter:IDisposable {
  public MemoryStream Stream;public FastBufferWriter(int n,Unity.Collections.Allocator a){Stream=new MemoryStream();}
  public void WriteValueSafe<T>(T v) where T:struct {var b=new BinaryWriter(Stream);if(v is int i)b.Write(i);else if(v is uint u)b.Write(u);else if(v is float f)b.Write(f);else throw new Exception(typeof(T).Name);}
  public void Dispose(){}public byte[] Bytes()=>Stream.ToArray();
 }
 public struct FastBufferReader {
  readonly MemoryStream stream;public FastBufferReader(byte[] b){stream=new(b);}
  public bool TryBeginRead(int n)=>stream.Length-stream.Position>=n;
  public void ReadValueSafe<T>(out T v) where T:struct {var r=new BinaryReader(stream);object o;if(typeof(T)==typeof(uint))o=r.ReadUInt32();else if(typeof(T)==typeof(int))o=r.ReadInt32();else if(typeof(T)==typeof(float))o=r.ReadSingle();else throw new Exception();v=(T)o;}
 }
 public class CustomMessagingManager {
  public readonly Dictionary<string,Action<ulong,FastBufferReader>> Handlers=new();
  public readonly List<(string Name,ulong Client,byte[] Data)> Sent=new();
  public void RegisterNamedMessageHandler(string n,Action<ulong,FastBufferReader> h){Handlers[n]=h;}
  public void UnregisterNamedMessageHandler(string n){Handlers.Remove(n);}
  public void SendNamedMessage(string n,ulong c,FastBufferWriter w,NetworkDelivery d){Sent.Add((n,c,w.Bytes()));}
  public void Deliver(string n,ulong sender,byte[] b){Handlers[n](sender,new(b));}
 }
}
namespace Alterverse.AI {public class MobaBotBrain:UnityEngine.Component {}public static class BotDifficultySettings {public static int OnlineDifficulty;public static void BeginMatchOverride(int n){}public static void EndMatchOverride(){}}}
namespace Alterverse.Characters {public class HeroIdentity:UnityEngine.Component {}public class HeroMotor:UnityEngine.Component {}}
namespace Alterverse.Combat {public class Health:UnityEngine.Component {public static List<Health> Active=new();public bool IsDead;}}
namespace Alterverse.Match {public class DimensionsFront:UnityEngine.Component {public bool HasEnded;}public static class ArenaVariantRuntime {public static void BeginCoopMatch(int v){}}}
namespace Alterverse.Visuals {
 public static class LunarSurfaceField {public static bool Steep;public static UnityEngine.Vector3 WorldSurface(float x,float z)=>new(x,Steep?x*10:0,z);}
 public static class PrototypeVfx {public static void Ring(UnityEngine.Vector3 p,UnityEngine.Color c,float r,float t){}}
}
namespace Alterverse.Multiplayer
{
 public class MatchPlayerSlot {public string PlayerId;public bool IsBot,IsLocal,IsHost,Connected=true;}
 public class MatchSession {public const int TeamSize=4,MaxPlayers=8;public MatchPlayerSlot[] Slots=new MatchPlayerSlot[8];}
 public class CoopMatchPlan {public string MatchId="round-1";public MatchSession Session=new();public int Difficulty,Arena;}
 public class OnlineMatchPlan:CoopMatchPlan {}
 public static class CoopRosterBuilder {public static int FindLocalHumanSlot(CoopMatchPlan p){for(int i=0;i<p.Session.Slots.Length;i++)if(p.Session.Slots[i]!=null&&!p.Session.Slots[i].IsBot&&p.Session.Slots[i].IsLocal)return i;return -1;}}
 public static class CoopPlanHandshake {public static uint Fingerprint(CoopMatchPlan p)=>p.MatchId=="round-1"?123u:456u;public static bool IsAuthorizedClient(ulong c,string p)=>c==7&&p=="guest";}
 public static class OnlinePlanHandshake {public static bool IsAuthorizedClient(ulong c,string p)=>CoopPlanHandshake.IsAuthorizedClient(c,p);}
 public static class DedicatedOnlineMatchHandshake {public static bool IsAuthorizedClient(ulong c,string p)=>CoopPlanHandshake.IsAuthorizedClient(c,p);}
}
namespace Alterverse.Profile
{
 public static class AccountProfilePrefs {
  public static string Account="account-a";public static string FailPrefix;
  static readonly Dictionary<string,Dictionary<string,object>> Stores=new();
  static Dictionary<string,object> Data {get {string a=Account??"";if(!Stores.ContainsKey(a))Stores[a]=new();return Stores[a];}}
  public static void Reset(){Stores.Clear();Account="account-a";FailPrefix=null;}
  public static bool HasKey(string k)=>Data.ContainsKey(k);public static int GetInt(string k,int f=0)=>Data.TryGetValue(k,out var v)?(int)v:f;
  public static string GetString(string k,string f="")=>Data.TryGetValue(k,out var v)?(string)v:f;
  public static void SetInt(string k,int v){if(FailPrefix!=null&&k.StartsWith(FailPrefix)){FailPrefix=null;throw new IOException("Simulated interrupted write");}Data[k]=v;}
  public static void SetString(string k,string v){Data[k]=v;}public static void DeleteKey(string k){Data.Remove(k);}public static void Save(){}
 }
}
namespace Alterverse.Shop {public sealed class PaidShardCommerce {public static int Balance;public static bool Spending;public static PaidShardCommerce Instance;public bool Ready;public bool BeginPurchase(string s)=>false;}}

namespace Alterverse.UI
{
 using UnityEngine;
 public class FrontEndTheme {
  public GUIStyle Small=new(),Heading=new(),Body=new();public readonly List<string> Labels=new();
  public void Panel(UnityEngine.Rect r){}public static void Fill(UnityEngine.Rect r,UnityEngine.Color c){}
  public void FitLabel(UnityEngine.Rect r,string s,GUIStyle style,bool flag=false){Labels.Add(s);}
  public void StatusBadge(UnityEngine.Rect r,string s,UnityEngine.Color c){Labels.Add(s);}
 }
 public sealed partial class MainMenuController {
  public float Right=1280,Left=0,ContentWidth=1280;public bool HomeAdvertisingActive;
  public UnityEngine.Rect HomeBannerMenuRect=new(0,700,100,100);public FrontEndTheme theme=new();
 }
}

