// Managed API fixtures. They check configuration and ownership, not native particles or GPU output.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
 public class Object
 {
  public string name;public bool destroyed;
  public static void Destroy(Object value){if(value==null)return;value.destroyed=true;if(value is GameObject go)foreach(var child in GameObject.All.Where(x=>x.transform.parent==go.transform).ToArray())Destroy(child);}
  public static void DontDestroyOnLoad(Object value){}
  public static T FindAnyObjectByType<T>() where T:class=>GameObject.All.Where(g=>!g.destroyed).SelectMany(g=>g.components).OfType<T>().FirstOrDefault();
 }
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;}
 public class MonoBehaviour:Component {public bool enabled=true;}
 public class GameObject:Object
 {
  public static List<GameObject> All=new();public List<Component> components=new();public Transform transform;public bool activeSelf=true;
  public GameObject(string name){this.name=name;transform=new Transform{gameObject=this};All.Add(this);}
  public static GameObject Find(string name)=>All.FirstOrDefault(g=>g.name==name&&!g.destroyed&&g.activeSelf);
  public T AddComponent<T>() where T:Component,new(){var value=new T{gameObject=this};components.Add(value);if(value is ParticleSystem)AddComponent<ParticleSystemRenderer>();return value;}
  public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault();
  public void SetActive(bool value)=>activeSelf=value;
 }
 public class Transform:Component {public Transform parent;public Vector3 localPosition;public void SetParent(Transform value,bool keepWorld)=>parent=value;}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 up=>new(0,1,0);public static Vector3 operator*(Vector3 v,float a)=>new(v.x*a,v.y*a,v.z*a);}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1);}
 public static class Mathf
 {
  public const float PI=(float)Math.PI;
  public static float Abs(float a)=>Math.Abs(a);public static float Sin(float a)=>(float)Math.Sin(a);public static float Max(float a,float b)=>Math.Max(a,b);
  public static float InverseLerp(float a,float b,float v)=>Math.Clamp((v-a)/(b-a),0,1);
  public static float SmoothStep(float a,float b,float t){t=Math.Clamp(t,0,1);return a+(b-a)*t*t*(3-2*t);}
 }
 public static class Time {public static float unscaledTime,deltaTime=.016f,timeScale=1;}
 public static class Application {public static bool isBatchMode;}
 public static class SystemInfo {public static Rendering.GraphicsDeviceType graphicsDeviceType;}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){}}
 public enum RuntimeInitializeLoadType {AfterSceneLoad}
 public class Shader:Object {public bool isSupported=true;}
 public static class Resources {public static bool Available=true;public static int Loads;public static T Load<T>(string name) where T:Object,new(){Loads++;return Available?new T():null;}}
 public class Material:Object {public static List<Material> All=new();public Dictionary<string,float> floats=new();public Material(Shader shader){All.Add(this);}public void SetFloat(string name,float value)=>floats[name]=value;}
 public static class Debug {public static List<string> Warnings=new();public static void LogWarning(string value)=>Warnings.Add(value);}
 public enum ParticleSystemStopBehavior {StopEmittingAndClear}
 public enum ParticleSystemSimulationSpace {World}
 public enum ParticleSystemShapeType {Box}
 public enum ParticleSystemRenderMode {Stretch,Billboard}
 public class ParticleSystem:Component
 {
  public class MainModule {public bool playOnAwake,loop,prewarm,useUnscaledTime;public float duration;public int maxParticles;public ParticleSystemSimulationSpace simulationSpace;public MinMaxCurve startLifetime,startSpeed,startSize,startRotation,gravityModifier;public Color startColor;}
  public class ShapeModule {public bool enabled;public ParticleSystemShapeType shapeType;public Vector3 scale;}
  public class EmissionModule {public MinMaxCurve rateOverTime;}
  public class VelocityOverLifetimeModule {public bool enabled;public ParticleSystemSimulationSpace space;public MinMaxCurve x,y,z;}
  public class ColorOverLifetimeModule {public bool enabled;public Gradient color;}
  public struct MinMaxCurve {public float constant,constantMin,constantMax;public MinMaxCurve(float min,float max){constant=0;constantMin=min;constantMax=max;}public static implicit operator MinMaxCurve(float v)=>new(){constant=v,constantMin=v,constantMax=v};}
  public MainModule main=new();public ShapeModule shape=new();public EmissionModule emission=new();public VelocityOverLifetimeModule velocityOverLifetime=new();public ColorOverLifetimeModule colorOverLifetime=new();
  public bool useAutoRandomSeed,isPlaying,isStopped=true;public uint randomSeed;
  public void Stop(bool children,ParticleSystemStopBehavior mode){isPlaying=false;isStopped=true;}
  public void Play(){isPlaying=true;isStopped=false;}
 }
 public class ParticleSystemRenderer:Component {public Material sharedMaterial;public ParticleSystemRenderMode renderMode;public float lengthScale,velocityScale;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;public Rendering.LightProbeUsage lightProbeUsage;public Rendering.ReflectionProbeUsage reflectionProbeUsage;}
 public struct GradientColorKey {public GradientColorKey(Color color,float time){}}
 public struct GradientAlphaKey {public GradientAlphaKey(float alpha,float time){}}
 public class Gradient {public void SetKeys(GradientColorKey[] colors,GradientAlphaKey[] alphas){}}
}
namespace UnityEngine.Rendering {public enum GraphicsDeviceType {Other,Null}public enum ShadowCastingMode {Off}public enum LightProbeUsage {Off}public enum ReflectionProbeUsage {Off}}
namespace Alterverse.Match
{
 public enum ArenaVariant {Moon,Mars,GreenPlanet,Noxara,Kryon,Earth,Pyraxis,Neptune,Dark00,DiamondCore,GJ504b,Wasp76b,Trappist1e,Toi715b}
 public static class ArenaVariantRuntime {public static ArenaVariant Current;}
 public static class MatchClock {public static bool InBackground;}
}
namespace Alterverse.Combat {public static class ArenaLayout {public static float LengthScale=1.25f;}}
namespace Alterverse.Multiplayer {public static class DedicatedServerNetworkRuntime {public static bool Requested;}}
namespace Alterverse.Performance {public static class AdaptiveVfxBudget {public static bool AllowDecorative=true;public static float Intensity=1;}}
namespace Alterverse.UI {public static class MainMenuController {public static bool IsVisible;}}
