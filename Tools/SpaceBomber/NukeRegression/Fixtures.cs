// Minimal controlled engine/editor substitutes. They do not render the licensed asset.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object value) { if(value is GameObject g) g.Destroyed=true; }
        public static void DestroyImmediate(Object value) { if(value is Component c)c.gameObject.Components.Remove(c);else Destroy(value); }
        public static GameObject Instantiate(GameObject value,Transform parent,bool world) {var g=value.Clone();g.transform.SetParent(parent,false);return g;}
        public static GameObject Instantiate(GameObject value,Vector3 position,Quaternion rotation) {var g=value.Clone();g.transform.position=position;return g;}
    }
    public class Component:Object
    {
        public GameObject gameObject;
        public Transform transform=>gameObject.transform;
        public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool inactive=false)where T:class=>gameObject.GetComponentsInChildren<T>(inactive);
    }
    public class MonoBehaviour:Component {}
    public class GameObject:Object
    {
        public static readonly List<GameObject> All=new();
        public readonly List<Component> Components=new();
        public bool activeSelf=true,Destroyed;public int layer;public string tag;
        public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
        public Transform transform=>Components.OfType<Transform>().Single();
        public GameObject(string name=""){this.name=name;All.Add(this);Components.Add(new Transform{gameObject=this});}
        public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
        public T GetComponent<T>()where T:class=>Components.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool inactive=false)where T:class=>(inactive||activeInHierarchy?Components.OfType<T>():Enumerable.Empty<T>())
            .Concat(transform.Children.SelectMany(c=>c.gameObject.GetComponentsInChildren<T>(inactive))).ToArray();
        public void SetActive(bool value)
        {
            bool before=activeSelf;activeSelf=value;
            if(value&&!before){foreach(var ps in GetComponentsInChildren<ParticleSystem>())if(ps.main.playOnAwake)ps.Play(false);
                foreach(var c in GetComponentsInChildren<MonoBehaviour>())Invoke(c,"Awake");}
        }
        public static void Invoke(object value,string method)=>value.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(value,null);
        public GameObject Clone()
        {
            var copy=new GameObject(name){activeSelf=activeSelf,layer=layer,tag=tag};copy.transform.localScale=transform.localScale;copy.transform.localPosition=transform.localPosition;
            foreach(var source in Components.Where(c=>c is not Transform))
            {
                var target=(Component)Activator.CreateInstance(source.GetType());target.gameObject=copy;copy.Components.Add(target);
                foreach(var field in source.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))if(field.Name!="gameObject"&&!field.IsInitOnly)field.SetValue(target,field.GetValue(source));
                if(source is ParticleSystem sp&&target is ParticleSystem tp){tp.main=sp.main.Copy();tp.collision=new(){enabled=sp.collision.enabled};tp.trigger=new(){enabled=sp.trigger.enabled};tp.lights=new(){enabled=sp.lights.enabled};}
                if(source is Renderer sr&&target is Renderer tr)tr.sharedMaterials=sr.sharedMaterials.ToArray();
            }
            foreach(var child in transform.Children)child.gameObject.Clone().transform.SetParent(copy.transform,false);
            return copy;
        }
    }
    public sealed class Transform:Component
    {
        public Vector3 localPosition,localScale=Vector3.one;public Transform parent;public readonly List<Transform> Children=new();
        public Vector3 position {get=>parent==null?localPosition:parent.position+localPosition;set=>localPosition=value;}
        public void SetParent(Transform next,bool world){parent?.Children.Remove(this);parent=next;parent?.Children.Add(this);}
    }
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new(1,1,1);
        public static Vector3 operator*(Vector3 v,float s)=>new(v.x*s,v.y*s,v.z*s);public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);}
    public struct Vector4 {public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}}
    public struct Quaternion {public static Quaternion identity=>new();}
    public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1);}
    public struct Bounds {public Vector3 center,extents;}
    public static class Time {public static float time;}
    public static class Mathf {public static float Max(params float[] v)=>v.Max();public static float Abs(float v)=>Math.Abs(v);}
    public class HideInInspector:Attribute {}public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
    public enum RuntimeInitializeLoadType {SubsystemRegistration}
    public static class Debug {public static int Warnings;public static void LogWarning(object value){Warnings++;}public static void LogError(object value){throw new Exception(value.ToString());}}
    public static class Resources {public static GameObject Prefab;public static T Load<T>(string path)where T:class=>Prefab as T;}
    public sealed class MaterialPropertyBlock {public readonly Dictionary<string,Vector4> Vectors=new();public void SetVector(string key,Vector4 value)=>Vectors[key]=value;}
    public class Renderer:Component
    {
        public bool enabled=true,receiveShadows;public Rendering.ShadowCastingMode shadowCastingMode;public Rendering.LightProbeUsage lightProbeUsage;public Rendering.ReflectionProbeUsage reflectionProbeUsage;
        public Material[] sharedMaterials=Array.Empty<Material>();public Bounds bounds=>new(){center=transform.position,extents=new(6,50,4)};
        public MaterialPropertyBlock Block;public void GetPropertyBlock(MaterialPropertyBlock block){}public void SetPropertyBlock(MaterialPropertyBlock block)=>Block=block;
    }
    public sealed class MeshRenderer:Renderer {}public sealed class MeshFilter:Component {}
    public class ParticleSystemRenderer:Renderer {public Material trailMaterial;}
    public enum ParticleSystemStopBehavior {StopEmittingAndClear}
    public enum ParticleSystemStopAction {None,Destroy}
    public enum ParticleSystemScalingMode {Hierarchy,Local}
    public enum ParticleSystemSimulationSpace {Local,World}
    public enum ParticleSystemCullingMode {AlwaysSimulate}
    public sealed class ParticleSystem:Component
    {
        public sealed class Module {public bool enabled=true;}
        public sealed class MainModule
        {
            public bool loop=true,playOnAwake=true,useUnscaledTime;public int maxParticles=321;public float startLifetime=7,startSpeed=8;
            public ParticleSystemStopAction stopAction=ParticleSystemStopAction.Destroy;public ParticleSystemScalingMode scalingMode=ParticleSystemScalingMode.Local;
            public ParticleSystemSimulationSpace simulationSpace=ParticleSystemSimulationSpace.World;public ParticleSystemCullingMode cullingMode;
            public MainModule Copy()=>(MainModule)MemberwiseClone();
        }
        public MainModule main=new();public Module collision=new(),trigger=new(),lights=new(),subEmitters=new();
        public bool useAutoRandomSeed=true,Playing;public uint randomSeed;public float Age;
        public int particleCount=>IsAlive(false)?1:0;
        public bool IsAlive(bool children)=>Playing&&Age<.3f;
        public void Stop(bool children,ParticleSystemStopBehavior behavior){Playing=false;Age=0;}
        public void Play(bool children){Playing=true;Age=0;}
        public void Simulate(float delta,bool children,bool restart,bool fixedTimeStep)
        {Age+=delta;if(children)foreach(var c in transform.Children)foreach(var ps in c.gameObject.GetComponentsInChildren<ParticleSystem>(true))ps.Age+=delta;}
    }
    public class Shader:Object {public static Shader Find(string name)=>new(){name=name};}
    public class Texture:Object {}
    public class Material:Object
    {
        public Shader shader;public int renderQueue;public readonly Dictionary<string,object> Values=new();
        public Material(Shader shader){this.shader=shader;}
        public bool HasProperty(string key)=>Values.ContainsKey(key);public Texture GetTexture(string key)=>Values.GetValueOrDefault(key) as Texture;
        public Vector3 GetTextureScale(string key)=>Vector3.one;public Vector3 GetTextureOffset(string key)=>new();
        public void SetTextureScale(string key,Vector3 value){}public void SetTextureOffset(string key,Vector3 value){}
        public void SetTexture(string key,Texture value)=>Values[key]=value;public void SetColor(string key,Color value)=>Values[key]=value;
        public Color GetColor(string key)=>(Color)Values[key];public void SetFloat(string key,float value)=>Values[key]=value;public float GetFloat(string key)=>(float)Values[key];
    }
    public class AudioSource:Component {}public class Collider:Component {}public class DemoController:MonoBehaviour {}
}
namespace UnityEngine.Rendering {public enum ShadowCastingMode{Off}public enum LightProbeUsage{Off}public enum ReflectionProbeUsage{Off}public enum BlendMode{One=1,SrcAlpha=5,OneMinusSrcAlpha=10}}
namespace UnityEngine.SceneManagement {public struct Scene {}public static class SceneManager {public static void MoveGameObjectToScene(UnityEngine.GameObject g,Scene s){}}}
namespace UnityEditor
{
    public class InitializeOnLoadAttribute:Attribute {}public class MenuItem:Attribute {public MenuItem(string name){}}
    public enum PlayModeStateChange {ExitingEditMode}
    public static class EditorApplication {public static bool isPlaying,isCompiling;public static Action delayCall;public static Action<PlayModeStateChange> playModeStateChanged;}
    public static class GameObjectUtility {public static void RemoveMonoBehavioursWithMissingScript(UnityEngine.GameObject g){}}
    public static class EditorUtility {public static void SetDirty(object value){}}
    public static class AssetDatabase
    {
        public static readonly Dictionary<string,UnityEngine.Object> Assets=new();public static int Saves;public static string Dependency="1";
        public static T LoadAssetAtPath<T>(string path)where T:class=>Assets.GetValueOrDefault(path) as T;
        public static string GetAssetPath(UnityEngine.Object value)=>Assets.FirstOrDefault(p=>ReferenceEquals(p.Value,value)).Key??"shader";
        public static string AssetPathToGUID(string path)=>"source-material";
        public static string GetAssetDependencyHash(string path)=>Dependency;
        public static void CreateAsset(UnityEngine.Object value,string path)=>Assets[path]=value;
        public static void Refresh(){}public static void SaveAssets(){Saves++;}
    }
    public static class PrefabUtility {public static UnityEngine.GameObject SaveAsPrefabAsset(UnityEngine.GameObject root,string path){var copy=root.Clone();AssetDatabase.Assets[path]=copy;return copy;}}
}
namespace UnityEditor.Build {public interface IPreprocessBuildWithReport {int callbackOrder{get;}void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report);}public class BuildFailedException:Exception{public BuildFailedException(string message):base(message){}}}
namespace UnityEditor.Build.Reporting {public class BuildReport {}}
namespace UnityEditor.SceneManagement {public static class EditorSceneManager {public static UnityEngine.SceneManagement.Scene NewPreviewScene()=>new();public static bool ClosePreviewScene(UnityEngine.SceneManagement.Scene s)=>true;}}
namespace Alterverse.Match {public static class ArenaVariantRuntime {public static ArenaVariant Current;public static bool IsKryon=>Current==ArenaVariant.Kryon;public static bool IsNoxara=>Current==ArenaVariant.Noxara;}}
namespace Alterverse.Combat {public static class ArenaVariantRuntime {public static bool IsKryon=>Alterverse.Match.ArenaVariantRuntime.IsKryon;public static bool IsNoxara=>Alterverse.Match.ArenaVariantRuntime.IsNoxara;}}
namespace Alterverse.Visuals {public static class BomberExplosionVfx {public static int Calls;public static void Play(UnityEngine.Vector3 p,UnityEngine.Color c,float r){Calls++;}}}
namespace Alterverse.Audio {public enum DimensionSound {Explosion}public static class DimensionAudio {public static int Calls;public static float Gain;public static void PlayAt(DimensionSound cue,UnityEngine.Vector3 p,float gain){Calls++;Gain=gain;}}}
