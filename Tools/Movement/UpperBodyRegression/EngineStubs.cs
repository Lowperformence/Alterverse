using System;
using System.Collections.Generic;
using NQ = System.Numerics.Quaternion;
using NV = System.Numerics.Vector3;
namespace UnityEngine {
 public class Object {}
 [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent:Attribute {}
 [AttributeUsage(AttributeTargets.Class)] public class DefaultExecutionOrder:Attribute { public DefaultExecutionOrder(int n){} }
 public static class Time { public static float time, deltaTime=1f/60f; }
 public static class Debug { public static void Log(string s,object o=null){} public static void LogError(string s,object o=null){throw new Exception(s);} }
 public static class Mathf {
  public const float PI=(float)Math.PI, Deg2Rad=PI/180f;
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
  public static Quaternion identity=>new Quaternion(0,0,0,1); internal NQ N=>new NQ(x,y,z,w);
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
 public class Component : Object { public GameObject gameObject; public Transform transform=>gameObject.transform;
  public T GetComponent<T>() where T:class{return gameObject.GetComponent<T>();}
  public T GetComponentInChildren<T>() where T:class{return gameObject.GetComponentInChildren<T>();}
  public T[] GetComponentsInChildren<T>(bool includeInactive=false) where T:class{return gameObject.GetComponentsInChildren<T>(includeInactive);}
 }
 public class MonoBehaviour:Component {public bool isActiveAndEnabled=true;}
 public class GameObject {
  private List<Component> components=new List<Component>(); public Transform transform;public string name;
  public GameObject(string name){this.name=name;transform=new Transform{name=name,gameObject=this};components.Add(transform);}
  public T AddComponent<T>() where T:Component,new(){T c=new T{gameObject=this};components.Add(c);return c;}
  public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T)return c as T;return null;}
  public T GetComponentInChildren<T>() where T:class{var a=GetComponentsInChildren<T>();return a.Length>0?a[0]:null;}
  public T[] GetComponentsInChildren<T>(bool inactive=false) where T:class {var l=new List<T>();foreach(var c in components)if(c is T)l.Add(c as T);foreach(var child in transform.children)l.AddRange(child.gameObject.GetComponentsInChildren<T>(inactive));return l.ToArray();}
 }
 public class Transform:Component {
  public string name;public readonly List<Transform> children=new List<Transform>();private Transform p;
  public Transform parent {get{return p;}set{if(p!=null)p.children.Remove(this);p=value;if(p!=null)p.children.Add(this);}}
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
namespace Alterverse.Characters {
 public enum HeroId {Nyra,Kaelon,Nerok,Mira,Veyr,Rook,Elyra,Traveler,Timmy}
 public class HeroIdentity:UnityEngine.MonoBehaviour {public HeroId Hero;public string DisplayName=>Hero.ToString();}
 public class HeroLocomotion:UnityEngine.MonoBehaviour {public void FaceAction(UnityEngine.Vector3 v,float time){} }
 public static class HeroCombatProfile {public class Stats {public float AttackInterval=1f;}public static Stats StatsFor(HeroId h){return new Stats();}}
}
namespace Alterverse.Multiplayer {public static class CoopAuthority {public static bool CanSimulateHostGameplay=>true;}}
namespace Alterverse.Combat {
 public class LaneMinion:UnityEngine.MonoBehaviour {}
 public class HotZoneTank:UnityEngine.MonoBehaviour {}
 public class BasicAttack:UnityEngine.MonoBehaviour {public float AttackCooldown=1f;}
 public class Health:UnityEngine.MonoBehaviour {public bool IsDead;}
 public class HeroCombatStatus:UnityEngine.MonoBehaviour {public bool Stunned;public static bool CanAct(UnityEngine.GameObject g){return true;}}
}
namespace Alterverse.Visuals {
 public class HeroVisualMotion:UnityEngine.MonoBehaviour {public bool AbilityActionActive,ArmActionActive,Dashing,GroundingSuspended;
  public float PlanarSpeed=6,LocomotionWeight=1,RunBlend=1,GaitPhase;
  public UnityEngine.Quaternion PresentedRotation=UnityEngine.Quaternion.identity;
  public UnityEngine.Vector3 PresentedVelocity=UnityEngine.Vector3.forward*6;
  public UnityEngine.Vector3 GaitDirection=>PresentedVelocity.normalized;
  public void PulseBasic(float t){} }
 public class AuthoredHeroPresentation:UnityEngine.MonoBehaviour {public int finalized;public void FinalizeCombatGrip(bool b){finalized++;}}
 public partial class SiegeTankAppearance:UnityEngine.MonoBehaviour { }
 public class MechMachineGunVisual:UnityEngine.MonoBehaviour {public void SetAim(UnityEngine.Vector3 a,Alterverse.Combat.Health h){}public void Pulse(float t){}}
 public class MonsterAttackCue:UnityEngine.MonoBehaviour {public void Begin(float t,UnityEngine.Vector3 a,Alterverse.Combat.Health h){}}
 public class CreatureAttackMotion:UnityEngine.MonoBehaviour {public void Begin(float t,UnityEngine.Vector3 v){} }
 public class ReferenceMonsterRig:UnityEngine.MonoBehaviour {public void PulseAttack(float t){} }
 public class PlanetSentinelVisual:UnityEngine.MonoBehaviour {public void PulseAttack(float t){} }
 public class LaneUnitVisualMotion:UnityEngine.MonoBehaviour {public void PulseAttack(float t){} }
 public static class CombatAnimationLibrary {
  public static readonly string[] Bones={"Hips","Spine","Chest","Head","UpperArmL","ForearmL","HandL","UpperArmR","ForearmR","HandR","ThighL","ShinL","FootL","ThighR","ShinR","FootR"};
  public class Clip {public string name;public UnityEngine.Quaternion Sample(int bone,float frame,int count){return UnityEngine.Quaternion.identity;}}
  public static Clip Find(string name){return new Clip{name=name};}
  public static float SourceFrame(Clip c,float age,float contact,float end){return 0;}
  public static int MirrorBone(int i){return i;}public static UnityEngine.Quaternion Mirror(UnityEngine.Quaternion q){return q;}
  public static UnityEngine.Quaternion Frame(UnityEngine.Vector3 along,UnityEngine.Vector3 forward){if(along.sqrMagnitude<.000001f)along=UnityEngine.Vector3.up;
   if(UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(along.normalized,forward.normalized))>.98f)forward=UnityEngine.Vector3.up;
   return UnityEngine.Quaternion.LookRotation(along,forward);}
 }
}
