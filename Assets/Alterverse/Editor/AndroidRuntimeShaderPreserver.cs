#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.EditorChecks
{
    /// <summary>
    /// Procedural visuals resolve URP shaders with Shader.Find at runtime. Unity can strip
    /// shaders that are not referenced by serialized scene/material assets, which turns
    /// those objects magenta in Android players. Dedicated arena construction also creates
    /// materials before the match can launch, even with -nographics. Keep its runtime shaders.
    /// </summary>
    public sealed class AndroidRuntimeShaderPreserver : IPreprocessBuildWithReport
    {
        private static readonly string[] RequiredShaders=
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "DimensionsFront/HeroEnergy"
        };

        public int callbackOrder=>-100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.Android)return;
            EnsureRequiredShaders(true);
            ValidateCombatMaterial();
            HeroPresentationChecks.CheckCombatLineTransforms();
        }

        private static void ValidateCombatMaterial()
        {
            Material material=Resources.Load<Material>("CombatVfx/HeroEnergy");
            Shader shader=Resources.Load<Shader>("CombatVfx/HeroEnergy");
            if(material==null||shader==null||material.shader!=shader||
                shader.name!="DimensionsFront/HeroEnergy"||ShaderUtil.ShaderHasError(shader))
                throw new BuildFailedException("HeroEnergy material/shader is missing, mismatched or has compiler errors. Android spell VFX cannot be validated.");
        }

        [InitializeOnLoadMethod]
        private static void EnsureWhenAndroidProjectLoads()
        {
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)return;
            EditorApplication.delayCall+=()=>EnsureRequiredShaders(false);
        }

        [MenuItem("DIMENSIONS FRONT/Release/Ensure Android runtime shaders")]
        public static void EnsureFromMenu()=>EnsureRequiredShaders(true);

        public static void EnsureForDedicatedServer()=>EnsureRequiredShaders(true,"dedicated server");

        private static void EnsureRequiredShaders(bool failIfMissing,string buildKind="Android")
        {
            GraphicsSettings settings=AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if(settings==null)
            {
                if(failIfMissing)throw new BuildFailedException("GraphicsSettings.asset could not be loaded.");
                Debug.LogWarning($"DIMENSIONS FRONT: GraphicsSettings.asset could not be loaded for {buildKind} shader preservation.");
                return;
            }

            SerializedObject serialized=new SerializedObject(settings);
            SerializedProperty shaders=serialized.FindProperty("m_AlwaysIncludedShaders");
            if(shaders==null)
            {
                if(failIfMissing)throw new BuildFailedException("Always Included Shaders setting could not be found.");
                return;
            }

            bool changed=false;
            foreach(string shaderName in RequiredShaders)
            {
                Shader shader=Shader.Find(shaderName);
                if(shader==null)
                {
                    string message=$"Required {buildKind} runtime shader is missing: "+shaderName;
                    if(failIfMissing)throw new BuildFailedException(message);
                    Debug.LogWarning("DIMENSIONS FRONT: "+message);
                    continue;
                }

                bool present=false;
                for(int i=0;i<shaders.arraySize;i++)
                {
                    if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==shader)
                    {
                        present=true;
                        break;
                    }
                }
                if(present)continue;

                int index=shaders.arraySize;
                shaders.InsertArrayElementAtIndex(index);
                shaders.GetArrayElementAtIndex(index).objectReferenceValue=shader;
                changed=true;
            }

            if(!changed)return;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"DIMENSIONS FRONT: preserved URP Lit/Unlit and HeroEnergy shaders for {buildKind} arena construction.");
        }
    }
}
#endif

