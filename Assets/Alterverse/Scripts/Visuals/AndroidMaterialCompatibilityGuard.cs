using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

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
        private const int RepairsPerFrame=48;
        private Renderer[] repairQueue;
        private int repairCursor;
        private Transform worldRoot,lastVerifiedWorld;
        private readonly List<Material> materials=new List<Material>(8);
        private readonly Dictionary<EntityId,Material> verifiedRenderers=new Dictionary<EntityId,Material>(1024);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(GraphicsSettings.currentRenderPipeline==null||
                Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Requested||
                SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
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
            BeginRepair();
            RepairBatch(int.MaxValue);
            nextRepair=Time.unscaledTime+.35f;
        }

        private void LateUpdate()
        {
            // Runtime objects are created after the scene loads (menu, arena, mechs, VFX).
            // A low-frequency pass catches them without touching gameplay simulation.
            if(repairQueue==null)
            {
                if(Time.unscaledTime<nextRepair)return;
                BeginRepair();
            }
            RepairBatch(RepairsPerFrame);
        }

        private void BeginRepair()
        {
            nextRepair=Time.unscaledTime+1f;
            if(litShader==null)litShader=RuntimeShaderLibrary.Lit;
            if(unlitShader==null)unlitShader=RuntimeShaderLibrary.Unlit;
            if(litShader==null||unlitShader==null)return;

            repairQueue=FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            repairCursor=0;
            GameObject world=GameObject.Find("DF_MatchWorld");
            worldRoot=world!=null?world.transform:null;
            if(worldRoot!=lastVerifiedWorld||verifiedRenderers.Count>4096)
            {
                lastVerifiedWorld=worldRoot;
                verifiedRenderers.Clear();
            }
        }

        private void RepairBatch(int budget)
        {
            if(repairQueue==null)return;
            int processed=0;
            while(repairCursor<repairQueue.Length&&processed<budget)
            {
                Renderer renderer=repairQueue[repairCursor++];
                if(renderer==null||!renderer.gameObject.activeInHierarchy)continue;
                EntityId id=renderer.GetEntityId();
                Material shared=renderer.sharedMaterial;
                if(shared!=null&&verifiedRenderers.TryGetValue(id,out Material prev)&&ReferenceEquals(prev,shared))
                    continue;
                processed++;
                RepairRenderer(renderer);
                SurfaceEdgeQuality.Apply(renderer,worldRoot);
                Material after=renderer.sharedMaterial;
                if(after!=null&&!NeedsRepair(after))
                    verifiedRenderers[id]=after;
            }
            if(repairCursor==repairQueue.Length)
            {
                repairQueue=null;worldRoot=null;
                nextRepair=Time.unscaledTime+2f;
            }
        }

        private void RepairRenderer(Renderer renderer)
        {
            if(renderer==null)return;
            renderer.GetSharedMaterials(materials);
            bool changed=false;
            for(int i=0;i<materials.Count;i++)
            {
                Material source=materials[i];
                Material replacement;
                if(NeedsRepair(source))
                {
                    Shader shader=PreferUnlit(renderer,source)?RuntimeShaderLibrary.Unlit:RuntimeShaderLibrary.Lit;
                    replacement=Replacement(source,shader);
                }
                else replacement=RepairInvalidNormalMap(source);
                if(replacement==source)continue;
                materials[i]=replacement;
                RuntimeMaterialPalette.For(renderer.gameObject).OwnRepair(replacement);
                changed=true;
            }
            if(changed)renderer.SetSharedMaterials(materials);
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

        private static Material RepairInvalidNormalMap(Material source)
        {
            // A shader swap or cached model import can leave a cubemap in a 2D
            // normal-map slot even when the URP shader itself is supported.
            Shader shader=source.shader;
            int property=shader.FindPropertyIndex("_BumpMap");
            if(property<0||shader.GetPropertyType(property)!=ShaderPropertyType.Texture||
               shader.GetPropertyTextureDimension(property)!=TextureDimension.Tex2D)return source;
            Texture normal=source.GetTexture("_BumpMap");
            if(normal==null||normal.dimension==TextureDimension.Tex2D)return source;
            // Keep shared imported assets and every other material setting intact.
            Material repaired=new Material(source){name=source.name+" [2D Normal Repair]"};
            repaired.SetTexture("_BumpMap",null);
            if(repaired.HasProperty("_BumpScale"))repaired.SetFloat("_BumpScale",0f);
            repaired.DisableKeyword("_NORMALMAP");
            return repaired;
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
                    Texture texture=source.GetTexture(map);
                    if(texture==null||texture.dimension==TextureDimension.Tex2D)
                    {
                        replacement.SetTexture("_BaseMap",texture);
                        replacement.SetTextureScale("_BaseMap",source.GetTextureScale(map));
                        replacement.SetTextureOffset("_BaseMap",source.GetTextureOffset(map));
                    }
                }
            }
            if(source!=null&&source.HasProperty("_BumpMap")&&replacement.HasProperty("_BumpMap"))
            {
                Texture normal=source.GetTexture("_BumpMap");
                if(normal!=null&&normal.dimension==TextureDimension.Tex2D)
                {
                    replacement.SetTexture("_BumpMap",normal);
                    replacement.SetTextureScale("_BumpMap",source.GetTextureScale("_BumpMap"));
                    replacement.SetTextureOffset("_BumpMap",source.GetTextureOffset("_BumpMap"));
                    if(source.HasProperty("_BumpScale")&&replacement.HasProperty("_BumpScale"))
                        replacement.SetFloat("_BumpScale",source.GetFloat("_BumpScale"));
                    if(replacement.HasProperty("_NormalEncoding"))
                        replacement.SetFloat("_NormalEncoding",source.HasProperty("_NormalEncoding")?source.GetFloat("_NormalEncoding"):1f);
                    replacement.EnableKeyword("_NORMALMAP");
                }
            }

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


