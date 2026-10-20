using System;
using System.Collections.Generic;
using System.Linq;

// Minimal lifecycle/material-state substitutes. These do not simulate Unity rendering.
namespace UnityEngine
{
    public class Object
    {
        internal static readonly List<Object> Live = new();
        public bool destroyed;
        public Object() { Live.Add(this); }
        public static T[] FindObjectsByType<T>() where T : Object => Live.OfType<T>().Where(x => !x.destroyed).ToArray();
        public static void Destroy(Object value)
        {
            if(value == null) return;
            value.destroyed = true;
            if(value is GameObject go)
                foreach(Component component in go.components) component.destroyed = true;
        }
        public static void Reset() { Live.Clear(); }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
    }
    public class MonoBehaviour : Component { }
    public class Transform
    {
        public Quaternion rotation;
        public Transform parent;
        public void SetParent(Transform value, bool worldPositionStays) { parent = value; }
    }
    public class GameObject : Object
    {
        public string name;
        public readonly Transform transform = new();
        internal readonly List<Component> components = new();
        public GameObject(string value) { name = value; }
        public T AddComponent<T>() where T : Component, new()
        {
            T result = new() { gameObject = this };
            components.Add(result);
            return result;
        }
    }
    public struct Color : IEquatable<Color>
    {
        public float r,g,b,a;
        public Color(float red,float green,float blue,float alpha=1) { r=red;g=green;b=blue;a=alpha; }
        public bool Equals(Color other) => r==other.r && g==other.g && b==other.b && a==other.a;
        public override bool Equals(object obj) => obj is Color other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(r,g,b,a);
        public static Color operator *(Color c,float value) => new(c.r*value,c.g*value,c.b*value,c.a*value);
        public static Color Lerp(Color a,Color b,float t) => new(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);
        public override string ToString() => $"{r:R},{g:R},{b:R},{a:R}";
    }
    public struct Quaternion : IEquatable<Quaternion>
    {
        public float x,y,z;
        public static Quaternion Euler(float x,float y,float z) => new() { x=x,y=y,z=z };
        public bool Equals(Quaternion other) => x==other.x && y==other.y && z==other.z;
        public override bool Equals(object obj) => obj is Quaternion other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x,y,z);
    }
    public enum LightType { Directional, Point, Spot }
    public enum LightShadows { None, Hard, Soft }
    public enum ShadowQuality { Disable, HardOnly, All }
    public class Light : Component
    {
        public bool enabled=true;
        public LightType type;
        public float intensity=1,shadowStrength=1;
        public int cullingMask=-1;
        public Color color=new(1,1,1);
        public LightShadows shadows=LightShadows.Hard;
    }
    public static class QualitySettings { public static ShadowQuality shadows; }
    public static class RenderSettings
    {
        public static Color ambientSkyColor,ambientEquatorColor,ambientGroundColor;
        public static Rendering.AmbientMode ambientMode;
        public static bool fog;
        public static float reflectionIntensity;
        public static Light sun;
    }
}
namespace UnityEngine.Rendering { public enum AmbientMode { Skybox, Trilight, Flat } }
namespace Alterverse.Match
{
    public static class ArenaVariantRuntime
    {
        public static ArenaVariant Current;
        public static bool IsDark00 => Current==ArenaVariant.Dark00;
    }
}
