using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Safety net for procedural visuals that were authored with runtime Shader.Find/default
    /// primitive materials. Release builds may strip those shaders and render the material pink.
    /// The rescue pass only touches missing/unsupported/built-in fallback shaders and preserves
    /// the authored color/emission while swapping to project-owned Resources shaders.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class RuntimeShaderRescue : MonoBehaviour
    {
        private float nextScan;
        private float intensiveScanUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(FindAnyObjectByType<RuntimeShaderRescue>()!=null)return;
            GameObject go=new("DF_RuntimeShaderRescue");
            DontDestroyOnLoad(go);
            go.AddComponent<RuntimeShaderRescue>();
        }

        private void Awake()
        {
            intensiveScanUntil=Time.unscaledTime+12f;
            RepairAll();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            // Every frame during the initial build window prevents a one-frame magenta flash.
            // Afterwards this becomes a cheap low-frequency safety pass for later spawned events.
            nextScan=Time.unscaledTime+(Time.unscaledTime<intensiveScanUntil ? 0f : 2.5f);
            RepairAll();
        }

        private static void RepairAll()
        {
            Renderer[] renderers=FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            for(int i=0;i<renderers.Length;i++)Repair(renderers[i]);
        }

        private static void Repair(Renderer renderer)
        {
            if(renderer==null)return;
            Material[] source=renderer.sharedMaterials;
            if(source==null||source.Length==0)return;

            Material[] repaired=null;
            for(int i=0;i<source.Length;i++)
            {
                Material material=source[i];
                if(!NeedsRepair(material))continue;

                if(material==null)
                {
                    repaired??=(Material[])source.Clone();
                    repaired[i]=RuntimeShaderLibrary.Create(Color.white,IsUnlit(renderer));
                    continue;
                }

                RepairInPlace(material,renderer);
            }
            if(repaired!=null)renderer.sharedMaterials=repaired;
        }

        private static bool NeedsRepair(Material material)
        {
            if(material==null||material.shader==null)return true;
            Shader shader=material.shader;
            string name=shader.name??string.Empty;
            if(!shader.isSupported)return true;
            if(name=="Hidden/InternalErrorShader")return true;

            // These built-in fallbacks are not valid URP player materials. They are commonly
            // reached after a stripped URP Shader.Find call and are the usual magenta culprit.
            return name=="Standard"||name=="Unlit/Color";
        }

        private static void RepairInPlace(Material material,Renderer renderer)
        {
            Color color=Color.white;
            Color emission=Color.black;
            float metallic=0f,smoothness=.2f;

            if(material.HasProperty("_BaseColor"))color=material.GetColor("_BaseColor");
            else if(material.HasProperty("_Color"))color=material.GetColor("_Color");
            else color=material.color;
            if(material.HasProperty("_EmissionColor"))emission=material.GetColor("_EmissionColor");
            if(material.HasProperty("_Metallic"))metallic=material.GetFloat("_Metallic");
            if(material.HasProperty("_Smoothness"))smoothness=material.GetFloat("_Smoothness");
            else if(material.HasProperty("_Glossiness"))smoothness=material.GetFloat("_Glossiness");

            Shader safe=IsUnlit(renderer)?RuntimeShaderLibrary.Unlit:RuntimeShaderLibrary.Lit;
            if(safe==null)return;
            material.shader=safe;

            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",emission);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",Mathf.Clamp01(metallic));
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",Mathf.Clamp01(smoothness));
        }

        private static bool IsUnlit(Renderer renderer)
            =>renderer.shadowCastingMode==ShadowCastingMode.Off&&!renderer.receiveShadows;
    }
}
