using UnityEngine;
using Alterverse.Match;
namespace Alterverse.Visuals
{
    /// <summary>Deterministic visual geology shared by every arena variant. No physics, navigation or global random state.</summary>
    public static class LunarSurfaceField
    {
        // Shared impact anchors. The Moon uses a stronger crater profile while the other arena
        // variants keep the same placements with their existing restrained relief.
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
            new(6,.44f,3),new(8,.6f,-2),new(-1,.38f,9),new(3,.42f,-9),

            // Large reference-image basins. These are intentionally much larger than the previous dents
            // and sit away from bases / the exact objective center so they read from the gameplay camera.
            new(-22.5f,7.8f,24.0f),new(23.5f,7.3f,-24.5f),
            new(-31.0f,6.1f,-17.0f),new(30.0f,6.4f,18.5f),
            new(-10.0f,5.5f,-28.0f),new(11.0f,5.2f,28.0f),
            new(-17.5f,4.7f,11.5f),new(18.0f,4.5f,-10.5f),
            new(-2.5f,4.0f,21.5f),new(3.5f,4.2f,-21.0f),

            // Reference-inspired crater families. These cluster around major outer impacts instead of
            // being evenly distributed, producing overlapping bowls and secondary impacts like the photo.
            new(-27,2.7f,30),new(-31,1.15f,31.5f),new(-25,.72f,32.4f),
            new(27,2.55f,-30),new(31,1.05f,-31.3f),new(24.7f,.66f,-32.2f),
            new(-37,2.25f,8),new(-39.2f,.82f,10.1f),new(-34.7f,.62f,9.9f),
            new(37,2.15f,-8),new(39.1f,.76f,-10.2f),new(34.8f,.58f,-9.7f),
            new(-18,2.35f,-30),new(-20.4f,.86f,-32),new(-15.7f,.62f,-31.8f),
            new(18,2.25f,30),new(20.1f,.82f,31.7f),new(15.8f,.58f,32.1f)
        };

        private static bool IsMoon=>ArenaVariantRuntime.IsMoon||ArenaVariantRuntime.IsExoplanet;
        private static float Noise(float x,float z,float f,float seed)=>Mathf.PerlinNoise(x*f+seed,z*f+seed*.71f);

        // Kept only as a compatibility hook for callers from older content. The current game has
        // no lane corridors, so terrain receives no lane-specific flattening or tint.
        public static float LaneWeight(float x,float z)=>0f;

