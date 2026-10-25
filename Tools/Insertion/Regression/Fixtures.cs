// Controlled managed substitutes: validates geometry and lifecycle, not Unity rendering or GPU speed.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine {
 public class Object {
  public string name;
  public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T:Component => GameObject.All.Where(g=>g.activeInHierarchy).SelectMany(g=>g.components).OfType<T>().ToArray();
  public static void Destroy(Object obj){}
  public static GameObject Instantiate(GameObject original,Transform parent,bool world){
   GameObject Copy(GameObject source,Transform target){
    var copy=new GameObject(source.name);copy.transform.SetParent(target,false);copy.transform.localPosition=source.transform.localPosition;
    copy.transform.localRotation=source.transform.localRotation;copy.transform.localScale=source.transform.localScale;copy.SetActive(source.activeSelf);
    if(source.GetComponent<Renderer>() is Renderer r)copy.AddComponent<MeshRenderer>().sharedMaterial=r.sharedMaterial;
    foreach(var child in GameObject.All.ToArray().Where(g=>g.transform.parent==source.transform))Copy(child,copy.transform);
    return copy;
   }
   return Copy(original,parent);
  }
 }
 public class Component:Object {
  public GameObject gameObject;public Transform transform=>gameObject.transform;
  public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();
  public T[] GetComponentsInChildren<T>(bool inactive=false)where T:class=>gameObject.GetComponentsInChildren<T>(inactive);
 }
 public class MonoBehaviour:Component{public bool enabled=true;}
 public class GameObject:Object {
  public static readonly List<GameObject> All=new();public readonly List<Component> components=new();
  public bool activeSelf=true;public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
  public Transform transform;
  public GameObject(string name=""){this.name=name;transform=new Transform{gameObject=this};All.Add(this);}
  public T AddComponent<T>()where T:Component,new(){var t=new T{gameObject=this};components.Add(t);typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(t,null);return t;}
  public T GetComponent<T>()where T:class=>components.OfType<T>().FirstOrDefault();
  public T GetComponentInChildren<T>()where T:class=>GetComponentsInChildren<T>().FirstOrDefault();
  public T[] GetComponentsInChildren<T>(bool inactive=false)where T:class=>All.Where(g=>(inactive||g.activeInHierarchy)&&(g==this||g.transform.IsChildOf(transform))).SelectMany(g=>g.components).OfType<T>().ToArray();
  public void SetActive(bool active){activeSelf=active;}
 }
 public class Transform:Component {
  public Transform parent;public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;
  public Vector3 position{get=>parent==null?localPosition:parent.TransformPoint(localPosition);set=>localPosition=value;}
  public Quaternion rotation{get=>localRotation;set=>localRotation=value;}
  public void SetParent(Transform p,bool world=true){parent=p;}
  public Transform Find(string name)=>GameObject.All.FirstOrDefault(g=>g.transform.parent==this&&g.name==name)?.transform;
  public bool IsChildOf(Transform p){for(var t=parent;t!=null;t=t.parent)if(t==p)return true;return false;}
  public Vector3 TransformPoint(Vector3 v){var q=localRotation*new Vector3(v.x*localScale.x,v.y*localScale.y,v.z*localScale.z)+localPosition;return parent==null?q:parent.TransformPoint(q);}
 }
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public float sqrMagnitude=>x*x+y*y+z*z;
  public static Vector3 zero=>new();public static Vector3 one=>new(1,1,1);public static Vector3 up=>new(0,1,0);public static Vector3 down=>new(0,-1,0);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator*(Vector3 v,float f)=>new(v.x*f,v.y*f,v.z*f);
  public static Vector3 operator/(Vector3 v,float f)=>v*(1/f);
  public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Mathf.Clamp01(t);
 }
 public struct Quaternion {
  public System.Numerics.Quaternion value;
  public static Quaternion identity=>new(){value=System.Numerics.Quaternion.Identity};
  public static Quaternion LookRotation(Vector3 direction,Vector3 up)=>identity;
  public static Quaternion Euler(float x,float y,float z)=>new(){value=System.Numerics.Quaternion.CreateFromYawPitchRoll(y*Mathf.PI/180,x*Mathf.PI/180,z*Mathf.PI/180)};
  public static Quaternion operator*(Quaternion a,Quaternion b)=>new(){value=a.value*b.value};
  public static Vector3 operator*(Quaternion q,Vector3 p){var v=System.Numerics.Vector3.Transform(new(p.x,p.y,p.z),q.value);return new(v.X,v.Y,v.Z);}
 }
 public struct Color {
  public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
  public static Color operator*(Color c,float f)=>new(c.r*f,c.g*f,c.b*f,c.a*f);
 }
 public static class Mathf {
  public const float PI=(float)Math.PI;
  public static float Clamp01(float x)=>Math.Clamp(x,0,1);public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);
  public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);
  public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);
  public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
  public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return Lerp(a,b,t*t*(3-2*t));}
  public static float Exp(float a)=>(float)Math.Exp(a);
  public static float Sin(float a)=>(float)Math.Sin(a);public static float Cos(float a)=>(float)Math.Cos(a);
 }
 public class Shader:Object{public static Shader Find(string s)=>new();}
 public class Material:Object {
  public static int Created;public Color color;public Material(Shader s){Created++;}
  public void SetColor(string s,Color c){if(s=="_BaseColor")color=c;}
  public void SetFloat(string s,float f){}public void EnableKeyword(string s){}
 }
 public class Mesh:Object {
  public static int Created;public List<Vector3> vertices;public List<int> triangles;
  public Mesh(){Created++;}
  public void SetVertices(List<Vector3> v){vertices=v;}public void SetTriangles(List<int> t,int i){triangles=t;}
  public void RecalculateNormals(){}public void RecalculateBounds(){}public void UploadMeshData(bool b){}
 }
 public class Renderer:Component {public bool forceRenderingOff,receiveShadows;public Rendering.ShadowCastingMode shadowCastingMode;public Material sharedMaterial;}
 public class MeshRenderer:Renderer{}
 public class MeshFilter:Component{public Mesh sharedMesh;}
 public enum FindObjectsSortMode{None}
 public class SerializeField:Attribute{}
 public class RequireComponent:Attribute{public RequireComponent(Type type){}}
 public class Min:Attribute{public Min(float value){}}
 public class Range:Attribute{public Range(float a,float b){}}
 public class Camera:Component{public static Camera main;public float fieldOfView;}

 public enum RuntimeInitializeLoadType{SubsystemRegistration}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value){}}
 public static class Time {public static float unscaledDeltaTime=.1f,timeScale=1,deltaTime;}
 public static class Debug {public static void LogWarning(string text){}}
 public class AudioClip:Object{public float length=14f;public bool LoadAudioData()=>true;}
 public static class Resources {
  public static int Loads;public static readonly Dictionary<string,Object> Assets=new();
  public static T Load<T>(string path)where T:Object{Loads++;if(Assets.TryGetValue(path,out var asset))return asset as T;
   return typeof(T)==typeof(AudioClip)?new AudioClip{name=path} as T:null;}
 }
 public class AudioSource:Component {
  public bool playOnAwake,loop;public float spatialBlend,volume,pitch,dopplerLevel,time;public int priority,plays;public AudioClip clip;
  public bool isPlaying,paused;private bool resumable;public readonly List<string> cues=new();
  public void Play(){plays++;isPlaying=true;resumable=true;}public void PlayOneShot(AudioClip clip,float gain){cues.Add(clip.name);isPlaying=true;resumable=true;}
  public void Pause(){paused=true;isPlaying=false;}public void UnPause(){if(paused){paused=false;isPlaying=resumable;}}
  public void Stop(){isPlaying=false;resumable=false;paused=false;}
 }
 public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int i){}}
}
namespace UnityEngine.Rendering{public enum ShadowCastingMode{On,Off}}
namespace Alterverse.Characters {public class HeroIdentity:UnityEngine.MonoBehaviour{}public class HeroLocomotion:UnityEngine.Component{public bool OwnsMovementFrame;}}
namespace Alterverse.Combat {public class TeamMember:UnityEngine.MonoBehaviour{public int Team;}}
namespace Unity.Netcode {public class NetworkManager{public static NetworkManager Singleton;public bool IsServer,IsClient,IsListening;}}
namespace Alterverse.Multiplayer {public static class CoopAuthority{public static bool Active,LocalHasServerAuthority;}}
namespace Alterverse.Profile {public static class PlayerProfile {public static float MasterVolume=1,SfxVolume=1;}}
namespace Alterverse.UI {public static class MainMenuController{public static bool IsVisible;}public static class MatchEntryCurtain{public static bool AwaitingArena;}}
namespace Alterverse.Visuals {public static class StylizedArtDirection{public static readonly UnityEngine.Color ZeroEnergy=new(.76f,.24f,1f),LumenEnergy=new(.18f,.92f,1f);}}
