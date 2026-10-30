// API/lifecycle fixtures only: these do not emulate Unity's importer or GPU renderer.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace UnityEngine
{
    [Flags] public enum HideFlags { None=0,DontSaveInEditor=4,DontSaveInBuild=16,HideAndDontSave=61 }
    public class Object
    {
        public static readonly List<Object> All=new();public string name;public HideFlags hideFlags;public bool destroyed;
        public Object(){All.Add(this);}
        public static bool operator==(Object a,Object b)=>ReferenceEquals(a,b)||((ReferenceEquals(a,null)||a.destroyed)&&(ReferenceEquals(b,null)||b.destroyed));
        public static bool operator!=(Object a,Object b)=>!(a==b);
        public override bool Equals(object o)=>ReferenceEquals(this,o);public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static void DestroyImmediate(Object obj)
        {
            if(ReferenceEquals(obj,null)||obj.destroyed)return;obj.destroyed=true;
            if(obj is GameObject go){foreach(var child in go.transform.Children.ToArray())DestroyImmediate(child.gameObject);foreach(var part in go.Components)part.destroyed=true;}
        }
    }
    public class Component:Object { public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>(); }
    public class Behaviour:Component { public bool enabled=true; }
    public class MonoBehaviour:Behaviour { }
    public class Transform:Component
    {
        public readonly List<Transform> Children=new();public Transform parent;
        public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;
        public Vector3 position {get=>parent==null?localPosition:parent.TransformPoint(localPosition);set=>localPosition=parent==null?value:parent.InversePoint(value);}
        public Quaternion rotation {get=>parent==null?localRotation:parent.rotation*localRotation;set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value;}
        public Vector3 TransformPoint(Vector3 p)=>position+rotation*Vector3.Scale(p,WorldScale);
        public Vector3 WorldScale=>parent==null?localScale:Vector3.Scale(parent.WorldScale,localScale);
        public Vector3 InversePoint(Vector3 p){var v=Quaternion.Inverse(rotation)*(p-position);var s=WorldScale;return new(v.x/s.x,v.y/s.y,v.z/s.z);}
        public void SetParent(Transform p,bool keepWorld){var pos=position;parent?.Children.Remove(this);parent=p;p?.Children.Add(this);if(keepWorld)position=pos;}
    }
    public class GameObject:Object
    {
        public readonly List<Component> Components=new();public Transform transform;public bool activeSelf=true;public int layer;
        public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
        public GameObject(string n){name=n;transform=new Transform{gameObject=this};Components.Add(transform);}
        public static GameObject Find(string n)=>All.OfType<GameObject>().LastOrDefault(x=>!x.destroyed&&x.activeInHierarchy&&x.name==n);
        public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
        public T GetComponent<T>() where T:Component=>Components.OfType<T>().FirstOrDefault(x=>!x.destroyed);
    }
    public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
    public struct Vector3
    {
        public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}
        public static Vector3 one=>new(1,1,1);public static Vector3 up=>new(0,1,0);public static Vector3 forward=>new(0,0,1);
        public float magnitude=>MathF.Sqrt(x*x+y*y+z*z);public Vector3 normalized=>this*(1/MathF.Max(.000001f,magnitude));
        public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator*(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
        public static Vector3 Scale(Vector3 a,Vector3 b)=>new(a.x*b.x,a.y*b.y,a.z*b.z);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
    }
    public struct Quaternion
    {
        public System.Numerics.Quaternion Value;public static Quaternion identity=>new(){Value=System.Numerics.Quaternion.Identity};
        public static Quaternion Euler(float x,float y,float z)=>new(){Value=System.Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180)};
        public static Quaternion FromToRotation(Vector3 a,Vector3 b)=>identity;
        public static Quaternion Inverse(Quaternion q)=>new(){Value=System.Numerics.Quaternion.Inverse(q.Value)};
        public static Quaternion operator*(Quaternion a,Quaternion b)=>new(){Value=a.Value*b.Value};
        public static Vector3 operator*(Quaternion a,Vector3 b){var v=System.Numerics.Vector3.Transform(new(b.x,b.y,b.z),a.Value);return new(v.X,v.Y,v.Z);}
    }
    public struct Color
    {
        public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white=>new(1,1,1);public Color gamma=>this;
        public static Color Lerp(Color a,Color b,float t)=>new(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t);
    }
    public static class Mathf { public const float PI=MathF.PI;public static float Sin(float v)=>MathF.Sin(v);public static float Cos(float v)=>MathF.Cos(v);public static float Min(float a,float b)=>MathF.Min(a,b); }
    public class Shader:Object
    {
        public static string Missing;public bool isSupported=true;
        public static Shader Find(string n)=>n==Missing?null:new(){name=n};
    }
    public class Texture:Object { }
    public class Texture2D:Texture
    {
        public FilterMode filterMode;public TextureWrapMode wrapMode;public int width,height;
        public Texture2D(int w,int h,TextureFormat f,bool m){width=w;height=h;}
        public void SetPixel(int x,int y,Color c){}public void Apply(bool a,bool b){}
    }
    public enum FilterMode {Point}public enum TextureWrapMode {Repeat,Clamp}public enum TextureFormat {RGBA32}
    public class Material:Object
    {
        public Shader shader;public static int SkyPasses=1;public int passCount=>shader.name=="Skybox/Panoramic"?SkyPasses:1;
        public readonly Dictionary<string,object> Properties=new();public Material(Shader s){shader=s;}
        public bool HasProperty(string p)=>true;public void SetTexture(string p,Texture2D t){Properties[p]=t;}
        public void SetFloat(string p,float v){Properties[p]=v;}public void SetColor(string p,Color c){Properties[p]=c;}public void SetInt(string p,int v){Properties[p]=v;}
    }
    public class Mesh:Object
    {
        public List<Vector3> Vertices=new(),Normals=new();public List<int> Triangles=new();public static bool Throw;
        public void SetVertices(List<Vector3> v){if(Throw)throw new Exception("Injected mesh allocation failure");Vertices=v;}
        public void SetNormals(List<Vector3> v){Normals=v;}public void SetUVs(int i,List<Vector2> v){}public void SetTriangles(List<int> v,int i){Triangles=v;}
        public void RecalculateBounds(){}public void RecalculateNormals(){}public void RecalculateTangents(){}
    }
    public class MeshFilter:Component {public Mesh sharedMesh;}
    public class MeshRenderer:Component {public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;public Rendering.LightProbeUsage lightProbeUsage;public Rendering.ReflectionProbeUsage reflectionProbeUsage;}
    public enum CameraClearFlags {Skybox,SolidColor,Depth}
    public class Camera:Behaviour
    {
        public static Camera main;public bool orthographic;public Texture targetTexture;public int cullingMask=1234;public float farClipPlane=1000,nearClipPlane=.3f,fieldOfView=46;
        public CameraClearFlags clearFlags=CameraClearFlags.SolidColor;public Color backgroundColor=new(.1f,.2f,.3f);
    }
    public class Skybox:Behaviour {public Material material;}
    public class GUIContent {public GUIContent(string s){}}
    public class GUIStyle { }public static class GUILayout {public static bool Button(string s)=>false;}
    public static class Debug {public static void LogWarning(object s){} }
}
namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode {Off}public enum LightProbeUsage {Off}public enum ReflectionProbeUsage {Off}
    public static class GraphicsSettings {public static object currentRenderPipeline=new();}
}
namespace UnityEditor
{
    public class MenuItem:Attribute {public MenuItem(string s){}}
    public class EditorWindow:UnityEngine.Object {public UnityEngine.GUIContent titleContent;public UnityEngine.Vector2 minSize;public static T GetWindow<T>() where T:EditorWindow,new()=>new();public void Show(){}public void Repaint(){}}
    public static class EditorApplication
    {
        public static bool isPlaying=true,isCompiling;public static event Action update;public static event Action<PlayModeStateChange> playModeStateChanged;
        public static void Tick()=>update?.Invoke();public static void State(PlayModeStateChange s)=>playModeStateChanged?.Invoke(s);
    }
    public static class AssemblyReloadEvents {public static event Action beforeAssemblyReload;public static void Reload()=>beforeAssemblyReload?.Invoke();}
    public enum PlayModeStateChange {ExitingPlayMode,ExitingEditMode,EnteredPlayMode,EnteredEditMode}
    public enum MessageType {Info,None}
    public static class EditorStyles {public static UnityEngine.GUIStyle boldLabel=new(),wordWrappedLabel=new();}
    public static class EditorGUILayout {public static void LabelField(string a,UnityEngine.GUIStyle b=null){}public static void HelpBox(string a,MessageType m){}public static void Space(){}}
    public static class EditorGUI {public class DisabledScope:IDisposable {public DisabledScope(bool b){}public void Dispose(){}}}
    public static class AssetDatabase {public static readonly Dictionary<string,UnityEngine.Texture2D> Assets=new();public static T LoadAssetAtPath<T>(string p) where T:UnityEngine.Object=>Assets.TryGetValue(p,out var t)?t as T:null;}
    public class AssetImporter { }public class AssetPostprocessor {public string assetPath;public AssetImporter assetImporter;}
    public enum TextureImporterType {Default}public enum TextureImporterShape {Texture2D}public enum TextureImporterAlphaSource {None}public enum TextureImporterCompression {Compressed}
    public class TextureImporter:AssetImporter {public TextureImporterType textureType;public TextureImporterShape textureShape;public bool sRGBTexture,isReadable,mipmapEnabled;public TextureImporterAlphaSource alphaSource;public UnityEngine.TextureWrapMode wrapModeU,wrapModeV;public int maxTextureSize,compressionQuality;public TextureImporterCompression textureCompression;}
}
namespace Alterverse.CameraSystem { public class MobaCamera:UnityEngine.MonoBehaviour { } }
namespace Alterverse.UI {public static class MainMenuController {public static bool IsVisible;}}
namespace Alterverse.Multiplayer {public static class CoopAuthority {public static bool Active;}}
namespace Unity.Netcode {public class NetworkManager {public static NetworkManager Singleton;public bool IsListening;}}