        public static void Sample(float x,float z,out float relief,out float tone,out float dust)
        {
            if(FrontierPlanetLayout.Active)
            {
                FrontierPlanetLayout.Sample(x,z,out relief,out tone,out dust);return;
            }
            bool moon=IsMoon;
            bool mars=ArenaVariantRuntime.IsMars||ArenaVariantRuntime.IsGJ504b;
            bool earth=ArenaVariantRuntime.IsEarth;

            // Earth deliberately uses very broad, low-amplitude undulation. It must read as natural
            // ground rather than the dune/crater relief used by the planetary arenas.
            float macro=Noise(x,z,mars?.032f:earth?.024f:.055f,12);
            float medium=Noise(x,z,mars?.105f:earth?.075f:.24f,31);
            float fine=Noise(x,z,mars?.62f:earth?.45f:1.9f,7);
            if(earth)
            {
                relief=(macro-.5f)*.045f+(medium-.5f)*.012f;
            }
            else
            {
                relief=(macro-.5f)*(mars?.27f:moon?.20f:.19f)
                      +(medium-.5f)*(mars?.055f:moon?.09f:.085f);
                if(mars)
                {
                    float dunePhase=z*.30f+x*.075f+(macro-.5f)*2.2f;
                    relief+=Mathf.Sin(dunePhase)*(.075f+.035f*macro);
                }
            }

            tone=earth
                ?.50f+(macro-.5f)*.10f+(medium-.5f)*.028f+(fine-.5f)*.006f
                :.48f+(macro-.5f)*.22f+(medium-.5f)*(mars?.055f:.085f)
                 +(fine-.5f)*(mars?.012f:moon?.040f:.018f);
            dust=0f;
            float craterWear=0f;

            // Earth intentionally skips the shared crater field so the map remains smooth.
            float outer=Mathf.Clamp01(Mathf.Max((Mathf.Abs(x)-20f)/18f,(Mathf.Abs(z)-15f)/18f));
            if(!earth)
            {
                // The Moon gets flatter crater floors, steeper walls and brighter broken rims. Actual gameplay
                // collision remains unchanged: this is presentation geometry only. Relief near the active centre
                // is deliberately restrained while outer landmark craters can read much deeper from the MOBA camera.
                for(int i=0;i<craters.Length;i++)
                {
                    Vector3 c=craters[i];float dx=x-c.x,dz=z-c.z,r=c.y;
                    if(Mathf.Abs(dx)>r*1.65f||Mathf.Abs(dz)>r*1.65f)continue;
                    float angle=Mathf.Atan2(dz,dx);
                    float wobble=1+.10f*Mathf.Sin(angle*(3+i%4)+i*.71f)+.045f*Mathf.Sin(angle*(7+i%3)+i*.37f);
                    float q=Mathf.Sqrt(dx*dx+dz*dz)/(r*wobble);
                    if(q>1.62f)continue;

                    float wallT=Mathf.InverseLerp(.34f,.98f,q);
                    float bowl=q<=.34f?1f:1f-Mathf.SmoothStep(0f,1f,wallT);
                    float rimWidth=moon?.095f:.14f;
                    float rim=Mathf.Exp(-Mathf.Pow((q-1.0f)/rimWidth,2));
                    float eroded=.58f+.42f*Noise(x,z,moon?.92f:.8f,19+i);
                    float basinScale=Mathf.Lerp(.72f,1.30f,Mathf.InverseLerp(.4f,7.8f,r));

                    float depth;
                    float rimHeight;
                    if(moon)
                    {
                        float sizeWeight=Mathf.InverseLerp(.38f,7.8f,r);
                        depth=Mathf.Lerp(.12f,.54f,sizeWeight)*Mathf.Lerp(.80f,1.20f,outer);
                        rimHeight=Mathf.Lerp(.060f,.235f,sizeWeight)*Mathf.Lerp(.86f,1.18f,outer);
                    }
                    else
                    {
                        depth=.205f*basinScale;
                        rimHeight=.102f*basinScale;
                    }

                    // Small floor roughness prevents perfect circular cookie-cutter bowls while preserving
                    // the broad flat-bottomed impact character visible in the reference image.
                    float floorVariation=moon&&q<.5f?(Noise(x,z,1.35f,67+i)-.5f)*.030f:0f;
                    relief+=(-bowl*depth+rim*rimHeight)*eroded+floorVariation*bowl;

                    float lightSide=Mathf.Clamp((dx*.72f-dz*.46f)/Mathf.Max(.3f,r),-1f,1f);
                    float shadowStrength=moon?.075f:ArenaVariantRuntime.IsGreenPlanet?.036f:.055f;
                    float interiorShadow=bowl*shadowStrength*(1f+Mathf.Max(0f,-lightSide)*.70f);
                    float litRim=rim*(moon?.205f:.075f)*(1f+Mathf.Max(0f,lightSide)*.48f);
                    tone+=litRim*eroded-interiorShadow;
                    craterWear=Mathf.Max(craterWear,Mathf.Clamp01(bowl*.86f+rim*.50f));

                    // Sparse ejecta rays outside rims break the otherwise circular silhouette.
                    if(q>1f&&q<1.62f)
                    {
                        float ejecta=1f-(q-1f)/.62f;
                        float ray=Mathf.Max(0f,Mathf.Sin(angle*(9+i%5)+i*.63f)*.5f+.5f);
                        tone+=(moon?.036f:.014f)*ejecta*ray*eroded;
                    }
                }

                tone-=craterWear*(moon?.025f:ArenaVariantRuntime.IsGreenPlanet?.012f:.022f);
                dust=Mathf.Max(dust,craterWear*(moon?.36f:ArenaVariantRuntime.IsGreenPlanet?.19f:.26f));

                float radial=Mathf.Sqrt(x*x+z*z),a0=Mathf.Atan2(z,x);
                float frontRing=Mathf.Exp(-Mathf.Pow((radial-(9.2f+.26f*Mathf.Sin(a0*5)))/.26f,2));
                float broken=Mathf.Clamp01(.35f+Mathf.Sin(a0*3+.8f));
                tone-=frontRing*.045f*broken;
                relief-=frontRing*.025f*broken;
            }

            if(moon)
            {
                float minRelief=Mathf.Lerp(-.30f,-.62f,outer);
                float maxRelief=Mathf.Lerp(.17f,.28f,outer);
                relief=Mathf.Clamp(relief,minRelief,maxRelief);
                tone=Mathf.Clamp(tone,.22f,.74f);
            }
            else if(earth)
            {
                // Keep the Earth map close to flat while preserving enough large-scale variation to
                // avoid a synthetic plane. No crater bowls or dune ridges survive this clamp.
                relief=Mathf.Clamp(relief,-.035f,.045f);
                tone=Mathf.Clamp(tone,.38f,.62f);
                dust=0f;
            }
            else
            {
                relief=Mathf.Clamp(relief,mars?-.30f:-.245f,mars?.18f:.115f);
                tone=Mathf.Clamp(tone,ArenaVariantRuntime.IsGreenPlanet?.34f:.30f,.68f);
            }
        }

