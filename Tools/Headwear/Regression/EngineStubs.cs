using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object o){if(o is GameObject go)go.transform.SetParent(null,false);}
        public static void DestroyImmediate(Object o)=>Destroy(o);
    }
    public sealed class DisallowMultipleComponent:Attribute{}
    public class Component:Object
    {
        public GameObject gameObject;public Transform transform=>gameObject.transform;
        public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool inactive=true) where T:class=>transform.GetComponentsInChildren<T>(inactive);
    }
    public class MonoBehaviour:Component{}
    public class GameObject:Object
    {
        readonly Dictionary<Type,Component> components=new();public int layer;public Transform transform;
        public GameObject(string n=""){name=n;transform=new Transform{gameObject=this,name=n};}
        public T GetComponent<T>() where T:class=>typeof(T)==typeof(Transform)?transform as T:components.GetValueOrDefault(typeof(T)) as T;
        public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components[typeof(T)]=c;return c;}
        public T[] GetComponentsInChildren<T>(bool inactive=true) where T:class=>transform.GetComponentsInChildren<T>(inactive);
        public void SetActive(bool value){}
    }
    public class Transform:Component
    {
        readonly List<Transform> children=new();
        public Transform parent;public Matrix4x4 worldToLocalMatrix=Matrix4x4.identity,localToWorldMatrix=Matrix4x4.identity;
        public void SetParent(Transform t,bool world){parent?.children.Remove(this);parent=t;parent?.children.Add(this);}
        public Transform Find(string n)=>children.FirstOrDefault(t=>t.name==n);
        public new T[] GetComponentsInChildren<T>(bool inactive=true) where T:class
        {
            var result=new List<T>();var own=GetComponent<T>();if(own!=null)result.Add(own);
            foreach(var child in children)result.AddRange(child.GetComponentsInChildren<T>(inactive));return result.ToArray();
        }
    }
    public struct Vector3
    {
        public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}
        public static Vector3 zero=>new(0,0,0);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
        public static Vector3 Scale(Vector3 a,Vector3 b)=>new(a.x*b.x,a.y*b.y,a.z*b.z);
    }
    public struct Quaternion{public static Quaternion identity=>default;}
    public struct Matrix4x4
    {
        Vector3 scale,position;
        public static Matrix4x4 identity=>TRS(Vector3.zero,Quaternion.identity,new Vector3(1,1,1));
        public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new(){position=p,scale=s};
        public Vector3 MultiplyPoint3x4(Vector3 p)=>Vector3.Scale(p,scale)+position;
        public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b)=>TRS(a.MultiplyPoint3x4(b.position),default,Vector3.Scale(a.scale,b.scale));
    }
    public struct Bounds
    {
        public Vector3 center,size;public Bounds(Vector3 p,Vector3 s){center=p;size=s;}
        public Vector3 extents=>size*.5f;public Vector3 min=>center-extents;public Vector3 max=>center+extents;
        public void Encapsulate(Vector3 p)
        {var a=min;var b=max;var lo=new Vector3(Math.Min(a.x,p.x),Math.Min(a.y,p.y),Math.Min(a.z,p.z));
         var hi=new Vector3(Math.Max(b.x,p.x),Math.Max(b.y,p.y),Math.Max(b.z,p.z));center=(lo+hi)*.5f;size=hi-lo;}
    }
    public struct BoneWeight{public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;public float weight0,weight1,weight2,weight3;}
    public class Mesh:Object
    {
        public int subMeshCount;public Vector3[] vertices=Array.Empty<Vector3>();public BoneWeight[] boneWeights=Array.Empty<BoneWeight>();
        public bool isReadable=true;public Bounds bounds;public int vertexCount=>vertices.Length;
        public readonly Dictionary<int,int[]> Indices=new();
        public void SetVertices(List<Vector3> v){vertices=v.ToArray();}
        public void SetTriangles(List<int> t,int sub){Indices[sub]=t.ToArray();}
        public void RecalculateNormals(){}
        public void RecalculateBounds(){if(vertices.Length==0)return;bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);}
    }
    public class MeshFilter:Component{public Mesh sharedMesh;}
    public class MeshRenderer:Component{public Material[] sharedMaterials;}
    public class SkinnedMeshRenderer:Component{public Mesh sharedMesh;public Transform[] bones;public void BakeMesh(Mesh mesh){mesh.vertices=sharedMesh.vertices;}}
    public class Shader:Object{public static bool Available=true;public static Shader Find(string n)=>Available?new():null;}
    public class Material:Object{public Material(Shader s){}public bool HasProperty(string p)=>true;public void SetColor(string p,Color c){}public void SetFloat(string p,float f){}public void EnableKeyword(string name){}}
    public static class Application{public static bool isPlaying;}
    public class Texture2D{public string Resource;}
    public class TextAsset{public string text;}
    public static class JsonUtility{public static T FromJson<T>(string text)=>System.Text.Json.JsonSerializer.Deserialize<T>(text,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});}
    public struct Color{public Color(float r,float g,float b,float a=1){}}
    public static class Resources
    {
        public static readonly Dictionary<string,int> Loads=new();
        public static T Load<T>(string p) where T:class
        {Loads[p]=Loads.GetValueOrDefault(p)+1;if(typeof(T)==typeof(TextAsset))return new TextAsset{text=System.IO.File.ReadAllText("Assets/Alterverse/Resources/"+p+".json")} as T;return new Texture2D{Resource=p} as T;}
    }
    public static class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Cos(float v)=>(float)Math.Cos(v);public static float Sin(float v)=>(float)Math.Sin(v);
        public static float Sqrt(float v)=>(float)Math.Sqrt(v);
        public static float Abs(float v)=>Math.Abs(v);
        public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Min(float a,float b)=>Math.Min(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
        public static float Floor(float v)=>(float)Math.Floor(v);public static int FloorToInt(float v)=>(int)Math.Floor(v);
        public static int CeilToInt(float v)=>(int)Math.Ceiling(v);
        public static int RoundToInt(float v)=>(int)Math.Round(v,MidpointRounding.ToEven);
        public static int Clamp(int v,int lo,int hi)=>Math.Clamp(v,lo,hi);public static float Clamp(float v,float lo,float hi)=>Math.Clamp(v,lo,hi);
    }
    public static class Debug{public static void Log(object o)=>Console.WriteLine(o);}
}
namespace UnityEditor{public sealed class MenuItem:Attribute{public MenuItem(string label){}}}
namespace Alterverse.Characters
{
    public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy,VoidKnight}
    public sealed class HeroIdentity:UnityEngine.Component{public const int HeroCount=10;public HeroId Hero;}
    public static class PlayerHeroSelection{public static HeroId Selected;public static void Select(HeroId h)=>Selected=h;}
}
namespace Alterverse.Profile
{
    public static class AccountProfilePrefs
    {
        public static Dictionary<string,int> Ints=new();public static Dictionary<string,string> Strings=new();
        public static bool HasKey(string k)=>Ints.ContainsKey(k)||Strings.ContainsKey(k);
        public static int GetInt(string k,int d=0)=>Ints.GetValueOrDefault(k,d);
        public static string GetString(string k,string d="")=>Strings.GetValueOrDefault(k,d);
        public static void SetInt(string k,int v)=>Ints[k]=v;public static void SetString(string k,string v)=>Strings[k]=v;
        public static void Save(){}
    }
    public static class ProfileTransaction
    {
        public static readonly List<Write[]> Commits=new();
        public sealed class Write{public string key,text;public int number;public bool isText;
            public Write(string k,int v){key=k;number=v;}public Write(string k,string v){key=k;text=v;isText=true;}}
        public static void Recover(){}
        public static void Commit(params Write[] writes){Commits.Add(writes);foreach(var w in writes)
            if(w.isText)AccountProfilePrefs.SetString(w.key,w.text);else AccountProfilePrefs.SetInt(w.key,w.number);}
        public static int Add(int a,int b)=>(int)Math.Min(int.MaxValue,Math.Max(0L,(long)a+b));
    }
    public enum BattleItemType{None,Heal,Shield,Speed,TeamHeal}
    public static class BattleItemInventory
    {public static string Name(BattleItemType i)=>i.ToString();public static string StorageKey(BattleItemType i)=>"DF_BattleItem_"+(int)i;
     public static int Count(BattleItemType i)=>AccountProfilePrefs.GetInt(StorageKey(i));}
    public static class AvatarInventory{public static string StorageKey(int i)=>"DF_AvatarOwned_"+i;public static bool IsOwned(int i)=>AccountProfilePrefs.GetInt(StorageKey(i))==1;}
    public static class PlayerProfile{public static int Avatar;}
    public static class PlayerWallet
    {public static int Balance=>EarnedBalance;public static int EarnedBalance=>AccountProfilePrefs.GetInt("DF_Shards");
     public static bool BeginServerPurchase(string s)=>false;public static bool CanAfford(int n)=>Balance>=n;}
}
namespace Alterverse.Shop
{
    public enum AvatarPurchaseResult{InvalidItem,AlreadyOwned,InsufficientShards,Pending,Purchased}
    public class PaidShardCommerce{public static PaidShardCommerce Instance;public bool Ready;public static bool OwnsVoidfall=>false;}
    public class SkinPreviewSelection:UnityEngine.Component{public UniversalHeroCosmetic Headwear;}
}
namespace Alterverse.Characters{public static class LocalHeroContext{public static bool IsLocal(UnityEngine.GameObject actor)=>false;}}
namespace Alterverse.Audio
{public enum DimensionSound{Purchase}public static class DimensionAudio{public static void Play(DimensionSound s,float v){}}}
