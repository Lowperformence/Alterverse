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
            ArenaVariant.Moon=>new(new(.92f,.955f,1f),new(.38f,.41f,.47f),new(.23f,.25f,.29f),new(.12f,.13f,.16f)),
            ArenaVariant.Mars=>new(new(1f,.84f,.71f),new(.36f,.30f,.28f),new(.25f,.21f,.20f),new(.16f,.105f,.08f),1.40f,.60f,.28f),
            ArenaVariant.GreenPlanet=>new(new(.92f,1f,.86f),new(.29f,.37f,.34f),new(.19f,.26f,.24f),new(.10f,.15f,.12f),1.40f,.58f,.34f),
            ArenaVariant.Noxara=>new(new(.80f,.87f,1f),new(.27f,.30f,.42f),new(.17f,.20f,.31f),new(.09f,.11f,.19f),1.36f,.56f,.42f),
            ArenaVariant.Kryon=>new(new(.87f,.96f,1f),new(.39f,.46f,.52f),new(.22f,.29f,.34f),new(.12f,.17f,.21f),1.36f,.54f,.46f),
            ArenaVariant.Earth=>new(new(1f,.96f,.85f),new(.32f,.39f,.43f),new(.23f,.28f,.26f),new(.12f,.15f,.095f),1.40f,.60f,.34f),
            ArenaVariant.Pyraxis=>new(new(1f,.89f,.76f),new(.32f,.28f,.29f),new(.24f,.18f,.17f),new(.21f,.075f,.025f),1.34f,.60f,.30f),
            ArenaVariant.Neptune=>new(new(.81f,.95f,1f),new(.25f,.39f,.46f),new(.15f,.25f,.32f),new(.065f,.13f,.19f),1.38f,.54f,.48f),
            ArenaVariant.Dark00=>new(new(.72f,.78f,.86f),new(.006f,.007f,.009f),new(.004f,.005f,.006f),new(.002f,.003f,.004f),.075f,.18f,.04f),
            ArenaVariant.DiamondCore=>new(new(.91f,.97f,1f),new(.38f,.45f,.51f),new(.22f,.28f,.34f),new(.11f,.16f,.21f),1.34f,.52f,.48f),
            ArenaVariant.GJ504b=>new(new(1f,.91f,.96f),new(.40f,.29f,.37f),new(.28f,.20f,.26f),new(.16f,.095f,.14f),1.38f,.58f,.30f),
            ArenaVariant.Wasp76b=>new(new(1f,.88f,.72f),new(.40f,.32f,.27f),new(.26f,.22f,.21f),new(.17f,.10f,.07f),1.36f,.60f,.44f),
            ArenaVariant.Trappist1e=>new(new(.92f,.88f,1f),new(.32f,.35f,.43f),new(.20f,.25f,.30f),new(.095f,.14f,.16f),1.38f,.56f,.42f),
            ArenaVariant.Toi715b=>new(new(1f,.86f,.73f),new(.40f,.31f,.27f),new(.27f,.21f,.19f),new(.17f,.10f,.08f),1.40f,.60f,.30f),
            _=>throw new System.ArgumentOutOfRangeException(nameof(planet))
        };
    }
}


