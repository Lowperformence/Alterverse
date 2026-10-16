#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace Alterverse.EditorChecks
{
    /// <summary>Release-only R8 settings and optimized Gradle defaults; SDK JNI APIs stay named.</summary>
    public sealed class AndroidR8Optimization
    {
        internal const string RulesPath = "Assets/Plugins/Android/proguard-user.txt";

        public static void ApplySettings()
        {
            // Unity 6 uses R8 whenever minification is enabled; no legacy Use R8 flag is needed.
            PlayerSettings.Android.minifyRelease = true;
            PlayerSettings.Android.minifyDebug = false;
            // Unity 6 exposes this checkbox as serialized PlayerSettings data, not a
            // public PlayerSettings.Android.useCustomProguardFile C# property.
            using (SerializedObject settings = LoadPlayerSettings())
            {
                SerializedProperty customRules = settings.FindProperty("useCustomProguardFile");
                if (customRules == null) throw new BuildFailedException("Unity PlayerSettings has no custom ProGuard file setting.");
                if (!customRules.boolValue)
                {
                    customRules.boolValue = true;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        public static void AddValidationErrors(List<string> errors)
        {
            if (!PlayerSettings.Android.minifyRelease)
                errors.Add("Enable Minify > Release for the R8-optimized Android bundle.");
            if (PlayerSettings.Android.minifyDebug)
                errors.Add("Minify > Debug must remain disabled for development builds.");
            using (SerializedObject settings = LoadPlayerSettings())
            {
                SerializedProperty customRules = settings.FindProperty("useCustomProguardFile");
                if (customRules == null || !customRules.boolValue || !File.Exists(RulesPath))
                    errors.Add("Android R8 requires the project's proguard-user.txt JNI keep rules.");
            }
        }

        private static SerializedObject LoadPlayerSettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
                throw new BuildFailedException("Unity PlayerSettings could not be loaded for Android R8 configuration.");
            return new SerializedObject(assets[0]);
        }

        // After the device-compatibility manifest callback. Keep Unity's generated templates,
        // signing, Unity consumer rules, SDK rules and custom plugin settings intact.
        internal const int GradleCallbackOrder = 1000100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (EditorUserBuildSettings.development) return;
            string library = Path.Combine(path, "build.gradle");
            DirectoryInfo parent = Directory.GetParent(path);
            string launcher = parent == null ? "" : Path.Combine(parent.FullName, "launcher", "build.gradle");
            if (!File.Exists(library) || !File.Exists(launcher))
                throw new BuildFailedException("R8 could not find the generated unityLibrary/launcher Gradle files.");

            UpgradeDefaultRules(library);
            UpgradeDefaultRules(launcher);
            if (!File.ReadAllText(launcher).Contains("proguard-android-optimize.txt"))
                throw new BuildFailedException("R8 release requires an optimized default ProGuard configuration in launcher/build.gradle. Inspect the launcher Gradle template.");
        }

        private static void UpgradeDefaultRules(string path)
        {
            string source = File.ReadAllText(path);
            // The legacy default includes -dontoptimize. Replace only the exact file name;
            // do not replace the buildTypes block or discard third-party keep rules.
            string updated = source.Replace("proguard-android.txt", "proguard-android-optimize.txt");
            if (!string.Equals(source, updated, StringComparison.Ordinal)) File.WriteAllText(path, updated);
        }
    }
#if UNITY_ANDROID
    public sealed class AndroidR8GradlePostprocessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => AndroidR8Optimization.GradleCallbackOrder;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            new AndroidR8Optimization().OnPostGenerateGradleAndroidProject(path);
        }
    }
#endif
}
#endif
