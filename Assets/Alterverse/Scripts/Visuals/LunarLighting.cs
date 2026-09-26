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

            key.transform.rotation=Quaternion.Euler(42f,-36f,0f);
            key.intensity=1.40f;
            key.shadowStrength=.56f;
            if(QualitySettings.shadows!=ShadowQuality.Disable)key.shadows=LightShadows.Soft;
            key.color=ArenaVariantRuntime.IsMars?new Color(1f,.82f,.68f):
                ArenaVariantRuntime.IsGreenPlanet?new Color(.86f,1f,.78f):
                ArenaVariantRuntime.IsNoxara?new Color(.78f,.86f,1f):
                ArenaVariantRuntime.IsKryon?new Color(.82f,.94f,1f):new Color(.92f,.955f,1f);
            key.cullingMask&=~(1<<31); // Menu showcase has its own isolated studio lights.

            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=ArenaVariantRuntime.IsMars?new Color(.36f,.30f,.28f):
                ArenaVariantRuntime.IsGreenPlanet?new Color(.27f,.36f,.30f):
                ArenaVariantRuntime.IsNoxara?new Color(.26f,.29f,.40f):new Color(.40f,.43f,.49f);
            RenderSettings.ambientEquatorColor=new Color(.24f,.26f,.30f);
            RenderSettings.ambientGroundColor=new Color(.14f,.15f,.18f);
            RenderSettings.reflectionIntensity=.38f;
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
}
