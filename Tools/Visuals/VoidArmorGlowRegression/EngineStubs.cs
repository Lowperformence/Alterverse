using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
 public class Object { public string name; public static void Destroy(Object o){} public static void DestroyImmediate(Object o){} }
 public class Component:Object { public GameObject gameObject; public Transform transform=>gameObject.transform; public new string name {get=>gameObject.name;set=>gameObject.name=value;} public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>(); public T[] GetComponentsInChildren<T>(bool all) where T:class=>gameObject.GetComponentsInChildren<T>(all); }
 public class MonoBehaviour:Component{}
 public class DisallowMultipleComponent:Attribute{}
 public class GameObject:Object
 {
  public int layer; public Transform transform; public List<Component> components=new();
  public GameObject(string name){this.name=name;transform=new Transform{gameObject=this};components.Add(transform);}
  public T AddComponent<T>() where T:Component,new(){var t=new T{gameObject=this};components.Add(t);return t;}
  public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault();
  public T[] GetComponentsInChildren<T>(bool all) where T:class=>components.OfType<T>().Concat(transform.children.SelectMany(c=>c.gameObject.GetComponentsInChildren<T>(all))).ToArray();
 }
 public class Transform:Component {public List<Transform> children=new();public void SetParent(Transform t,bool world){t.children.Add(this);}}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color operator*(Color c,float s)=>new(c.r*s,c.g*s,c.b*s,c.a*s);}
 public struct Matrix4x4 {public int fixture;}
 public struct BoneWeight {public float weight0,weight1,weight2,weight3;public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;}
 public struct Bounds{public void Expand(float value){}}
 public enum SkinQuality {Auto}
 public class Mesh:Object
 {
  public bool isReadable=true;public Vector3[] vertices,normals;public Color[] colors;public BoneWeight[] boneWeights;public Matrix4x4[] bindposes;
  public UnityEngine.Rendering.IndexFormat indexFormat;public List<int[]> submeshes=new();public int subMeshCount=>submeshes.Count;public int vertexCount=>vertices.Length;
  public int[] GetTriangles(int slot)=>submeshes[slot];
  public void SetTriangles(int[] indices,int slot){while(submeshes.Count<=slot)submeshes.Add(null);submeshes[slot]=indices;}
  public void RecalculateBounds(){}
 }
 public class Shader:Object{}
 public class Material:Object {public Shader shader;public Dictionary<string,Color> colors=new();public Dictionary<string,float> floats=new();public Material(Shader shader){this.shader=shader;}public void SetColor(string key,Color c)=>colors[key]=c;public void SetFloat(string key,float n)=>floats[key]=n;}
 public class SkinnedMeshRenderer:Component
 {
  public Mesh sharedMesh;public Material[] sharedMaterials;public Material sharedMaterial;public Transform[] bones;public Transform rootBone;public Bounds localBounds;public SkinQuality quality;public bool updateWhenOffscreen,receiveShadows;
  public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;public UnityEngine.Rendering.LightProbeUsage lightProbeUsage;public UnityEngine.Rendering.ReflectionProbeUsage reflectionProbeUsage;
 }
 public static class Resources {public static bool missing;public static T Load<T>(string path) where T:class=>missing?null:new Shader{name=path} as T;}
 public static class Debug {public static int errors;public static void LogError(object x,Object context){errors++;}}
 public static class Application {public static bool isPlaying;}
}
namespace UnityEngine.Rendering {public enum IndexFormat {UInt16,UInt32}public enum ShadowCastingMode {Off,On}public enum LightProbeUsage {Off,BlendProbes}public enum ReflectionProbeUsage {Off,BlendProbes}}
