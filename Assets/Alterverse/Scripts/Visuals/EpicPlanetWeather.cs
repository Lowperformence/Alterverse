using Alterverse.Match;
using Alterverse.Performance;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    // Two camera-local, cosmetic weather layers at most. Combat slots and telegraphs
    // have their own budget; weather is removed first when the device is under load.
    public sealed class EpicPlanetWeather : MonoBehaviour
    {
        private readonly GameObject[] layers=new GameObject[2];
        private ArenaVariant builtFor=(ArenaVariant)(-1);
        private Transform world;
        private float nextRefresh;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null||GameObject.Find("DF_EpicPlanetWeather")!=null)return;
            var host=new GameObject("DF_EpicPlanetWeather");Object.DontDestroyOnLoad(host);host.AddComponent<EpicPlanetWeather>();
        }
        internal static string[] LayersFor(ArenaVariant planet)=>planet switch
        {
            ArenaVariant.Kryon=>new[]{"WeatherSnow","WeatherFog"},
            ArenaVariant.Earth=>new[]{"WeatherRain","WeatherWind"},
            ArenaVariant.GreenPlanet or ArenaVariant.Trappist1e=>new[]{"WeatherRain","WeatherFog"},
            ArenaVariant.Mars or ArenaVariant.DiamondCore=>new[]{"WeatherWind"},
            ArenaVariant.Dark00 or ArenaVariant.Noxara=>new[]{"WeatherFog"},
            ArenaVariant.Pyraxis or ArenaVariant.Neptune or ArenaVariant.GJ504b or ArenaVariant.Wasp76b or ArenaVariant.Toi715b=>new[]{"WeatherWind","WeatherFog"},
            _=>System.Array.Empty<string>() // No rain/snow in the Moon's vacuum.
        };
        private void Update()
        {
            if(Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+.5f;
                var match=GameObject.Find("DF_MatchWorld");
                if(match==null||!AdaptiveVfxBudget.AllowDecorative)
                {Clear();world=null;builtFor=(ArenaVariant)(-1);return;}
                if(world!=match.transform||builtFor!=ArenaVariantRuntime.Current)
                {Clear();world=match.transform;builtFor=ArenaVariantRuntime.Current;Build();}
            }
            if(world==null)return;
            var camera=Camera.main;if(camera==null)return;
            Ray ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))return;
            Vector3 center=ray.GetPoint(distance);
            for(int i=0;i<layers.Length;i++)if(layers[i]!=null)
            {
                bool fog=layers[i].name.Contains("Fog");
                // Haze sits beyond the central combat view, not over health bars/aiming.
                layers[i].transform.position=center+(fog?new Vector3(0,.12f,10f):new Vector3(0,3f,0));
            }
        }
        private void Build()
        {
            var library=Resources.Load<EpicVfxLibrary>(EpicVfxLibrary.ResourcePath);if(library==null)return;
            string[] keys=LayersFor(builtFor);
            for(int i=0;i<keys.Length&&i<layers.Length;i++)
            {
                var entry=library.Find(keys[i]);if(entry?.prefab==null)continue;
                var root=Object.Instantiate(entry.prefab,transform,false);layers[i]=root;root.name=keys[i];
                bool fog=keys[i]=="WeatherFog";
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);
                int cap=Mathf.Max(1,(fog?20:64)/Mathf.Max(1,systems.Length));
                foreach(var ps in systems)
                {
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=ps.main;main.loop=true;main.maxParticles=Mathf.Min(main.maxParticles,cap);
                    main.cullingMode=ParticleSystemCullingMode.Pause;main.simulationSpace=ParticleSystemSimulationSpace.World;
                }
                var block=new MaterialPropertyBlock();Color tint=Color.white;
                if(builtFor==ArenaVariant.Mars||builtFor==ArenaVariant.Toi715b)tint=new Color(.72f,.44f,.28f);
                else if(builtFor==ArenaVariant.Pyraxis||builtFor==ArenaVariant.Wasp76b)tint=new Color(.72f,.38f,.21f);
                else if(builtFor==ArenaVariant.Neptune||builtFor==ArenaVariant.Kryon)tint=new Color(.66f,.86f,1f);
                tint.a=fog?.10f:keys[i]=="WeatherSnow"?.30f:.18f;
                block.SetColor(Shader.PropertyToID("_Tint"),tint);
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.SetPropertyBlock(block);
                root.SetActive(true);foreach(var ps in systems)ps.Play(false);
            }
        }
        private void Clear(){for(int i=0;i<layers.Length;i++){if(layers[i]!=null)Destroy(layers[i]);layers[i]=null;}}
        private void OnDisable(){Clear();world=null;builtFor=(ArenaVariant)(-1);nextRefresh=0;}
    }
}
