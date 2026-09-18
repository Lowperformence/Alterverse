using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    // One directional key; no added point lights or post-process volumes. Restore scene state on teardown.
    public sealed class LunarLighting:MonoBehaviour
    {
        private Light key;private bool created,applied;
        private Color sky,equator,ground,lightColor;private AmbientMode mode;private bool fog;
        private float intensity,reflection;private Quaternion rotation;private int mask;
        private void OnEnable()
        {
            sky=RenderSettings.ambientSkyColor;equator=RenderSettings.ambientEquatorColor;ground=RenderSettings.ambientGroundColor;
            mode=RenderSettings.ambientMode;fog=RenderSettings.fog;reflection=RenderSettings.reflectionIntensity;
            key=RenderSettings.sun;
            if(key==null||!key.enabled||key.type!=LightType.Directional)
            {key=null;foreach(Light light in FindObjectsByType<Light>())if(light.type==LightType.Directional&&light.enabled){key=light;break;}}
            if(key==null){GameObject obj=new("DF_LunarKey");obj.transform.SetParent(transform,false);key=obj.AddComponent<Light>();key.type=LightType.Directional;created=true;}
            rotation=key.transform.rotation;intensity=key.intensity;lightColor=key.color;mask=key.cullingMask;
            key.transform.rotation=Quaternion.Euler(39,-38,0);key.intensity=1.2f;key.color=new Color(.93f,.96f,1f);
            key.cullingMask&=~(1<<31); // Menu showcase has its own isolated studio lights.
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.27f,.30f,.36f);
            RenderSettings.ambientEquatorColor=new Color(.105f,.12f,.145f);RenderSettings.ambientGroundColor=new Color(.055f,.065f,.08f);
            RenderSettings.reflectionIntensity=.25f;RenderSettings.fog=false;applied=true;
        }
        private void OnDisable()
        {
            if(!applied)return;applied=false;
            RenderSettings.ambientMode=mode;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
            RenderSettings.reflectionIntensity=reflection;RenderSettings.fog=fog;
            if(key!=null){if(created)Destroy(key.gameObject);else{key.transform.rotation=rotation;key.intensity=intensity;key.color=lightColor;key.cullingMask=mask;}}
            key=null;created=false;
        }
    }
}
