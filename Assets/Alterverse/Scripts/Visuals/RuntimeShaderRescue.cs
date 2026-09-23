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
                repaired??=(Material[])source.Clone();
                repaired[i]=CreateReplacement(material,renderer);
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

        private static Material CreateReplacement(Material source,Renderer renderer)
        {
            Color color=Color.white;
            Color emission=Color.black;
            float metallic=0f,smoothness=.2f;

            if(source!=null)
            {
                if(source.HasProperty("_BaseColor"))color=source.GetColor("_BaseColor");
                else if(source.HasProperty("_Color"))color=source.GetColor("_Color");
                else color=source.color;
                if(source.HasProperty("_EmissionColor"))emission=source.GetColor("_EmissionColor");
                if(source.HasProperty("_Metallic"))metallic=source.GetFloat("_Metallic");
                if(source.HasProperty("_Smoothness"))smoothness=source.GetFloat("_Smoothness");
                else if(source.HasProperty("_Glossiness"))smoothness=source.GetFloat("_Glossiness");
            }

            bool unlit=renderer.shadowCastingMode==ShadowCastingMode.Off&&!renderer.receiveShadows;
            Material replacement=RuntimeShaderLibrary.Create(color,unlit,metallic,smoothness,emission);
            if(replacement!=null)replacement.name="DF_RuntimeSafe_"+(source!=null?source.name:"MissingMaterial");
            return replacement;
        }
    }
}
