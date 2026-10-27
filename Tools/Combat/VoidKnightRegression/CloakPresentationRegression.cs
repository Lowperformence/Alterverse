// Runs the production material swap with controlled Unity objects. No GPU shader execution.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Alterverse.Characters;
using Alterverse.Visuals;

namespace UnityEngine
{
 public class Object {public string name;public HideFlags hideFlags;public bool Destroyed;public static void Destroy(Object o){o.Destroyed=true;}}
 public enum HideFlags{HideAndDontSave}
 public class DisallowMultipleComponent:Attribute{}
 public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int order){}}
 public class Component:Object
 {
  public GameObject gameObject;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();
  public T[] GetComponentsInChildren<T>(bool inactive)where T:class=>gameObject.GetComponentsInChildren<T>(inactive);
 }
 public class MonoBehaviour:Component{}
 public class GameObject:Object
 {
  public readonly List<Component> Components=new();public readonly List<GameObject> Children=new();
  public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
  public T GetComponent<T>()where T:class=>Components.OfType<T>().FirstOrDefault();
  public T[] GetComponentsInChildren<T>(bool inactive)where T:class=>Components.OfType<T>().Concat(Children.SelectMany(c=>c.GetComponentsInChildren<T>(inactive))).ToArray();
 }
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1,1);}
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public static class Time {public static float time;}
 public static class Application {public static bool isBatchMode;}
 public class Shader:Object {public bool isSupported=true;public static int PropertyToID(string n)=>n.GetHashCode();}
 public static class Resources {public static T Load<T>(string p)where T:class=>new Shader() as T;}
 public class Texture:Object{}
 public class Material:Object
 {
  public Shader shader;public readonly Dictionary<int,object> Values=new();
  public Material(Shader s){shader=s;}
  public bool HasProperty(string p)=>Values.ContainsKey(Shader.PropertyToID(p));
  public Color GetColor(string p)=>(Color)Values[Shader.PropertyToID(p)];
  public Texture GetTexture(string p)=>Values[Shader.PropertyToID(p)] as Texture;
  public void SetFloat(int p,float v){Values[p]=v;}public void SetColor(int p,Color v){Values[p]=v;}
  public void SetTexture(int p,Texture v){Values[p]=v;}
  public Vector2 GetTextureScale(string p)=>new(2,3);public Vector2 GetTextureOffset(string p)=>new(.1f,.2f);
  public void SetTextureScale(string p,Vector2 v){}public void SetTextureOffset(string p,Vector2 v){}
 }
 public class Renderer:Component {public Material[] sharedMaterials=Array.Empty<Material>();public ShadowCastingMode shadowCastingMode=ShadowCastingMode.On;public bool enabled=true,receiveShadows=true;}
 public class MeshRenderer:Renderer{}public class SkinnedMeshRenderer:Renderer{}public class ParticleSystemRenderer:Renderer{}public class TrailRenderer:Renderer{}public class LineRenderer:Renderer{}
}
namespace UnityEngine.Rendering {public enum ShadowCastingMode{Off,On}}
namespace Alterverse.Combat {public class TeamMember:Component{public int Team;}}
namespace Alterverse.Characters
{
 public class VoidKnightState:Component {public bool Cloaked;}
 public static class LocalHeroContext
 {
  public static GameObject Owner;public static Alterverse.Combat.TeamMember Member;
  public static bool IsLocal(GameObject actor)=>actor==Owner;
 }
}
class CloakPresentationRegression
{
 static int checks;static void Check(bool c,string why){if(!c)throw new Exception(why);checks++;}
 static void Tick(VoidKnightStealth p)=>typeof(VoidKnightStealth).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(p,null);
 static float Opacity(Renderer r)=>(float)r.sharedMaterials[0].Values[Shader.PropertyToID("_StealthOpacity")];
 static void Main()
 {
  var actor=new GameObject();var state=actor.AddComponent<VoidKnightState>();state.Cloaked=true;
  actor.AddComponent<Alterverse.Combat.TeamMember>().Team=0;
  var mesh=actor.AddComponent<SkinnedMeshRenderer>();var original=new Material(new Shader{name="DimensionsFront/HeroSurface"});
  original.SetColor(Shader.PropertyToID("_BaseColor"),new Color(1,1,1,0));mesh.sharedMaterials=new[]{original};
  var effects=actor.AddComponent<ParticleSystemRenderer>();var trail=actor.AddComponent<TrailRenderer>();trail.enabled=false;
  var p=actor.AddComponent<VoidKnightStealth>();LocalHeroContext.Owner=actor;
  Tick(p);var ghost=mesh.sharedMaterials[0];Check(ghost!=original,"per-instance cloak material installed");
  Check(Math.Abs(Opacity(mesh)-.2f)<.0001,"owner sees 20 percent opacity");
  Check((float)ghost.Values[Shader.PropertyToID("_StealthVertexColor")]==1,"vertex paint retained");
  Check(!original.Values.ContainsKey(Shader.PropertyToID("_StealthOpacity")),"shared source material untouched");
  Check(mesh.shadowCastingMode==ShadowCastingMode.Off&&!mesh.receiveShadows,"cloak has no opaque shadow");
  Check(!effects.enabled&&!trail.enabled,"attached effects cannot reveal cloak");
  LocalHeroContext.Owner=null;var viewer=new GameObject().AddComponent<Alterverse.Combat.TeamMember>();viewer.Team=1;LocalHeroContext.Member=viewer;
  Tick(p);Check(Math.Abs(Opacity(mesh)-.02f)<.0001,"enemy sees 2 percent opacity");
  viewer.Team=0;Tick(p);Check(Math.Abs(Opacity(mesh)-.2f)<.0001,"ally sees 20 percent opacity");
  var hat=new GameObject();actor.Children.Add(hat);var hatMesh=hat.AddComponent<MeshRenderer>();var hatMaterial=new Material(new Shader());var texture=new Texture();
  hatMaterial.SetTexture(Shader.PropertyToID("_BaseMap"),texture);hatMesh.sharedMaterials=new[]{hatMaterial};Time.time=.2f;
  Tick(p);Check(hatMesh.sharedMaterials[0]!=hatMaterial,"late-attached hat also fades");Check(hatMesh.sharedMaterials[0].GetTexture("_StealthMap")==texture,"surface texture retained");
  state.Cloaked=false;Tick(p);Check(mesh.sharedMaterials[0]==original&&hatMesh.sharedMaterials[0]==hatMaterial,"expiry restores original body and hat materials");
  Check(mesh.shadowCastingMode==ShadowCastingMode.On&&mesh.receiveShadows,"expiry restores shadow settings");
  Check(effects.enabled&&!trail.enabled,"expiry restores exact effect enabled states");Check(ghost.Destroyed,"temporary cloak materials released");
  state.Cloaked=true;Tick(p);typeof(VoidKnightStealth).GetMethod("OnDisable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(p,null);
  Check(mesh.sharedMaterials[0]==original,"disable restores original materials");
  Application.isBatchMode=true;Tick(p);Check(mesh.sharedMaterials[0]==original,"headless server does not allocate cloak materials");
  Console.WriteLine($"PASS {checks} production cloak material, owner/enemy opacity, equipment, shadow and cleanup checks. GPU rendering remains native acceptance.");
 }
}
