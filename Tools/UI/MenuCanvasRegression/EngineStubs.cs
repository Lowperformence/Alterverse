// Deterministic substitutes for geometry/lifecycle tests. No Unity renderer, native UI or shader execution.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public bool Destroyed;
        public static void Destroy(Object value)
        {
            if (value == null) return;
            value.Destroyed = true;
            if (value is GameObject go)
            {
                foreach (var child in go.transform.Children.ToArray()) Destroy(child.gameObject);
                foreach (var component in go.Components) component.Destroyed = true;
                go.transform.SetParent(null, false);
            }
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }
    public class GameObject : Object
    {
        public static readonly List<GameObject> All = new();
        public readonly List<Component> Components = new();
        public Transform transform;
        public bool activeSelf = true;
        public GameObject(string name = "", params Type[] types)
        {
            this.name = name;
            transform = types.Contains(typeof(RectTransform)) ? new RectTransform() : new Transform();
            transform.gameObject = this; Components.Add(transform); All.Add(this);
            foreach (var type in types) if (type != typeof(RectTransform)) AddComponent(type);
        }
        private Component AddComponent(Type type)
        {
            var component = (Component)Activator.CreateInstance(type, true);
            component.gameObject = this; Components.Add(component);
            if (component is ParticleSystem && GetComponent<ParticleSystemRenderer>() == null) AddComponent(typeof(ParticleSystemRenderer));
            type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            return component;
        }
        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));
        public T GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault();
        public void SetActive(bool value) => activeSelf = value;
    }
    public class Transform : Component
    {
        public Transform parent;
        public readonly List<Transform> Children = new();
        public Vector3 localScale, localPosition;
        public void SetParent(Transform value, bool keepWorld)
        { parent?.Children.Remove(this); parent = value; parent?.Children.Add(this); }
        public void SetAsLastSibling() { if (parent == null) return; parent.Children.Remove(this); parent.Children.Add(this); }
        public bool IsChildOf(Transform value) { for (Transform t = this; t != null; t = t.parent) if (t == value) return true; return false; }
    }
    public class RectTransform : Transform { public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta; }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude => (float)Math.Sqrt(x*x+y*y);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 one => new(1,1,1);
    }
    public struct Vector4 { public float x,y,z,w; public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;} }
    public struct Quaternion { }
    public class RectOffset { }
    public class AnimationCurve { }
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float x,float y,float width,float height){this.x=x;this.y=y;this.width=width;this.height=height;}
        public float xMax => x+width; public float yMax => y+height;
        public static Rect MinMaxRect(float a,float b,float c,float d)=>new(a,b,c-a,d-b);
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white=>new(1,1,1,1);
        public static Color operator*(Color x,Color y)=>new(x.r*y.r,x.g*y.g,x.b*y.b,x.a*y.a);
    }
    public static class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Max(float a,float b)=>Math.Max(a,b); public static int Max(int a,int b)=>Math.Max(a,b);
        public static int Min(int a,int b)=>Math.Min(a,b);
        public static int RoundToInt(float a)=>(int)Math.Round(a); public static float Clamp01(float a)=>Math.Min(1,Math.Max(0,a));
        public static float Pow(float a,float b)=>(float)Math.Pow(a,b); public static float Repeat(float a,float b)=>a-(float)Math.Floor(a/b)*b;
    }
    public static class Screen { public static int width=1920,height=1080; }
    public static class Time { public static float timeScale=0,unscaledTime; }
    public enum EventType { Layout,Repaint,MouseUp }
    public class Event { public static Event current=new(); public EventType type; }
    public static class GUI { public static Color color=Color.white; }
    public static class GUIUtility
    {
        public static Vector2 Origin; public static float Scale=1;
        public static Vector2 GUIToScreenPoint(Vector2 value)=>new(Origin.x+value.x*Scale,Origin.y+value.y*Scale);
    }
    public enum ScaleMode { StretchToFill,ScaleToFit,ScaleAndCrop }
    public enum RenderMode { ScreenSpaceOverlay }
    public class Canvas : Behaviour { public RenderMode renderMode; public int sortingOrder; public bool pixelPerfect; }
    public class CanvasGroup : Behaviour { public float alpha; public bool interactable,blocksRaycasts; }
    public class CanvasRenderer : Component { }
    public enum FontStyle { Normal,Bold }
    public enum TextAnchor { MiddleLeft,MiddleCenter }
    public class Font : Object { }
    public class GUIStyleState { public Color textColor=Color.white; }
    public class GUIStyle { public Font font; public int fontSize=20; public FontStyle fontStyle; public TextAnchor alignment; public bool wordWrap; public GUIStyleState normal=new(); }
    public class Texture : Object { public int width,height; }
    public enum TextureFormat { RGBA32 }
    public enum TextureWrapMode { Clamp }
    public class Texture2D : Texture
    {
        public TextureWrapMode wrapMode;
        public Texture2D(int width,int height,TextureFormat format,bool mipmap){this.width=width;this.height=height;}
        public void SetPixels(Color[] value){ } public void Apply(bool mipmap,bool unreadable){ }
    }
    public enum SpriteMeshType { FullRect }
    public class Sprite : Object
    {
        public Vector4 border;
        public static Sprite Create(Texture2D texture,Rect rect,Vector2 pivot,float pixelsPerUnit,uint extrude,SpriteMeshType type,Vector4 border)=>new(){border=border};
    }
    public class Shader : Object { public static Shader Find(string name)=>new(){name=name}; }
    public class Material : Object
    {
        public Shader shader; public Texture mainTexture;
        public Material(Shader shader){this.shader=shader;} public Material(Material source){shader=source.shader;}
    }
    public class ShaderVariantCollection : Object { public void WarmUp(){ } }
    public static class Resources
    {
        public static T Load<T>(string path) where T:Object => typeof(T)==typeof(Material)?new Material(Shader.Find("UI/Additive")) as T:null;
        public static T GetBuiltinResource<T>(string path) where T:Object,new()=>new T();
    }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear }
    public enum ParticleSystemSimulationSpace { Local }
    public enum ParticleSystemScalingMode { Local }
    public enum ParticleSystemShapeType { Box }
    public enum ParticleSystemCurveMode { Constant,Curve,TwoCurves,TwoConstants }
    public class ParticleSystem : Component
    {
        public struct MinMaxCurve
        {
            public float min,max;
            public ParticleSystemCurveMode mode;
            public MinMaxCurve(float value){min=max=value;mode=ParticleSystemCurveMode.Constant;}
            public MinMaxCurve(float min,float max){this.min=min;this.max=max;mode=ParticleSystemCurveMode.TwoConstants;}
            public static implicit operator MinMaxCurve(float value)=>new(value);
        }
        public class MainModule { public bool loop,playOnAwake,useUnscaledTime; public int maxParticles; public Color startColor; public MinMaxCurve startSize,startSpeed,startLifetime,startRotation; public ParticleSystemSimulationSpace simulationSpace; public ParticleSystemScalingMode scalingMode; }
        public class ShapeModule { public bool enabled; public Vector3 scale; public ParticleSystemShapeType shapeType; }
        public class EmissionModule { public MinMaxCurve rateOverTime; }
        public class ColorOverLifetimeModule { public bool enabled; public Gradient color; }
        public class VelocityOverLifetimeModule { public bool enabled; public ParticleSystemSimulationSpace space; public MinMaxCurve x,y,z; }
        public readonly MainModule main=new(); public readonly ShapeModule shape=new(); public readonly EmissionModule emission=new(); public readonly ColorOverLifetimeModule colorOverLifetime=new(); public readonly VelocityOverLifetimeModule velocityOverLifetime=new();
        public bool isPlaying,useAutoRandomSeed; public uint randomSeed;
        public void Stop(bool children,ParticleSystemStopBehavior behavior)=>isPlaying=false;
        public void Play()
        {
            var v=velocityOverLifetime;
            if(v.enabled&&(v.x.mode!=v.y.mode||v.x.mode!=v.z.mode))
                throw new InvalidOperationException("Particle Velocity curves must all be in the same mode");
            isPlaying=true;
        }
    }
    public class ParticleSystemRenderer : Component { public Material sharedMaterial; }
    public struct GradientColorKey { public GradientColorKey(Color color,float time){ } }
    public struct GradientAlphaKey { public GradientAlphaKey(float alpha,float time){ } }
    public class Gradient { public void SetKeys(GradientColorKey[] color,GradientAlphaKey[] alpha){ } }
}
namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Behaviour { public bool raycastTarget; public UnityEngine.Color color; public UnityEngine.RectTransform rectTransform=>transform as UnityEngine.RectTransform; }
    public class RawImage : Graphic { public UnityEngine.Texture texture; public UnityEngine.Rect uvRect; }
    public class Image : Graphic { public enum Type{Sliced} public Type type; public UnityEngine.Sprite sprite; public float pixelsPerUnitMultiplier; }
    public enum HorizontalWrapMode { Wrap,Overflow }
    public enum VerticalWrapMode { Truncate }
    public class Text : Graphic { public string text; public bool supportRichText,resizeTextForBestFit; public UnityEngine.Font font; public int fontSize,resizeTextMinSize,resizeTextMaxSize; public UnityEngine.FontStyle fontStyle; public UnityEngine.TextAnchor alignment; public HorizontalWrapMode horizontalOverflow; public VerticalWrapMode verticalOverflow; }
    public class RectMask2D : UnityEngine.Behaviour { }
}
namespace Coffee.UIEffects
{
    public enum GradationMode { None,Vertical }
    public enum GradationColorFilter { Multiply }
    public enum TransitionFilter { None,Shiny }
    public class UIEffect : UnityEngine.Behaviour { public GradationMode gradationMode; public GradationColorFilter gradationColorFilter; public UnityEngine.Color gradationColor1,gradationColor2,transitionColor; public TransitionFilter transitionFilter; public float transitionWidth,transitionSoftness,transitionRate; }
}
namespace Coffee.UISoftMask
{
    public class SoftMask : UnityEngine.Behaviour { public enum MaskingMode{AntiAliasing} public MaskingMode maskingMode; public bool showMaskGraphic; }
}
namespace Coffee.UIExtensions
{
    public class UIParticle : UnityEngine.UI.Graphic { public enum AutoScalingMode{None} public AutoScalingMode autoScalingMode; public float scale; public void RefreshParticles(){ } }
}
#if HEADLESS_TWEEN
namespace DG.Tweening
{
    public enum Ease { OutCubic,OutQuad }
    public class Tween
    {
        public bool Active=true,Unscaled; public object Target; public Action Complete; public Action<float> Setter; public float Duration,Elapsed,End;
        public Tween SetEase(Ease value)=>this; public Tween SetUpdate(bool value){Unscaled=value;return this;} public Tween SetTarget(object value){Target=value;return this;} public Tween OnComplete(Action value){Complete=value;return this;} public void Kill()=>Active=false;
    }
    public static class DOTween
    {
        public static readonly List<Tween> Tweens=new();
        public static Tween To(Func<float> getter,Action<float> setter,float end,float duration){var tween=new Tween(){Setter=setter,End=end,Duration=duration};Tweens.Add(tween);return tween;}
        public static void Tick(float delta){foreach(var t in Tweens.ToArray())if(t.Active){t.Elapsed+=delta*(t.Unscaled?1:UnityEngine.Time.timeScale);if(t.Elapsed>=t.Duration){t.Setter(t.End);t.Active=false;t.Complete?.Invoke();}}}
    }
}
#endif
namespace Alterverse.UI
{
    public static class FrontEndTheme { public static UnityEngine.Texture2D RoundedTexture(bool outline)=>new(48,48,UnityEngine.TextureFormat.RGBA32,false); }
}
