// Test-only engine/service substitutes. Never included in Unity Assets.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
namespace UnityEngine {
 public class Object { public static void Destroy(Object o){} public static void DontDestroyOnLoad(Object o){} public static T FindAnyObjectByType<T>() where T:class=>null; }
 public class MonoBehaviour:Object { public GameObject gameObject=new GameObject(); public bool isActiveAndEnabled=true; public void StartCoroutine(IEnumerator e){} public void StopAllCoroutines(){} }
 public class GameObject:Object { public GameObject(string n=""){} public T AddComponent<T>() where T:new(){var o=new T();typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.Invoke(o,null);return o;} }
 public class TextAsset {public string text;}
 public static class Resources {
  public static TextAsset AccountConfig, RankedConfig;
  public static T Load<T>(string s) where T:class => (s=="Social/SafeIdentityCatalog" ? new TextAsset{text=System.IO.File.ReadAllText(Environment.GetEnvironmentVariable("DF_TEST_IDENTITY_CATALOG"))} : s=="Commerce/LegacyProfileKeys" ? null : s=="RankedAvailability" ? RankedConfig : AccountConfig) as T;
 }
 public static class PlayerPrefs {
  static readonly Dictionary<string,object> values=new();
  public static bool HasKey(string key)=>values.ContainsKey(key);
  public static string GetString(string key,string fallback="")=>values.TryGetValue(key,out var v)&&v is string s?s:fallback;
  public static int GetInt(string key,int fallback=0)=>values.TryGetValue(key,out var v)&&v is int n?n:fallback;
  public static float GetFloat(string key,float fallback=0)=>values.TryGetValue(key,out var v)&&v is float n?n:fallback;
  public static void SetString(string key,string value)=>values[key]=value;
  public static void SetInt(string key,int value)=>values[key]=value;
  public static void SetFloat(string key,float value)=>values[key]=value;
  public static void DeleteKey(string key)=>values.Remove(key);
  public static void Save(){}
 }
 public static class JsonUtility {
#if MONO_FIXTURE
 public static string ToJson(object o){using var stream=new System.IO.MemoryStream();new System.Runtime.Serialization.Json.DataContractJsonSerializer(o.GetType()).WriteObject(stream,o);return System.Text.Encoding.UTF8.GetString(stream.ToArray());}
 // Unity defaults missing fields; Serializable DataContract JSON would require
 // every field. Read JSON through Mono's XML bridge and populate public fields.
 public static T FromJson<T>(string s){using var reader=System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(System.Text.Encoding.UTF8.GetBytes(s),System.Xml.XmlDictionaryReaderQuotas.Max);var doc=new System.Xml.XmlDocument();doc.Load(reader);return (T)ReadNode(typeof(T),doc.DocumentElement);}
 static object ReadNode(Type type,System.Xml.XmlElement node){if(node.GetAttribute("type")=="null")return null;if(type==typeof(string))return node.InnerText;if(type.IsEnum)return Enum.Parse(type,node.InnerText);if(type.IsPrimitive)return Convert.ChangeType(node.InnerText,type,System.Globalization.CultureInfo.InvariantCulture);if(type.IsArray){var array=Array.CreateInstance(type.GetElementType(),node.ChildNodes.Count);for(int i=0;i<array.Length;i++)array.SetValue(ReadNode(type.GetElementType(),(System.Xml.XmlElement)node.ChildNodes[i]),i);return array;}var value=Activator.CreateInstance(type,true);foreach(var field in type.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)){var child=node[field.Name];if(child!=null)field.SetValue(value,ReadNode(field.FieldType,child));}return value;}
#else
 static System.Text.Json.JsonSerializerOptions opts=new(){IncludeFields=true}; public static string ToJson(object o)=>System.Text.Json.JsonSerializer.Serialize(o,o.GetType(),opts); public static T FromJson<T>(string s)=>System.Text.Json.JsonSerializer.Deserialize<T>(s,opts);
#endif
 }
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int n){}}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public enum RuntimeInitializeLoadType {BeforeSceneLoad,AfterSceneLoad}
 public static class Time {public static float unscaledTime,realtimeSinceStartup;}
 public static class Debug {public static void Log(object o){}public static void LogWarning(object o){}public static void LogError(object o){}}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);public static int Clamp(int n,int lo,int hi)=>Math.Max(lo,Math.Min(hi,n));public static int Max(int a,int b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Clamp01(float a)=>Math.Max(0,Math.Min(1,a));}
 public struct Vector2 {public float x,y;public static Vector2 zero=>new();}
 public struct Vector3 {public Vector3(float x,float y,float z){}public static Vector3 one=>new(); public static Vector3 operator*(Vector3 v,float f)=>v;}
 public struct Quaternion {public static Quaternion identity=>new();}
 public struct Matrix4x4 {public static Matrix4x4 TRS(Vector3 v,Quaternion q,Vector3 s)=>new();}
 public struct Rect {public float x,y,width,height;public float yMax=>y+height; public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}}
 public static class Screen {public static Rect safeArea=>new(0,0,1280,720);public static int height=720;}
 public class GUIStyle {public int fontSize;public bool wordWrap;public GUIStyle(){}public GUIStyle(GUIStyle source){fontSize=source.fontSize;wordWrap=source.wordWrap;}public float CalcHeight(GUIContent c,float w)=>30;}public class GUIContent {public GUIContent(string s){}}public class Texture2D {public static Texture2D whiteTexture=new();}
 public static class GUI {public static bool enabled=true;public static bool Toggle(Rect r,bool v,string s)=>v;public static void Label(Rect r,string text,GUIStyle style){}public static string TextField(Rect r,string v,int max,GUIStyle style)=>v;public static Matrix4x4 matrix;public static int depth;public static Vector2 BeginScrollView(Rect r,Vector2 s,Rect inner)=>s;public static void EndScrollView(){}}
}
namespace UnityEngine.Networking {
 public class DownloadHandlerBuffer {public string text="{}";}
 public class UploadHandlerRaw {public readonly byte[] data;public UploadHandlerRaw(byte[] b){data=b;}}
 public class UnityWebRequestAsyncOperation {public volatile bool isDone=true;}
 public class UnityWebRequest:IDisposable {public static Func<UnityWebRequest,UnityWebRequestAsyncOperation> Handler;public readonly string url,method;public readonly Dictionary<string,string> Headers=new();public enum Result {Success,ConnectionError};public Result result=Result.Success;public long responseCode=200;public string error;public int timeout;public UploadHandlerRaw uploadHandler;public DownloadHandlerBuffer downloadHandler;public UnityWebRequest(string u,string m){url=u;method=m;}public static UnityWebRequest Delete(string u)=>new(u,"DELETE");public static string EscapeURL(string s)=>Uri.EscapeDataString(s);public void SetRequestHeader(string n,string v){Headers[n]=v;}public UnityWebRequestAsyncOperation SendWebRequest()=>Handler?.Invoke(this)??new();public void Dispose(){}}
}

