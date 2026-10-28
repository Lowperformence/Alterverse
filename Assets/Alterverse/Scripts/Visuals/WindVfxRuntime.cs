using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Performance;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Camera-local Vefects wind on all fourteen maps. Entirely cosmetic, no wind force or network state.</summary>
    public sealed class WindVfxRuntime : MonoBehaviour
    {
        GameObject root,failedWorld;
        ParticleSystem flow,debris;
        ArenaVariant builtFor;
        float nextBudget,elapsed;

        public static bool UseLeaves(ArenaVariant map)=>map==ArenaVariant.Earth||map==ArenaVariant.GreenPlanet||map==ArenaVariant.Trappist1e||map==ArenaVariant.Toi715b;
        // The requested stylized flow remains present even on the airless maps; no atmospheric fog is added there.
        public static float Speed(ArenaVariant map)=>Mathf.Max(.55f,PlanetWeatherRuntime.For(map).Wind);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
#if !UNITY_SERVER
            if(DedicatedServerNetworkRuntime.Requested||Application.isBatchMode||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            if(FindAnyObjectByType<WindVfxRuntime>()!=null)return;
            var host=new GameObject("DF_WindVfxRuntime");DontDestroyOnLoad(host);host.AddComponent<WindVfxRuntime>();
#endif
        }
        void Update()
        {
            if(DedicatedServerNetworkRuntime.Requested){Clear();enabled=false;return;}
            if(root==null||builtFor!=ArenaVariantRuntime.Current)
            {
                Clear();var world=GameObject.Find("DF_MatchWorld");
                if(world==null||world==failedWorld)return;
                try{Build(world);}
                catch(System.Exception error){Clear();failedWorld=world;Debug.LogWarning("DIMENSIONS FRONT: optional authored wind unavailable: "+error.Message);return;}
            }
            bool visible=!Alterverse.UI.MainMenuController.IsVisible&&!MatchClock.InBackground;
            if(root.activeSelf!=visible){root.SetActive(visible);nextBudget=0f;}
            if(!visible||Time.timeScale<=0f)return;
            FollowCamera();elapsed+=Time.deltaTime;
            float speed=Speed(builtFor)*PlanetWeatherRuntime.Gust(elapsed,(int)builtFor);
            SetVelocity(flow,speed);SetVelocity(debris,speed*.65f);
            if(Time.unscaledTime>=nextBudget){nextBudget=Time.unscaledTime+.5f;ApplyBudget();}
        }
        void Build(GameObject world)
        {
            var catalog=Resources.Load<PlanetWindCatalog>("PlanetWindCatalog");
            if(catalog==null||catalog.flows==null||catalog.dust==null||catalog.leaves==null)throw new System.InvalidOperationException("Wind VFX catalog missing");
            builtFor=ArenaVariantRuntime.Current;elapsed=0f;
            root=new GameObject("DF_AuthoredWind_"+builtFor);root.transform.SetParent(world.transform,false);FollowCamera();
            flow=Create(catalog.flows,false);
            debris=Create(UseLeaves(builtFor)?catalog.leaves:catalog.dust,true);
            ApplyBudget();
        }
        ParticleSystem Create(GameObject prefab,bool small)
        {
            var instance=Instantiate(prefab,root.transform,false);
            ParticleSystem selected=null;
            // Vendor prefabs include a renderer-less container particle system. It must never emit.
            foreach(var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var renderer=particles.GetComponent<ParticleSystemRenderer>();
                if(selected==null&&renderer!=null&&renderer.enabled&&renderer.sharedMaterial!=null){selected=particles;continue;}
                var unused=particles.emission;unused.enabled=false;
            }
            if(selected==null)throw new System.InvalidOperationException("No visible wind particle layer in "+prefab.name);
            selected.useAutoRandomSeed=false;selected.randomSeed=(uint)(893+(int)builtFor*31+(small?7:0));
            var main=selected.main;main.playOnAwake=false;main.loop=true;main.prewarm=false;main.duration=6f;
            main.useUnscaledTime=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(4f,6f);main.startSpeed=0f;
            var visibleRenderer=selected.GetComponent<ParticleSystemRenderer>();
            float meshExtent=visibleRenderer.mesh!=null?visibleRenderer.mesh.bounds.size.magnitude:1f;
            float size=(small?(UseLeaves(builtFor)?.22f:.65f):2.3f)/Mathf.Max(.01f,meshExtent);
            main.startSize=new ParticleSystem.MinMaxCurve(size*.7f,size);
            Color color=Color.Lerp(PlanetWeatherRuntime.For(builtFor).Tint,Color.white,.60f);
            if(small&&UseLeaves(builtFor))color=new Color(.58f,.69f,.38f);
            color.a=small?.35f:.24f;main.startColor=color;
            var shape=selected.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;
            shape.position=Vector3.zero;shape.scale=new Vector3(28f,small?.45f:.65f,20f);
            selected.transform.localPosition=new Vector3(0,small?.35f:.70f,0);
            var collision=selected.collision;collision.enabled=false;
            var trigger=selected.trigger;trigger.enabled=false;
            var subEmitters=selected.subEmitters;subEmitters.enabled=false;
            var velocity=selected.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=Speed(builtFor);velocity.y=0f;velocity.z=Speed(builtFor)*.22f;
            var emission=selected.emission;emission.enabled=true;emission.SetBursts(new ParticleSystem.Burst[0]);
            visibleRenderer.shadowCastingMode=ShadowCastingMode.Off;visibleRenderer.receiveShadows=false;
            // Keep the authored renderer, shared material, mesh, shaders and lifetime fades.
            return selected;
        }
        void FollowCamera()
        {
            var camera=UnityEngine.Camera.main;if(camera==null||root==null)return;
            Vector3 position=camera.transform.position,forward=camera.transform.forward;
            if(forward.y<-.01f)position+=forward*(-position.y/forward.y);
            position.x=Mathf.Clamp(position.x,-ArenaLayout.HalfLength+10f,ArenaLayout.HalfLength-10f);
            position.z=Mathf.Clamp(position.z,-ArenaLayout.HalfBreadth+8f,ArenaLayout.HalfBreadth-8f);
            position.y=0f;root.transform.position=position;
        }
        static void SetVelocity(ParticleSystem particles,float speed)
        {if(particles==null)return;var velocity=particles.velocityOverLifetime;velocity.x=speed;velocity.z=speed*.22f;}
        void ApplyBudget()
        {
            float intensity=AdaptiveVfxBudget.Intensity;
            if(flow!=null)
            {
                var main=flow.main;main.maxParticles=AdaptiveVfxBudget.AllowDecorative?16:8;
                var emission=flow.emission;emission.rateOverTime=2.6f*intensity;
                if(!flow.isPlaying)flow.Play(false);
            }
            if(debris!=null)
            {
                var main=debris.main;main.maxParticles=UseLeaves(builtFor)?20:36;
                var emission=debris.emission;emission.rateOverTime=AdaptiveVfxBudget.AllowDecorative?(UseLeaves(builtFor)?2.8f:5f)*intensity:0f;
                if(AdaptiveVfxBudget.AllowDecorative){if(!debris.isPlaying)debris.Play(false);}
                else debris.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        void Clear(){if(root!=null){root.SetActive(false);Destroy(root);}root=null;flow=debris=null;}
        void OnDestroy()=>Clear();
    }
}
