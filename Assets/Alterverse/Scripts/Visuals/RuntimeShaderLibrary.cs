using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Runtime-created visuals previously relied only on Shader.Find. Android release builds may strip
    /// those shaders when no serialized material references them, producing Unity's magenta error shader.
    /// These project shaders live in Resources, so they are guaranteed to be included in player builds.
    /// </summary>
    public static class RuntimeShaderLibrary
    {
        private static Shader lit;
        private static Shader unlit;
        private static Shader groundTelegraph;

        public static Shader Lit
        {
            get
            {
                if(lit==null)lit=Resources.Load<Shader>("DF_RuntimeLit");
                if(lit==null)lit=Shader.Find("Universal Render Pipeline/Lit");
                return lit;
            }
        }

        public static Shader Unlit
        {
            get
            {
                if(unlit==null)unlit=Resources.Load<Shader>("DF_RuntimeUnlit");
                if(unlit==null)unlit=Shader.Find("Universal Render Pipeline/Unlit");
                return unlit;
            }
        }

        public static Shader GroundTelegraph
        {
            get
            {
                if(groundTelegraph==null)groundTelegraph=Resources.Load<Shader>("DF_RuntimeGroundTelegraph");
                return groundTelegraph;
            }
        }

        public static Material Create(Color color,bool useUnlit=false,float metallic=0f,float smoothness=.2f,Color emission=default)
        {
            Shader shader=useUnlit?Unlit:Lit;
            if(shader==null)return null;

            Material material=new(shader);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            else material.color=color;
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",Mathf.Clamp01(metallic));
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",Mathf.Clamp01(smoothness));
            if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",emission);
            return material;
        }

        public static Material CreateGroundTelegraph(Color color,float fillAlpha,float ringAlpha,float ringWidth)
        {
            Shader shader=GroundTelegraph;
            if(shader==null)return null;
            Material material=new(shader);
            material.SetColor("_BaseColor",color);
            material.SetFloat("_FillAlpha",Mathf.Clamp01(fillAlpha));
            material.SetFloat("_RingAlpha",Mathf.Clamp01(ringAlpha));
            material.SetFloat("_RingWidth",Mathf.Clamp(ringWidth,.005f,.25f));
            return material;
        }

        // Readable call form for visual builders that think in color/emission/material response order.
        public static Material Create(Color color,bool useUnlit,Color emission,float metallic,float smoothness)
            =>Create(color,useUnlit,metallic,smoothness,emission);
    }
}
