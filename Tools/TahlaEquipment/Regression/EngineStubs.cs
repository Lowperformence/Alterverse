using System;
using System.Collections.Generic;
using NQ = System.Numerics.Quaternion;
using NV = System.Numerics.Vector3;
namespace UnityEngine {
 [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent:Attribute {}
 [AttributeUsage(AttributeTargets.Class)] public class DefaultExecutionOrder:Attribute { public DefaultExecutionOrder(int n){} }
 public static class Time { public static float time, deltaTime=1f/60f, unscaledTime, unscaledDeltaTime=1f/60f, timeScale=1f; }
 public static class Debug { public static void Log(string s,object o=null){} public static void LogWarning(string s,object o=null){} public static void LogError(string s,object o=null){throw new Exception(s);} }
 public static class Mathf {
  public static int Max(int a,int b){return Math.Max(a,b);} public static int RoundToInt(float a){return (int)Math.Round(a);} public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);} public static float InverseLerp(float a,float b,float x){return Clamp01((x-a)/(b-a));} public const float PI=(float)Math.PI, Deg2Rad=PI/180f;
  public static float Clamp(float v,float a,float b){return Math.Max(a,Math.Min(b,v));}
  public static float Clamp01(float v){return Clamp(v,0,1);} public static float Abs(float v){return Math.Abs(v);}
  public static float Sign(float v){return v>=0?1:-1;} public static float Sin(float v){return (float)Math.Sin(v);}
  public static float Cos(float v){return (float)Math.Cos(v);} public static float Pow(float a,float b){return (float)Math.Pow(a,b);}
  public static float Sqrt(float v){return (float)Math.Sqrt(v);} public static float Max(float a,float b){return Math.Max(a,b);}
  public static float Min(float a,float b){return Math.Min(a,b);} public static float Lerp(float a,float b,float t){return a+(b-a)*Clamp01(t);}
  public static float MoveTowards(float v,float target,float delta){return Abs(target-v)<=delta?target:v+Sign(target-v)*delta;}
 }
 public struct Vector3 {
  public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;}
  public static Vector3 zero=>new Vector3(); public static Vector3 one=>new Vector3(1,1,1);
  public static Vector3 up=>new Vector3(0,1,0); public static Vector3 forward=>new Vector3(0,0,1); public static Vector3 right=>new Vector3(1,0,0);
  public float sqrMagnitude=>x*x+y*y+z*z; public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public Vector3 normalized=>magnitude>1e-10f?this/magnitude:zero; public void Normalize(){this=normalized;}
  public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
  public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
  public static Vector3 operator -(Vector3 a){return a*-1f;} public static Vector3 operator *(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);}
  public static Vector3 operator *(float b,Vector3 a){return a*b;} public static Vector3 operator /(Vector3 a,float b){return a*(1f/b);}
  public static float Dot(Vector3 a,Vector3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
  public static Vector3 Cross(Vector3 a,Vector3 b){return new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
  public static float Distance(Vector3 a,Vector3 b){return (a-b).magnitude;}
  public static Vector3 Lerp(Vector3 a,Vector3 b,float t){return a+(b-a)*Mathf.Clamp01(t);}
  public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n){return a-n*Dot(a,n)/n.sqrMagnitude;}
  public static Vector3 Scale(Vector3 a,Vector3 b){return new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);}
  internal NV N=>new NV(x,y,z); internal static Vector3 From(NV v){return new Vector3(v.X,v.Y,v.Z);}
 }
 public struct Quaternion {
  public float x,y,z,w; public Quaternion(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}
  public Vector3 eulerAngles=>Vector3.zero; public static Quaternion LookRotation(Vector3 f){return LookRotation(f,Vector3.up);} public static Quaternion identity=>new Quaternion(0,0,0,1); internal NQ N=>new NQ(x,y,z,w);
  internal static Quaternion From(NQ q){q=NQ.Normalize(q);return new Quaternion(q.X,q.Y,q.Z,q.W);}
  public static Quaternion operator *(Quaternion a,Quaternion b){return From(a.N*b.N);}
  public static Vector3 operator *(Quaternion a,Vector3 v){return Vector3.From(NV.Transform(v.N,NQ.Normalize(a.N)));}
  public static Quaternion Inverse(Quaternion q){return From(NQ.Inverse(q.N));}
  public static Quaternion AngleAxis(float angle,Vector3 axis){return axis.sqrMagnitude<1e-12f?identity:From(NQ.CreateFromAxisAngle(axis.normalized.N,angle*Mathf.Deg2Rad));}
  // Unity's Euler convention applies Z, then X, then Y.
  public static Quaternion Euler(float x,float y,float z){return AngleAxis(y,Vector3.up)*AngleAxis(x,Vector3.right)*AngleAxis(z,Vector3.forward);}
  public static Quaternion Slerp(Quaternion a,Quaternion b,float t){return From(NQ.Slerp(a.N,b.N,Mathf.Clamp01(t)));}
  public static float Angle(Quaternion a,Quaternion b){double dot=(double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z+(double)a.w*b.w;
   double na=Math.Sqrt((double)a.x*a.x+(double)a.y*a.y+(double)a.z*a.z+(double)a.w*a.w),nb=Math.Sqrt((double)b.x*b.x+(double)b.y*b.y+(double)b.z*b.z+(double)b.w*b.w);
   return (float)(2*Math.Acos(Math.Min(1,Math.Abs(dot/(na*nb))))*180/Math.PI);}
  public static Quaternion RotateTowards(Quaternion a,Quaternion b,float delta){float angle=Angle(a,b);return Slerp(a,b,angle<1e-8f?1:Math.Min(1,delta/angle));}
  public static Quaternion FromToRotation(Vector3 from,Vector3 to){Vector3 a=from.normalized,b=to.normalized;float d=Vector3.Dot(a,b);
   if(d>.999999f)return identity;if(d<-.999999f){Vector3 axis=Vector3.Cross(a,Vector3.right);if(axis.sqrMagnitude<1e-6f)axis=Vector3.Cross(a,Vector3.up);return AngleAxis(180,axis);}
   Vector3 c=Vector3.Cross(a,b);return From(new NQ(c.x,c.y,c.z,1+d));}
  public static Quaternion LookRotation(Vector3 f,Vector3 u){f=f.normalized;Vector3 r=Vector3.Cross(u,f).normalized;u=Vector3.Cross(f,r);
   var m=new System.Numerics.Matrix4x4(r.x,r.y,r.z,0,u.x,u.y,u.z,0,f.x,f.y,f.z,0,0,0,0,1);return From(NQ.CreateFromRotationMatrix(m));}
 }
 public class Component:Object { public GameObject gameObject; public Transform transform=>gameObject.transform;
  public T GetComponent<T>() where T:class{return gameObject.GetComponent<T>();}
  public T GetComponentInChildren<T>() where T:class{return gameObject.GetComponentInChildren<T>();}
  public T[] GetComponentsInChildren<T>(bool includeInactive=false) where T:class{return gameObject.GetComponentsInChildren<T>(includeInactive);}
 }
 public class MonoBehaviour:Component {public bool isActiveAndEnabled=true; public bool enabled=true; public new static T FindAnyObjectByType<T>() where T:class {return Object.FindAnyObjectByType<T>();}}
 public class GameObject:Object {
  private List<Component> components=new List<Component>(); public Transform transform;public int layer;public string name;public bool activeInHierarchy=true;public void SetActive(bool active){activeInHierarchy=active;}
  public GameObject(string name){this.name=name;transform=new Transform{name=name,gameObject=this};components.Add(transform);}
  public T AddComponent<T>() where T:Component,new(){T c=new T{gameObject=this};components.Add(c); var awake=typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);if(awake!=null)awake.Invoke(c,null);return c;}
  public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T)return c as T;return null;}
  public T GetComponentInChildren<T>() where T:class{var a=GetComponentsInChildren<T>();return a.Length>0?a[0]:null;}
  public T[] GetComponentsInChildren<T>(bool inactive=false) where T:class {var l=new List<T>();foreach(var c in components)if(c is T)l.Add(c as T);foreach(var child in transform.children)l.AddRange(child.gameObject.GetComponentsInChildren<T>(inactive));return l.ToArray();}
 }
 public class Transform:Component {
  public string name;public readonly List<Transform> children=new List<Transform>();private Transform p;
  public void SetParent(Transform parent,bool worldPositionStays=false){this.parent=parent;} public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;} public Transform parent {get{return p;}set{if(p!=null)p.children.Remove(this);p=value;if(p!=null)p.children.Add(this);}}
  private Vector3 lp=Vector3.zero,ls=Vector3.one;private Quaternion lr=Quaternion.identity;
  public int rotationWrites,positionWrites,scaleWrites;
  public Vector3 localPosition {get{return lp;}set{lp=value;positionWrites++;}} public Vector3 localScale {get{return ls;}set{ls=value;scaleWrites++;}}
  public Quaternion localRotation {get{return lr;}set{lr=Quaternion.From(value.N);rotationWrites++;}}
  public Vector3 position {get{return p==null?lp:p.TransformPoint(lp);}set{lp=p==null?value:p.InverseTransformPoint(value);positionWrites++;}}
  public Quaternion rotation {get{return p==null?lr:p.rotation*lr;}set{lr=p==null?value:Quaternion.Inverse(p.rotation)*value;rotationWrites++;}}
  public Vector3 right=>rotation*Vector3.right;public Vector3 up=>rotation*Vector3.up;public Vector3 forward=>rotation*Vector3.forward;
  public Vector3 TransformPoint(Vector3 v){v=lr*Vector3.Scale(ls,v)+lp;return p==null?v:p.TransformPoint(v);}
  public Vector3 InverseTransformPoint(Vector3 v){if(p!=null)v=p.InverseTransformPoint(v);v=Quaternion.Inverse(lr)*(v-lp);return new Vector3(v.x/ls.x,v.y/ls.y,v.z/ls.z);}
  public Vector3 TransformVector(Vector3 v){v=lr*Vector3.Scale(ls,v);return p==null?v:p.TransformVector(v);}
  public Vector3 InverseTransformVector(Vector3 v){if(p!=null)v=p.InverseTransformVector(v);v=Quaternion.Inverse(lr)*v;return new Vector3(v.x/ls.x,v.y/ls.y,v.z/ls.z);}
  public Vector3 TransformDirection(Vector3 v){return rotation*v;}public Vector3 InverseTransformDirection(Vector3 v){return Quaternion.Inverse(rotation)*v;}
  public Transform Find(string n){foreach(var c in children)if(c.name==n)return c;return null;}
  public void ClearWrites(){rotationWrites=positionWrites=scaleWrites=0;foreach(var c in children)c.ClearWrites();}
 }
}

