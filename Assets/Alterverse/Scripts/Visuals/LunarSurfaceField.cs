using UnityEngine;
using Alterverse.Match;
namespace Alterverse.Visuals
{
    /// <summary>Deterministic visual geology shared by every arena variant. No physics, navigation or global random state.</summary>
    public static class LunarSurfaceField
    {
        // Shared impact field for ALL maps. Every arena variant uses these same crater positions and
        // relief before its own surface palette is applied, so Moon, Mars, Green Planet, Noxara and Kryon
        // all retain visible impact geology without changing gameplay collision/pathing.
        private static readonly Vector3[] craters={
            new(-18,1.65f,5),new(-11,1.1f,-7),new(17,1.45f,-6),new(10,.9f,7),
            new(-8,2.1f,29),new(9,1.8f,-29),new(-34,1.55f,27),new(34,1.65f,-27),
            new(-2,1.25f,31),new(3,1.05f,-31),new(-36,.48f,12),new(-34,.64f,-7),
            new(-25,.8f,5),new(-15,.96f,31),new(-4,.48f,-31),new(35,.64f,-11),
            new(33,.8f,8),new(24,.96f,-5),new(15,.48f,-31),new(5,.64f,31),
            new(-23,5.3f,27),new(22,4.8f,-27),new(-24,1.25f,-8),new(-22.5f,.55f,-7.2f),
            new(-16,.85f,7),new(24,1.5f,8),new(25.2f,.6f,7.3f),new(17,1.1f,-7),
            new(-29,1.8f,26),new(-27.5f,.7f,27),new(-15,1.3f,-27),new(28,1.65f,-26),
            new(16,1.2f,28),new(17.2f,.55f,27.4f),

            // Broad arena coverage mixes shallow small impacts with larger edge-field basins.
            new(-39,1.55f,20),new(-38,.95f,-18),new(-31,1.45f,17),new(-30,.86f,-19),
            new(-27,1.25f,12),new(-26,1.4f,-15),new(-21,.9f,19),new(-20,1.3f,-20),
            new(-14,1.15f,13),new(-13,1.3f,-15),new(-8,.95f,18),new(-7,1.2f,-20),
            new(-2,1.2f,12),new(-1,1.4f,-13),new(5,.95f,19),new(6,1.25f,-18),
            new(11,1.1f,12),new(12,1.3f,-14),new(18,.9f,19),new(19,1.25f,-21),
            new(26,1.25f,13),new(27,1.45f,-16),new(32,.95f,18),new(33,1.2f,-19),
            new(38,1.5f,22),new(39,.9f,-21),

            // Larger isolated basins add landmarks in the outer lanes; neighboring craters overlap lightly.
            new(-35,.44f,4),new(-32,.58f,-25),new(-23,.5f,22),new(-18,.46f,-12),
            new(-36,3.1f,-30),new(36,3.25f,30),new(-32,2.55f,-2),new(32,2.7f,2),
            new(-5,2.75f,34),new(7,2.9f,-34),
            new(-12,.54f,24),new(-6,.42f,-25),new(1,.48f,23),new(7,.56f,-26),
            new(14,.46f,23),new(21,.54f,-24),new(29,.48f,22),new(35,.58f,-4),
            new(-28,.42f,-2),new(-20,.5f,2),new(20,.5f,-2),new(28,.42f,2),

            // Central micro impacts keep the objective court geological without obscuring it.
            new(-9,.46f,2),new(-7,.58f,-3),new(-3,.42f,6),new(2,.5f,-5),
            new(6,.44f,3),new(8,.6f,-2),new(-1,.38f,9),new(3,.42f,-9)
        };
        private static float Noise(float x,float z,float f,float seed)=>Mathf.PerlinNoise(x*f+seed,z*f+seed*.71f);
        // Kept only as a compatibility hook for callers from older content. The current game has
        // no lane corridors, so terrain receives no lane-specific flattening or tint.
        public static float LaneWeight(float x,float z)=>0f;
        public static void Sample(float x,float z,out float relief,out float tone,out float dust)
        {
            float macro=Noise(x,z,.055f,12),medium=Noise(x,z,.24f,31),fine=Noise(x,z,1.9f,7);
            relief=(macro-.5f)*.19f+(medium-.5f)*.085f;
            tone=.48f+(macro-.5f)*.2f+(medium-.5f)*.075f+(fine-.5f)*.018f;
            dust=0f;
            float craterWear=0f;
            // This crater pass is deliberately variant-agnostic: every map receives the same bowl/rim
            // structure, while the later Bake palette makes it read as rock, rust, alien mineral or ice.
            for(int i=0;i<craters.Length;i++)
            {
                Vector3 c=craters[i];float dx=x-c.x,dz=z-c.z,r=c.y;
                if(Mathf.Abs(dx)>r*1.6f||Mathf.Abs(dz)>r*1.6f)continue;
                float angle=Mathf.Atan2(dz,dx);
                float wobble=1+.08f*Mathf.Sin(angle*(3+i%3)+i*.71f)+.035f*Mathf.Sin(angle*7+i);
                float q=Mathf.Sqrt(dx*dx+dz*dz)/(r*wobble);
                if(q>1.6f)continue;
                float bowl=Mathf.Clamp01(1-q*q),rim=Mathf.Exp(-Mathf.Pow((q-.96f)/.14f,2));
                float eroded=.58f+.42f*Noise(x,z,.8f,19+i);
                float basinScale=Mathf.Lerp(.72f,1.2f,Mathf.InverseLerp(.4f,5.5f,r));
                relief+=(-bowl*.205f+rim*.102f)*eroded*basinScale;
                float shadow=bowl*(.07f+.075f*Mathf.Clamp01((dx-dz)/r+.5f));
                tone+=rim*.075f*eroded-shadow*.95f;
                craterWear=Mathf.Max(craterWear,Mathf.Clamp01(bowl*.78f+rim*.42f));
                if(q>1&&q<1.6f)tone+=.014f*(1-(q-1)/.6f)*Mathf.Max(0,Mathf.Sin(angle*11+i)*.5f+.5f);
            }
            // A subtle shared darkening keeps craters legible even on the brighter Kryon/Green palettes.
            tone-=craterWear*.026f;
            dust=Mathf.Max(dust,craterWear*.28f);
            float radial=Mathf.Sqrt(x*x+z*z),a0=Mathf.Atan2(z,x);
            float frontRing=Mathf.Exp(-Mathf.Pow((radial-(9.2f+.26f*Mathf.Sin(a0*5)))/.26f,2));
            float broken=Mathf.Clamp01(.35f+Mathf.Sin(a0*3+.8f));
            tone-=frontRing*.045f*broken;
            relief-=frontRing*.025f*broken;
            relief=Mathf.Clamp(relief,-.245f,.115f);tone=Mathf.Clamp(tone,.28f,.66f);
        }
        public static Vector3 Surface(float x,float z)
        {
            float wx=Mathf.Pow(Mathf.Abs(x)/42f,14),wz=Mathf.Pow(Mathf.Abs(z)/36f,14);
            x+=Mathf.Sign(x)*wx*(.5f+Noise(x,z,.36f,81)*2.2f);
            z+=Mathf.Sign(z)*wz*(.4f+Noise(x,z,.29f,63)*2.1f);
            Sample(x,z,out float relief,out _,out _);return new Vector3(x,.015f+relief,z);
        }
        public static void Bake(out Texture2D albedo,out Texture2D normals)
        {
            const int size=512;const float spanX=96,spanZ=84;
            Color32[] pixels=new Color32[size*size],normalPixels=new Color32[pixels.Length];float[] heights=new float[pixels.Length];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                Sample(-48+spanX*x/(size-1),-42+spanZ*z/(size-1),out float h,out float tone,out float dust);
                int i=z*size+x;heights[i]=h;
                // The shared crater wear slightly lowers local value on every variant, then each map
                // applies its own palette so the crater remains native to that world rather than pasted on.
                float value=Mathf.Lerp(.17f,.40f,Mathf.InverseLerp(.28f,.66f,tone));
                value*=1f-dust*.055f;
                if(ArenaVariantRuntime.IsMars)
                {
                    // Iron-rich, dusty Mars palette: muted rust rather than saturated comic red.
                    float warm=Mathf.Lerp(.88f,1.08f,Mathf.InverseLerp(.28f,.66f,tone));
                    pixels[i]=new Color(value*1.28f*warm,value*.58f,value*.39f,1);
                }
                else if(ArenaVariantRuntime.IsGreenPlanet)
                {
                    // Mineral alien regolith: olive/moss/grey-green.
                    float mineral=Mathf.Lerp(.88f,1.10f,Mathf.InverseLerp(.28f,.66f,tone));
                    pixels[i]=new Color(value*.62f,value*1.03f*mineral,value*.54f,1);
                }
                else if(ArenaVariantRuntime.IsNoxara)
                {
                    // Noxara reuses the exact shared relief while shifting the regolith into a
                    // deep cobalt/navy range. Keep values restrained so heroes remain readable.
                    float cobalt=Mathf.Lerp(.92f,1.10f,Mathf.InverseLerp(.28f,.66f,tone));
                    pixels[i]=new Color(value*.30f,value*.46f*cobalt,value*1.18f,1);
                }
                else if(ArenaVariantRuntime.IsKryon)
                {
                    // Kryon uses the same crater geometry but reads as compacted ice and frozen dust.
                    float frost=Mathf.Lerp(.48f,.80f,Mathf.InverseLerp(.28f,.66f,tone));
                    frost*=1f-dust*.025f;
                    pixels[i]=new Color(frost*.88f,frost*.97f,Mathf.Min(1f,frost*1.08f),1);
                }
                else pixels[i]=new Color(value*.94f,value*.98f,value*1.055f,1);
            }
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float dx=(heights[z*size+Mathf.Min(size-1,x+1)]-heights[z*size+Mathf.Max(0,x-1)])/(2*spanX/(size-1));
                float dz=(heights[Mathf.Min(size-1,z+1)*size+x]-heights[Mathf.Max(0,z-1)*size+x])/(2*spanZ/(size-1));
                Vector3 n=new Vector3(-dx*1.8f,-dz*1.8f,1).normalized;normalPixels[z*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            albedo=new Texture2D(size,size,TextureFormat.RGBA32,true,false){name="DF_UniqueRegolithAlbedo",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
            normals=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="DF_UniqueRegolithNormal",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
            albedo.SetPixels32(pixels);albedo.Apply(true,true);normals.SetPixels32(normalPixels);normals.Apply(true,true);
        }
    }
}
