using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Temporary per-viewer materials; the server-owned timer lives in VoidKnightState.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(3200)]
    public sealed class VoidKnightStealth : MonoBehaviour
    {
        private sealed class Surface
        {
            internal Renderer Renderer;
            internal Material[] Original, Ghost;
            internal ShadowCastingMode Shadows;
            internal bool ReceivesShadows, Enabled, Effect;
        }
        private readonly Dictionary<Renderer,Surface> surfaces=new();
        private readonly Dictionary<Material,Material> materials=new();
        private VoidKnightState state;
        private Shader shader;
        private float nextScan;
        private bool applied;
        private static readonly int OpacityId=Shader.PropertyToID("_StealthOpacity");
        private static readonly int TintId=Shader.PropertyToID("_StealthTint");
        private static readonly int TextureId=Shader.PropertyToID("_StealthMap");

        public static float OpacityFor(bool ownerOrAlly)=>ownerOrAlly
            ?VoidKnightBalance.OwnerCloakOpacity:VoidKnightBalance.EnemyCloakOpacity;

        private void LateUpdate()
        {
            state??=GetComponent<VoidKnightState>();
            if(state==null||!state.Cloaked){if(applied)Restore();return;}
            // A dedicated server still simulates the cloak without creating rendering resources.
            if(Application.isBatchMode)return;
            if(shader==null)shader=Resources.Load<Shader>("CombatVfx/VoidKnightStealth");
            if(shader==null||!shader.isSupported)return;
            if(!applied||Time.time>=nextScan)
            {
                CaptureSurfaces();nextScan=Time.time+.1f;applied=true;
            }
            var viewer=LocalHeroContext.Member;
            var owner=GetComponent<Alterverse.Combat.TeamMember>();
            bool friendly=LocalHeroContext.IsLocal(gameObject)||viewer!=null&&owner!=null&&viewer.Team==owner.Team;
            float opacity=OpacityFor(friendly);
            foreach(Material ghost in materials.Values)if(ghost!=null)ghost.SetFloat(OpacityId,opacity);
            foreach(Surface surface in surfaces.Values)
                if(surface.Renderer!=null)
                {
                    surface.Renderer.shadowCastingMode=ShadowCastingMode.Off;
                    if(surface.Effect)surface.Renderer.enabled=false;
                }
        }

        private void CaptureSurfaces()
        {
            foreach(Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if(surfaces.ContainsKey(renderer))continue;
                var surface=new Surface{Renderer=renderer,Original=renderer.sharedMaterials,
                    Shadows=renderer.shadowCastingMode,ReceivesShadows=renderer.receiveShadows,
                    Enabled=renderer.enabled,Effect=renderer is ParticleSystemRenderer||renderer is TrailRenderer||renderer is LineRenderer};
                surface.Ghost=new Material[surface.Original.Length];
                for(int i=0;i<surface.Original.Length;i++)
                {
                    Material original=surface.Original[i];
                    if(original==null)continue;
                    if(!materials.TryGetValue(original,out Material ghost))
                    {
                        ghost=new Material(shader){name="Void cloak (temporary)",hideFlags=HideFlags.HideAndDontSave};
                        Color tint=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):
                            original.HasProperty("_Color")?original.GetColor("_Color"):Color.white;
                        bool painted=original.shader!=null&&original.shader.name=="DimensionsFront/HeroSurface";
                        if(painted&&tint.a<=0)tint=Color.white;
                        tint.a=1f;ghost.SetColor(TintId,tint);
                        ghost.SetFloat(Shader.PropertyToID("_StealthVertexColor"),painted?1f:0f);
                        Texture texture=original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap"):
                            original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null;
                        if(texture!=null)ghost.SetTexture(TextureId,texture);
                        string map=original.HasProperty("_BaseMap")?"_BaseMap":original.HasProperty("_MainTex")?"_MainTex":null;
                        if(map!=null)
                        {
                            ghost.SetTextureScale("_StealthMap",original.GetTextureScale(map));
                            ghost.SetTextureOffset("_StealthMap",original.GetTextureOffset(map));
                        }
                        materials.Add(original,ghost);
                    }
                    surface.Ghost[i]=ghost;
                }
                surfaces.Add(renderer,surface);
                if(!surface.Effect)renderer.sharedMaterials=surface.Ghost;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(surface.Effect)renderer.enabled=false;
            }
        }

        private void Restore()
        {
            foreach(Surface surface in surfaces.Values)
            {
                Renderer renderer=surface.Renderer;if(renderer==null)continue;
                // Do not undo a new skin installed by another system during the cloak.
                Material[] current=renderer.sharedMaterials;
                bool ours=current.Length==surface.Ghost.Length;
                for(int i=0;i<current.Length&&ours;i++)ours=current[i]==surface.Ghost[i];
                if(ours&&!surface.Effect)renderer.sharedMaterials=surface.Original;
                renderer.shadowCastingMode=surface.Shadows;renderer.receiveShadows=surface.ReceivesShadows;
                if(surface.Effect)renderer.enabled=surface.Enabled;
            }
            foreach(Material material in materials.Values)if(material!=null)Destroy(material);
            materials.Clear();surfaces.Clear();applied=false;
        }
        private void OnDisable()=>Restore();
        private void OnDestroy()=>Restore();
    }
}
