using UnityEngine;
using Alterverse.Match;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Clean high-resolution presentation surface shared by every arena variant.
    /// No dune bands, no wind-ripple striping and no high-frequency grain are generated here.
    /// Each planet keeps its established color family from the arena code.
    /// Gameplay collision/navigation are unchanged.
    /// </summary>
    public static class LunarSurfaceField
    {
        private static bool IsMoon=>!ArenaVariantRuntime.IsMars&&!ArenaVariantRuntime.IsGreenPlanet&&
                                   !ArenaVariantRuntime.IsNoxara&&!ArenaVariantRuntime.IsKryon;

        public static float LaneWeight(float x,float z)=>0f;

        /// <summary>
        /// Very restrained broad relief only. This avoids the previous visible dune/ripple bands while
        /// preventing the ground from looking like a perfectly synthetic flat card.
        /// </summary>
        public static void Sample(float x,float z,out float relief,out float tone,out float dust)
        {
            float broadA=Mathf.Sin(x*.031f+z*.017f+1.1f);
            float broadB=Mathf.Sin(x*.014f-z*.026f-1.7f);
            float broadC=Mathf.Sin((x+z)*.0085f+2.3f);

            relief=broadA*.022f+broadB*.016f+broadC*.010f;
            tone=.50f+broadA*.018f+broadB*.012f+broadC*.008f;
            dust=0f;

            if(ArenaVariantRuntime.IsGreenPlanet)tone=Mathf.Clamp(tone,.40f,.60f);
            else if(IsMoon)tone=Mathf.Clamp(tone,.40f,.62f);
            else tone=Mathf.Clamp(tone,.40f,.60f);
        }

        public static Vector3 Surface(float x,float z)
        {
            // Preserve the established broken arena perimeter while keeping the playable interior clean.
            float wx=Mathf.Pow(Mathf.Abs(x)/42f,14);
            float wz=Mathf.Pow(Mathf.Abs(z)/36f,14);
            x+=Mathf.Sign(x)*wx*(.70f+Mathf.Sin(z*.31f+1.2f)*.34f);
            z+=Mathf.Sign(z)*wz*(.62f+Mathf.Sin(x*.27f-1.6f)*.31f);

            Sample(x,z,out float relief,out _,out _);
            return new Vector3(x,.015f+relief,z);
        }

        public static void Bake(out Texture2D albedo,out Texture2D normals)
        {
            // 2048 is the practical high-quality ceiling for two runtime RGBA maps on Android.
            // Sampling is performed directly at final resolution: no 512 -> 2048 upscaling and no dune detail pass.
            const int size=2048;
            const float spanX=96f,spanZ=84f;
            bool moon=IsMoon;

            Color32[] pixels=new Color32[size*size];
            Color32[] normalPixels=new Color32[pixels.Length];
            float[] heights=new float[pixels.Length];

            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float wx=-48f+spanX*x/(size-1);
                float wz=-42f+spanZ*z/(size-1);
                Sample(wx,wz,out float h,out float tone,out _);

                int i=z*size+x;
                heights[i]=h;

                // Smooth, narrow brightness range for a clean surface with no speckle or stripe pattern.
                float value=moon
                    ?Mathf.Lerp(.36f,.52f,Mathf.InverseLerp(.40f,.62f,tone))
                    :ArenaVariantRuntime.IsGreenPlanet
                        ?Mathf.Lerp(.34f,.48f,Mathf.InverseLerp(.40f,.60f,tone))
                        :Mathf.Lerp(.32f,.46f,Mathf.InverseLerp(.40f,.60f,tone));
                value=Mathf.Clamp01(value);

                // Preserve the existing planet color families from the code.
                if(ArenaVariantRuntime.IsMars)
                {
                    float warm=Mathf.Lerp(.94f,1.04f,Mathf.InverseLerp(.40f,.60f,tone));
                    pixels[i]=new Color(value*1.28f*warm,value*.58f,value*.39f,1f);
                }
                else if(ArenaVariantRuntime.IsGreenPlanet)
                {
                    float mineral=Mathf.Lerp(.97f,1.03f,Mathf.InverseLerp(.40f,.60f,tone));
                    pixels[i]=new Color(value*.74f,value*.95f*mineral,value*.66f,1f);
                }
                else if(ArenaVariantRuntime.IsNoxara)
                {
                    float cobalt=Mathf.Lerp(.96f,1.04f,Mathf.InverseLerp(.40f,.60f,tone));
                    pixels[i]=new Color(value*.30f,value*.46f*cobalt,value*1.18f,1f);
                }
                else if(ArenaVariantRuntime.IsKryon)
                {
                    float frost=Mathf.Lerp(.58f,.72f,Mathf.InverseLerp(.40f,.60f,tone));
                    pixels[i]=new Color(frost*.88f,frost*.97f,Mathf.Min(1f,frost*1.08f),1f);
                }
                else
                {
                    pixels[i]=new Color(value*.94f,value*.965f,value,1f);
                }
            }

            // Keep the normal map extremely restrained so lighting remains smooth and clean.
            const float normalStrength=1.15f;
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float dx=(heights[z*size+Mathf.Min(size-1,x+1)]-heights[z*size+Mathf.Max(0,x-1)])/(2f*spanX/(size-1));
                float dz=(heights[Mathf.Min(size-1,z+1)*size+x]-heights[Mathf.Max(0,z-1)*size+x])/(2f*spanZ/(size-1));
                Vector3 n=new Vector3(-dx*normalStrength,-dz*normalStrength,1f).normalized;
                normalPixels[z*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1f);
            }

            albedo=new Texture2D(size,size,TextureFormat.RGBA32,true,false)
            {
                name="DF_CleanGroundAlbedo_2K",
                wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Trilinear,
                anisoLevel=16
            };
            normals=new Texture2D(size,size,TextureFormat.RGBA32,true,true)
            {
                name="DF_CleanGroundNormal_2K",
                wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Trilinear,
                anisoLevel=16
            };

            albedo.SetPixels32(pixels);
            albedo.Apply(true,true);
            normals.SetPixels32(normalPixels);
            normals.Apply(true,true);
        }
    }
}
