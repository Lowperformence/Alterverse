using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Colour-only exoplanet biomes; Moon terrain and gameplay remain authoritative.
    /// Warm/cold bands run across Z, perpendicular to the left/right team axis.</summary>
    public static class ExoplanetArenaProfile
    {
        public static string SurfaceResource(ArenaVariant variant)=>variant switch {
            ArenaVariant.Wasp76b=>"Dark00", ArenaVariant.Trappist1e=>"Earth", _=>variant.ToString() };
        public static string MonsterName(ArenaVariant variant)=>variant==ArenaVariant.Wasp76b?"FERROX":"VESPERA";
        public static Color Energy(ArenaVariant variant)=>variant==ArenaVariant.Wasp76b
            ?new Color(1f,.48f,.12f):new Color(.20f,.92f,.78f);

        public static Color GroundColor(ArenaVariant variant,float x,float z,float value)
        {
            // Mirror mineral noise across the team axis; never tint one base as the hot/cold side.
            x=Mathf.Abs(x);
            float mineral=Mathf.PerlinNoise(x*.095f+37f,z*.095f+91f);
            if(variant==ArenaVariant.Wasp76b)
            {
                Color iron=new(value*.82f,value*.84f,value*.86f);
                Color copper=new(value*1.34f,value*.74f,value*.44f);
                Color crust=Color.Lerp(iron,copper,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.40f,.73f,mineral))*.78f);
                float ridge=Mathf.Abs(Mathf.PerlinNoise(x*.12f+71f,z*.12f+23f)-.5f);
                float seam=(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.012f,.045f,ridge)));
                // Sparse molten inclusions at the rim, never a fake damage telegraph in the lanes.
                seam*=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(18f,29f,Mathf.Abs(z)))*.62f;
                return Color.Lerp(crust,new Color(.87f,.33f,.065f),seam);
            }
            float cold=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-15f,-30f,z));
            float warm=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(15f,30f,z));
            Color soil=new(value*.57f,value*.84f,value*.85f);
            Color purple=new(value*.87f,value*.57f,value*1.11f);
            Color twilight=Color.Lerp(soil,purple,mineral*.50f);
            twilight=Color.Lerp(twilight,new Color(value*1.28f,value*1.48f,value*1.63f),cold*.78f);
            twilight=Color.Lerp(twilight,new Color(value*1.13f,value*.81f,value*.54f),warm*.57f);
            float pools=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.69f,.80f,mineral));
            return Color.Lerp(twilight,new Color(.045f,.28f,.31f),pools*(1f-cold)*.50f);
        }

        public static Color BackdropColor(ArenaVariant variant,float x,float z)
        {
            float clouds=Mathf.PerlinNoise(x*6f+41f,z*9f+17f);
            clouds=.65f*clouds+.35f*Mathf.PerlinNoise(x*17f+93f,z*23f+51f);
            if(variant==ArenaVariant.Wasp76b)
                return Color.Lerp(new Color(.040f,.035f,.033f),new Color(.23f,.105f,.040f),clouds*clouds);
            float dusk=Mathf.SmoothStep(0f,1f,z);
            Color atmosphere=Color.Lerp(new Color(.026f,.045f,.075f),new Color(.14f,.065f,.083f),dusk);
            return Color.Lerp(atmosphere,new Color(.12f,.19f,.22f),clouds*.25f);
        }
    }
}
