using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Visuals
{
    [DefaultExecutionOrder(3100)]
    public sealed class CosmicNebulaConcealment:MonoBehaviour
    {
        // 60% transparent means 40% of the source material's original opacity.
        private const float ConcealedOpacity=.4f;
        private readonly Dictionary<Renderer,bool> hidden=new();
        private readonly Dictionary<Renderer,Material[]> originals=new();
        private readonly Dictionary<Material,Material> faded=new();
        private readonly List<Renderer> scan=new();
        private Health health;
        private Shader ghostShader;
        private float nextScan;
        private bool wasHidden,wasGhost;
        private void LateUpdate()
        {
            if(Application.isBatchMode)return;
            health??=GetComponent<Health>();
            var viewer=LocalHeroContext.Member;
            if(viewer==null){Restore();return;}
            bool hiddenFromViewer=CosmicNebulaField.HiddenFrom(viewer.gameObject,health);
            // Void Knight owns its materials while cloaked. Run before its presentation so
            // it captures original materials, then wait for its restoration before fading.
            var cloak=GetComponent<VoidKnightStealth>();
            bool cloakOwnsMaterials=GetComponent<VoidKnightState>()?.Cloaked==true||cloak!=null&&cloak.Applied;
            bool ghost=!hiddenFromViewer&&!cloakOwnsMaterials&&CosmicNebulaField.IsConcealed(health);
            if(hiddenFromViewer!=wasHidden||ghost!=wasGhost)nextScan=0;
            wasHidden=hiddenFromViewer;wasGhost=ghost;
            if(!hiddenFromViewer)RestoreHidden();
            if(!ghost)RestoreMaterials();
            if(!hiddenFromViewer&&!ghost)return;
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.25f;
            scan.Clear();GetComponentsInChildren(true,scan);
            foreach(var renderer in scan)
            {
                if(renderer==null)continue;
                if(hiddenFromViewer)
                {
                    if(!hidden.ContainsKey(renderer))hidden.Add(renderer,renderer.forceRenderingOff);
                    renderer.forceRenderingOff=true;
                }
                else if(ghost&&(renderer is MeshRenderer||renderer is SkinnedMeshRenderer))Fade(renderer);
            }
        }
        private void Fade(Renderer renderer)
        {
            if(originals.TryGetValue(renderer,out var previous))
            {
                var current=renderer.sharedMaterials;bool unchanged=current.Length==previous.Length;
                for(int i=0;i<current.Length&&unchanged;i++)
                    unchanged=previous[i]==null?current[i]==null:faded.TryGetValue(previous[i],out var prior)&&current[i]==prior;
                if(unchanged)return;
                // A skin or another presentation changed slots while concealed. Preserve it,
                // but recover any slots still owned by this effect before fading again.
                for(int i=0;i<current.Length&&i<previous.Length;i++)
                    if(previous[i]!=null&&faded.TryGetValue(previous[i],out var prior)&&current[i]==prior)current[i]=previous[i];
                renderer.sharedMaterials=current;originals.Remove(renderer);
            }
            if(ghostShader==null)ghostShader=Resources.Load<Shader>("CombatVfx/VoidKnightStealth");
            if(ghostShader==null||!ghostShader.isSupported)return;
            var source=renderer.sharedMaterials;if(source==null||source.Length==0)return;
            var replacement=(Material[])source.Clone();bool changed=false;
            for(int i=0;i<source.Length;i++)
            {
                var original=source[i];if(original==null)continue;
                if(!faded.TryGetValue(original,out var copy))
                {
                    // Custom hero shaders can ignore blend settings and color alpha. The
                    // resource shader blends explicitly and survives Android stripping.
                    copy=new Material(ghostShader){name=original.name+" (nebula)"};
                    Color tint=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):
                        original.HasProperty("_Color")?original.GetColor("_Color"):Color.white;
                    bool painted=original.shader!=null&&original.shader.name=="DimensionsFront/HeroSurface";
                    if(painted&&tint.a<=0)tint=Color.white;
                    copy.SetColor("_StealthTint",tint);
                    copy.SetFloat("_StealthOpacity",ConcealedOpacity);
                    copy.SetFloat("_StealthVertexColor",painted?1f:0f);
                    string map=original.HasProperty("_BaseMap")?"_BaseMap":original.HasProperty("_MainTex")?"_MainTex":null;
                    if(map!=null)
                    {
                        var texture=original.GetTexture(map);
                        if(texture!=null)copy.SetTexture("_StealthMap",texture);
                        copy.SetTextureScale("_StealthMap",original.GetTextureScale(map));
                        copy.SetTextureOffset("_StealthMap",original.GetTextureOffset(map));
                    }
                    faded.Add(original,copy);
                }
                replacement[i]=copy;changed=true;
            }
            if(changed){originals.Add(renderer,source);renderer.sharedMaterials=replacement;}
        }
        private void RestoreHidden()
        {foreach(var pair in hidden)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;hidden.Clear();}
        private void RestoreMaterials()
        {
            if(originals.Count==0)return;
            foreach(var entry in originals)
            {
                if(entry.Key==null)continue;
                var current=entry.Key.sharedMaterials;var source=entry.Value;bool changed=false;
                for(int i=0;i<current.Length&&i<source.Length;i++)
                    if(source[i]!=null&&faded.TryGetValue(source[i],out var copy)&&current[i]==copy)
                    {current[i]=source[i];changed=true;}
                if(changed)entry.Key.sharedMaterials=current;
            }
            originals.Clear();foreach(var material in faded.Values)if(material!=null)Destroy(material);faded.Clear();
        }
        private void Restore(){RestoreHidden();RestoreMaterials();nextScan=0;wasHidden=wasGhost=false;}
        private void OnDisable()=>Restore();
        private void OnDestroy()=>Restore();
    }
}
