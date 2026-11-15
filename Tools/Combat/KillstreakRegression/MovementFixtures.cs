using System;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;

namespace UnityEngine
{
 public enum Space {Self}
 public struct Vector3
 {
  public float x,y,z;
  public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new();public static Vector3 up=>new(0,1,0);
  public static Vector3 right=>new(1,0,0);public static Vector3 left=>new(-1,0,0);
  public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public Vector3 normalized=>magnitude>0?this*(1/magnitude):zero;
  public void Normalize(){this=normalized;}
  public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
 }
 public class Transform {public Vector3 position;public float Rotation;public void Rotate(Vector3 axis,float angle,Space space){Rotation+=angle;}}
 public static class Time {public static float time,deltaTime=.1f;}
 public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);}
}
namespace Alterverse.Combat
{
 public static class ArenaLayout {public const float MovementHalfWidth=41,MovementHalfHeight=35;}
 public static class LunarSurfaceField {public static Vector3 WorldSurface(float x,float z)=>new(x,2,z);}
}
namespace Alterverse.Characters
{
 // Collision is controlled: straight movement is blocked while a sideways detour is clear.
 public class HeroLocomotion
 {
  public Transform Actor;public bool BlockStraight,CanFindDirection=true;public int Moves,Probes;public Vector3 Facing;
  public Vector3 Move(Vector3 step,float width,float height)
  {Moves++;if(BlockStraight&&Math.Abs(step.x)>Math.Abs(step.z))return Vector3.zero;Actor.position+=step;return step;}
  public void FaceMovement(Vector3 direction){Facing=direction;}
  public bool TryFindClearDirection(Vector3 desired,float distance,int side,out Vector3 direction)
  {Probes++;direction=new Vector3(0,0,side);return CanFindDirection;}
 }
}
namespace Alterverse.UI
{
 public class KillstreakBoltTurret
 {
  public Transform transform=new();private HeroLocomotion locomotion;
  private float blockedTime,detourUntil;private Vector3 detourDirection;
  private int movementRecoveryAttempt,team;
  private readonly Transform[] wheels={new Transform(),new Transform(),new Transform(),new Transform()};
  public HeroLocomotion Motor=>locomotion;
  public float WheelRotation=>wheels[0].Rotation;
  public KillstreakBoltTurret(){locomotion=new HeroLocomotion{Actor=transform};}
  public void Step(Vector3 destination,float stopDistance)=>MoveToward(destination,stopDistance);
  /*PRODUCTION_METHOD*/
 }
}
static class MovementRegression
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Main()
 {
  var t=new Alterverse.UI.KillstreakBoltTurret();
  Time.time=0;t.Step(new Vector3(10,0,0),0);
  Check(t.transform.position.x>0&&t.transform.position.y==2&&t.WheelRotation>0,"direct driving follows terrain and rotates wheels");
  int before=t.Motor.Moves;t.Step(t.transform.position,1);Check(t.Motor.Moves==before,"turret stops at its destination");
  t=new Alterverse.UI.KillstreakBoltTurret();t.Motor.BlockStraight=true;
  for(int i=0;i<5;i++){Time.time=i*.1f;t.Step(new Vector3(10,0,0),0);}
  Check(t.Motor.Probes>0&&Math.Abs(t.transform.position.z)>0,"blocked turret probes and drives around an obstacle");
  t=new Alterverse.UI.KillstreakBoltTurret();t.Motor.BlockStraight=true;t.Motor.CanFindDirection=false;
  for(int i=0;i<5;i++){Time.time=i*.1f;t.Step(new Vector3(10,0,0),0);}
  Check(t.Motor.Probes>0&&Math.Abs(t.transform.position.z)>0,"fallback steering still attempts a sideways detour");
  Console.WriteLine("PASS mobile turret movement, stopping and obstacle recovery (controlled collision fixture)");
 }
}
