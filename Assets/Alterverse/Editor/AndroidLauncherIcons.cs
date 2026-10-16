#if UNITY_EDITOR && UNITY_ANDROID
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    /// <summary>Assign both adaptive layers explicitly; the old application-icon API only sets layer zero.</summary>
    internal static class AndroidLauncherIcons
    {
        private const string Root = "Assets/Alterverse/Branding/";
        internal const string BackgroundPath = Root + "DimensionsFrontLauncherBackground.png";
        internal const string ForegroundPath = Root + "DimensionsFrontLauncherForeground.png";

        [MenuItem("DIMENSIONS FRONT/Release/Repair Android launcher icons")]
        internal static void Apply()
        {
            Texture2D background = LoadRequired(BackgroundPath);
            Texture2D foreground = LoadRequired(ForegroundPath);
            // Unity 6000.6 rejects Round and Legacy with CS0619. Android's
            // adaptive icon is the supported launcher icon for this project.
            bool changed = SetKind(AndroidPlatformIconKind.Adaptive, background, foreground);
            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("DIMENSIONS FRONT: repaired both layers in all Android adaptive launcher icon slots.");
            }
        }

        internal static void AddValidationErrors(List<string> errors)
        {
            Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            Texture2D foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
            if (background == null || foreground == null)
            {
                errors.Add("Android launcher icon assets are missing from " + Root + ". Restore both adaptive launcher textures.");
                return;
            }
            CheckKind(errors, AndroidPlatformIconKind.Adaptive, background, foreground);
        }

        private static Texture2D LoadRequired(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new BuildFailedException("Required Android launcher icon asset is missing: " + path);
            return texture;
        }

        private static bool SetKind(PlatformIconKind kind, params Texture2D[] layers)
        {
            PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (slots == null || slots.Length == 0)
                throw new BuildFailedException("Unity reported no Android " + kind + " icon slots. Install Android Build Support.");
            bool changed = false;
            foreach (PlatformIcon slot in slots)
            {
                if (Matches(slot, layers)) continue;
                // Unity's adaptive order is background (0), foreground (1).
                // SetTextures replaces the entire layer list, including a stale Unity foreground.
                slot.SetTextures(layers);
                changed = true;
            }
            if (changed) PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            return changed;
        }

        private static void CheckKind(List<string> errors, PlatformIconKind kind, params Texture2D[] layers)
        {
            PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (slots != null && slots.Length > 0)
            {
                bool valid = true;
                foreach (PlatformIcon slot in slots) valid &= Matches(slot, layers);
                if (valid) return;
            }
            errors.Add("Android " + kind + " launcher icons are incomplete or use unexpected textures. Run Repair Android launcher icons.");
        }

        private static bool Matches(PlatformIcon slot, Texture2D[] layers)
        {
            if (slot.layerCount != layers.Length) return false;
            for (int i = 0; i < layers.Length; i++)
                if (slot.GetTexture(i) != layers[i]) return false;
            return true;
        }
    }
}
#endif
