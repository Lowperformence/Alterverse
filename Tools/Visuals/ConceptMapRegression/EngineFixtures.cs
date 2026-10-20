// Engine substitutes for the actual landscape builder. These verify placement/batching
// contracts; they do not simulate Unity rendering, native physics, or Android performance.
using System;
using System.Collections.Generic;
using System.Linq;
using N=System.Numerics;
namespace UnityEngine {
 public class DisallowMultipleComponent:Attribute {}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value){} }
 public enum RuntimeInitializeLoadType{AfterSceneLoad}
 public enum FindObjectsInactive{Exclude}public enum FindObjectsSortMode{None}
 public class Object {
  public string name; public bool Destroyed;
  public static readonly List<Object> All=new();
  public Object(){All.Add(this);}
  public static void Destroy(Object value){
   if(value==null)return;value.Destroyed=true;
   if(value is GameObject go){
    foreach(var child in All.OfType<Transform>().Where(t=>ReferenceEquals(t.Parent,go.transform)).ToArray())Destroy(child.gameObject);
    go.transform.Destroyed=true;foreach(var component in go.Components)component.Destroyed=true;
   }
  }
  public static void DontDestroyOnLoad(Object value){}
  public static T[] FindObjectsByType<T>(FindObjectsInactive inactive,FindObjectsSortMode order)where T:Object=>All.OfType<T>().Where(o=>o!=null).ToArray();
  public static bool operator==(Object a,Object b){bool an=ReferenceEquals(a,null)||(!ReferenceEquals(a,null)&&a.Destroyed),bn=ReferenceEquals(b,null)||(!ReferenceEquals(b,null)&&b.Destroyed);return an||bn?an&&bn:ReferenceEquals(a,b);}
  public static bool operator!=(Object a,Object b)=>!(a==b);
  public override bool Equals(object other)=>ReferenceEquals(this,other);public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
 }
 public class Component:Object {public GameObject gameObject; public Transform transform=>gameObject.transform;public T GetComponent<T>()where T:Component=>gameObject.GetComponent<T>();public T[] GetComponentsInChildren<T>(bool inactive)where T:Component=>Object.All.OfType<T>().Where(o=>o!=null).ToArray();}
 public class MonoBehaviour:Component {public bool enabled=true;}
 public class GameObject:Object {
  public Transform transform;public List<Component> Components=new();
  public GameObject(string name=""){this.name=name;transform=new Transform{gameObject=this};}
  public T AddComponent<T>()where T:Component,new(){var component=new T{gameObject=this};Components.Add(component);return component;}
  public T GetComponent<T>()where T:Component=>Components.OfType<T>().FirstOrDefault(c=>c!=null);
  public static GameObject Find(string name)=>All.OfType<GameObject>().FirstOrDefault(g=>g!=null&&g.name==name);
  public static GameObject CreatePrimitive(PrimitiveType type){var go=new GameObject();go.AddComponent<Collider>();go.AddComponent<MeshRenderer>();return go;}
 }
 public enum PrimitiveType{Sphere,Cylinder,Cube,Capsule}
 public class Collider:Component{public bool enabled=true;}
 public class Transform:Component {
  public Vector3 position,localPosition;public Quaternion localRotation;public Vector3 localScale=Vector3.one;
  public Transform Parent;public void SetParent(Transform parent,bool world){Parent=parent;}
 }
 public struct Vector2 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 }
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new();public static Vector3 one=>new(1,1,1);public static Vector3 up=>new(0,1,0);
  public float magnitude=>MathF.Sqrt(x*x+y*y+z*z);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator*(Vector3 a,float s)=>new(a.x*s,a.y*s,a.z*s);
  public N.Vector3 Native=>new(x,y,z);public static Vector3 From(N.Vector3 p)=>new(p.X,p.Y,p.Z);
 }
 public struct Quaternion {
  public N.Quaternion Native;
  public static Quaternion identity=>new(){Native=N.Quaternion.Identity};
  public static Quaternion Euler(float x,float y,float z)=>new(){Native=N.Quaternion.CreateFromYawPitchRoll(y*Mathf.Deg2Rad,x*Mathf.Deg2Rad,z*Mathf.Deg2Rad)};
  public static Quaternion FromToRotation(Vector3 a,Vector3 b){
   var source=N.Vector3.Normalize(a.Native);var target=N.Vector3.Normalize(b.Native);float dot=N.Vector3.Dot(source,target);
   if(dot>.99999f)return identity;if(dot<-.99999f)return Euler(180,0,0);
   var cross=N.Vector3.Cross(source,target);return new(){Native=N.Quaternion.Normalize(new N.Quaternion(cross,1+dot))};
  }
  public static Quaternion operator*(Quaternion a,Quaternion b)=>new(){Native=a.Native*b.Native};
  public static Vector3 operator*(Quaternion q,Vector3 p)=>Vector3.From(N.Vector3.Transform(p.Native,q.Native));
 }
 public struct Matrix4x4 {
  public N.Matrix4x4 Native;
  public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new(){Native=N.Matrix4x4.CreateScale(s.Native)*N.Matrix4x4.CreateFromQuaternion(q.Native)*N.Matrix4x4.CreateTranslation(p.Native)};
  public static Matrix4x4 Translate(Vector3 p)=>new(){Native=N.Matrix4x4.CreateTranslation(p.Native)};
  // System.Numerics row-vector multiplication is the reverse of Unity's convention.
  public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new(){Native=b.Native*a.Native};
  public Vector3 MultiplyPoint3x4(Vector3 p)=>Vector3.From(N.Vector3.Transform(p.Native,Native));
 }
 public struct Color {
  public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
  public static Color black=>new(0,0,0);public static Color white=>new(1,1,1);
  public static Color operator*(Color c,float s)=>new(c.r*s,c.g*s,c.b*s,c.a*s);
 }
 public static class Mathf {
  public const float PI=MathF.PI,Deg2Rad=PI/180,Rad2Deg=180/PI;
  public static float Sin(float x)=>MathF.Sin(x);public static float Cos(float x)=>MathF.Cos(x);
  public static float Lerp(float a,float b,float t)=>a+(b-a)*Math.Clamp(t,0,1);
 }
 public static class Time{public static float time,unscaledTime;}
 public static class Debug {public static readonly List<string> Errors=new();public static void LogError(string message)=>Errors.Add(message);public static void LogException(Exception error)=>Errors.Add(error.Message);}
 public class Material:Object {public void SetFloat(string key,float value){} }
 public struct Bounds{public Vector3 center,size;}
 public struct CombineInstance {public Mesh mesh;public Matrix4x4 transform;}
 public class Mesh:Object {
  public Vector3[] vertices=Array.Empty<Vector3>();public List<CombineInstance> Instances=new();
  public int vertexCount=>vertices.Length;
  public Bounds bounds{get{var min=new Vector3(vertices.Min(v=>v.x),vertices.Min(v=>v.y),vertices.Min(v=>v.z));var max=new Vector3(vertices.Max(v=>v.x),vertices.Max(v=>v.y),vertices.Max(v=>v.z));return new(){center=(min+max)*.5f,size=max-min};}}
  public void SetVertices(List<Vector3> data){vertices=data.ToArray();}
  public void SetTriangles(List<int> values,int submesh){}
  public void RecalculateNormals(){}public void RecalculateBounds(){}
  public void CombineMeshes(CombineInstance[] parts,bool combine,bool transform){
   Instances=parts.ToList();vertices=parts.SelectMany(part=>part.mesh.vertices.Select(v=>part.transform.MultiplyPoint3x4(v))).ToArray();
  }
 }
 public class Renderer:Component {public bool enabled=true;}
 public class MeshRenderer:Renderer {public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;public Rendering.LightProbeUsage lightProbeUsage;public Rendering.ReflectionProbeUsage reflectionProbeUsage;}
 public class MeshFilter:Component {public Mesh sharedMesh;}
 public class LODGroup:Component {public LOD[] Lods;public void SetLODs(LOD[] values){Lods=values;}public void RecalculateBounds(){} }
 public struct LOD {public float screenRelativeTransitionHeight;public Renderer[] renderers;public LOD(float transition,Renderer[] renderers){screenRelativeTransitionHeight=transition;this.renderers=renderers;} }
}
namespace UnityEngine.Rendering {public enum ShadowCastingMode{Off}public enum LightProbeUsage{BlendProbes}public enum ReflectionProbeUsage{BlendProbes}}
namespace Alterverse.Match {
 public enum ArenaVariant {Moon,Mars,GreenPlanet,Noxara,Kryon,Earth,Pyraxis,Neptune,Dark00,DiamondCore,GJ504b,Wasp76b,Trappist1e,Toi715b}
 public static class ArenaVariantRuntime {public static ArenaVariant Current;public static bool IsNeptune=>Current==ArenaVariant.Neptune;}
}
namespace Alterverse.Visuals {
 using UnityEngine;using Alterverse.Match;
 public static class LunarSurfaceField {public static Vector3 Surface(float x,float z)=>new(x,.0002f*(x*x+z*z),z);}
 public static class FixtureMesh {
  // Deliberately bounded input geometry. Native authored-mesh vertex budgets need Unity.
  public static Mesh Make()=>new(){vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,1,.5f)}};
 }
 public static class ProductionMeshFactory {public static Mesh Disc(float radius,float thickness,int sides)=>FixtureMesh.Make();}
 public static class SceneryMeshQuality {
  public static Mesh SoftProfile(string name,int sides,Vector2[] rings)=>FixtureMesh.Make();
  public static Mesh CurvedFrond()=>FixtureMesh.Make();public static Mesh RoundedBeam()=>FixtureMesh.Make();
 }
 public static class RuntimeShaderLibrary {
  public static int FailAfter=-1,Creates;
  public static Material Create(Color color,bool transparent,float metallic,float smoothness,Color emission=default){if(FailAfter>=0&&Creates++>=FailAfter)return null;return new();}
 }
 public enum SceneryFinish{Stone,Bark,Leaf,WetStone}
 public static class PlanetSceneryMaterials {public static SceneryFinish LandscapeFinish(ArenaVariant variant,int layer)=>SceneryFinish.Stone;public static void Apply(Material material,ArenaVariant variant,SceneryFinish finish){} }
 public sealed class SceneryBuildingClearance {
  public static bool RejectAll;public static readonly List<(Mesh Mesh,Matrix4x4 Placement)> Probes=new();
  public bool Blocks(Mesh mesh,Matrix4x4 placement){Probes.Add((mesh,placement));return RejectAll;}
  public void RemoveIfBlocked(GameObject value){if(RejectAll)Object.Destroy(value);}
 }
 public sealed class EarthMapDressing:MonoBehaviour {public void Build(){} }
 public static class PlanetWaterMaterials {public static void Apply(Material material){} }
 public static class AuthoredHotZoneArt {public static void Apply(Transform root,string name){} }
}
namespace Alterverse.Combat {
 public sealed class HotZoneTank:UnityEngine.MonoBehaviour {}

}
