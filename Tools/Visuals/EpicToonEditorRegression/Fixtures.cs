using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void DestroyImmediate(Object value){}
        public static GameObject Instantiate(GameObject source,Transform parent,bool world)=>source.Clone(parent);
    }
    public class ScriptableObject : Object {}
    public class Component : Object
    {
        public GameObject gameObject;
        public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
    }
    public sealed class GameObject : Object
    {
        public readonly List<Component> Components=new();
        public Transform transform=>GetComponent<Transform>();
        public GameObject(string name=""){this.name=name;Add<Transform>();}
        public T Add<T>() where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
        public T GetComponent<T>() where T:class=>Components.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class=>Components.OfType<T>()
            .Concat(transform.Children.SelectMany(t=>t.gameObject.GetComponentsInChildren<T>(inactive))).ToArray();
        public GameObject Clone(Transform parent)
        {
            var clone=new GameObject{name=name};clone.transform.SetParent(parent,false);
            foreach(var source in Components.Where(c=>c is not Transform))
            {
                var copy=(Component)Activator.CreateInstance(source.GetType());copy.gameObject=clone;copy.name=source.name;clone.Components.Add(copy);
                if(source is Renderer sr&&copy is Renderer cr)cr.sharedMaterials=sr.sharedMaterials;
                if(source is ParticleSystem sp&&copy is ParticleSystem cp){cp.main.maxParticles=sp.main.maxParticles;cp.main.simulationSpace=sp.main.simulationSpace;}
            }
            foreach(var child in transform.Children)child.gameObject.Clone(clone.transform);
            return clone;
        }
    }
    public struct Quaternion
    {
        public float x,y,z,w;
        public static Quaternion identity=>new(){w=1};
        public static Quaternion Euler(float x,float y,float z)
        {
            var q=System.Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180);
            return new(){x=q.X,y=q.Y,z=q.Z,w=q.W};
        }
    }
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 one=>new(1,1,1);public static Vector3 right=>new(1,0,0);
        public static Vector3 up=>new(0,1,0);public static Vector3 forward=>new(0,0,1);
        public static Vector3 operator *(Vector3 a,float s)=>new(a.x*s,a.y*s,a.z*s);
    }
    public sealed class Transform : Component
    {
        public Quaternion localRotation=Quaternion.identity;public Vector3 localPosition,localScale=Vector3.one;
        public Transform parent;public readonly List<Transform> Children=new();
        public void SetParent(Transform value,bool world){parent?.Children.Remove(this);parent=value;parent?.Children.Add(this);}
    }
    public sealed class MeshFilter : Component {}
    public class Renderer : Component {public Material[] sharedMaterials=Array.Empty<Material>();}
    public sealed class MeshRenderer : Renderer {}
    public enum ParticleSystemRenderMode{Billboard,Stretch,HorizontalBillboard,VerticalBillboard,Mesh,None}
    public enum ParticleSystemRenderSpace{View,World,Local,Facing,Velocity}
    public enum ParticleSystemSimulationSpace{Local,World,Custom}
    public sealed class ParticleSystemRenderer : Renderer {public ParticleSystemRenderMode renderMode;public ParticleSystemRenderSpace alignment;}
    public sealed class ParticleSystem : Component
    {
        public sealed class TrailsModule{public bool enabled;}public TrailsModule trails=new();
        public struct MinMaxCurve{public float constantMax;public static implicit operator MinMaxCurve(float v)=>new(){constantMax=v};}
        public struct Burst{public float time;public short minCount;public Burst(float time,short count){this.time=time;minCount=count;}}
        public sealed class EmissionModule
        {
            public bool enabled=true;public MinMaxCurve rateOverTime,rateOverDistance;
            public Burst[] bursts=Array.Empty<Burst>();public int burstCount=>bursts.Length;
            public void SetBursts(Burst[] value){bursts=value;}
        }
        public EmissionModule emission=new();
        public sealed class MainModule {public bool startRotation3D;public int maxParticles=96;public ParticleSystemSimulationSpace simulationSpace;public MinMaxCurve startRotationX,startRotationY,startRotationZ;}
        public MainModule main=new();
    }
    public static class Mathf
    {
        public const float PI=MathF.PI;public static float Sin(float x)=>MathF.Sin(x);public static float Cos(float x)=>MathF.Cos(x);
        public static float Clamp(float v,float min,float max)=>Math.Clamp(v,min,max);
        public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
        public static float Max(float a,float b)=>Math.Max(a,b);
    }
    public sealed class Shader : Object {}
    public sealed class Material : Object {public Shader shader;public object mainTexture;}
}
namespace UnityEditor
{
    public static class PrefabUtility
    {
        public static UnityEngine.GameObject SaveAsPrefabAsset(UnityEngine.GameObject source,string path)
        {AssetDatabase.Assets[path]=source;return source;}
    }
    public static class AssetDatabase
    {
        public static readonly Dictionary<string,object> Assets=new();
        public static T LoadAssetAtPath<T>(string path) where T:class=>Assets.TryGetValue(path,out var value)?value as T:null;
    }
}
namespace UnityEditor.Build {public sealed class BuildFailedException:Exception{public BuildFailedException(string message):base(message){}}}