namespace UnityEngine {
 public class Object { public static T Instantiate<T>(T value) where T:Object { if(value is Mesh)return (value as Mesh).Clone() as T;throw new NotImplementedException(); } public static void Destroy(Object o){} public static T FindAnyObjectByType<T>() where T:class{return null;} }
 public enum RuntimeInitializeLoadType { SubsystemRegistration,AfterSceneLoad }
 [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){} }
 public static class Application {public static bool isMobilePlatform=true,isFocused=true; public static int targetFrameRate=60;}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color black=>new Color(0,0,0,1); public static Color operator *(Color c,float s)=>new Color(c.r*s,c.g*s,c.b*s,c.a); public static Color white=>new Color(1,1,1,1); }
 public class Shader:Object {public string name="DimensionsFront/HeroEnergy";public bool isSupported=true;private static Dictionary<string,int> ids=new Dictionary<string,int>();public static int PropertyToID(string n){if(!ids.ContainsKey(n))ids[n]=ids.Count+1;return ids[n];}}
 public class Material:Object {public string name;public Shader shader;public Material(Shader s){shader=s;} public void EnableKeyword(string k){} }
 public static class Resources {public static Dictionary<string,object> Assets=new Dictionary<string,object>();public static T Load<T>(string path) where T:class {if(Assets.ContainsKey(path))return Assets[path] as T;if(typeof(T)==typeof(Material))return new Material(new Shader()) as T;if(typeof(T)==typeof(Shader))return new Shader() as T;return null;}}
 public class MaterialPropertyBlock {private Dictionary<int,object> data=new Dictionary<int,object>();public void SetFloat(int k,float v){data[k]=v;}public void SetColor(int k,Color v){data[k]=v;}public float GetFloat(int k){return data.ContainsKey(k)?(float)data[k]:0;}public Color GetColor(int k){return data.ContainsKey(k)?(Color)data[k]:new Color();}}
 public class Mesh:Object {
 public string name;public Vector3[] vertices=new Vector3[0],normals=new Vector3[0];public Vector2[] uv;public BoneWeight[] boneWeights;public Matrix4x4[] bindposes;
 public int vertexCount=>vertices.Length;public bool isReadable=true;public int subMeshCount;public UnityEngine.Rendering.IndexFormat indexFormat;
 public Bounds bounds;private Dictionary<int,int[]> triangles=new Dictionary<int,int[]>();
 public int[] GetTriangles(int sub)=>triangles.ContainsKey(sub)?(int[])triangles[sub].Clone():new int[0];
 public void SetTriangles(int[] data,int sub,bool calculateBounds=true){triangles[sub]=(int[])data.Clone();}
 public void SetTriangles(List<int> data,int sub,bool calculateBounds=true){SetTriangles(data.ToArray(),sub,calculateBounds);}
 public void RecalculateBounds(){if(vertices.Length==0)return;bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);}
 public void RecalculateTangents(){}
 public Mesh Clone(){var m=(Mesh)MemberwiseClone();m.triangles=new Dictionary<int,int[]>(triangles);return m;}
 }
 public class MeshFilter:Component {public Mesh sharedMesh;}
 public class MeshRenderer:Renderer {public Material sharedMaterial;public bool enabled,receiveShadows; public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;public UnityEngine.Rendering.LightProbeUsage lightProbeUsage;public UnityEngine.Rendering.ReflectionProbeUsage reflectionProbeUsage;public void SetPropertyBlock(MaterialPropertyBlock b){} }
}
namespace UnityEngine.Rendering {public enum IndexFormat{UInt16,UInt32} public enum ShadowCastingMode{Off,On} public enum LightProbeUsage{Off} public enum ReflectionProbeUsage{Off}}
namespace Alterverse.Characters {public enum HeroId {Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy} public class HeroIdentity:UnityEngine.MonoBehaviour{public HeroId Hero;}}
namespace Alterverse.Combat {public class Health:UnityEngine.MonoBehaviour {public bool IsDead;public float Shield=100;}}
namespace Alterverse.Performance {public enum MobileQuality {Battery,Balanced,Performance} public static class MobilePerformanceProfile {public static MobileQuality Current=MobileQuality.Performance;} }
namespace Alterverse.Visuals {public static class NerokAnchorMaterial {public static UnityEngine.Material Create(bool b){return new UnityEngine.Material(new UnityEngine.Shader());}} }
namespace Alterverse.Visuals {
 public static class RuntimeShaderLibrary {public static UnityEngine.Material Create(UnityEngine.Color color,bool unlit=false,float metal=0,float smooth=.2f,UnityEngine.Color emission=default(UnityEngine.Color)){return new UnityEngine.Material(new UnityEngine.Shader());}}
 public class HeroVisualMotion:UnityEngine.MonoBehaviour {public UnityEngine.Vector3 ReleasePoint=>transform.position;}
}

