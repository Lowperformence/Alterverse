using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
 public class Object {public static void Destroy(Object o){}public static readonly List<object> Scene=new();public static T[] FindObjectsByType<T>()=>Scene.OfType<T>().ToArray();}
 public class MonoBehaviour:Object {public Transform transform=new();}
 public class Transform {public Vector3 position;public Vector3 localScale=Vector3.one;}
 public class SerializeField:Attribute {}
 public static class Time {public static float time,deltaTime=1f;}
 public class DisallowMultipleComponent:Attribute {}
 public enum RuntimeInitializeLoadType {SubsystemRegistration}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public static class Physics {public static void IgnoreLayerCollision(int a,int b,bool enabled){}}
 public struct Vector2
 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
  public float magnitude=>MathF.Sqrt(x*x+y*y);
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);
  public static Vector2 operator *(Vector2 a,float s)=>new(a.x*s,a.y*s);
 }
 public struct Vector3
 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new(0,0,0);public static Vector3 one=>new(1,1,1);
 }
 public static class Mathf
 {
  public const float Deg2Rad=MathF.PI/180f;
  public static float Max(float a,float b)=>MathF.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);
  public static float Min(float a,float b)=>MathF.Min(a,b);
  public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);
  public static float Abs(float v)=>MathF.Abs(v);public static float Cos(float v)=>MathF.Cos(v);public static float Sin(float v)=>MathF.Sin(v);
 }
 public class Mesh:Object
 {
  public string name;public List<Vector3> vertices;public List<int> triangles;
  public void SetVertices(List<Vector3> v){vertices=v;}public void SetTriangles(List<int> t,int sub){triangles=t;}
  public void RecalculateNormals(){}public void RecalculateBounds(){}
 }
}

namespace Alterverse.Multiplayer {public static class CoopAuthority {public static bool CanSimulateHostGameplay=true;}}
namespace Alterverse.Combat
{
 public class Health:UnityEngine.MonoBehaviour {public bool IsDead;public float Received;public TeamMember Member;public T GetComponent<T>() where T:class=>Member as T;public void Heal(float amount){Received+=amount;}}
 public class TeamMember {public int Team;public Health Health;public T GetComponent<T>() where T:class=>Health as T;}
 public static class DefenseBunker {public static readonly System.Collections.Generic.List<UnityEngine.Vector3> Positions=new();public static void Spawn(int team,UnityEngine.Vector3 p,string flank){Positions.Add(p);}}
 public static class DefenseTrench {public static readonly System.Collections.Generic.List<UnityEngine.Vector3> Positions=new();public static void Spawn(int team,UnityEngine.Vector3 p,string flank){Positions.Add(p);}}
}
