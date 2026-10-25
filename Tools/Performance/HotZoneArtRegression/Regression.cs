using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Alterverse.Visuals;
using UnityEngine;
using Object=UnityEngine.Object;

namespace UnityEngine
{
 public enum RuntimeInitializeLoadType{SubsystemRegistration}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public class Object
 {
  public string name="";public bool destroyed;
  public static void Destroy(Object value){if(value==null||value.destroyed)return;value.destroyed=true;if(value is GameObject g){foreach(var c in g.components.ToArray())Destroy(c);foreach(var child in g.transform.children.ToArray())Destroy(child.gameObject);}value.GetType().GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(value,null);}
  public static void DestroyImmediate(Object value)=>Destroy(value);
 }
 public class Component:Object
 {
  public GameObject gameObject;public Transform transform=>gameObject.transform;
  public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();
  public T[] GetComponentsInChildren<T>(bool all)where T:class=>gameObject.GetComponentsInChildren<T>(all);
 }
 public class MonoBehaviour:Component{}
 public class GameObject:Object
 {
  public readonly List<Component> components=new();public Transform transform;
  public GameObject(string name="actor"){this.name=name;transform=new Transform{gameObject=this};}
  public T AddComponent<T>()where T:Component,new(){var value=new T{gameObject=this};components.Add(value);return value;}
  public T GetComponent<T>()where T:class=>components.OfType<T>().FirstOrDefault();
  public T[] GetComponentsInChildren<T>(bool all)where T:class=>components.OfType<T>().Concat(transform.children.SelectMany(x=>x.gameObject.GetComponentsInChildren<T>(all))).ToArray();
 }
 public class Transform:Component
 {
  public Transform parent;public readonly List<Transform> children=new();public Matrix4x4 localToWorldMatrix=Matrix4x4.identity;
  public Matrix4x4 worldToLocalMatrix=>localToWorldMatrix.inverse;
  public Transform Find(string path)=>children.FirstOrDefault(x=>x.gameObject.name==path);
  public void SetParent(Transform value,bool preserve){parent?.children.Remove(this);parent=value;value?.children.Add(this);}
 }
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Vector3
 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 one=>new(1,1,1);public float magnitude=>MathF.Sqrt(x*x+y*y+z*z);public Vector3 normalized=>magnitude>0?this/magnitude:new();
  public static Vector3 Scale(Vector3 a,Vector3 b)=>new(a.x*b.x,a.y*b.y,a.z*b.z);
  public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator*(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
  public static Vector3 operator/(Vector3 a,float b)=>new(a.x/b,a.y/b,a.z/b);
 }
 public struct Matrix4x4
 {
  public System.Numerics.Matrix4x4 value;public static Matrix4x4 identity=>new(){value=System.Numerics.Matrix4x4.Identity};
  public static Matrix4x4 Translation(float x)=>new(){value=System.Numerics.Matrix4x4.CreateTranslation(x,0,0)};
  public Matrix4x4 inverse{get{System.Numerics.Matrix4x4.Invert(value,out var result);return new(){value=result};}}public Matrix4x4 transpose=>new(){value=System.Numerics.Matrix4x4.Transpose(value)};
  public float this[int i]=>i switch{0=>value.M11,1=>value.M12,2=>value.M13,3=>value.M14,4=>value.M21,5=>value.M22,6=>value.M23,7=>value.M24,8=>value.M31,9=>value.M32,10=>value.M33,11=>value.M34,12=>value.M41,13=>value.M42,14=>value.M43,_=>value.M44};
  public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new(){value=b.value*a.value};
  public Vector3 MultiplyPoint3x4(Vector3 p){var result=System.Numerics.Vector3.Transform(new(p.x,p.y,p.z),value);return new(result.X,result.Y,result.Z);}
  public Vector3 MultiplyVector(Vector3 p){var result=System.Numerics.Vector3.TransformNormal(new(p.x,p.y,p.z),value);return new(result.X,result.Y,result.Z);}
 }
 public struct Color
 {
  public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
  public static Color black=>new(0,0,0);public float maxColorComponent=>Math.Max(r,Math.Max(g,b));public static Color operator*(Color a,float b)=>new(a.r*b,a.g*b,a.b*b,a.a*b);
 }
 public struct Bounds{public Vector3 center,size;public Bounds(Vector3 center,Vector3 size){this.center=center;this.size=size;}public Vector3 min=>center-size*.5f;public Vector3 max=>center+size*.5f;public void Expand(Vector3 delta){size+=delta;}}
 public struct BoneWeight{public int boneIndex0;public float weight0;}
 public class Mesh:Object
 {
  public Vector3[] vertices=Array.Empty<Vector3>(),normals=Array.Empty<Vector3>();public Vector2[] uv;public int[] triangles=Array.Empty<int>();public BoneWeight[] boneWeights;public Matrix4x4[] bindposes;public int subMeshCount=1;public Bounds bounds=new(new(),Vector3.one);public Rendering.IndexFormat indexFormat;
  public readonly Dictionary<int,int[]> submeshes=new();public void SetTriangles(int[] values,int index,bool recalculate){submeshes[index]=values;triangles=values;}
  public void SetTriangles(List<int> values,int index)=>SetTriangles(values.ToArray(),index,false);
  public void SetVertices(List<Vector3> value){vertices=value.ToArray();}public void SetUVs(int channel,List<Vector2> value){uv=value.ToArray();}
  public void RecalculateBounds(){if(vertices.Length==0)return;Vector3 min=vertices[0],max=vertices[0];foreach(var v in vertices){min=new(Math.Min(v.x,min.x),Math.Min(v.y,min.y),Math.Min(v.z,min.z));max=new(Math.Max(v.x,max.x),Math.Max(v.y,max.y),Math.Max(v.z,max.z));}bounds=new((min+max)*.5f,max-min);}
  public void RecalculateTangents(){}public void RecalculateNormals(){}
  public int uploads;public bool discarded;public void UploadMeshData(bool discard){uploads++;discarded=discard;}
 }
 public class Material:Object
 {
  public readonly Dictionary<string,object> properties=new();public Material(){}public Material(Material source){name=source.name;foreach(var pair in source.properties)properties[pair.Key]=pair.Value;}
  public bool HasProperty(string key)=>true;public float GetFloat(string key)=>properties.TryGetValue(key,out var value)?(float)value:0;
  public void SetFloat(string key,float value){properties[key]=value;}public Texture GetTexture(string key)=>properties.TryGetValue(key,out var value)?(Texture)value:null;
  public void SetTexture(string key,Texture value){properties[key]=value;}public Color GetColor(string key)=>properties.TryGetValue(key,out var value)?(Color)value:Color.black;
  public void SetColor(string key,Color value){properties[key]=value;}public void EnableKeyword(string key){}
 }
 public class Renderer:Component{public bool enabled=true;public Material[] sharedMaterials=Array.Empty<Material>();}
 public class MeshRenderer:Renderer{}
 public class MeshFilter:Component{public Mesh sharedMesh;}
 public class SkinnedMeshRenderer:Renderer{public Mesh sharedMesh;public Transform[] bones;public Transform rootBone;public Bounds localBounds;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;}
 public class Texture:Object{}public class Texture2D:Texture{}public class TextAsset:Object{public byte[] bytes;public string text;}
 public static class Resources{public static Dictionary<string,Object> assets=new();public static int loads;public static T Load<T>(string key)where T:class{loads++;return assets.TryGetValue(key,out var result)?result as T:null;}}
 public static class JsonUtility{public static T FromJson<T>(string value)=>JsonSerializer.Deserialize<T>(value,new JsonSerializerOptions{IncludeFields=true});}
 public static class Application{public static bool isPlaying=true;}
 public static class Debug{public static void LogError(string message)=>throw new Exception(message);}
 public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Abs(float x)=>Math.Abs(x);public static float Clamp(float x,float min,float max)=>Math.Clamp(x,min,max);}
}
namespace UnityEngine.Rendering{public enum IndexFormat{UInt16,UInt32}public enum ShadowCastingMode{On}}
namespace Alterverse.Visuals
{
 public static class CreatureSurfaceUV{public class Mapping{public int[] sourceVertices;public Vector2[] uv;public ResourceCreatureMesh.Submesh[] submeshes;}public static Mapping Load(string resource,byte[] bytes,ResourceCreatureMesh.Data data)=>null;public static string Family(string resource)=>null;}
 public static class RuntimeShaderLibrary{public static object Lit=new();public static Material Create(Color c,bool transparent,float metal,float smooth,Color emission)=>new();}
}
static class Regression
{
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static ResourceCreatureMesh.Data Data()=>new(){version=1,bonePaths=new[]{""},vertices=new[]{0f,0,0,1,0,0,0,1,0},normals=new[]{0f,0,1,0,0,1,0,0,1},boneIndices=new[]{0,0,0},submeshes=new[]{new ResourceCreatureMesh.Submesh{triangles=new[]{0,1,2}}},joints=Array.Empty<ResourceCreatureMesh.Joint>()};
 static ResourceCreatureMesh Build(ResourceCreatureMesh.Data data,bool shared=true,float boneOffset=0)
 {
  var root=new GameObject();var bone=new GameObject();bone.transform.localToWorldMatrix=Matrix4x4.Translation(boneOffset);
  var owner=root.AddComponent<ResourceCreatureMesh>();typeof(ResourceCreatureMesh).GetMethod("Build",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(owner,new object[]{root.transform,data,new[]{bone.transform},new[]{Color.black},null,shared});return owner;
 }
 static Mesh Skin(ResourceCreatureMesh owner)=>owner.GetComponent<SkinnedMeshRenderer>().sharedMesh;
 static GameObject ArtHost()
 {
  var host=new GameObject();var part=new GameObject("part");part.transform.SetParent(host.transform,false);part.AddComponent<MeshFilter>().sharedMesh=new Mesh{name="Custom",vertices=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,0)},triangles=new[]{0,1,2}};part.AddComponent<MeshRenderer>().sharedMaterials=new[]{new Material{name="armor"}};return host;
 }
 static void Main()
 {
  var data=Data();var first=Build(data);var second=Build(data);var mesh=Skin(first);
  Check(ReferenceEquals(mesh,Skin(second)),"identical reference rest poses share the completed mesh");
  Check(mesh.vertices.Length==3&&mesh.boneWeights.All(x=>x.weight0==1)&&mesh.submeshes[0].SequenceEqual(new[]{0,1,2}),"vertices, rigid weights and triangles retained");
  Check(!ReferenceEquals(first.GetComponent<SkinnedMeshRenderer>().sharedMaterials[0],second.GetComponent<SkinnedMeshRenderer>().sharedMaterials[0]),"materials stay independent for damage tint");
  var different=Build(data,boneOffset:2);Check(!ReferenceEquals(mesh,Skin(different)),"different bind/rest poses cannot reuse geometry");
  var privateMesh=Build(data,shared:false);Check(!ReferenceEquals(mesh,Skin(privateMesh)),"unrelated callers retain original private geometry path");
  Object.Destroy(first.gameObject);Check(!mesh.destroyed,"destroying one actor does not destroy another actor's shared model");
  Object.Destroy(second.gameObject);Check(mesh.destroyed,"last owner releases shared geometry");
  var again=Build(data);Check(!ReferenceEquals(mesh,Skin(again))&&!Skin(again).destroyed,"later owner rebuilds after final release");
  Object.Destroy(different.gameObject);Object.Destroy(privateMesh.gameObject);Object.Destroy(again.gameObject);
  var cache=(IDictionary)typeof(ResourceCreatureMesh).GetField("geometryCache",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);Check(cache.Count==0,"no geometry cache entries leak after match actors are destroyed");

  Resources.assets["AuthoredHotZone/Parts"]=new TextAsset{text="{\"version\":1,\"parts\":[],\"surfaceInset\":{\"inset\":0.02,\"barycentric\":[0.33333334,0.33333334,0.33333334]}}"};
  var a=ArtHost();var b=ArtHost();AuthoredHotZoneArt.Apply(a.transform,"Machine");int loaded=Resources.loads;
  AuthoredHotZoneArt.Apply(b.transform,"Machine");
  var aMesh=a.GetComponentsInChildren<MeshFilter>(true)[0].sharedMesh;var bMesh=b.GetComponentsInChildren<MeshFilter>(true)[0].sharedMesh;
  Check(aMesh.vertices.Length==9&&aMesh.triangles.Length==9,"authored triangle inset retains the full three-face fan");
  Check(aMesh.vertices.SequenceEqual(bMesh.vertices)&&aMesh.triangles.SequenceEqual(bMesh.triangles)&&aMesh.uv.SequenceEqual(bMesh.uv),"prepared copies retain identical authored geometry/UVs");
  Check(Resources.loads-loaded==2,"prepared path reuses parsed library; only surface/normal lookups remain");
  var aMaterial=a.GetComponentsInChildren<MeshRenderer>(true)[0].sharedMaterials[0];Check(aMaterial.name.EndsWith("_BlenderSurface"),"authored material layer retained");
  var uploaded=new HashSet<Mesh>();AuthoredHotZoneArt.UploadPreparedGeometry(a.transform,uploaded);AuthoredHotZoneArt.UploadPreparedGeometry(a.transform,uploaded);
  Check(aMesh.uploads==1&&!aMesh.discarded,"prepared mesh uploads once per match and remains readable for existing systems");
  Object.Destroy(a);Check(aMesh.destroyed&&aMaterial.destroyed&&!bMesh.destroyed,"cancellation/exit releases only owned authored meshes/materials");Object.Destroy(b);
  Check(bMesh.destroyed,"last authored actor cleanup completes");
  Console.WriteLine($"PASS {checks} production mesh-cache/art lifetime checks. Native graphics operations are controlled fixtures; no device timing claim.");
 }
}
