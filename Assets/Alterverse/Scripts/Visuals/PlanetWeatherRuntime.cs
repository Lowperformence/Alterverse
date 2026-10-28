using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Performance;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Local, cosmetic weather. No physics, visibility rules, global fog or network messages.</summary>
    public sealed class PlanetWeatherRuntime : MonoBehaviour
    {
        public readonly struct Profile
        {
            public readonly Color Tint;
            public readonly float Wind, Dust, Mist;
            public Profile(Color tint,float wind,float dust,float mist)
            {Tint=tint;Wind=wind;Dust=dust;Mist=mist;}
        }

        public static Profile For(ArenaVariant arena)
        {
            switch(arena)
            {
                // Airless arenas have a few settling grains, with no atmospheric wind or mist.
                case ArenaVariant.Moon: return new Profile(new Color(.66f,.67f,.69f),0f,2f,0f);
                case ArenaVariant.Dark00: return new Profile(new Color(.40f,.43f,.49f),0f,1f,0f);
                case ArenaVariant.DiamondCore: return new Profile(new Color(.62f,.66f,.72f),0f,1.5f,0f);
                case ArenaVariant.Mars: return new Profile(new Color(.70f,.46f,.30f),1.3f,13f,.025f);
                case ArenaVariant.GreenPlanet: return new Profile(new Color(.56f,.66f,.58f),.65f,4f,.030f);
                case ArenaVariant.Noxara: return new Profile(new Color(.46f,.40f,.59f),.55f,3f,.035f);
                case ArenaVariant.Kryon: return new Profile(new Color(.68f,.79f,.84f),1f,7f,.030f);
                case ArenaVariant.Earth: return new Profile(new Color(.66f,.67f,.60f),.75f,4f,.025f);
                case ArenaVariant.Pyraxis: return new Profile(new Color(.55f,.46f,.38f),1.1f,10f,.025f);
                case ArenaVariant.Neptune: return new Profile(new Color(.36f,.56f,.70f),1.4f,6f,.040f);
                case ArenaVariant.GJ504b: return new Profile(new Color(.75f,.57f,.46f),1.2f,12f,.025f);
                case ArenaVariant.Wasp76b: return new Profile(new Color(.65f,.52f,.43f),1.5f,10f,.030f);
                case ArenaVariant.Trappist1e: return new Profile(new Color(.55f,.68f,.66f),.65f,4f,.030f);
                case ArenaVariant.Toi715b: return new Profile(new Color(.57f,.65f,.70f),.85f,5f,.035f);
                default: return For(ArenaVariant.Moon);
            }
        }

        // The same boundary mask is used in the shader: fog cannot drift into the combat court.
        public static float MistAt(float x,float z,float stretch)
            =>Mathf.Max(Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(39f*stretch,46f*stretch,Mathf.Abs(x))),
                Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(25f,32f,Mathf.Abs(z))));

        public static float Gust(float time,int arena)
            =>1f+.18f*Mathf.Sin(time*.23f+arena)+.08f*Mathf.Sin(time*.61f+arena*.7f);

        GameObject weatherRoot,failedWorld;
        Material grainsMaterial,mistMaterial;
        readonly ParticleSystem[] layers=new ParticleSystem[6];
        readonly float[] rates=new float[6];
        Profile profile;
        ArenaVariant builtFor;
        float nextSearch,nextQuality,weatherTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
