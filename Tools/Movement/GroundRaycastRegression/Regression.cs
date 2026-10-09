using System;
using System.Collections.Generic;

// Minimal substitutes: layer/trigger filtering and the hit-buffer limit are
// deliberately inside Physics, where Unity performs them before Ground sees hits.
struct Vector3
{
    public float x, y, z;
    public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
    public static Vector3 up => new Vector3(0,1,0);
    public static Vector3 down => new Vector3(0,-1,0);
}
class Transform
{
    public Transform parent;
    public bool IsChildOf(Transform ancestor)
    { for (Transform current=this; current!=null; current=current.parent) if(current==ancestor)return true; return false; }
}
class HeroIdentity {}
class CharacterController {}
class Collider
{
    public Transform transform=new Transform();
    public int layer;
    public bool trigger, hero, controller;
    public T GetComponentInParent<T>() where T:class
    {
        if(typeof(T)==typeof(HeroIdentity) && hero)return new HeroIdentity() as T;
        if(typeof(T)==typeof(CharacterController) && controller)return new CharacterController() as T;
        return null;
    }
}
struct RaycastHit { public Collider collider; public Vector3 point, normal; public float distance; }
enum QueryTriggerInteraction { Ignore }
static class GameplayCollision { public const int Ground=10; }
static class Mathf { public static float Min(float a,float b)=>Math.Min(a,b); }
static class Physics
{
    public const int DefaultRaycastLayers=~(1<<2);
    public static readonly List<RaycastHit> scene=new List<RaycastHit>();
    public static int lastMask, lastCount;
    public static int RaycastNonAlloc(Vector3 origin,Vector3 direction,RaycastHit[] buffer,float range,int mask,QueryTriggerInteraction triggers)
    {
        if(direction.y!=-1 || range<=0)throw new Exception("Unexpected query geometry");
        lastMask=mask; int count=0;
        foreach(RaycastHit original in scene)
        {
            Collider collider=original.collider;
            if(collider!=null && ((mask&(1<<collider.layer))==0 || collider.trigger))continue;
            float distance=origin.y-original.point.y;
            if(distance<0 || distance>range)continue;
            if(count==buffer.Length)break;
            RaycastHit hit=original; hit.distance=distance; buffer[count++]=hit;
        }
        lastCount=count; return count;
    }
}
partial class GroundHarness
{
    class Leg { public float sole=.08f; }
    class Motion { public Vector3 PresentedPosition=new Vector3(0,1,0); }
    readonly RaycastHit[] hits=new RaycastHit[24];
    readonly Motion motion=new Motion();
    readonly Transform transform=new Transform();
    float floorOffset=-1.02f;
    float LegLength=>1f;
    static int assertions;
    static void Check(bool condition,string message)
    { assertions++; if(!condition)throw new Exception(message); }
    static void Near(float actual,float expected,string message)
    { Check(Math.Abs(actual-expected)<.00001f,message+": "+actual+" expected "+expected); }
    Collider Add(int layer,float y,Vector3 normal,bool trigger=false,bool self=false,bool hero=false,bool controller=false)
    {
        var collider=new Collider { layer=layer,trigger=trigger,hero=hero,controller=controller };
        if(self)collider.transform.parent=transform;
        Physics.scene.Add(new RaycastHit {collider=collider,point=new Vector3(0,y,0),normal=normal});
        return collider;
    }
    void Both(float floor,Vector3 expectedNormal,string scenario,int expectedHits=-1)
    {
        foreach(float x in new[]{-.18f,.18f})
        {
            var point=new Vector3(x,999,.3f);
            Vector3 normal;
            Vector3 result=Ground(point,new Leg(),out normal);
            Near(result.y,floor+.08f,scenario+" selected floor");
            Near(result.x,x,scenario+" preserves foot x"); Near(result.z,.3f,scenario+" preserves foot z");
            Near(normal.x,expectedNormal.x,scenario+" normal x");
            Near(normal.y,expectedNormal.y,scenario+" normal y");
            Near(normal.z,expectedNormal.z,scenario+" normal z");
            Check(Physics.lastMask==(1<<10),scenario+" Ground layer exclusively");
            if(expectedHits>=0)Check(Physics.lastCount==expectedHits,scenario+" prefiltered hit count");
        }
    }
    void Run()
    {
        // More unrelated hits than the production buffer can hold. A default
        // mask loses the real floor entirely, even if later code ignores heroes.
        foreach(int layer in new[]{8,0,9})for(int i=0;i<20;i++)Add(layer,.05f+i*.04f,Vector3.up);
        Add(10,-.02f,Vector3.up);
        Both(-.02f,Vector3.up,"crowded units/default/structures",1);

        Physics.scene.Clear();
        Add(10,.8f,Vector3.up,trigger:true);
        Add(10,.7f,new Vector3(.9f,.4f,0)); // steep surface
        Add(10,.6f,Vector3.up,self:true);
        Add(10,.5f,Vector3.up,hero:true);
        Add(10,.4f,Vector3.up,controller:true);
        Add(10,1.3f,Vector3.up); // exceeds actor-height gate
        Add(10,-.02f,Vector3.up);
        Add(10,-.2f,Vector3.up); // farther than the real floor
        Both(-.02f,Vector3.up,"triggers slopes self actors height and nearest",7);

        Physics.scene.Clear();
        var slope=new Vector3(.6f,.8f,0);
        Add(10,-.02f,Vector3.up); Add(10,.1f,slope);
        Both(.1f,slope,"valid sloping Ground",2);

        Physics.scene.Clear();
        Both(-.02f,Vector3.up,"empty scene deterministic fallback",0);
        Add(8,.8f,Vector3.up); Add(0,.7f,Vector3.up); Add(9,.6f,Vector3.up);
        Both(-.02f,Vector3.up,"no Ground but unrelated geometry fallback",0);
    }
    static void Main()
    { new GroundHarness().Run(); Console.WriteLine("Ground raycast regression PASS: "+assertions+" assertions (simulated physics, exact production Ground; no Unity runtime)."); }
}
