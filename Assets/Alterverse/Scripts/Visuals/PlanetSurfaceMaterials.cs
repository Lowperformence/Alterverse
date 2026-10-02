using UnityEngine;
using Alterverse.Match;

namespace Alterverse.Visuals
{
    /// <summary>Owned by the live arena. Packed linear data, never a replacement planet palette.</summary>
    public sealed class PlanetSurfaceMaterials
    {
        readonly Shader shader;
        readonly Texture2D sand, stone;
        readonly bool ownsSand,ownsStone;
        readonly Texture macro;
        readonly Texture2D geology;
        public PlanetSurfaceMaterials(Texture macro)
        {
            this.macro=macro;
            shader=Resources.Load<Shader>("DF_PlanetSurface");
            if(shader==null||!shader.isSupported)
            {
                Debug.LogError("DIMENSIONS FRONT: DF_PlanetSurface shader missing or unsupported; retaining existing materials.");
                shader=null;
                return;
            }
            string profile=ArenaVariantRuntime.Current.ToString();
            geology=Resources.Load<Texture2D>("PlanetSurfaces/"+profile+"/Macro");
            sand=Resources.Load<Texture2D>("PlanetSurfaces/"+profile+"/Ground");
            stone=Resources.Load<Texture2D>("PlanetSurfaces/"+profile+"/Stone");
            ownsSand=sand==null;ownsStone=stone==null;
            if(ownsSand||ownsStone)Debug.LogWarning("DIMENSIONS FRONT: authored surface detail missing for "+profile+"; using generated detail.");
            if(ownsSand)sand=Bake(false);
            if(ownsStone)stone=Bake(true);
        }

        public void Apply(Material material,bool ground=false,bool crater=false)
        {
            if(material==null||shader==null)return;
            // Material identity, original tint, metallic and smoothness remain intact.
            material.shader=shader;
            material.enableInstancing=true;
            material.SetTexture("_SandData",sand);
            material.SetTexture("_StoneData",stone);
            material.SetTexture("_GroundMap",macro);
            material.SetTexture("_MacroData",geology!=null?geology:Texture2D.grayTexture);
            material.SetFloat("_MacroScale",.047f);
            material.SetFloat("_MacroStrength",ArenaVariantRuntime.IsDiamondCore?.34f:ArenaVariantRuntime.IsKryon?.40f:.52f);
            material.SetFloat("_SurfaceMode",ground?0f:crater?1f:2f);
            material.SetFloat("_DetailScale",ArenaVariantRuntime.IsDiamondCore?.18f:ArenaVariantRuntime.IsKryon?.16f:ArenaVariantRuntime.IsMars?.34f:ArenaVariantRuntime.IsGJ504b?.21f:.27f); // Finer world-space detail, shared by ground, craters and cliffs.
            material.SetFloat("_SandStrength",ArenaVariantRuntime.IsGJ504b?.46f:ArenaVariantRuntime.IsMars?.33f:
                ArenaVariantRuntime.IsKryon?.13f:ArenaVariantRuntime.IsGreenPlanet?.20f:
                ArenaVariantRuntime.IsEarth?.12f:ArenaVariantRuntime.IsPyraxis?.08f:ArenaVariantRuntime.IsNeptune?.10f:.27f);
            material.SetFloat("_RockStrength",ArenaVariantRuntime.IsGJ504b?.42f:ArenaVariantRuntime.IsPyraxis?.48f:ArenaVariantRuntime.IsNeptune?.40f:.34f);
            material.SetFloat("_BumpScale",ground?(ArenaVariantRuntime.IsEarth?.34f:ArenaVariantRuntime.IsNeptune?.38f:.44f):0f);
        }

        static Texture2D Bake(bool rock)
        {
            // A pair of 256-square RGBA8 maps including mip chains costs ~0.67 MiB total.
            // Numerical height derivatives use ordinary linear RGBA, NOT Unity normal-map packing.
            // Runtime data has no TextureImporter; mipmaps and CPU-copy release are explicit.
            const int size=256;
            var heights=new float[size*size];
            var pixels=new Color32[heights.Length];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=z/(float)size;
                float a=u*Mathf.PI*2,b=v*Mathf.PI*2;
                float noise=Periodic(u,v,rock?17:5);
                float warp=.72f*Mathf.Sin(b*2+.55f*Mathf.Sin(a))+.36f*Mathf.Sin(a*2-b*3);
                float ripple=Mathf.Sin(a*9+b*2+warp*2.4f);
                // Warped crest families and a second scale avoid straight manufactured stripes.
                float ripples=.5f*ripple+.20f*Mathf.Sin(a*17+b*4+warp*4.3f);
                float fracture=1f-Mathf.Clamp01(Mathf.Abs(noise)*12f);
                heights[z*size+x]=rock?noise*.30f-fracture*.065f:
                    ripples*(.045f+.035f*(.5f+.5f*Mathf.Sin(a-b)))+noise*.018f;
            }
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                int i=z*size+x;
                float dx=(heights[z*size+(x+1)%size]-heights[z*size+(x+size-1)%size])*size*.5f;
                float dz=(heights[((z+1)%size)*size+x]-heights[((z+size-1)%size)*size+x])*size*.5f;
                float h=heights[i];
                // Gradients normalized to a bounded slope; B roughness, A centered neutral value.
                float encodingScale=rock?.045f:.10f;
                pixels[i]=new Color(.5f+Mathf.Clamp(dx*encodingScale,-.49f,.49f),
                    .5f+Mathf.Clamp(dz*encodingScale,-.49f,.49f),
                    Mathf.Clamp01((rock?.72f:.90f)+h*.75f),Mathf.Clamp01(.5f+h*(rock?1.8f:2.5f)));
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true)
            {name=rock?"DF_PackedStoneDetail":"DF_PackedSandRipples",wrapMode=TextureWrapMode.Repeat,
                filterMode=FilterMode.Trilinear,anisoLevel=8};
            texture.SetPixels32(pixels);texture.Apply(true,true);
            return texture;
        }

        static float Periodic(float u,float v,int seed)
        {
            // Integer Fourier frequencies guarantee wrap-continuity in both axes.
            float sum=0f;
            for(int i=0;i<7;i++)
            {
                int x=1+(seed+i*7)%19,z=1+(seed*3+i*11)%17;
                sum+=Mathf.Sin((u*x+v*z)*Mathf.PI*2+i*2.17f)/(3f+i);
            }
            return sum;
        }

        public void Dispose()
        {
            if(ownsSand&&sand!=null)Object.Destroy(sand);
            if(ownsStone&&stone!=null)Object.Destroy(stone);
        }
    }
}
