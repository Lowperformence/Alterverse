using System;
using System.Collections.Generic;
namespace UnityEngine
{
 public class Object {public static void Destroy(Object o){}}
 public class MonoBehaviour:Object {}
 public class Transform {public Vector3 localScale=Vector3.one;}
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
