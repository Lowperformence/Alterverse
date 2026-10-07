#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class SurfaceQualityChecks
    {
        // Run after import, then repeat with Android selected as the build target.
        [MenuItem("DIMENSIONS FRONT/Checks/Surface quality shaders")]
        public static void Run()
        {
            string[] paths =
            {
                "Assets/Resources/DF_RuntimeLit.shader",
                "Assets/Resources/DF_PlanetSurface.shader",
                "Assets/Alterverse/Resources/HeroArt/HeroSurface.shader",
                "Assets/Alterverse/Resources/HeroArt/IoneArmorOrnaments.shader",
                "Assets/Alterverse/Resources/HeroArt/HeroUndersuitFabric.shader",
                "Assets/Alterverse/Resources/HeroArt/SkinOrnaments.shader",
                "Assets/Alterverse/Resources/HeroArt/TankWoodlandCamo.shader",
                "Assets/Alterverse/Resources/CyberShark/AnchorMetal.shader",
                "Assets/Alterverse/Resources/CyberShark/JacketFabric.shader"
            };
            foreach (string path in paths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null || ShaderUtil.ShaderHasError(shader) || !shader.isSupported)
                    throw new InvalidOperationException("Surface shader import/support failed: " + path);
                Material probe = new Material(shader);
                try
                {
                    foreach (string pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals" })
                        if (probe.FindPass(pass) < 0)
                            throw new InvalidOperationException("Missing " + pass + " on " + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(probe); }
            }
            Debug.Log("Surface quality: 9 shaders imported without reported errors for " +
                EditorUserBuildSettings.activeBuildTarget +
                ". Check live hero previews, every arena and Android frame time separately.");
        }
    }
}
#endif
