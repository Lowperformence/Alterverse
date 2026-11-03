using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    [DefaultExecutionOrder(4100)]
    public sealed class CosmicNebulaConcealment:MonoBehaviour
    {
        // 60% transparent means 40% of the source material's original opacity.
        private const float ConcealedOpacity=.4f;
        private readonly Dictionary<Renderer,bool> hidden=new();
        private readonly Dictionary<Renderer,Material[]> originals=new();
        private readonly Dictionary<Material,Material> faded=new();
        private readonly List<Renderer> scan=new();
        private Health health;
        private float nextScan;
        private bool wasHidden,wasGhost;
        private void LateUpdate()
        {
            if(Application.isBatchMode)return;
            health??=GetComponent<Health>();
            var viewer=LocalHeroContext.Member;
            if(viewer==null){Restore();return;}
            bool hiddenFromViewer=CosmicNebulaField.HiddenFrom(viewer.gameObject,health);
            bool ghost=!hiddenFromViewer&&CosmicNebulaField.IsConcealed(health);
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
            if(originals.ContainsKey(renderer))return;
            var source=renderer.sharedMaterials;if(source==null||source.Length==0)return;
            var replacement=(Material[])source.Clone();bool changed=false;
            for(int i=0;i<source.Length;i++)
            {
                var original=source[i];if(original==null)continue;
                if(!faded.TryGetValue(original,out var copy))
                {
                    copy=new Material(original){name=original.name+" (nebula)"};
                    if(copy.HasProperty("_Surface"))copy.SetFloat("_Surface",1);
                    if(copy.HasProperty("_Mode"))copy.SetFloat("_Mode",3);
                    if(copy.HasProperty("_SrcBlend"))copy.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                    if(copy.HasProperty("_DstBlend"))copy.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    if(copy.HasProperty("_ZWrite"))copy.SetFloat("_ZWrite",0);
                    copy.SetOverrideTag("RenderType","Transparent");copy.renderQueue=3000;
                    copy.DisableKeyword("_ALPHATEST_ON");copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    copy.EnableKeyword("_ALPHABLEND_ON");copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    foreach(string property in new[]{"_BaseColor","_Color"})
                        if(copy.HasProperty(property)){Color c=copy.GetColor(property);c.a*=ConcealedOpacity;copy.SetColor(property,c);}
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
