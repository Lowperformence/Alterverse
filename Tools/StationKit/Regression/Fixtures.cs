// Controlled engine fixtures: native Blender geometry is checked separately; this is not a Unity renderer.
using System;using System.Collections.Generic;using System.Linq;using System.Text.Json;
namespace UnityEngine {
 public class Object {static int next;readonly int id=++next;public string name;public bool destroyed;[Obsolete("Use GetEntityId instead.", true)] public int GetInstanceID()=>id;
  public static T FindAnyObjectByType<T>() where T:class=>GameObject.All.Where(g=>g.activeInHierarchy).SelectMany(g=>g.components).OfType<T>().FirstOrDefault();
  public static void DontDestroyOnLoad(Object o){}public static void Destroy(Object o){if(o==null)return;o.destroyed=true;if(o is GameObject g)g.SetActive(false);}
 }
 public class Component:Object {public GameObject gameObject;public new string name {get=>gameObject.name;set=>gameObject.name=value;}public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();public T[] GetComponentsInChildren<T>(bool inactive=false) where T:class=>gameObject.GetComponentsInChildren<T>(inactive);}
 public class MonoBehaviour:Component {public bool enabled=true;}
 public class GameObject:Object {public static List<GameObject> All=new();public List<Component> components=new();public Transform transform;public bool activeSelf=true;public bool activeInHierarchy=>!destroyed&&activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
  public GameObject(string name=""){this.name=name;transform=new Transform{gameObject=this};All.Add(this);}
  public T AddComponent<T>() where T:Component,new(){var t=new T{gameObject=this};components.Add(t);return t;}
  public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault();
  public T[] GetComponentsInChildren<T>(bool inactive=false) where T:class=>All.Where(g=>!g.destroyed&&(inactive||g.activeInHierarchy)&&(g==this||g.transform.IsChildOf(transform))).SelectMany(g=>g.components).OfType<T>().ToArray();
  public static GameObject Find(string name)=>All.FirstOrDefault(g=>g.name==name&&g.activeInHierarchy);public void SetActive(bool v)=>activeSelf=v;
 }
 public class Transform:Component {public Transform parent;public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation;public Vector3 position{get=>parent==null?localPosition:parent.position+localPosition;set=>localPosition=parent==null?value:value-parent.position;}public Quaternion rotation{get=>localRotation;set=>localRotation=value;}
  public Vector3 lossyScale=>parent==null?localScale:new(localScale.x*parent.lossyScale.x,localScale.y*parent.lossyScale.y,localScale.z*parent.lossyScale.z);
  public int childCount=>GameObject.All.Count(g=>!g.destroyed&&g.transform.parent==this);public Transform GetChild(int i)=>GameObject.All.Where(g=>!g.destroyed&&g.transform.parent==this).ElementAt(i).transform;
  public void SetParent(Transform p,bool world){var old=position;parent=p;if(world)position=old;}public bool IsChildOf(Transform p){for(var t=parent;t!=null;t=t.parent)if(t==p)return true;return false;}
  public Transform Find(string n){var names=n.Split('/');Transform t=this;foreach(var name in names){t=GameObject.All.FirstOrDefault(g=>!g.destroyed&&g.transform.parent==t&&g.name==name)?.transform;if(t==null)return null;}return t;}
 }
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new(1,1,1);public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);}
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Quaternion {public static Quaternion identity=>new();}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color black=>new(0,0,0);public static Color operator*(Color c,float f)=>new(c.r*f,c.g*f,c.b*f,c.a);}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Abs(float v)=>Math.Abs(v);}
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int v){}}
 public enum RuntimeInitializeLoadType {SubsystemRegistration,AfterSceneLoad}public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public static class Time {public static float unscaledTime;}
 public static class Application {public static bool isBatchMode;}
 public static class SystemInfo {public static Rendering.GraphicsDeviceType graphicsDeviceType;}
 public class Material:Object {public bool Emission;public bool IsKeywordEnabled(string s)=>Emission;}
 public class Renderer:Component {public bool enabled=true,receiveShadows;public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;}
 public class MeshRenderer:Renderer {}public class MeshFilter:Component {public Mesh sharedMesh;}
 public class Mesh:Object {public static int Created;public Mesh(){Created++;}public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles;public Rendering.IndexFormat indexFormat;public void RecalculateBounds(){}public void RecalculateTangents(){}}
 public class TextAsset:Object {public byte[] bytes;}
 public static class Resources {public static byte[] Source;public static int Loads;public static T Load<T>(string name) where T:class {Loads++;return new TextAsset{bytes=Source} as T;}}
 public static class JsonUtility {public static T FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,new JsonSerializerOptions{IncludeFields=true});}
 public static class Debug {public static List<string> Warnings=new();public static void LogWarning(string s)=>Warnings.Add(s);}
}
namespace UnityEngine.Rendering {public enum GraphicsDeviceType {Other,Null}public enum IndexFormat {UInt16,UInt32}public enum ShadowCastingMode {Off,On}}
namespace Alterverse.Multiplayer {public static class DedicatedServerNetworkRuntime {public static bool Requested;}}
namespace Alterverse.Combat {public class TeamMember:UnityEngine.Component {public int Team;}public class DefenseBunker:UnityEngine.Component {}public class DefenseTrench:UnityEngine.Component {}public class CentralHealingPickup:UnityEngine.Component {}}
namespace Alterverse.Visuals {
 public class RealityCoreShell:UnityEngine.Component {}public class ProductionBaseEnvironment:UnityEngine.Component {}public class HealingZoneMarker:UnityEngine.Component {}
 public static class StylizedArtDirection {public static UnityEngine.Color ZeroEnergy=new(.6f,.2f,.9f),LumenEnergy=new(.1f,.8f,1);}
 public static class LunarSurfaceField {public static UnityEngine.Vector3 WorldSurface(float x,float z)=>new(x,.01f,z);}
 public static class RuntimeShaderLibrary {public static bool Available=true;public static UnityEngine.Material Create(UnityEngine.Color c,bool unlit,float metal,float smooth,UnityEngine.Color emission)=>Available?new():null;}
}
