using System;
using System.Collections.Generic;
using System.Reflection;

// Lifecycle and draw-command fixtures, not a Unity decoder or GPU renderer.
namespace UnityEngine
{
    public class DisallowMultipleComponent : Attribute { }
    public class Object
    {
        public string name;
        public bool destroyed;
        public static void Destroy(Object value) { if (value != null) value.destroyed = true; }
    }
    public class Component : Object { public GameObject gameObject; }
    public class MonoBehaviour : Component { public bool isActiveAndEnabled = true; }
    public class GameObject : Object
    {
        public readonly List<Component> Components = new();
        public T AddComponent<T>() where T : Component, new()
        { var value = new T { gameObject = this }; Components.Add(value); return value; }
    }
    public class Texture : Object { public int width, height; }
    public enum TextureFormat { RGBA32 }
    public enum RenderTextureFormat { ARGB32 }
    public enum FilterMode { Bilinear }
    public enum TextureWrapMode { Clamp }
    public enum ColorSpace { Gamma, Linear }
    public enum ScaleMode { ScaleAndCrop }
    public static class QualitySettings { public static ColorSpace activeColorSpace = ColorSpace.Linear; }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r=r; this.g=g; this.b=b; this.a=a; }
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x=x; this.y=y; this.width=width; this.height=height; }
    }
    public class Texture2D : Texture
    {
        public TextureWrapMode wrapMode;
        public FilterMode filterMode;
        public Texture2D(int width, int height, TextureFormat format, bool mipmaps) { this.width=width; this.height=height; }
        public void SetPixel(int x, int y, Color color) { }
        public void Apply(bool mipmaps, bool makeUnreadable) { }
    }
    public class RenderTexture : Texture
    {
        public static RenderTexture active;
        public static bool CreateSucceeds = true;
        public static readonly List<RenderTexture> Allocated = new();
        public bool sRGB, useMipMap, autoGenerateMips;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        bool created;
        public RenderTexture(int width, int height, int depth, RenderTextureFormat format)
        { this.width=width; this.height=height; sRGB=QualitySettings.activeColorSpace==ColorSpace.Linear; Allocated.Add(this); }
        public bool Create() => created = CreateSucceeds;
        public bool IsCreated() => created;
        public void Release() { created = false; }
    }
    public static class GL { public static bool sRGBWrite; }
    public static class Graphics
    {
        public static int Blits;
        public static bool Throw, LastWrite;
        public static Texture LastSource;
        public static RenderTexture LastTarget;
        public static void Blit(Texture source, RenderTexture target)
        {
            Blits++; LastSource=source; LastTarget=target; LastWrite=GL.sRGBWrite; RenderTexture.active=target;
            if (Throw) throw new InvalidOperationException("fixture GPU failure");
        }
    }
    public static class Resources
    {
        public static readonly Dictionary<string,Object> Assets = new();
        public static int Loads, Unloads;
        public static T Load<T>(string path) where T : Object
        { Loads++; return Assets.TryGetValue(path, out var value) ? value as T : null; }
        public static void UnloadAsset(Object value) { Unloads++; }
    }
    public static class GUI
    {
        public record Draw(Rect Rect, Texture Texture, ScaleMode Mode, bool Blend, Color Tint);
        public static Color color = new(1,1,1,1);
        public static readonly List<Draw> Draws = new();
        public static void DrawTexture(Rect rect, Texture image, ScaleMode mode, bool alphaBlend)
        { Draws.Add(new Draw(rect, image, mode, alphaBlend, color)); }
        public static void DrawTextureWithTexCoords(Rect rect, Texture image, Rect coords) { }
    }
    public static class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a,b);
        public static float Clamp01(float x) => Math.Clamp(x,0,1);
        public static float SmoothStep(float from, float to, float t)
        { t=Clamp01(t); return from+(to-from)*t*t*(3-2*t); }
        public static int Clamp(int x, int min, int max) => Math.Clamp(x,min,max);
        public static float Clamp(float x, float min, float max) => Math.Clamp(x,min,max);
    }
    public static class Time { public static float unscaledTime; }
    public static class Application { public static bool isBatchMode; }
    public static class Debug
    {
        public static int Warnings;
        public static void LogWarning(object value, Object context = null) { Warnings++; }
    }
}
namespace UnityEngine.Video
{
    using UnityEngine;
    public enum VideoSource { VideoClip }
    public enum VideoRenderMode { RenderTexture }
    public enum VideoAudioOutputMode { None }
    public enum VideoTimeUpdateMode { UnscaledGameTime }
    public class VideoClip : Object { public uint width=1918, height=1080; }
    public class VideoPlayer : Component
    {
        public bool playOnAwake, isLooping, waitForFirstFrame, skipOnDrop, sendFrameReadyEvents, isPrepared;
        public VideoSource source;
        public VideoRenderMode renderMode;
        public VideoAudioOutputMode audioOutputMode;
        public VideoTimeUpdateMode timeUpdateMode;
        public VideoClip clip;
        public RenderTexture targetTexture;
        public int Plays, Pauses, Stops, Prepares;
        public bool SeededBeforePrepare;
        public event Action<VideoPlayer> prepareCompleted;
        public event Action<VideoPlayer,long> frameReady;
        public event Action<VideoPlayer,string> errorReceived;
        public int Subscriptions => (prepareCompleted?.GetInvocationList().Length??0) +
            (frameReady?.GetInvocationList().Length??0) + (errorReceived?.GetInvocationList().Length??0);
        public void Prepare() { Prepares++; SeededBeforePrepare=Graphics.LastTarget==targetTexture && Graphics.Blits>0; }
        public void CompletePrepare() { isPrepared=true; prepareCompleted?.Invoke(this); }
        public void DeliverFrame() { if(sendFrameReadyEvents) frameReady?.Invoke(this,0); }
        public void Fail() => errorReceived?.Invoke(this,"fixture decoder failure");
        public void Play() { Plays++; }
        public void Pause() { Pauses++; }
        public void Stop() { Stops++; isPrepared=false; }
    }
}
namespace Alterverse.UI
{
    internal static class FrontEndTheme { public static UnityEngine.Color Background = new(.01f,.02f,.03f); }
}
