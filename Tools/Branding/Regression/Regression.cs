using System;
using System.Collections.Generic;
using Alterverse.EditorChecks;

// API fixtures for the Unity 6000.6 compile error seen on the target editor.
// They deliberately reject Round/Legacy references at compile time.
namespace UnityEngine
{
    public sealed class Texture2D { }
    public static class Debug { public static void Log(object message) { } }
}
namespace UnityEditor.Build
{
    public struct NamedBuildTarget { public static NamedBuildTarget Android; }
    public sealed class BuildFailedException : Exception { public BuildFailedException(string message) : base(message) { } }
}
namespace UnityEditor
{
    using UnityEngine;
    using UnityEditor.Build;
    public sealed class MenuItem : Attribute { public MenuItem(string path) { } }
    public sealed class PlatformIconKind { }
    public sealed class PlatformIcon
    {
        private Texture2D[] textures = Array.Empty<Texture2D>();
        public int layerCount => textures.Length;
        public Texture2D GetTexture(int index) => textures[index];
        public void SetTextures(params Texture2D[] layers) { textures = (Texture2D[])layers.Clone(); }
    }
    public static class AssetDatabase
    {
        public static readonly Dictionary<string, Texture2D> Assets = new Dictionary<string, Texture2D>();
        public static int Saves;
        public static T LoadAssetAtPath<T>(string path) where T : class => Assets.TryGetValue(path, out var value) ? value as T : null;
        public static void SaveAssets() { Saves++; }
    }
    public static class PlayerSettings
    {
        public static PlatformIcon[] Slots;
        public static int Writes;
        public static PlatformIcon[] GetPlatformIcons(NamedBuildTarget target, PlatformIconKind kind)
        {
            if (kind != Android.AndroidPlatformIconKind.Adaptive) throw new Exception("Unsupported Unity 6000.6 icon kind");
            return Slots;
        }
        public static void SetPlatformIcons(NamedBuildTarget target, PlatformIconKind kind, PlatformIcon[] slots) { GetPlatformIcons(target, kind); Slots = slots; Writes++; }
    }
}
namespace UnityEditor.Android
{
    public static class AndroidPlatformIconKind
    {
        public static readonly UnityEditor.PlatformIconKind Adaptive = new UnityEditor.PlatformIconKind();
        [Obsolete("Use AndroidPlatformIconKind.Adaptive instead.", true)]
        public static readonly UnityEditor.PlatformIconKind Round = new UnityEditor.PlatformIconKind();
        [Obsolete("Use AndroidPlatformIconKind.Adaptive instead.", true)]
        public static readonly UnityEditor.PlatformIconKind Legacy = new UnityEditor.PlatformIconKind();
    }
}
internal static class Regression
{
    private static int checks;
    private static void Require(bool condition) { checks++; if (!condition) throw new Exception("Launcher regression failed at check " + checks); }
    private static List<string> Errors() { var errors = new List<string>(); AndroidLauncherIcons.AddValidationErrors(errors); return errors; }
    private static void Main()
    {
        var background = new UnityEngine.Texture2D();
        var foreground = new UnityEngine.Texture2D();
        var unity = new UnityEngine.Texture2D();
        UnityEditor.AssetDatabase.Assets[AndroidLauncherIcons.BackgroundPath] = background;
        UnityEditor.AssetDatabase.Assets[AndroidLauncherIcons.ForegroundPath] = foreground;
        // No legacy asset is supplied: Unity 6.6 must not require or access it.
        UnityEditor.PlayerSettings.Slots = new UnityEditor.PlatformIcon[6];
        for (int i = 0; i < 6; i++) { var slot = new UnityEditor.PlatformIcon(); slot.SetTextures(unity); UnityEditor.PlayerSettings.Slots[i] = slot; }
        Require(Errors().Count == 1);
        AndroidLauncherIcons.Apply();
        Require(Errors().Count == 0);
        foreach (var slot in UnityEditor.PlayerSettings.Slots) Require(slot.layerCount == 2 && slot.GetTexture(0) == background && slot.GetTexture(1) == foreground);
        int writes = UnityEditor.PlayerSettings.Writes, saves = UnityEditor.AssetDatabase.Saves;
        AndroidLauncherIcons.Apply();
        Require(writes == UnityEditor.PlayerSettings.Writes && saves == UnityEditor.AssetDatabase.Saves);
        UnityEditor.PlayerSettings.Slots[5].SetTextures(background, unity);
        Require(Errors().Count == 1); AndroidLauncherIcons.Apply(); Require(Errors().Count == 0);
        UnityEditor.PlayerSettings.Slots[0].SetTextures(background, null);
        Require(Errors().Count == 1); AndroidLauncherIcons.Apply(); Require(Errors().Count == 0);
        UnityEditor.AssetDatabase.Assets.Remove(AndroidLauncherIcons.ForegroundPath);
        Require(Errors().Count == 1);
        bool threw = false; try { AndroidLauncherIcons.Apply(); } catch (UnityEditor.Build.BuildFailedException) { threw = true; }
        Require(threw);
        UnityEditor.AssetDatabase.Assets[AndroidLauncherIcons.ForegroundPath] = foreground;
        UnityEditor.PlayerSettings.Slots = Array.Empty<UnityEditor.PlatformIcon>();
        Require(Errors().Count == 1);
        threw = false; try { AndroidLauncherIcons.Apply(); } catch (UnityEditor.Build.BuildFailedException) { threw = true; }
        Require(threw);
        Console.WriteLine(checks + " launcher checks passed with Unity 6000.6 obsolete-as-error API fixtures. Not a native Unity build.");
    }
}
