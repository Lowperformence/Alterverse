using System;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero { get { return new Vector3(); } }
        public static Vector3 up { get { return new Vector3(0,1,0); } }
        public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
        public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
        public static Vector3 operator *(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);}
        public static float Dot(Vector3 a,Vector3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
    }
    public static class Mathf
    {
        public static float Abs(float x){return Math.Abs(x);}
        public static float Max(float x,float y){return Math.Max(x,y);}
    }
    public class Transform
    {
        public Transform parent;
        public Vector3 lossyScale=new Vector3(1,1,1),up=Vector3.up;
        public bool IsChildOf(Transform other){for(Transform t=this;t!=null;t=t.parent)if(t==other)return true;return false;}
        public Vector3 TransformPoint(Vector3 point){return point;}
    }
    public class CharacterController
    {
        public float radius=.5f,height=2f,skinWidth=.08f;
        public Vector3 center;
    }
    public class Collider
    {
        public Transform transform=new Transform();
        public object unit;
        public T GetComponentInParent<T>() where T:class {return unit as T;}
    }
    public struct RaycastHit { public Collider collider; public Vector3 normal; public float distance; }
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics
    {
        public static RaycastHit[] Results;
        public static int FallbackCalls;
        public static int CapsuleCastNonAlloc(Vector3 a,Vector3 b,float radius,Vector3 direction,RaycastHit[] buffer,float distance,int mask,QueryTriggerInteraction triggers)
        {int count=Math.Min(buffer.Length,Results.Length);Array.Copy(Results,buffer,count);return count;}
        public static RaycastHit[] CapsuleCastAll(Vector3 a,Vector3 b,float radius,Vector3 direction,float distance,int mask,QueryTriggerInteraction triggers)
        {FallbackCalls++;return Results;}
    }
}
namespace Alterverse.Characters { public class HeroIdentity {} }
namespace Alterverse.Combat
{
    public class LaneMinion {}
    public class TrenchSoldier {}
    public static class GameplayCollision { public const int MovementMask=1; }
}
public static class Regression
{
    private static UnityEngine.RaycastHit Hit(UnityEngine.Collider collider,float distance,UnityEngine.Vector3 normal)
    {return new UnityEngine.RaycastHit{collider=collider,distance=distance,normal=normal};}
    private static void Expect(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Main()
    {
        SweepUnderTest source=new SweepUnderTest();
        UnityEngine.Vector3 into=new UnityEngine.Vector3(-1,0,0);
        UnityEngine.RaycastHit[] ignored=new UnityEngine.RaycastHit[40];
        for(int i=0;i<ignored.Length;i++)
        {
            UnityEngine.Collider c=new UnityEngine.Collider();
            UnityEngine.Vector3 normal=into;
            switch(i%5)
            {
                case 0:c.unit=new Alterverse.Characters.HeroIdentity();break;
                case 1:c.unit=new Alterverse.Combat.LaneMinion();break;
                case 2:c.unit=new Alterverse.Combat.TrenchSoldier();break;
                case 3:c.transform.parent=source.transform;break;
                case 4:normal=UnityEngine.Vector3.up;break;
            }
            ignored[i]=Hit(c,.1f,normal);
        }
        UnityEngine.Physics.Results=ignored;
        source.Run(out float allowed,out UnityEngine.Vector3 normalOut,out UnityEngine.Collider blocker);
        Expect(Math.Abs(allowed-2f)<.00001f&&blocker==null,"Saturated ignored hits blocked movement");
        Expect(UnityEngine.Physics.FallbackCalls==1,"Saturated query did not load full results");
        UnityEngine.Collider near=new UnityEngine.Collider(),far=new UnityEngine.Collider();
        UnityEngine.RaycastHit[] crowded=new UnityEngine.RaycastHit[42];Array.Copy(ignored,crowded,40);
        crowded[40]=Hit(far,1.5f,into);crowded[41]=Hit(near,.45f,into);
        UnityEngine.Physics.Results=crowded;
        source.Run(out allowed,out normalOut,out blocker);
        Expect(Math.Abs(allowed-.425f)<.00001f&&blocker==near,"Hidden nearest solid blocker was missed");
        UnityEngine.Physics.Results=new[]{Hit(far,1.5f,into),Hit(near,.45f,into)};
        int fallbackBefore=UnityEngine.Physics.FallbackCalls;
        source.Run(out allowed,out normalOut,out blocker);
        Expect(Math.Abs(allowed-.425f)<.00001f&&blocker==near,"Normal query changed collision distance");
        Expect(UnityEngine.Physics.FallbackCalls==fallbackBefore,"Normal query allocated fallback results");
        UnityEngine.Physics.Results=new[]{Hit(near,.1f,new UnityEngine.Vector3(1,0,0))};
        source.Run(out allowed,out normalOut,out blocker);
        Expect(Math.Abs(allowed-2f)<.00001f&&blocker==null,"Moving away from a contact was blocked");
        Console.WriteLine("PASS: saturated ignored hits, hidden nearest blocker, allocation-free normal query, moving away from contact");
    }
}
