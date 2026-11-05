// Compile the actual Android LocalizedText path with lightweight Unity substitutes.
using System;
using System.Reflection;
using Alterverse.UI;
using UnityEngine;
namespace UnityEngine
{
    public class Object { public static int Destroyed; public static void Destroy(Object value) { Destroyed++; } }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) {} }
    public static class Application
    {
        public static event Action lowMemory;
        public static event Action<bool> focusChanged;
        public static int MemoryHandlers => lowMemory?.GetInvocationList().Length ?? 0;
        public static int FocusHandlers => focusChanged?.GetInvocationList().Length ?? 0;
        public static void LowMemory() => lowMemory?.Invoke();
        public static void Focus(bool value) => focusChanged?.Invoke(value);
    }
    public class AndroidJavaClass : IDisposable
    {
        public static int Created, Disposed;
        public AndroidJavaClass(string name) { Created++; }
        public T CallStatic<T>(string name, params object[] values) => (T)(object)new byte[] { 1 };
        public void Dispose() { Disposed++; }
    }
    public struct Rect { public float width, height; public Rect(float w, float h) { width=w; height=h; } }
    public enum FontStyle { Normal, Bold, BoldAndItalic }
    public class GUIStyleState { public Color textColor; }
    public class GUIStyle { public int fontSize, alignment; public FontStyle fontStyle; public bool wordWrap; public GUIStyleState normal=new(); }
    public struct Color { public static Color operator *(Color a, Color b) => a; }
    public enum EventType { Repaint, Layout }
    public class Event { public static Event current=new(); public EventType type; }
    public enum TextureFormat { RGBA32 }
    public enum HideFlags { HideAndDontSave }
    public enum FilterMode { Bilinear }
    public enum ScaleMode { StretchToFill }
    public class Texture2D : Object
    {
        public static int Created;
        public int width, height; public HideFlags hideFlags; public FilterMode filterMode;
        public Texture2D(int w, int h, TextureFormat format, bool mipmaps) { width=w; height=h; Created++; }
    }
    public static class ImageConversion { public static bool LoadImage(Texture2D texture, byte[] bytes, bool nonReadable) => true; }
    public static class GUI { public static Color color; public static void DrawTexture(Rect rect, Texture2D texture, ScaleMode mode, bool alpha) {} }
    public static class Mathf { public static int Clamp(int n,int min,int max)=>Math.Clamp(n,min,max); public static int CeilToInt(float n)=>(int)Math.Ceiling(n); public static int Max(int a,int b)=>Math.Max(a,b); }
    public static class Debug { public static void LogWarning(string text) {} }
}
class Regression
{
    static int checks;
    static void Check(bool condition,string reason) { checks++; if(!condition)throw new Exception(reason); }
    static void Main()
    {
        var reset=typeof(LocalizedText).GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Static);
        reset.Invoke(null,null);reset.Invoke(null,null);
        Check(Application.MemoryHandlers==1&&Application.FocusHandlers==1,"initialization must not accumulate handlers");
        var rect=new Rect(120,30);var style=new GUIStyle{fontSize=14};
        Check(LocalizedText.Draw(rect,"日本語",style),"international label renders");
        LocalizedText.Draw(rect,"日本語",style);
        Check(Texture2D.Created==1,"normal draws reuse the texture cache");
        Application.Focus(true);
        Check(UnityEngine.Object.Destroyed==0,"regaining focus does not discard valid textures");
        Application.Focus(false);
        Check(UnityEngine.Object.Destroyed==1&&AndroidJavaClass.Disposed==1,"background transition releases cache and JNI handle");
        LocalizedText.Draw(rect,"日本語",style);
        Check(Texture2D.Created==2,"returning to app recreates text lazily");
        Application.LowMemory();Application.LowMemory();
        Check(UnityEngine.Object.Destroyed==2&&AndroidJavaClass.Disposed==2,"repeated low-memory callbacks are safe");
        Check(LocalizedText.Draw(rect,"日本語",style),"text remains functional after memory pressure");
        Check(Texture2D.Created==3,"cache can repopulate after memory pressure");
        reset.Invoke(null,null);
        Check(UnityEngine.Object.Destroyed==3&&Application.MemoryHandlers==1,"subsystem reset cleans textures and keeps one callback");
        Console.WriteLine(checks+" actual Android text-cache lifecycle checks passed with Unity substitutes.");
    }
}
