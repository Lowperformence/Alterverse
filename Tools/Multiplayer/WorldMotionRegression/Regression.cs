using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Alterverse.Multiplayer;
using Alterverse.Visuals;

namespace UnityEngine
{
    public class SerializeField:Attribute {}
    public class DefaultExecutionOrder:Attribute {public int Order;public DefaultExecutionOrder(int order){Order=order;}}
    public class Component
    {
        public GameObject gameObject;public Transform transform=>gameObject.transform;
        public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool inactive)where T:class=>gameObject.Components.OfType<T>().ToArray();
    }
    public class MonoBehaviour:Component {}
    public class GameObject
    {
        public string name="monster";public Transform transform=new();public readonly List<object> Components=new();
        public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
        public T GetComponent<T>()where T:class=>Components.OfType<T>().FirstOrDefault();
    }
    public class Renderer:Component {public bool enabled=true;}
    public class Collider:Component {public bool enabled=true;}
    public class Transform
    {
        public Vector3 position;public Quaternion rotation=Quaternion.identity;
        public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}
    }
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new();public float sqrMagnitude=>x*x+y*y+z*z;
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Math.Clamp(t,0,1);
    }
    public struct Quaternion
    {
        public float x,y,z,w;public static Quaternion identity=>new(){w=1};
        public static Quaternion Euler(float x,float yaw,float z){float h=yaw*MathF.PI/360;return new(){y=MathF.Sin(h),w=MathF.Cos(h)};}
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)
        {var q=System.Numerics.Quaternion.Slerp(new(a.x,a.y,a.z,a.w),new(b.x,b.y,b.z,b.w),t);return new(){x=q.X,y=q.Y,z=q.Z,w=q.W};}
    }
    public static class Mathf
    {
        public static int RoundToInt(float n)=>(int)MathF.Round(n);
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static float Exp(float n)=>MathF.Exp(n);
        public static float InverseLerp(float a,float b,float value)=>a==b?0:Math.Clamp((value-a)/(b-a),0,1);
    }
    public static class Time {public static float unscaledTime,unscaledDeltaTime;}
}
namespace Alterverse.Combat {public class TeamMember:Component {public int Team;}}
namespace Alterverse.Multiplayer {public static class CoopAuthority {public static bool Active=true,LocalIsHost,LocalHasServerAuthority;}}
class Regression
{
    static int checks;
    static readonly MethodInfo tick=typeof(CoopWorldEntityKey).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance);
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static CoopWorldEntityKey New()
    {var g=new GameObject();g.AddComponent<Renderer>();g.AddComponent<Collider>();var key=g.AddComponent<CoopWorldEntityKey>();key.Configure("HOTZONE:0");return key;}
    static double Deviation(List<float> samples)
    {double average=samples.Average();return Math.Sqrt(samples.Select(x=>(x-average)*(x-average)).Average());}
    static void ArrivalJitterProbe()
    {
        foreach(int fps in new[]{30,60,120})
        {
            var worldKey=New();float worldPrevious=0;var buffer=new HeroPresentationBuffer();var corrected=new HeroPresentationBuffer();int next=0;float previous=0,correctedPrevious=0;var speeds=new List<float>();var correctedSpeeds=new List<float>();
            for(int frame=0;frame<fps*6;frame++)
            {
                float now=(float)frame/fps;
                while(next*.05f+(next%2==0?.02f:.06f)<=now+.000001f)
                {
                    // Server moves at a constant 4.77 units/s and sends at exactly 20Hz.
                    // Only network arrival spacing alternates by 40ms.
                    float received=next*.05f+(next%2==0?.02f:.06f);
                    buffer.Push(new Vector3(next*.05f*4.77f,0,0),Quaternion.identity,received,false);
                    corrected.PushRemote(new Vector3(next*.05f*4.77f,0,0),Quaternion.identity,next*.05f+900f,received,false);
                    Time.unscaledTime=received;worldKey.ApplyTarget(new Vector3(next*.05f*4.77f,0,0),0,false,next*.05f+900f);next++;
                }
                buffer.Evaluate(now,out var p,out _);corrected.Evaluate(now,out var fixedPose,out _);
                if(frame>fps){correctedSpeeds.Add((fixedPose.x-correctedPrevious)*fps);Check(Math.Abs((fixedPose.x-correctedPrevious)*fps-4.77f)<.025f,"source timeline preserves steady speed under 40ms arrival jitter and clock offset");}
                correctedPrevious=fixedPose.x;
                Time.unscaledTime=now;tick.Invoke(worldKey,null);
                if(frame>fps)Check(Math.Abs((worldKey.transform.position.x-worldPrevious)*fps-4.77f)<.025f,"production world entity forwards source time before gait sampling");
                worldPrevious=worldKey.transform.position.x;
                if(frame>fps)speeds.Add((p.x-previous)*fps);
                previous=p.x;
            }
            Console.WriteLine($"ARRIVAL JITTER PROBE {fps} FPS: constant server speed 4.77, rendered min={speeds.Min():F2}, max={speeds.Max():F2}, speed deviation={Deviation(speeds):F2}; corrected deviation={Deviation(correctedSpeeds):F4} units/s");
        }
    }
    static void Main()
    {
        ArrivalJitterProbe();
        var source=new HeroPresentationBuffer();source.PushRemote(new Vector3(1,0,0),Quaternion.identity,500,10,false);
        source.PushRemote(new Vector3(2,0,0),Quaternion.identity,500.1f,10.1f,false);
        source.Evaluate(11,out var held,out _);Check(held.x==2,"source-timed packet gap holds latest pose without extrapolation");
        source.PushRemote(new Vector3(30,0,0),Quaternion.identity,501,11,true);source.Evaluate(11,out var resetPose,out _);
        Check(resetPose.x==30,"source-timed relocation clears old motion immediately");
        source.PushRemote(new Vector3(99,0,0),Quaternion.identity,float.NaN,11.1f,false);source.Evaluate(12,out held,out _);
        Check(held.x==30,"nonfinite source timestamp cannot poison the buffer");
        source.Clear();source.Push(Vector3.zero,Quaternion.identity,0,false);source.Evaluate(0,out var localPose,out _);
        Check(localPose.x==0,"clearing source timeline restores local/offline clock semantics");
        foreach(int fps in new[]{30,60,120})
        {
            Time.unscaledTime=0;var key=New();key.ApplyTarget(new Vector3(0,0,0),0,true);
            float next=.125f,target=0,legacy=0,previous=0,oldPrevious=0;
            var velocities=new List<float>();var oldVelocities=new List<float>();
            for(int frame=1;frame<=fps*4;frame++)
            {
                Time.unscaledTime=(float)frame/fps;Time.unscaledDeltaTime=1f/fps;
                while(Time.unscaledTime+.000001f>=next)
                {target=next*2.25f;key.ApplyTarget(new Vector3(target,0,0),0,false);next+=.125f;}
                tick.Invoke(key,null);
                legacy+=(target-legacy)*(1-MathF.Exp(-16f/fps));
                float current=key.transform.position.x;
                Check(current>=previous-.00001f&&current<=target+.00001f,"world interpolation neither reverses nor extrapolates beyond received motion");
                if(frame>fps){velocities.Add((current-previous)*fps);oldVelocities.Add((legacy-oldPrevious)*fps);}
                previous=current;oldPrevious=legacy;
            }
            double smooth=Deviation(velocities),old=Deviation(oldVelocities);
            Console.WriteLine($"{fps} FPS / 8Hz: speed deviation old={old:F4}, buffered={smooth:F4}");
            Check(smooth<old*.5,"8 Hz movement no longer reaccelerates at each packet");
            Time.unscaledTime+=1;tick.Invoke(key,null);
            Check(Math.Abs(key.transform.position.x-target)<.00001f,"packet gap holds last known pose without overshoot");
            key.ApplyTarget(new Vector3(30,0,0),90,false);
            Check(key.transform.position.x==30,"large relocation resets buffer immediately");
            key.ApplyTarget(new Vector3(4,0,0),0,true);tick.Invoke(key,null);
            Check(key.transform.position.x==4,"explicit relocation cannot interpolate through the arena");
            key.ApplyAlivePresentation(false);
            Check(!key.GetComponent<Renderer>().enabled&&!key.GetComponent<Collider>().enabled,"death presentation still hides renderers and colliders");
            key.ApplyAlivePresentation(true);
            Check(key.GetComponent<Renderer>().enabled&&key.GetComponent<Collider>().enabled,"alive presentation restores authored state");
        }
        Time.unscaledTime=0;var authority=New();authority.ApplyTarget(Vector3.zero,0,true);
        Time.unscaledTime=.125f;authority.ApplyTarget(new Vector3(1,0,0),0,false);
        CoopAuthority.LocalHasServerAuthority=true;CoopAuthority.LocalIsHost=false;
        Time.unscaledTime=.3f;tick.Invoke(authority,null);
        Check(authority.transform.position.x==0,"dedicated server with LocalIsHost false is never driven by client interpolation");
        CoopAuthority.LocalHasServerAuthority=false;tick.Invoke(authority,null);
        Check(authority.transform.position.x>0,"remote client consumes received world movement");
        var defaultHeroBuffer=new HeroPresentationBuffer();defaultHeroBuffer.Push(Vector3.zero,Quaternion.identity,0,false);
        defaultHeroBuffer.Push(new Vector3(1,0,0),Quaternion.identity,.2f,false);
        defaultHeroBuffer.Evaluate(.2f,out var pose,out var yaw);
        Check(Math.Abs(pose.x-.5f)<.00001f,"hero buffer retains its existing 100 ms delay by default");
        var order=(DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(CoopWorldEntityKey),typeof(DefaultExecutionOrder));
        Check(order.Order<0,"world pose updates before default-order monster gait samples movement");
        Console.WriteLine($"PASS: {checks} world motion checks. Controlled timing/render fixtures; native Unity/network/Android untested.");
    }
}

