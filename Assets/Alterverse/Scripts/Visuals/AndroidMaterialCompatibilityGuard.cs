using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Android/URP safety net for procedural runtime materials.
    /// Some DIMENSIONS FRONT visuals are created entirely from code. If a runtime-created
    /// material falls back to a Built-in/legacy shader in a player build, URP renders it
    /// magenta. This guard replaces only known incompatible/error shaders and leaves all
    /// valid URP/custom materials untouched.
    /// </summary>
    public sealed class AndroidMaterialCompatibilityGuard : MonoBehaviour
    {
        private static Shader litShader;
        private static Shader unlitShader;
        private float nextRepair;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(GraphicsSettings.currentRenderPipeline==null)return;
            if(FindAnyObjectByType<AndroidMaterialCompatibilityGuard>()!=null)return;
            GameObject guard=new GameObject("DF_AndroidMaterialCompatibilityGuard");
            DontDestroyOnLoad(guard);
            guard.AddComponent<AndroidMaterialCompatibilityGuard>();
#endif
        }

        private void Awake()
        {
            // Resources-backed project shaders are guaranteed to be present in release players.
            // Do not depend on Shader.Find here: the guard must also work when URP variants were stripped.
            litShader=RuntimeShaderLibrary.Lit;
            unlitShader=RuntimeShaderLibrary.Unlit;
            RepairAll();
            nextRepair=Time.unscaledTime+.35f;
        }

        private void Update()
        {
            // Runtime objects are created after the scene loads (menu, arena, mechs, VFX).
            // A low-frequency pass catches them without touching gameplay simulation.
            if(Time.unscaledTime<nextRepair)return;
            nextRepair=Time.unscaledTime+1f;
            RepairAll();
        }

        private static void RepairAll()
        {
            if(litShader==null)litShader=RuntimeShaderLibrary.Lit;
            if(unlitShader==null)unlitShader=RuntimeShaderLibrary.Unlit;
            if(litShader==null||unlitShader==null)return;

            Renderer[] renderers=FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            foreach(Renderer renderer in renderers)
            {
                if(renderer==null)continue;
                Material[] materials=renderer.sharedMaterials;
                bool changed=false;
                for(int i=0;i<materials.Length;i++)
                {
                    Material source=materials[i];
                    if(!NeedsRepair(source))continue;
                    bool unlit=PreferUnlit(renderer,source);
                    materials[i]=Replacement(source,unlit?unlitShader:litShader);
                    changed|=materials[i]!=source;
                }
                if(changed)renderer.sharedMaterials=materials;
            }
        }

        private static bool NeedsRepair(Material material)
        {
            if(material==null||material.shader==null)return true;
            string shader=material.shader.name;
            return shader=="Hidden/InternalErrorShader"||
                   shader=="Standard"||
                   shader.StartsWith("Legacy Shaders/")||
                   shader=="Unlit/Color"||
                   shader=="Unlit/Texture";
        }

        private static bool PreferUnlit(Renderer renderer,Material material)
        {
            string name=(renderer.name+" "+(material!=null?material.name:string.Empty)).ToLowerInvariant();
            if(name.Contains("ground")||name.Contains("regolith")||name.Contains("surface")||
               name.Contains("rock")||name.Contains("debris")||name.Contains("planet"))return false;
            if(renderer.shadowCastingMode==ShadowCastingMode.Off)return true;
            return name.Contains("backdrop")||name.Contains("star")||name.Contains("cosmic")||
                   name.Contains("void")||name.Contains("ring")||name.Contains("energy")||
                   name.Contains("glow")||name.Contains("vfx");
        }

        private static Material Replacement(Material source,Shader shader)
        {
            if(shader==null)return source;
            Material replacement=new Material(shader)
            {
                name=(source!=null?source.name:"DF_RuntimeMaterial")+" [Android URP]"
            };

            Color color=Color.white;
            if(source!=null)
            {
                if(source.HasProperty("_BaseColor"))color=source.GetColor("_BaseColor");
                else if(source.HasProperty("_Color"))color=source.GetColor("_Color");
            }
            if(replacement.HasProperty("_BaseColor"))replacement.SetColor("_BaseColor",color);
            if(replacement.HasProperty("_Color"))replacement.SetColor("_Color",color);

            if(source!=null&&source.HasProperty("_BaseMap")&&replacement.HasProperty("_BaseMap"))
                replacement.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));
            if(source!=null&&source.HasProperty("_BumpMap")&&replacement.HasProperty("_BumpMap"))
                replacement.SetTexture("_BumpMap",source.GetTexture("_BumpMap"));
            if(source!=null&&source.HasProperty("_BumpScale")&&replacement.HasProperty("_BumpScale"))
                replacement.SetFloat("_BumpScale",source.GetFloat("_BumpScale"));

            if(source!=null&&source.HasProperty("_EmissionColor")&&replacement.HasProperty("_EmissionColor"))
            {
                Color emission=source.GetColor("_EmissionColor");
                replacement.SetColor("_EmissionColor",emission);
            }
            if(source!=null&&source.HasProperty("_Metallic")&&replacement.HasProperty("_Metallic"))
                replacement.SetFloat("_Metallic",source.GetFloat("_Metallic"));
            if(source!=null&&source.HasProperty("_Smoothness")&&replacement.HasProperty("_Smoothness"))
                replacement.SetFloat("_Smoothness",source.GetFloat("_Smoothness"));

            return replacement;
        }
    }
}
