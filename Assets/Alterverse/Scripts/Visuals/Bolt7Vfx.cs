using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed class Bolt7Vfx:MonoBehaviour
    {
        private sealed class Part{public GameObject go,source,owner;public ParticleSystem[] systems;public float until,start,seconds,delay;public Vector3 from,to;public bool lob;}
        private static Bolt7Vfx runner;
        private static Bolt7VfxLibrary library;
        private readonly List<Part> active=new(32);
        private readonly Dictionary<GameObject,Queue<Part>> pool=new();
        private bool warned;
        public static void Warm()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            if(runner!=null)return;
            runner=new GameObject("DF_Bolt7VfxPool").AddComponent<Bolt7Vfx>();
            var asset=Resources.Load<GameObject>("Bolt7/Vfx/Library");library=asset!=null?asset.GetComponent<Bolt7VfxLibrary>():null;
            if(library!=null)foreach(var p in new[]{library.muzzle,library.grenade,library.explosion,library.lightningOrb,library.starFog})
                if(p!=null){var a=runner.Take(p);var b=runner.Take(p);runner.Return(a);runner.Return(b);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){runner=null;library=null;}
        private Part Take(GameObject source)
        {
            if(pool.TryGetValue(source,out var q)&&q.Count>0)return q.Dequeue();
            var go=Instantiate(source,transform,false);go.SetActive(false);
            return new Part{go=go,source=source,systems=go.GetComponentsInChildren<ParticleSystem>(true)};
        }
        private void Return(Part p)
        {
            foreach(var s in p.systems)s.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            p.owner=null;p.go.SetActive(false);if(!pool.TryGetValue(p.source,out var q))pool[p.source]=q=new Queue<Part>();q.Enqueue(p);
        }
        private static void Spawn(GameObject source,Vector3 from,Vector3 to,float seconds,float elapsed=0,bool lob=false,float delay=0,GameObject owner=null)
        {
            Warm();if(runner==null||source==null)return;
            if(elapsed>=seconds+delay)return;
            var p=runner.Take(source);p.owner=owner;p.from=from;p.to=to;p.seconds=seconds;p.start=Time.time-elapsed;p.delay=delay;p.lob=lob;p.until=p.start+delay+seconds;
            p.go.transform.position=from;p.go.transform.rotation=Quaternion.identity;
            p.go.transform.localScale=source.transform.localScale*Bolt7Balance.FxScale;
            runner.active.Add(p);p.go.SetActive(elapsed>=delay);
            if(elapsed>=delay)foreach(var s in p.systems){if(s.main.playOnAwake){s.Play(false);if(elapsed>delay)s.Simulate(elapsed-delay,false,false,false);}}
        }
        public static void CancelOwner(GameObject actor)
        {
            if(runner==null)return;
            for(int i=runner.active.Count-1;i>=0;i--)if(runner.active[i].owner==actor)
            {runner.Return(runner.active[i]);runner.active.RemoveAt(i);}
        }
        public static void Basic(GameObject actor,Vector3 impact)
        {
            var model=actor.GetComponent<Bolt7Visual>();model?.Shot();
            HeroVfxPool.Trace(model!=null?model.Gatling:actor.transform.position,impact+Vector3.up*.25f,new Color(1,.72f,.25f),.018f,.085f);
        }
        public static void Cast(GameObject actor,int slot){actor.GetComponent<Bolt7Visual>()?.Skill(slot);}
        public static void Grenade(GameObject actor,Vector3 point,float seconds)
        {
            Warm();float elapsed=HeroVfxEvents.ReplayElapsed;
            var model=actor.GetComponent<Bolt7Visual>();Vector3 from=model!=null?model.Launcher:actor.transform.position;
            if(library!=null)
            {
                if(elapsed<.12f)Spawn(library.muzzle,from,from,.4f,elapsed);
                Spawn(library.grenade,from,point,Mathf.Max(.05f,seconds),elapsed,true,owner:actor);
            }
            else HeroVfxPool.Trace(from,point,new Color(1,.65f,.25f),.05f,.15f);
        }
        public static void Area(GameObject actor,Vector3 point,int slot)
        {
            if(slot==1){HeroVfxPool.Emit(HeroFxShape.Spark,point,point,Vector3.one*.35f,new Color(1,.7f,.3f),.2f);return;}
            Warm();float elapsed=HeroVfxEvents.ReplayElapsed;
            if(library==null)
            {
                if(runner!=null&&!runner.warned){runner.warned=true;Debug.LogWarning("BOLT-7: Epic Toon FX library missing. Use DIMENSIONS FRONT/BOLT-7/Prepare imported effects in Edit Mode.");}
                HeroVfxPool.Trace(point,point+Vector3.up*.35f,Color.yellow,.12f,.2f);return;
            }
            if(slot==3)
            {
                Spawn(library.lightningOrb,point,point,2f,elapsed);
                Spawn(library.starFog,point,point,1.8f,elapsed,false,.18f);
            }
            else if(slot==0)Spawn(library.explosion,point,point,3f,elapsed);
        }
        private void Update()
        {
            if(Time.timeScale<=0)return;
            for(int i=active.Count-1;i>=0;i--)
            {
                Part p=active[i];if(Time.time>=p.until){Return(p);active.RemoveAt(i);continue;}
                float age=Time.time-p.start;if(age<p.delay)continue;
                if(!p.go.activeSelf){p.go.SetActive(true);foreach(var s in p.systems)if(s.main.playOnAwake)s.Play(false);}
                if(p.lob){float t=Mathf.Clamp01((age-p.delay)/p.seconds);p.go.transform.position=Vector3.Lerp(p.from,p.to,t)+Vector3.up*(4*t*(1-t)*.65f);}
            }
        }
    }
}