namespace UnityEngine {
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Vector3Int{public int x,y,z;public Vector3Int(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
 public struct BoneWeight{public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;public float weight0,weight1,weight2,weight3;}
 public struct Bounds{public Vector3 min,max;public Bounds(Vector3 c,Vector3 size){min=c-size*.5f;max=c+size*.5f;}public void Encapsulate(Vector3 p){min=new Vector3(Math.Min(min.x,p.x),Math.Min(min.y,p.y),Math.Min(min.z,p.z));max=new Vector3(Math.Max(max.x,p.x),Math.Max(max.y,p.y),Math.Max(max.z,p.z));}}
 public struct Matrix4x4{
 public System.Numerics.Matrix4x4 value;
 public static Matrix4x4 identity=>new Matrix4x4{value=System.Numerics.Matrix4x4.Identity};
 public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new Matrix4x4{value=System.Numerics.Matrix4x4.CreateScale(s.x,s.y,s.z)*System.Numerics.Matrix4x4.CreateFromQuaternion(new System.Numerics.Quaternion(q.x,q.y,q.z,q.w))*System.Numerics.Matrix4x4.CreateTranslation(p.x,p.y,p.z)};
 public float determinant=>value.GetDeterminant();
 public Matrix4x4 inverse{get{System.Numerics.Matrix4x4 m;System.Numerics.Matrix4x4.Invert(value,out m);return new Matrix4x4{value=m};}}
 public Matrix4x4 transpose=>new Matrix4x4{value=System.Numerics.Matrix4x4.Transpose(value)};
 public Vector3 MultiplyPoint3x4(Vector3 p){var v=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(p.x,p.y,p.z),value);return new Vector3(v.X,v.Y,v.Z);}
 public Vector3 MultiplyVector(Vector3 p){var v=System.Numerics.Vector3.TransformNormal(new System.Numerics.Vector3(p.x,p.y,p.z),value);return new Vector3(v.X,v.Y,v.Z);}
 public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b)=>new Matrix4x4{value=b.value*a.value};
 }
 public class Renderer:Component{public string name{get{return gameObject.name;}set{gameObject.name=value;transform.name=value;}}public bool enabled=true,receiveShadows;public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;public Material[] sharedMaterials;}
 public enum SkinQuality{Bone1}
 public class SkinnedMeshRenderer:Renderer{public Mesh sharedMesh;public Transform[] bones;public Transform rootBone;public SkinQuality quality;public Bounds localBounds;public bool updateWhenOffscreen;}
 public class TextAsset:Object{public byte[] bytes;}
 public static class JsonUtility{
 public static T FromJson<T>(string json){return (T)ConvertTo(Parse(json),typeof(T));}
 public static object Parse(string json){return new Parser(json).Value();}
 sealed class Parser{
 readonly string text;int index;public Parser(string text){this.text=text;}
 void Space(){while(index<text.Length&&char.IsWhiteSpace(text[index]))index++;}
 string String(){Space();if(text[index++]!='"')throw new Exception("JSON string expected");var b=new System.Text.StringBuilder();while(index<text.Length){char c=text[index++];if(c=='"')return b.ToString();if(c=='\\'){c=text[index++];if(c=='u'){b.Append((char)int.Parse(text.Substring(index,4),System.Globalization.NumberStyles.HexNumber));index+=4;continue;}if(c=='n')c='\n';else if(c=='r')c='\r';else if(c=='t')c='\t';}b.Append(c);}throw new Exception("JSON string unterminated");}
 public object Value(){Space();char c=text[index];if(c=='{'){index++;Space();var m=new Dictionary<string,object>();if(text[index]=='}'){index++;return m;}while(true){string key=String();Space();if(text[index++]!=':')throw new Exception("JSON colon expected");m[key]=Value();Space();c=text[index++];if(c=='}')return m;if(c!=',')throw new Exception("JSON object comma expected");}}if(c=='['){index++;Space();var a=new List<object>();if(text[index]==']'){index++;return a.ToArray();}while(true){a.Add(Value());Space();c=text[index++];if(c==']')return a.ToArray();if(c!=',')throw new Exception("JSON array comma expected");}}if(c=='"')return String();if(c=='t'){index+=4;return true;}if(c=='f'){index+=5;return false;}if(c=='n'){index+=4;return null;}int start=index;while(index<text.Length&&"-+.0123456789eE".IndexOf(text[index])>=0)index++;return double.Parse(text.Substring(start,index-start),System.Globalization.CultureInfo.InvariantCulture);}
 }
 public static object ConvertTo(object value,Type type){if(value==null)return null;if(type.IsArray){var list=(object[])value;var array=Array.CreateInstance(type.GetElementType(),list.Length);for(int i=0;i<list.Length;i++)array.SetValue(ConvertTo(list[i],type.GetElementType()),i);return array;}if(type.IsPrimitive||type==typeof(string))return Convert.ChangeType(value,type,System.Globalization.CultureInfo.InvariantCulture);var instance=Activator.CreateInstance(type,true);var map=(Dictionary<string,object>)value;foreach(var field in type.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public)){object fieldValue;if(map.TryGetValue(field.Name,out fieldValue))field.SetValue(instance,ConvertTo(fieldValue,field.FieldType));}return instance;}
 }
}
namespace Alterverse.Visuals{public static class HeroVisualDiagnostics{public static void Trace(string text,UnityEngine.Object context){}}}
