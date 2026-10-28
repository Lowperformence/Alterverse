using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Alterverse.Combat;

// Unity/physics/rendering substitutes. Hazard methods come from production.
namespace UnityEngine
{
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int order){}}
 public class Object
 {
  public bool destroyed;
  public static bool operator ==(Object a,Object b)=>ReferenceEquals(a,b)||(ReferenceEquals(a,null)&&b.destroyed)||(ReferenceEquals(b,null)&&a.destroyed);
  public static bool operator !=(Object a,Object b)=>!(a==b);
  public override bool Equals(object o)=>ReferenceEquals(this,o);public override int GetHashCode()=>base.GetHashCode();
  public static T FindAnyObjectByType<T>()where T:class=>GameObject.All.Where(g=>!g.destroyed&&g.active).Select(g=>g.GetComponent<T>()).FirstOrDefault(x=>x!=null);
  public static void Destroy(Object o){if(ReferenceEquals(o,null))return;o.destroyed=true;if(o is GameObject g)foreach(var c in g.Components){c.destroyed=true;c.GetType().GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(c,null);}}
 }
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();public T GetComponentInParent<T>()where T:class=>GetComponent<T>()??transform.parent?.gameObject.GetComponent<T>();}
 public class MonoBehaviour:Component {public bool enabled=true;public bool isActiveAndEnabled=>!destroyed&&enabled&&gameObject.active&&!gameObject.destroyed;}
 public class GameObject:Object
 {
  public static readonly List<GameObject> All=new();public string name;public bool active=true;public Transform transform;public readonly List<Component> Components=new();
  public GameObject(string name="actor"){this.name=name;transform=new Transform{gameObject=this};All.Add(this);}
  public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
  public T GetComponent<T>()where T:class=>Components.OfType<T>().FirstOrDefault();
  public static GameObject Find(string name)=>All.FirstOrDefault(g=>g.active&&!g.destroyed&&g.name==name);
 }
 public enum Space{Self}
 public class Transform:Component {public Vector3 position,localScale;public Transform parent;public void SetParent(Transform p,bool preserve){parent=p;}public void Rotate(float a,float b,float c,Space s=Space.Self){}}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 up=>new(0,1,0);public float sqrMagnitude=>x*x+y*y+z*z;public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public class Material:Object{}
 public class LineRenderer:Component {public float widthMultiplier;public void SetPosition(int n,Vector3 p){}}
 public class Collider:Component{}
 public static class Physics {public static Collider[] Hits=Array.Empty<Collider>();public static int OverlapSphereNonAlloc(Vector3 p,float r,Collider[] buffer){int n=Math.Min(buffer.Length,Hits.Length);Array.Copy(Hits,buffer,n);return n;}}
 public static class Time {public static int frameCount;public static float time,timeScale=1,deltaTime=1f/60;}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static float Lerp(float a,float b,float t)=>a+(b-a)*t;public static float Clamp01(float a)=>Math.Clamp(a,0,1);public static float Sin(float a)=>(float)Math.Sin(a);}
 public static class Debug {public static int Exceptions,Warnings;public static void LogException(Exception e,Object context){Exceptions++;}public static void LogWarning(object value){Warnings++;}}
}
namespace Alterverse.Multiplayer {public static class DedicatedServerNetworkRuntime {public static bool Requested=true;}public static class CoopAuthority {public static bool CanSimulateHostGameplay=true;public static bool Active=true,LocalHasServerAuthority=true;}}
namespace Alterverse.Match
{
 public class DimensionsFront:MonoBehaviour {public bool HasEnded;}
 public class DimensionPlanet:MonoBehaviour {public bool Available=true;}
 public static class ArenaVariantRuntime {public static bool IsKryon,IsNoxara;}
}

namespace Alterverse.Combat
{
 public class Health:MonoBehaviour {public bool IsDead;public float Damage;public int HitCount;public void TakeDamage(float d,GameObject source){Damage+=d;HitCount++;}}
 public class TeamMember:Component {public int Team;}
 public class LaneMinion:Component{}public class RealityCore:Component{}public class LaneTower:Component{}
 public class HotZoneTank:MonoBehaviour
 {
  public static List<HotZoneTank> Active=new();
  public Health TankHealth=>GetComponent<Health>();public int Team;public bool IsNoxaraSpider=true;
 }
 public sealed partial class MeteorStrike:MonoBehaviour
 {
  readonly Collider[] hitBuffer=new Collider[32];float spawnAt,hitAt,damage;GameObject warning,meteor;Vector3 startPosition;Alterverse.Match.DimensionPlanet owner;Alterverse.Match.DimensionsFront front;LineRenderer trail,warningLine;
  public static int VisualBuilds;public float Deadline=>hitAt;public void Tick()=>Update();
 }
 public sealed partial class NoxaraMeteorStrike:MonoBehaviour
 {
  readonly Collider[] hitBuffer=new Collider[32];float spawnAt,hitAt,damage;GameObject warning,meteor;Vector3 impactPosition;HotZoneTank owner;Alterverse.Match.DimensionsFront front;LineRenderer trail,warningLine;bool resolved;
  static readonly Color PoisonGreenHot=new(.62f,1,.08f),PoisonGreen=new(.38f,1,.04f);
  public static int VisualBuilds;public float Deadline=>hitAt;public void Tick()=>Update();
 }
 public sealed partial class SpaceBombStrike:MonoBehaviour
 {
  readonly Collider[] hitBuffer=new Collider[32];float spawnAt,hitAt,damage,startHeight;GameObject warning,bomb;Vector3 target;Alterverse.Match.DimensionsFront front;LineRenderer warningLine;Transform finRoot,blink;bool resolved;
  public static int VisualBuilds;public float Deadline=>hitAt;public void Tick()=>Update();
 }
 public sealed partial class SpaceBomberDirector:MonoBehaviour
 {
  private bool materialsReady;public static int VisualBuilds;public bool Prepare(){BuildSharedMaterials();return materialsReady;}
 }
}
namespace Alterverse.Visuals {public static class BomberExplosionVfx {public static void Play(Vector3 p,Color c,float r){}}public static class PrototypeVfx {public static void Ring(Vector3 p,Color c,float r,float t){}}}
namespace Alterverse.Audio {public enum DimensionSound{Explosion}public static class DimensionAudio{public static void PlayAt(DimensionSound s,Vector3 p,float v){}}}
