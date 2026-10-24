using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public class Object { public string name; }
    public class ScriptableObject : Object {}
    public class Component : Object
    {
        public GameObject gameObject;
        public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
    }
    public sealed class GameObject : Object
    {
        public readonly List<Component> Components=new();
        public GameObject(){Add<Transform>();}
        public T Add<T>() where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
        public T GetComponent<T>() where T:class=>Components.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class=>Components.OfType<T>().ToArray();
    }
    public sealed class Transform : Component {}
    public sealed class MeshFilter : Component {}
    public class Renderer : Component {public Material[] sharedMaterials=Array.Empty<Material>();}
    public sealed class MeshRenderer : Renderer {}
    public sealed class ParticleSystemRenderer : Renderer {}
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
    }
    public static class Mathf {public static float Clamp(float v,float min,float max)=>Math.Clamp(v,min,max);}
    public sealed class Shader : Object {}
    public sealed class Material : Object {public Shader shader;public object mainTexture;}
}
namespace UnityEditor
{
    public static class AssetDatabase
    {
        public static readonly Dictionary<string,object> Assets=new();
        public static T LoadAssetAtPath<T>(string path) where T:class=>Assets.TryGetValue(path,out var value)?value as T:null;
    }
}
namespace UnityEditor.Build {public sealed class BuildFailedException:Exception{public BuildFailedException(string message):base(message){}}}
