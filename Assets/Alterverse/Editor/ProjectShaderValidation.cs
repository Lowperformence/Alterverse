#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.EditorChecks
{
    /// <summary>Prevents cyan editor placeholders and reports broken project shaders before play/build.</summary>
    public sealed class ProjectShaderValidation : IPreprocessBuildWithReport
    {
        private static readonly string[] ShaderRoots = { "Assets/Alterverse", "Assets/Resources" };
        private static readonly string[,] RequiredMaterials =
        {
            { "CombatVfx/HeroEnergy", "Assets/Alterverse/Resources/CombatVfx/HeroEnergy.shader" },
            { "CombatUI/AbilityButton", "Assets/Alterverse/Shaders/AbilityButton.shader" },
            { "HeroArt/HeroSurface", "Assets/Alterverse/Resources/HeroArt/HeroSurface.shader" }
        };

        public int callbackOrder => -90;

        // Run after AndroidRuntimeShaderPreserver (-100) has retained runtime shader dependencies.
        public void OnPreprocessBuild(BuildReport report) => ValidateProjectShaders(false);

        [InitializeOnLoadMethod]
        private static void RegisterPreflight()
        {
            EditorApplication.playModeStateChanged -= BeforePlay;
            EditorApplication.playModeStateChanged += BeforePlay;
        }

        private static void BeforePlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            // The cyan dummy shader belongs to asynchronous Editor compilation, not gameplay.
            // Cover package/URP shaders as well as our own shaders, including new attack variants.
            EditorSettings.asyncShaderCompilation = false;
            try { ValidateProjectShaders(true); }
            catch (Exception exception)
            {
                EditorApplication.isPlaying = false;
                Debug.LogError("DIMENSIONS FRONT: shader preflight stopped Play Mode. " + exception.Message);
            }
        }

        [MenuItem("DIMENSIONS FRONT/Validation/Validate and precompile project shaders")]
        public static void ValidateFromMenu() => ValidateProjectShaders(true);

        public static void ValidateProjectShaders(bool compileDefaultPasses)
        {
            var errors = new List<string>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:Shader", ShaderRoots);
            string[] paths = Array.ConvertAll(guids, AssetDatabase.GUIDToAssetPath);
            Array.Sort(paths, StringComparer.Ordinal);
            if (paths.Length == 0) errors.Add("No project shaders were found in the shader roots.");
            bool hasGraphicsDevice = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            int compiledPasses = 0;

            foreach (string path in paths)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) { errors.Add("Shader could not be loaded: " + path); continue; }
                if (!names.Add(shader.name)) errors.Add("Duplicate shader name: " + shader.name + " (" + path + ")");
                if (hasGraphicsDevice && !shader.isSupported)
                    errors.Add("Shader is unsupported on the current graphics device: " + path);

                // Warm the default passes before the menu is shown. New keyword combinations still
                // compile synchronously when first used; this is not an all-variant/device warmup.
                if (compileDefaultPasses && hasGraphicsDevice && shader.isSupported)
                {
                    Material material = null;
                    try
                    {
                        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                        if (material.passCount == 0) errors.Add("Shader has no usable render passes: " + path);
                        for (int pass = 0; pass < material.passCount; pass++)
                        {
                            ShaderUtil.CompilePass(material, pass, true);
                            compiledPasses++;
                        }
                    }
                    catch (Exception exception) { errors.Add("Shader compilation failed: " + path + " — " + exception.Message); }
                    finally { if (material != null) UnityEngine.Object.DestroyImmediate(material); }
                }
                if (ShaderUtil.ShaderHasError(shader)) errors.Add("Shader compiler errors: " + path);
            }

            for (int index = 0; index < RequiredMaterials.GetLength(0); index++)
            {
                string resource = RequiredMaterials[index, 0], shaderPath = RequiredMaterials[index, 1];
                Material material = Resources.Load<Material>(resource);
                Shader expected = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (material == null || expected == null || material.shader != expected)
                    errors.Add("Missing/mismatched Resources material: " + resource + " (expected " + shaderPath + ")");
            }

            if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors));
            Debug.Log("DIMENSIONS FRONT: validated " + paths.Length + " project shaders and the runtime material references; " +
                compiledPasses + " default render passes compiled on the current Editor device.");
        }
    }
}
#endif
