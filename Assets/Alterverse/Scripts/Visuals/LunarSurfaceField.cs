using UnityEngine;
using Alterverse.Match;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Unified presentation-only dune surface shared by every arena variant.
    /// The terrain shape and wind ripples are identical in style on every planet while each arena
    /// keeps the exact color family already defined for Mars, Green Planet, Noxara, Kryon and Moon.
    /// Gameplay collision/navigation are not modified here.
    /// </summary>
    public static class LunarSurfaceField
    {
        private static bool IsMoon=>!ArenaVariantRuntime.IsMars&&!ArenaVariantRuntime.IsGreenPlanet&&
                                   !ArenaVariantRuntime.IsNoxara&&!ArenaVariantRuntime.IsKryon;

        // Kept for compatibility with older arena callers. The current game has no lane-specific tint/flattening.
        public static float LaneWeight(float x,float z)=>0f;

        /// <summary>
        /// Creates broad flowing dunes plus fine wind ripples. The functions are deliberately smooth and
        /// directional instead of noisy so the surface reads as sand rather than grain/pixel noise.
        /// </summary>
        private static void DuneField(float x,float z,out float macroHeight,out float rippleHeight,out float shade)
        {
            // Rotate the dune flow about 18 degrees so the ridges never look aligned to the arena grid.
            const float cos=.9510565f;
            const float sin=.3090170f;
            float along=x*cos+z*sin;
            float across=-x*sin+z*cos;

            // Slow lateral drift bends the dune crests across long distances like a wind-shaped dune sea.
            float drift=
                Mathf.Sin(along*.055f)*2.35f+
                Mathf.Sin(along*.021f+1.32f)*4.75f+
                Mathf.Sin(along*.0105f-1.10f)*3.10f;

            float p=across+drift;

            // Three broad wavelengths produce large readable dune bodies without random speckle.
            float a=Mathf.Sin(p*.205f);
            float b=Mathf.Sin(p*.104f+along*.014f+1.65f);
            float c=Mathf.Sin(p*.052f-along*.008f-.72f);
            float broad=a*.50f+b*.34f+c*.16f;

            // Slightly sharpen crests while keeping valleys soft and traversable-looking.
            float shaped=Mathf.Sign(broad)*Mathf.Pow(Mathf.Abs(broad),1.28f);
            macroHeight=shaped*.66f;

            // Fine wind-carved ripples follow the same flowing direction and bend with the large dunes.
            float rippleWarp=
                Mathf.Sin(along*.155f+.8f)*.60f+
                Mathf.Sin(along*.061f-1.2f)*1.05f+
                Mathf.Sin(along*.028f+2.4f)*1.35f;
            float phase=p*2.85f+rippleWarp;
            float r1=Mathf.Sin(phase);
            float r2=Mathf.Sin(phase*2.03f+.62f);
            float r3=Mathf.Sin(phase*3.91f+1.37f);
            float ripple=r1*.72f+r2*.20f+r3*.08f;

            // Ripples remain present in valleys but become a little clearer on dune faces/crests.
            float faceMask=Mathf.Lerp(.62f,1f,Mathf.Clamp01(Mathf.Abs(broad)));
            rippleHeight=ripple*.038f*faceMask;

            // Neutral light/dark information only. Planet hue is applied later and never replaced by sand-yellow.
            shade=Mathf.Clamp(shaped*.72f+ripple*.43f*faceMask,-1f,1f);
        }

        public static void Sample(float x,float z,out float relief,out float tone,out float dust)
        {
            DuneField(x,z,out float macroHeight,out _,out float duneShade);

            // A very broad deterministic undulation keeps large areas natural without introducing Perlin grain.
            float broadUndulation=
                Mathf.Sin(x*.027f+z*.018f+1.1f)*.035f+
                Mathf.Sin(x*.012f-z*.023f-1.7f)*.025f;

            relief=macroHeight+broadUndulation;
            tone=.49f+broadUndulation*.55f+duneShade*.070f;
            dust=0f;

            // Keep presentation relief inside the same safe order of magnitude as the former crater terrain.
            relief=Mathf.Clamp(relief,-.72f,.72f);
            if(ArenaVariantRuntime.IsGreenPlanet)tone=Mathf.Clamp(tone,.34f,.68f);
            else if(IsMoon)tone=Mathf.Clamp(tone,.22f,.74f);
            else tone=Mathf.Clamp(tone,.30f,.68f);
        }

        public static Vector3 Surface(float x,float z)
        {
            // Preserve only the established irregular arena boundary. Interior terrain is now dune-driven.
            float wx=Mathf.Pow(Mathf.Abs(x)/42f,14);
            float wz=Mathf.Pow(Mathf.Abs(z)/36f,14);
            x+=Mathf.Sign(x)*wx*(.70f+Mathf.Sin(z*.31f+1.2f)*.34f);
            z+=Mathf.Sign(z)*wz*(.62f+Mathf.Sin(x*.27f-1.6f)*.31f);

            Sample(x,z,out float relief,out _,out _);
            return new Vector3(x,.015f+relief,z);
        }

        private static float BilinearHeight(float[] source,int sourceSize,float u,float v)
        {
            float fx=Mathf.Clamp01(u)*(sourceSize-1);
            float fz=Mathf.Clamp01(v)*(sourceSize-1);
            int x0=Mathf.FloorToInt(fx),z0=Mathf.FloorToInt(fz);
            int x1=Mathf.Min(sourceSize-1,x0+1),z1=Mathf.Min(sourceSize-1,z0+1);
            float tx=fx-x0,tz=fz-z0;
            float a=Mathf.Lerp(source[z0*sourceSize+x0],source[z0*sourceSize+x1],tx);
            float b=Mathf.Lerp(source[z1*sourceSize+x0],source[z1*sourceSize+x1],tx);
            return Mathf.Lerp(a,b,tz);
        }

        private static Color BilinearColor(Color32[] source,int sourceSize,float u,float v)
        {
            float fx=Mathf.Clamp01(u)*(sourceSize-1);
            float fz=Mathf.Clamp01(v)*(sourceSize-1);
            int x0=Mathf.FloorToInt(fx),z0=Mathf.FloorToInt(fz);
            int x1=Mathf.Min(sourceSize-1,x0+1),z1=Mathf.Min(sourceSize-1,z0+1);
            float tx=fx-x0,tz=fz-z0;
            Color a=Color.Lerp(source[z0*sourceSize+x0],source[z0*sourceSize+x1],tx);
            Color b=Color.Lerp(source[z1*sourceSize+x0],source[z1*sourceSize+x1],tx);
            return Color.Lerp(a,b,tz);
        }

        public static void Bake(out Texture2D albedo,out Texture2D normals)
        {
            // 2048 final maps give the wind ripples enough texel density for the normal gameplay camera.
            // Broad color/height is sampled once at 512 and smoothly reconstructed to keep match loading sane.
            const int baseSize=512;
            const int size=2048;
            const float spanX=96f,spanZ=84f;
            bool moon=IsMoon;

            Color32[] basePixels=new Color32[baseSize*baseSize];
            float[] baseHeights=new float[basePixels.Length];

            for(int z=0;z<baseSize;z++)for(int x=0;x<baseSize;x++)
            {
                float wx=-48f+spanX*x/(baseSize-1);
                float wz=-42f+spanZ*z/(baseSize-1);
                Sample(wx,wz,out float h,out float tone,out float dust);
                int i=z*baseSize+x;
                baseHeights[i]=h;

                // Preserve the existing arena brightness ranges and color formulas.
                float value=moon
                    ?Mathf.Lerp(.20f,.62f,Mathf.InverseLerp(.22f,.74f,tone))
                    :ArenaVariantRuntime.IsGreenPlanet
                        ?Mathf.Lerp(.245f,.49f,Mathf.InverseLerp(.34f,.68f,tone))
                        :Mathf.Lerp(.19f,.43f,Mathf.InverseLerp(.30f,.68f,tone));

                if(ArenaVariantRuntime.IsGreenPlanet)value=Mathf.Max(value,.235f);
                value=Mathf.Clamp01(value);

                if(ArenaVariantRuntime.IsMars)
                {
                    float warm=Mathf.Lerp(.88f,1.08f,Mathf.InverseLerp(.30f,.68f,tone));
                    basePixels[i]=new Color(value*1.28f*warm,value*.58f,value*.39f,1f);
                }
                else if(ArenaVariantRuntime.IsGreenPlanet)
                {
                    float mineral=Mathf.Lerp(.94f,1.05f,Mathf.InverseLerp(.34f,.68f,tone));
                    basePixels[i]=new Color(value*.74f,value*.95f*mineral,value*.66f,1f);
                }
                else if(ArenaVariantRuntime.IsNoxara)
                {
                    float cobalt=Mathf.Lerp(.92f,1.10f,Mathf.InverseLerp(.30f,.68f,tone));
                    basePixels[i]=new Color(value*.30f,value*.46f*cobalt,value*1.18f,1f);
                }
                else if(ArenaVariantRuntime.IsKryon)
                {
                    float frost=Mathf.Lerp(.48f,.80f,Mathf.InverseLerp(.30f,.68f,tone));
                    basePixels[i]=new Color(frost*.88f,frost*.97f,Mathf.Min(1f,frost*1.08f),1f);
                }
                else
                {
                    basePixels[i]=new Color(value*.94f,value*.965f,value,1f);
                }
            }

            Color32[] pixels=new Color32[size*size];
            Color32[] normalPixels=new Color32[pixels.Length];
            float[] heights=new float[pixels.Length];

            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float u=x/(float)(size-1);
                float v=z/(float)(size-1);
                float wx=-48f+spanX*u;
                float wz=-42f+spanZ*v;

                DuneField(wx,wz,out _,out float rippleHeight,out float duneShade);

                int i=z*size+x;
                heights[i]=BilinearHeight(baseHeights,baseSize,u,v)+rippleHeight;

                Color c=BilinearColor(basePixels,baseSize,u,v);
                // Exactly the same neutral multiplier is applied to R/G/B, so hue remains the planet's own hue.
                float neutral=Mathf.Clamp(1f+duneShade*.125f,.85f,1.15f);
                c.r=Mathf.Clamp01(c.r*neutral);
                c.g=Mathf.Clamp01(c.g*neutral);
                c.b=Mathf.Clamp01(c.b*neutral);
                c.a=1f;
                pixels[i]=c;
            }

            // Strong tangent-space response makes the narrow wind ripples read clearly without adding geometry noise.
            const float normalStrength=6.2f;
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float dx=(heights[z*size+Mathf.Min(size-1,x+1)]-heights[z*size+Mathf.Max(0,x-1)])/(2f*spanX/(size-1));
                float dz=(heights[Mathf.Min(size-1,z+1)*size+x]-heights[Mathf.Max(0,z-1)*size+x])/(2f*spanZ/(size-1));
                Vector3 n=new Vector3(-dx*normalStrength,-dz*normalStrength,1f).normalized;
                normalPixels[z*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1f);
            }

            albedo=new Texture2D(size,size,TextureFormat.RGBA32,true,false)
            {
                name="DF_HD_DuneAlbedo",
                wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Trilinear,
                anisoLevel=16
            };
            normals=new Texture2D(size,size,TextureFormat.RGBA32,true,true)
            {
                name="DF_HD_DuneNormal",
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
