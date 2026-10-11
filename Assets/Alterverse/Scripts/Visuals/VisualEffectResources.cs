using UnityEngine;

namespace Alterverse.Visuals
{
    // One shared offline texture. No camera buffers, compute shaders or runtime baking.
    public static class VisualEffectResources
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>Shader.SetGlobalFloat("_DF_VfxFlowReady",0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            Texture2D flow=Resources.Load<Texture2D>("CombatVfx/FlowNoise");
            Shader.SetGlobalTexture("_DF_VfxFlow",flow);
            Shader.SetGlobalFloat("_DF_VfxFlowReady",flow!=null?1f:0f);
        }
    }
}
