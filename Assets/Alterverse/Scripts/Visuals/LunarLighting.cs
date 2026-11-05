using UnityEngine;
using UnityEngine.Rendering;
using Alterverse.Match;
namespace Alterverse.Visuals
{
    // One directional key and tuned ambient fill: mobile-friendly depth without extra lights or fullscreen post FX.
    public sealed class LunarLighting:MonoBehaviour
    {
        private Light key;private bool created,applied;
        private Color sky,equator,ground,lightColor;private AmbientMode mode;private bool fog;
        private float intensity,reflection,shadowStrength;private Quaternion rotation;private int mask;private LightShadows shadowMode;
        private void OnEnable()
        {
            sky=RenderSettings.ambientSkyColor;equator=RenderSettings.ambientEquatorColor;ground=RenderSettings.ambientGroundColor;
            mode=RenderSettings.ambientMode;fog=RenderSettings.fog;reflection=RenderSettings.reflectionIntensity;
            key=RenderSettings.sun;
            if(key==null||!key.enabled||key.type!=LightType.Directional)
            {key=null;foreach(Light light in FindObjectsByType<Light>())if(light.type==LightType.Directional&&light.enabled){key=light;break;}}
            if(key==null){GameObject obj=new("DF_LunarKey");obj.transform.SetParent(transform,false);key=obj.AddComponent<Light>();key.type=LightType.Directional;created=true;}
            rotation=key.transform.rotation;intensity=key.intensity;shadowStrength=key.shadowStrength;lightColor=key.color;mask=key.cullingMask;shadowMode=key.shadows;

            ArenaLightingProfile profile=ArenaLightingProfile.For(ArenaVariantRuntime.Current);
            // Keep the same sun direction for both teams. Colour and fill describe the biome;
            // neither new realtime lights nor a fullscreen effect is needed for the concept pass.
            key.transform.rotation=Quaternion.Euler(42f,-36f,0f);
            key.intensity=profile.Intensity;
            key.shadowStrength=profile.ShadowStrength;
            key.shadows=QualitySettings.shadows==ShadowQuality.Disable||ArenaVariantRuntime.IsDark00
                ?LightShadows.None:LightShadows.Soft;
            key.color=profile.Key;
            key.cullingMask&=~(1<<31); // Menu showcase has its own isolated studio lights.

            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=profile.Sky;
            RenderSettings.ambientEquatorColor=profile.Equator;
            RenderSettings.ambientGroundColor=profile.Ground;
            RenderSettings.reflectionIntensity=profile.Reflection;
            RenderSettings.fog=false;
            applied=true;
        }
        private void OnDisable()
        {
            if(!applied)return;applied=false;
            RenderSettings.ambientMode=mode;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
            RenderSettings.reflectionIntensity=reflection;RenderSettings.fog=fog;
            if(key!=null){if(created)Destroy(key.gameObject);else{key.transform.rotation=rotation;key.intensity=intensity;key.shadowStrength=shadowStrength;key.shadows=shadowMode;key.color=lightColor;key.cullingMask=mask;}}
            key=null;created=false;
        }
    }

    /// <summary>Readable biome lighting from one key and three ambient colours.
    /// Dark00 retains its deliberate blackout; wet/crystalline maps have a cooler fill,
    /// and volcanic ground has restrained warm bounce rather than a global orange wash.</summary>
    internal readonly struct ArenaLightingProfile
    {
        public readonly Color Key,Sky,Equator,Ground;
        public readonly float Intensity,ShadowStrength,Reflection;

        ArenaLightingProfile(Color key,Color sky,Color equator,Color ground,
            float intensity=1.40f,float shadow=.58f,float reflection=.38f)
        {Key=key;Sky=sky;Equator=equator;Ground=ground;Intensity=intensity;ShadowStrength=shadow;Reflection=reflection;}

        public static ArenaLightingProfile For(ArenaVariant planet)=>planet switch
        {
            ArenaVariant.Moon=>new(new(.93f,.965f,1f),new(.40f,.435f,.50f),new(.24f,.265f,.31f),new(.115f,.128f,.165f),1.44f,.62f,.42f),
            ArenaVariant.Mars=>new(new(1f,.85f,.70f),new(.38f,.31f,.29f),new(.26f,.21f,.20f),new(.165f,.102f,.075f),1.45f,.64f,.32f),
            ArenaVariant.GreenPlanet=>new(new(.91f,1f,.85f),new(.30f,.395f,.36f),new(.20f,.275f,.25f),new(.095f,.148f,.118f),1.44f,.61f,.38f),
            ArenaVariant.Noxara=>new(new(.79f,.88f,1f),new(.28f,.32f,.45f),new(.175f,.215f,.33f),new(.085f,.108f,.195f),1.40f,.59f,.46f),
            ArenaVariant.Kryon=>new(new(.86f,.965f,1f),new(.41f,.485f,.55f),new(.23f,.305f,.36f),new(.115f,.17f,.215f),1.40f,.57f,.50f),
            ArenaVariant.Earth=>new(new(1f,.965f,.86f),new(.34f,.415f,.46f),new(.24f,.295f,.27f),new(.118f,.152f,.095f),1.45f,.63f,.38f),
            ArenaVariant.Pyraxis=>new(new(1f,.88f,.74f),new(.34f,.29f,.30f),new(.25f,.185f,.175f),new(.22f,.078f,.025f),1.38f,.63f,.34f),
            ArenaVariant.Neptune=>new(new(.80f,.955f,1f),new(.26f,.41f,.49f),new(.155f,.265f,.34f),new(.062f,.132f,.198f),1.42f,.57f,.52f),
            ArenaVariant.Dark00=>new(new(.72f,.78f,.86f),new(.006f,.007f,.009f),new(.004f,.005f,.006f),new(.002f,.003f,.004f),.075f,.18f,.04f),
            ArenaVariant.DiamondCore=>new(new(.90f,.975f,1f),new(.40f,.475f,.54f),new(.23f,.295f,.36f),new(.108f,.162f,.215f),1.38f,.55f,.52f),
            ArenaVariant.GJ504b=>new(new(1f,.90f,.96f),new(.42f,.305f,.39f),new(.29f,.205f,.27f),new(.16f,.092f,.142f),1.42f,.61f,.34f),
            ArenaVariant.Wasp76b=>new(new(1f,.875f,.71f),new(.42f,.335f,.28f),new(.27f,.225f,.21f),new(.172f,.098f,.068f),1.40f,.63f,.48f),
            ArenaVariant.Trappist1e=>new(new(.91f,.885f,1f),new(.335f,.37f,.455f),new(.205f,.26f,.315f),new(.092f,.14f,.165f),1.42f,.59f,.46f),
            ArenaVariant.Toi715b=>new(new(1f,.855f,.72f),new(.42f,.325f,.28f),new(.28f,.215f,.195f),new(.17f,.098f,.078f),1.44f,.63f,.34f),
            _=>throw new System.ArgumentOutOfRangeException(nameof(planet))
        };
    }
}


