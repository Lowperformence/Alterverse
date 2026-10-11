using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Single URP safety net for procedural runtime materials on all platforms.
    /// Some DIMENSIONS FRONT visuals are created entirely from code. If a runtime-created
    /// material falls back to a Built-in/legacy shader in a player build, URP renders it
    /// magenta. This guard replaces only known incompatible/error shaders and leaves all
    /// valid URP/custom materials untouched.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class AndroidMaterialCompatibilityGuard : MonoBehaviour
    {
        private static Shader litShader;
        private static Shader unlitShader;
        private float nextRepair;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(GraphicsSettings.currentRenderPipeline==null)return;
            if(FindAnyObjectByType<AndroidMaterialCompatibilityGuard>()!=null)return;
            GameObject guard=new GameObject("DF_AndroidMaterialCompatibilityGuard");
            DontDestroyOnLoad(guard);
            guard.AddComponent<AndroidMaterialCompatibilityGuard>();
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

        private void LateUpdate()
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
            GameObject world=GameObject.Find("DF_MatchWorld");
            Transform worldRoot=world!=null?world.transform:null;
            foreach(Renderer renderer in renderers)
            {
                RepairRenderer(renderer);
                SurfaceEdgeQuality.Apply(renderer,worldRoot);
            }
        }

        private static void RepairRenderer(Renderer renderer)
        {
            if(renderer==null)return;
            Material[] materials=renderer.sharedMaterials;
            bool changed=false;
            for(int i=0;i<materials.Length;i++)
            {
                Material source=materials[i];
                GameMaterialFinishes.Apply(source,renderer.name);
                if(!NeedsRepair(source))continue;
                Shader shader=PreferUnlit(renderer,source)?RuntimeShaderLibrary.Unlit:RuntimeShaderLibrary.Lit;
                Material replacement=Replacement(source,shader);
                GameMaterialFinishes.Apply(replacement,renderer.name);
                if(replacement==source)continue;
                materials[i]=replacement;
                RuntimeMaterialPalette.For(renderer.gameObject).OwnRepair(replacement);
                changed=true;
            }
            if(changed)renderer.sharedMaterials=materials;
        }

        private static bool NeedsRepair(Material material)
        {
            if(material==null||material.shader==null)return true;
            if(!material.shader.isSupported)return true;
            string shader=material.shader.name;
            return shader=="Hidden/InternalErrorShader"||
                   shader=="Standard"||
                   shader.StartsWith("Legacy Shaders/")||
                   shader=="Unlit/Color"||
                   shader=="Unlit/Texture";
        }

        private static bool PreferUnlit(Renderer renderer,Material material)
        {
            if(material!=null&&material.shader!=null&&material.shader.name.StartsWith("Unlit/"))return true;
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
            if(shader==null||!shader.isSupported)return source;
            Material replacement=new Material(shader)
            {
                name=(source!=null?source.name:"DF_RuntimeMaterial")+" [Runtime URP]"
            };

            Color color=Color.white;
            if(source!=null)
            {
                if(source.HasProperty("_BaseColor"))color=source.GetColor("_BaseColor");
                else if(source.HasProperty("_Color"))color=source.GetColor("_Color");
            }
            if(replacement.HasProperty("_BaseColor"))replacement.SetColor("_BaseColor",color);
            if(replacement.HasProperty("_Color"))replacement.SetColor("_Color",color);

            if(source!=null&&replacement.HasProperty("_BaseMap"))
            {
                string map=source.HasProperty("_BaseMap")?"_BaseMap":source.HasProperty("_MainTex")?"_MainTex":null;
                if(map!=null)
                {
                    replacement.SetTexture("_BaseMap",source.GetTexture(map));
                    replacement.SetTextureScale("_BaseMap",source.GetTextureScale(map));
                    replacement.SetTextureOffset("_BaseMap",source.GetTextureOffset(map));
                }
            }
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
            else if(source!=null&&source.HasProperty("_Glossiness")&&replacement.HasProperty("_Smoothness"))
                replacement.SetFloat("_Smoothness",source.GetFloat("_Glossiness"));

            return replacement;
        }
    }
}


