using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Cosmetic-only premium weapon pulse. It never changes attack timing, range, hitboxes or stats.
    /// Shared materials stay shared; per-weapon energy is driven with MaterialPropertyBlock.
    /// </summary>
    public sealed class PremiumWeaponPresentation : MonoBehaviour
    {
        private Renderer[] energyRenderers;
        private Color glow;
        private MaterialPropertyBlock block;
        private float pulse;
        private static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId=Shader.PropertyToID("_Color");
        private static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");

        public void Configure(Color color,Material energyMaterial)
        {
            glow=color;
            block??=new MaterialPropertyBlock();
            var found=new List<Renderer>(12);
            foreach(Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if(renderer!=null&&renderer.sharedMaterial==energyMaterial)found.Add(renderer);
            energyRenderers=found.ToArray();
            Apply(.08f);
        }

        public void Pulse(float strength=1f)=>pulse=Mathf.Max(pulse,Mathf.Clamp(strength,.2f,1.35f));

        private void Update()
        {
            if(energyRenderers==null||energyRenderers.Length==0)return;
            pulse=Mathf.MoveTowards(pulse,0f,Time.deltaTime*4.8f);
            float idle=.08f+.035f*(Mathf.Sin(Time.time*2.3f)*.5f+.5f);
            Apply(Mathf.Max(idle,pulse));
        }

        private void Apply(float energy)
        {
            if(block==null||energyRenderers==null)return;
            float emission=1.45f+energy*3.6f;
            Color baseColor=Color.Lerp(glow,Color.white,.10f+energy*.12f);
            foreach(Renderer renderer in energyRenderers)
            {
                if(renderer==null)continue;
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColor,baseColor);
                block.SetColor(ColorId,baseColor);
                block.SetColor(EmissionColor,glow*emission);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
