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
            ArenaVariant.Moon=>new(new(.94f,.97f,1f),new(.42f,.46f,.54f),new(.25f,.28f,.33f),new(.110f,.124f,.162f),1.52f,.68f,.50f),
            ArenaVariant.Mars=>new(new(1f,.86f,.71f),new(.40f,.33f,.31f),new(.27f,.22f,.21f),new(.160f,.098f,.072f),1.54f,.70f,.40f),
            ArenaVariant.GreenPlanet=>new(new(.92f,1f,.86f),new(.32f,.42f,.38f),new(.21f,.29f,.26f),new(.092f,.144f,.114f),1.52f,.67f,.46f),
            ArenaVariant.Noxara=>new(new(.80f,.89f,1f),new(.30f,.34f,.48f),new(.185f,.225f,.35f),new(.082f,.104f,.190f),1.48f,.65f,.54f),
            ArenaVariant.Kryon=>new(new(.87f,.97f,1f),new(.43f,.51f,.58f),new(.24f,.32f,.38f),new(.112f,.165f,.210f),1.48f,.63f,.58f),
            ArenaVariant.Earth=>new(new(1f,.97f,.87f),new(.36f,.44f,.49f),new(.25f,.31f,.28f),new(.114f,.148f,.092f),1.54f,.69f,.46f),
            ArenaVariant.Pyraxis=>new(new(1f,.89f,.75f),new(.36f,.31f,.32f),new(.26f,.195f,.185f),new(.215f,.075f,.024f),1.46f,.69f,.42f),
            ArenaVariant.Neptune=>new(new(.81f,.96f,1f),new(.28f,.43f,.52f),new(.165f,.28f,.36f),new(.060f,.128f,.192f),1.50f,.63f,.60f),
            ArenaVariant.Dark00=>new(new(.72f,.78f,.86f),new(.006f,.007f,.009f),new(.004f,.005f,.006f),new(.002f,.003f,.004f),.075f,.18f,.04f),
            ArenaVariant.DiamondCore=>new(new(.91f,.98f,1f),new(.42f,.50f,.57f),new(.24f,.31f,.38f),new(.105f,.158f,.210f),1.46f,.61f,.60f),
            ArenaVariant.GJ504b=>new(new(1f,.91f,.965f),new(.44f,.325f,.41f),new(.30f,.215f,.28f),new(.155f,.088f,.138f),1.50f,.67f,.42f),
            ArenaVariant.Wasp76b=>new(new(1f,.88f,.72f),new(.44f,.35f,.30f),new(.28f,.235f,.22f),new(.168f,.094f,.065f),1.48f,.69f,.56f),
            ArenaVariant.Trappist1e=>new(new(.92f,.895f,1f),new(.355f,.39f,.48f),new(.215f,.27f,.33f),new(.088f,.136f,.160f),1.50f,.65f,.54f),
            ArenaVariant.Toi715b=>new(new(1f,.865f,.73f),new(.44f,.345f,.30f),new(.29f,.225f,.205f),new(.165f,.094f,.075f),1.52f,.69f,.42f),
            _=>throw new System.ArgumentOutOfRangeException(nameof(planet))
        };
    }
}