        /// <summary>Sample an enlarged arena using world coordinates; authored scenery uses Surface.</summary>
        public static Vector3 WorldSurface(float x,float z)
            =>Alterverse.Combat.ArenaLayout.Stretch(Surface(x/Alterverse.Combat.ArenaLayout.LengthScale,z));

        public static Vector3 Surface(float x,float z)
        {
            if(FrontierPlanetLayout.Active)return FrontierPlanetLayout.Surface(x,z);
            float fracture=ShatteredArenaEdge.FractureDepth(x,z);
            Vector2 edge=ShatteredArenaEdge.Warp(x,z);x=edge.x;z=edge.y;
            Sample(x,z,out float relief,out _,out _);return new Vector3(x,.015f+relief-fracture,z);
        }

        public static void Bake(out Texture2D albedo,out Texture2D normals)
        {
            // Sharper colour and macro normals on desktop across every arena variant.
            // Smaller GPUs keep the established memory budget.
            int size=SystemInfo.graphicsMemorySize>=4096?2048:1536;
            const float spanX=96,spanZ=84;
            bool moon=IsMoon;
            Color32[] pixels=new Color32[size*size],normalPixels=new Color32[pixels.Length];float[] heights=new float[pixels.Length];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                Sample(-48+spanX*x/(size-1),-42+spanZ*z/(size-1),out float h,out float tone,out float dust);
                int i=z*size+x;heights[i]=h;
                float value=moon
                    ?Mathf.Lerp(.20f,.62f,Mathf.InverseLerp(.22f,.74f,tone))
                    :ArenaVariantRuntime.IsGreenPlanet
                        ?Mathf.Lerp(.245f,.49f,Mathf.InverseLerp(.34f,.68f,tone))
                        :Mathf.Lerp(.19f,.43f,Mathf.InverseLerp(.30f,.68f,tone));
                value*=1f-dust*(moon?.055f:ArenaVariantRuntime.IsGreenPlanet?.025f:.045f);
                // Broad mineral variation supplies material depth without the former high-frequency
                // speckle that read as pixel noise from the gameplay camera.
                float wx=-48+spanX*x/(size-1),wz=-42+spanZ*z/(size-1);
                float grain=Mathf.PerlinNoise(wx*(ArenaVariantRuntime.IsMars?.58f:1.15f)+91f,wz*(ArenaVariantRuntime.IsMars?.58f:1.15f)+47f)-.5f;
                float strata=Mathf.PerlinNoise(wx*.30f+wz*.10f+13f,wz*.30f-wx*.07f+29f)-.5f;
                float vein=Mathf.Abs(Mathf.PerlinNoise(wx*.42f+201f,wz*.42f+78f)-.5f);
                value*=1f+grain*.095f+strata*.12f;
                value+=Mathf.SmoothStep(.37f,.49f,vein)*(moon?.026f:.014f);
                if(ArenaVariantRuntime.IsGreenPlanet)value=Mathf.Max(value,.235f);
                value=Mathf.Clamp01(value);
                if(ArenaVariantRuntime.IsExoplanet)
                    pixels[i]=ExoplanetArenaProfile.GroundColor(ArenaVariantRuntime.Current,wx,wz,value);
                else if(ArenaVariantRuntime.IsGJ504b)
                {
                    float rock=Mathf.SmoothStep(.42f,.70f,Mathf.PerlinNoise(wx*.11f+71f,wz*.11f+19f));
                    Color sand=new Color(value*1.65f,value*.40f,value*1.13f,1f);
                    Color stone=new Color(value*.80f,value*.25f,value*.65f,1f);
                    pixels[i]=Color.Lerp(sand,stone,rock*.62f);
                }
                else if(ArenaVariantRuntime.IsMars)
                {
                    float warm=Mathf.Lerp(.88f,1.08f,Mathf.InverseLerp(.30f,.68f,tone));
                    pixels[i]=new Color(value*1.28f*warm,value*.58f,value*.39f,1);
                }
                else if(ArenaVariantRuntime.IsGreenPlanet)
                {
                    pixels[i]=Hd137010Surface.ColorAt(wx,wz,tone);
                }
                else if(ArenaVariantRuntime.IsNoxara)
                {
                    float cobalt=Mathf.Lerp(.92f,1.10f,Mathf.InverseLerp(.30f,.68f,tone));
                    pixels[i]=new Color(value*.30f,value*.46f*cobalt,value*1.18f,1);
                }
                else if(ArenaVariantRuntime.IsKryon)
                {
                    float frost=Mathf.Lerp(.48f,.80f,Mathf.InverseLerp(.30f,.68f,tone));
                    frost*=1f-dust*.025f;
                    Color ground=new Color(frost*.88f,frost*.97f,Mathf.Min(1f,frost*1.08f),1);
                    pixels[i]=KryonCentralIce.Sample(wx,wz,ground,out float iceRelief);
                    // Etched ice normals are presentation only; Surface() and collision stay unchanged.
                    heights[i]+=iceRelief;
                }
                else if(ArenaVariantRuntime.IsEarth)
                {
                    float biome=Mathf.PerlinNoise(wx*.055f+18f,wz*.055f+53f);
                    float vegetation=Mathf.SmoothStep(.35f,.68f,biome);
                    float water=Mathf.Clamp01((.38f-biome)/.12f);
                    Color soil=new Color(value*.88f,value*.60f,value*.33f,1f);
                    Color grass=new Color(value*.43f,value*1.04f,value*.34f,1f);
                    Color land=Color.Lerp(soil,grass,vegetation);
                    Color waterColor=new Color(.025f+value*.10f,.18f+value*.18f,.43f+value*.38f,1f);
                    pixels[i]=Color.Lerp(land,waterColor,water*.94f);
                }
                else if(ArenaVariantRuntime.IsPyraxis)
                {
                    float lavaNoise=Mathf.Abs(Mathf.PerlinNoise(wx*.085f+41f,wz*.085f+17f)-.5f);
                    float lavaVein=1f-Mathf.SmoothStep(.025f,.115f,lavaNoise);
                    float secondary=Mathf.Abs(Mathf.PerlinNoise(wx*.16f+9f,wz*.16f+71f)-.5f);
                    lavaVein=Mathf.Max(lavaVein,(1f-Mathf.SmoothStep(.018f,.072f,secondary))*.55f);
                    Color basalt=new Color(value*.17f,value*.105f,value*.085f,1f);
                    Color molten=Color.Lerp(new Color(.92f,.10f,.008f,1f),new Color(1f,.58f,.035f,1f),lavaVein);
                    pixels[i]=Color.Lerp(basalt,molten,Mathf.Clamp01(lavaVein*.92f));
                }
                else if(ArenaVariantRuntime.IsNeptune)
                {
                    float abyss=Mathf.PerlinNoise(wx*.075f+101f,wz*.075f+37f);
                    float glow=Mathf.Pow(Mathf.Clamp01((abyss-.62f)/.38f),2f);
                    // Brighter royal/cobalt Neptune palette inspired by the supplied reference image,
                    // while preserving the existing terrain variation and dark-to-light depth read.
                    Color deep=new Color(.04f+value*.10f,.075f+value*.17f,.22f+value*.40f,1f);
                    Color blue=new Color(.10f+value*.18f,.18f+value*.32f,.56f+value*.62f,1f);
                    pixels[i]=Color.Lerp(deep,blue,.30f+glow*.56f);
                }
                else
                {
                    // Neutral cold regolith with much stronger bowl/rim value separation, inspired by
                    // the supplied planetary-surface reference photograph.
                    pixels[i]=new Color(value*.94f,value*.965f,value,1);
                }
            }

            float normalStrength=moon?2.4f:ArenaVariantRuntime.IsEarth?.85f:1.8f;
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float dx=(heights[z*size+Mathf.Min(size-1,x+1)]-heights[z*size+Mathf.Max(0,x-1)])/(2*spanX/(size-1));
                float dz=(heights[Mathf.Min(size-1,z+1)*size+x]-heights[Mathf.Max(0,z-1)*size+x])/(2*spanZ/(size-1));
                Vector3 n=new Vector3(-dx*normalStrength,-dz*normalStrength,1).normalized;
                normalPixels[z*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            albedo=new Texture2D(size,size,TextureFormat.RGBA32,true,false){name="DF_UniqueRegolithAlbedo",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=16};
            normals=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="DF_UniqueRegolithNormal",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=16};
            albedo.SetPixels32(pixels);albedo.Apply(true,true);normals.SetPixels32(normalPixels);normals.Apply(true,true);
        }
    }
}





