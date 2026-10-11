#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    /// <summary>Validate the serialized shader references used by runtime-created menu effects.</summary>
    public sealed class MenuCanvasChecks : IPreprocessBuildWithReport
    {
        public int callbackOrder => -90;
        public void OnPreprocessBuild(BuildReport report) => Validate();

        [MenuItem("DIMENSIONS FRONT/Checks/Menu Canvas assets")]
        public static void Validate()
        {
            Material particles = Resources.Load<Material>("FrontEnd/MenuParticle");
            Shader effects = Shader.Find("Hidden/UI/Default (UIEffect)");
            ShaderVariantCollection variants = Resources.Load<ShaderVariantCollection>("FrontEnd/MenuUiVariants");
            if (particles == null || particles.shader == null || particles.shader.name != "UI/Additive")
                throw new BuildFailedException("MenuParticle must reference the installed UI Particle additive shader.");
            if (effects == null || ShaderUtil.ShaderHasError(effects) || ShaderUtil.ShaderHasError(particles.shader))
                throw new BuildFailedException("A menu addon shader is missing or failed to compile.");
            if (variants == null || variants.shaderCount != 2 || variants.variantCount < 20)
                throw new BuildFailedException("The menu's runtime gradients, shiny accents and masked particle variants are missing.");
            Debug.Log("DIMENSIONS FRONT: Canvas menu materials and runtime shader variants validated.");
        }
    }
}
#endif
