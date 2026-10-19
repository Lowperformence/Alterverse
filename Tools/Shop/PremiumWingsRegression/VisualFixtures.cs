// Engine boundary for the actual production wing presenter; no native renderer is simulated.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace UnityEngine {
 public class Object {public string name;public bool Destroyed;public static void Destroy(Object o){if(o!=null)o.Destroyed=true;}}
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;}
 public class MonoBehaviour:Component {}
 public class GameObject:Object {
  public Transform transform;readonly List<Component> components=new();public GameObject(string name=""){this.name=name;transform=new Transform{gameObject=this};}
  public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);return c;}
  public T GetComponent<T>() where T:Component=>components.OfType<T>().FirstOrDefault();
  public static GameObject CreatePrimitive(PrimitiveType t){var g=new GameObject();g.AddComponent<Collider>();g.AddComponent<MeshRenderer>();return g;}
 }
 public enum PrimitiveType{Sphere}
 public class Transform:Component {public Vector3 position,localPosition,localScale;public Quaternion localRotation;public List<Transform> Children=new();public void SetParent(Transform p,bool world){p.Children.Add(this);}}
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new(1,1,1);public static Vector3 forward=>new(0,0,1);
  public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z);public Vector3 normalized=>magnitude>0?this*(1/magnitude):new();
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator*(Vector3 a,float n)=>new(a.x*n,a.y*n,a.z*n);public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
 }
 public struct Quaternion {public float x,y,z;public static Quaternion Euler(float x,float y,float z)=>new(){x=x,y=y,z=z};}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1);public static Color operator*(Color c,float n)=>new(c.r*n,c.g*n,c.b*n,c.a*n);}
 public static class Mathf {public const float PI=(float)Math.PI;public static float Max(float a,float b)=>Math.Max(a,b);public static float Sin(float n)=>(float)Math.Sin(n);public static float Exp(float n)=>(float)Math.Exp(n);public static float Clamp01(float n)=>Math.Clamp(n,0,1);public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);public static float Repeat(float t,float length)=>t-(float)Math.Floor(t/length)*length;}
 public static class Time {public static float time,unscaledTime,deltaTime=.016f,unscaledDeltaTime=.016f;}
 public class Shader:Object {public static Shader Find(string n)=>new(){name=n};}
 public class Material:Object {
  public Shader shader;public Dictionary<string,float> Floats=new();public Dictionary<string,Color> Colors=new();public Color color;
  public Material(Shader s){shader=s;}public bool HasProperty(string n)=>true;public void SetFloat(string n,float f)=>Floats[n]=f;public void SetColor(string n,Color c)=>Colors[n]=c;public void EnableKeyword(string n){}
 }
 public class Mesh:Object {public Vector3[] vertices;public Color[] colors;public int subMeshCount;public Dictionary<int,int[]> Triangles=new();public void SetVertices(List<Vector3> v){vertices=v.ToArray();}public void SetTriangles(int[] t,int slot,bool bounds=false){Triangles[slot]=t;}public void SetTriangles(List<int> t,int slot,bool bounds=false){Triangles[slot]=t.ToArray();}public void RecalculateNormals(){}public void RecalculateBounds(){}}
 public class MeshFilter:Component {public Mesh sharedMesh;}
 public class MeshRenderer:Component {public Material[] sharedMaterials;public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;}
 public class Collider:Component {public bool enabled;}
 public class LineRenderer:Component {public bool useWorldSpace,receiveShadows;public Material sharedMaterial;public int positionCount;public float widthMultiplier;public Rendering.ShadowCastingMode shadowCastingMode;public void SetPosition(int i,Vector3 v){}}
 public class TextAsset:Object {public string text;}
 public static class Resources {public static T Load<T>(string path)where T:Object {if(typeof(T)==typeof(TextAsset))return new TextAsset{text=File.ReadAllText("Assets/Alterverse/Resources/Wings/PremiumWingMeshes.json")} as T;return new Shader{name=path} as T;}}
 public static class JsonUtility {public static T FromJson<T>(string text)=>System.Text.Json.JsonSerializer.Deserialize<T>(text,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});}
 public static class Debug {public static void LogWarning(string s)=>Console.WriteLine(s);}
}
namespace UnityEngine.Rendering {public enum ShadowCastingMode {Off}}
namespace Alterverse.Characters {public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy,VoidKnight}}
namespace Alterverse.Visuals {public static class RuntimeShaderLibrary {public static UnityEngine.Shader Lit=>UnityEngine.Shader.Find("Lit");public static UnityEngine.Shader Unlit=>UnityEngine.Shader.Find("Unlit");}}