#if !UNITY_SERVER
            if(DedicatedServerNetworkRuntime.Requested||Application.isBatchMode||
                SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            if(FindAnyObjectByType<PlanetWeatherRuntime>()!=null)return;
            var host=new GameObject("DF_PlanetWeatherRuntime");
            DontDestroyOnLoad(host);
            host.AddComponent<PlanetWeatherRuntime>();
#endif
        }

        void Update()
        {
            if(DedicatedServerNetworkRuntime.Requested){Clear();enabled=false;return;}
            if(weatherRoot==null||builtFor!=ArenaVariantRuntime.Current)
            {
                if(Time.unscaledTime<nextSearch)return;
                nextSearch=Time.unscaledTime+.5f;
                Clear();
                var world=GameObject.Find("DF_MatchWorld");
                if(world==null||world==failedWorld)return;
                try{Build(world);}
                catch(System.Exception error)
                {
                    Clear();failedWorld=world;
                    Debug.LogWarning("DIMENSIONS FRONT: optional planet weather unavailable: "+error.Message);
                }
            }
            if(weatherRoot==null)return;
            bool visible=!Alterverse.UI.MainMenuController.IsVisible&&!MatchClock.InBackground;
            if(weatherRoot.activeSelf!=visible)weatherRoot.SetActive(visible);
            if(!visible||Time.timeScale<=0f)return;
            weatherTime+=Time.deltaTime;
            if(Time.unscaledTime>=nextQuality){nextQuality=Time.unscaledTime+1f;ApplyBudget();}
            float speed=profile.Wind*Gust(weatherTime,(int)builtFor);
            for(int i=0;i<layers.Length;i++)
            {
                if(layers[i]==null)continue;
                var velocity=layers[i].velocityOverLifetime;
                velocity.x=speed*(i<2?1f:.20f);
                velocity.z=speed*(i<2?.28f:.06f);
            }
        }

        void Build(GameObject world)
        {
            Shader shader=Resources.Load<Shader>("DF_PlanetWeather");
            if(shader==null||!shader.isSupported)throw new System.InvalidOperationException("Weather shader missing or unsupported");
            builtFor=ArenaVariantRuntime.Current;profile=For(builtFor);weatherTime=0f;
            grainsMaterial=new Material(shader){name="DF_WeatherGrains"};
            mistMaterial=new Material(shader){name="DF_WeatherEdgeMist"};
            mistMaterial.SetFloat("_EdgeOnly",1f);
            mistMaterial.SetFloat("_ArenaStretch",ArenaLayout.LengthScale);
            weatherRoot=new GameObject("DF_Weather_"+builtFor);
            weatherRoot.transform.SetParent(world.transform,false);
            float stretch=ArenaLayout.LengthScale;
            // World-space particles are confined to a low ground layer; no fullscreen overlays.
            layers[0]=Layer(0,"Settling dust",Vector3.up*.45f,new Vector3(82f*stretch,.6f,58f),
                profile.Dust,112,.035f,.09f,5f,.13f,grainsMaterial);
            if(profile.Wind>0f)
                layers[1]=Layer(1,"Wind motes",Vector3.up*.75f,new Vector3(82f*stretch,.8f,58f),
                    2f,24,.025f,.045f,4f,.08f,grainsMaterial);
            if(profile.Mist>0f)
            {
                // Four small edge banks. The shader also masks the court and all three lanes.
                layers[2]=Layer(2,"North edge mist",new Vector3(0,.55f,31f),new Vector3(76f*stretch,.35f,2f),
                    .5f,8,4f,7f,12f,profile.Mist,mistMaterial);
                layers[3]=Layer(3,"South edge mist",new Vector3(0,.55f,-31f),new Vector3(76f*stretch,.35f,2f),
                    .5f,8,4f,7f,12f,profile.Mist,mistMaterial);
                layers[4]=Layer(4,"West edge mist",new Vector3(-44f*stretch,.55f,0),new Vector3(2f,.35f,48f),
                    .35f,8,4f,7f,12f,profile.Mist,mistMaterial);
                layers[5]=Layer(5,"East edge mist",new Vector3(44f*stretch,.55f,0),new Vector3(2f,.35f,48f),
                    .35f,8,4f,7f,12f,profile.Mist,mistMaterial);
            }
            ApplyBudget();
        }

        ParticleSystem Layer(int index,string label,Vector3 position,Vector3 box,float rate,int cap,
            float minSize,float maxSize,float life,float alpha,Material material)
        {
            var go=new GameObject("DF_Weather_"+label);go.transform.SetParent(weatherRoot.transform,false);
            go.transform.localPosition=position;
            var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed=false;particles.randomSeed=(uint)(179+(int)builtFor*31+index*17);
            var main=particles.main;
            main.playOnAwake=false;main.loop=true;main.prewarm=true;main.duration=life;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.useUnscaledTime=false;main.maxParticles=cap;
            main.startLifetime=new ParticleSystem.MinMaxCurve(life*.7f,life);
            main.startSpeed=0f;main.startSize=new ParticleSystem.MinMaxCurve(minSize,maxSize);
            Color tint=profile.Tint;tint.a=alpha;main.startColor=tint;
            main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
            main.gravityModifier=profile.Wind==0f?.015f:0f;
            var shape=particles.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=box;
            var emission=particles.emission;emission.rateOverTime=rate;rates[index]=rate;
            var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=profile.Wind*(index<2?1f:.20f);velocity.z=profile.Wind*(index<2?.28f:.06f);
            velocity.y=profile.Wind==0f?.08f:0f;
            var color=particles.colorOverLifetime;color.enabled=true;
            var fade=new Gradient();
            fade.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
                new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(1f,.2f),new GradientAlphaKey(1f,.65f),new GradientAlphaKey(0f,1f)});
            color.color=fade;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.renderMode=index==1?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;
            renderer.lengthScale=1.2f;renderer.velocityScale=.12f;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return particles;
        }

        void ApplyBudget()
        {
            bool decorations=AdaptiveVfxBudget.AllowDecorative;
            float intensity=AdaptiveVfxBudget.Intensity;
            for(int i=0;i<layers.Length;i++)
            {
                var particles=layers[i];if(particles==null)continue;
                bool active=i==0||decorations;
                var emission=particles.emission;emission.rateOverTime=active?rates[i]*intensity:0f;
                if(active){if(!particles.isPlaying)particles.Play();}
                else if(!particles.isStopped)particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void Clear()
        {
            if(weatherRoot!=null){weatherRoot.SetActive(false);Destroy(weatherRoot);}
            weatherRoot=null;
            for(int i=0;i<layers.Length;i++){layers[i]=null;rates[i]=0f;}
            if(grainsMaterial!=null)Destroy(grainsMaterial);
            if(mistMaterial!=null)Destroy(mistMaterial);
            grainsMaterial=mistMaterial=null;
        }
        void OnDisable()=>Clear();
        void OnDestroy()=>Clear();
    }
}
