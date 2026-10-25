using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed class Bolt7Vfx:MonoBehaviour
    {
        private sealed class Part{public GameObject go,source,owner;public ParticleSystem[] systems;public AudioSource[] sounds;public int selectedAudioIndex;public AudioClip[] originalClips;public float[] gains;public bool[] soundStarted;public Renderer[] renderers;public MaterialPropertyBlock clipBlock;public float until,start,seconds,delay;public Vector3 from,to;public bool lob,travel,emissionStopped;}
        private static Bolt7Vfx runner;
        private static Bolt7VfxLibrary library;
        private static AudioClip nullfieldSound;
        private readonly List<Part> active=new(32);
        private readonly Dictionary<GameObject,Queue<Part>> pool=new();
        private bool warned;
        private const float GrenadeVisualScale=.70f;
        private const float BasicVisualScale=1f/3f;
        private static readonly Color RamColor=new Color(1f,.68f,.22f,.95f);
        public static void Warm()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            if(runner!=null)return;
            runner=new GameObject("DF_Bolt7VfxPool").AddComponent<Bolt7Vfx>();
            var asset=Resources.Load<GameObject>("Bolt7/Vfx/Library");library=asset!=null?asset.GetComponent<Bolt7VfxLibrary>():null;
            if(library!=null)foreach(var p in new[]{library.muzzle,library.grenade,library.explosion,library.lightningOrb,library.starFog,library.heavyPunch,library.lightningNova,library.bulletMuzzle,library.bulletProjectile,library.bulletImpact})
                if(p!=null){var a=runner.Take(p);var b=runner.Take(p);runner.Return(a);runner.Return(b);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){runner=null;library=null;nullfieldSound=null;}
        private Part Take(GameObject source)
        {
            if(pool.TryGetValue(source,out var q)&&q.Count>0)return q.Dequeue();
            var go=Instantiate(source,transform,false);go.SetActive(false);
            var selected=go.AddComponent<AudioSource>();selected.playOnAwake=false;selected.spatialBlend=0;selected.volume=1f;
            var sounds=go.GetComponentsInChildren<AudioSource>(true);var gains=new float[sounds.Length];var originalClips=new AudioClip[sounds.Length];
            for(int i=0;i<sounds.Length;i++){gains[i]=Mathf.Max(.5f,sounds[i].volume);originalClips[i]=sounds[i].clip;sounds[i].playOnAwake=false;}
            return new Part{go=go,source=source,systems=go.GetComponentsInChildren<ParticleSystem>(true),sounds=sounds,selectedAudioIndex=System.Array.IndexOf(sounds,selected),originalClips=originalClips,gains=gains,soundStarted=new bool[sounds.Length],renderers=go.GetComponentsInChildren<Renderer>(true),clipBlock=new MaterialPropertyBlock()};
        }
        private void Return(Part p)
        {
            foreach(var s in p.systems)s.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(var sound in p.sounds)if(sound!=null)sound.Stop();
            System.Array.Clear(p.soundStarted,0,p.soundStarted.Length);
            p.owner=null;p.go.SetActive(false);if(!pool.TryGetValue(p.source,out var q))pool[p.source]=q=new Queue<Part>();q.Enqueue(p);
        }
        private static void Spawn(GameObject source,Vector3 from,Vector3 to,float seconds,float elapsed=0,bool lob=false,float delay=0,GameObject owner=null,float visualScale=1f,Quaternion rotation=default,float clipRadius=0,bool travel=false,AudioClip selectedSound=null,bool muteNativeSound=false)
        {
            Warm();if(runner==null||source==null)return;
            if(elapsed>=seconds+delay)return;
            var p=runner.Take(source);p.emissionStopped=false;p.travel=travel;p.owner=owner;p.from=from;p.to=to;p.seconds=seconds;p.start=Time.time-elapsed;p.delay=delay;p.lob=lob;p.until=p.start+delay+seconds;
            for(int i=0;i<p.sounds.Length;i++)p.sounds[i].clip=i==p.selectedAudioIndex?selectedSound:muteNativeSound?null:p.originalClips[i];
            p.go.transform.position=from;p.go.transform.rotation=rotation.x==0&&rotation.y==0&&rotation.z==0&&rotation.w==0?Quaternion.identity:rotation;
            p.go.transform.localScale=source.transform.localScale*(Bolt7Balance.FxScale*visualScale);
            foreach(var renderer in p.renderers)
            {renderer.GetPropertyBlock(p.clipBlock);p.clipBlock.SetVector("_AttackClip",new Vector4(from.x,from.z,clipRadius,clipRadius>0?1f:0f));renderer.SetPropertyBlock(p.clipBlock);}
            runner.active.Add(p);p.go.SetActive(elapsed>=delay);
            if(elapsed>=delay)foreach(var s in p.systems){if(s.main.playOnAwake){s.Play(false);if(elapsed>delay)s.Simulate(elapsed-delay,false,false,false);}}
        }
        public static void CancelOwner(GameObject actor)
        {
            if(runner==null)return;
            for(int i=runner.active.Count-1;i>=0;i--)if(runner.active[i].lob&&runner.active[i].owner==actor)
            {runner.Return(runner.active[i]);runner.active.RemoveAt(i);}
        }
        public static void Basic(GameObject actor,Vector3 impact,float seconds=.08f)
        {
            var model=actor.GetComponent<Bolt7Visual>();model?.Shot();
            Vector3 from=model!=null?model.Gatling:actor.transform.position,to=impact+Vector3.up*.25f;
            Warm();float elapsed=HeroVfxEvents.ReplayElapsed;
            if(library!=null&&library.bulletProjectile!=null)
            {
                Quaternion face=Quaternion.LookRotation(to-from);
                if(elapsed<.12f)Spawn(library.bulletMuzzle,from,from,.15f,elapsed,owner:actor,visualScale:BasicVisualScale,rotation:face);
                Spawn(library.bulletProjectile,from,to,Mathf.Max(.04f,seconds),elapsed,owner:actor,visualScale:BasicVisualScale,rotation:face,travel:true);
            }
            else HeroVfxPool.Trace(from,to,new Color(1,.72f,.25f),.018f*BasicVisualScale,.085f);
        }
        public static void BasicImpact(GameObject actor,Vector3 point)
        {Warm();if(library!=null)Spawn(library.bulletImpact,point,point,.4f,HeroVfxEvents.ReplayElapsed,owner:actor,visualScale:BasicVisualScale);}
        public static void Cast(GameObject actor,int slot)
        {
            actor.GetComponent<Bolt7Visual>()?.Skill(slot);
            if(slot!=1)return;
            Vector3 point=actor.transform.position+actor.transform.forward*.45f;
            HeroVfxPool.Emit(HeroFxShape.Halo,point,point,Vector3.one*.28f,RamColor,Bolt7Balance.RamWindup,
                HeroFxMotion.AttachedJet,Quaternion.identity,actor,warning:true,follow:actor.transform,cancelOnOwnerDeath:true);
        }
        private static Vector3 Forward(GameObject actor)
        {Vector3 f=actor.transform.forward;f.y=0;return f.sqrMagnitude>.001f?f.normalized:Vector3.forward;}
        public static void RamDash(GameObject actor,Vector3 from,Vector3 to,float duration)
        {
            Vector3 delta=to-from;delta.y=0;if(delta.sqrMagnitude<.0001f)return;
            Vector3 side=Vector3.Cross(Vector3.up,delta.normalized)*.23f;
            for(int i=-1;i<=1;i+=2)
            {
                Vector3 tip=to+side*i;tip.y=.12f;
                // Streak extends 1.4 units backwards; the shared pool adds its 1.5 presentation scale.
                HeroVfxPool.Emit(HeroFxShape.Streak,tip,tip,new Vector3(.20f,.16f,delta.magnitude/2.1f),
                    RamColor,Mathf.Max(.20f,duration),HeroFxMotion.Hold,Quaternion.LookRotation(delta),actor,cancelOnOwnerDeath:true);
            }
        }
        private static void RamImpact(GameObject actor,Vector3 point)
        {
            Vector3 forward=Forward(actor),contact=actor.transform.position+forward*.35f,ground=new Vector3(contact.x,.07f,contact.z);
            Vector3 cone=new Vector3(point.x,.09f,point.z);
            // RadialPulse bypasses the decorative 1.5 enlargement: 3m and 70 degrees
            // match the same origin/direction used for every authoritative melee hit.
            HeroVfxPool.Emit(HeroFxShape.RamSweep,cone,cone,Vector3.one*Bolt7Balance.RamRange,RamColor,.32f,
                HeroFxMotion.RadialPulse,Quaternion.LookRotation(forward),actor);
            HeroVfxPool.Emit(HeroFxShape.PressureWave,ground,ground,Vector3.one*.65f,RamColor,.32f,
                HeroFxMotion.RadialPulse,Quaternion.identity,actor);
            HeroVfxPool.Emit(HeroFxShape.ContactStar,contact,contact,Vector3.one*.34f,new Color(1f,.91f,.62f),.18f,
                HeroFxMotion.Contract,Quaternion.LookRotation(Vector3.up),actor);
            Warm();
            if(library!=null&&library.heavyPunch!=null)
            {
                float fit=Mathf.Min(1f,Bolt7Balance.RamReach*.65f/(Mathf.Max(.01f,library.heavyPunchRadius)*Bolt7Balance.FxScale));
                Spawn(library.heavyPunch,contact,contact,1.2f,HeroVfxEvents.ReplayElapsed,owner:actor,visualScale:fit,rotation:Quaternion.LookRotation(forward));
            }
            for(int i=-2;i<=2;i++)
            {
                Vector3 ray=Quaternion.Euler(0,i*12f,0)*forward;
                HeroVfxPool.Emit(HeroFxShape.Spark,contact,contact+ray*.45f,new Vector3(.14f,.14f,.24f),RamColor,.24f,
                    HeroFxMotion.Drift,Quaternion.LookRotation(ray),actor);
            }
        }
        public static void Grenade(GameObject actor,Vector3 point,float seconds,int slot=0)
        {
            Warm();float elapsed=HeroVfxEvents.ReplayElapsed;float size=slot==2?GrenadeVisualScale*Bolt7Balance.ImpulseRadius/Bolt7Balance.GrenadeRadius:GrenadeVisualScale;
            var model=actor.GetComponent<Bolt7Visual>();Vector3 from=model!=null?model.Launcher:actor.transform.position;
            if(library!=null)
            {
                if(elapsed<.12f)Spawn(library.muzzle,from,from,.4f,elapsed,owner:actor,visualScale:size);
                Spawn(library.grenade,from,point,Mathf.Max(.05f,seconds),elapsed,true,owner:actor,visualScale:size);
            }
            else HeroVfxPool.Trace(from,point,new Color(1,.65f,.25f),.05f,.15f);
        }
        public static void Area(GameObject actor,Vector3 point,int slot)
        {
            if(slot==1){RamImpact(actor,point);return;}
            Warm();float elapsed=HeroVfxEvents.ReplayElapsed;
            if(library==null)
            {
                if(runner!=null&&!runner.warned){runner.warned=true;Debug.LogWarning("BOLT-7: Epic Toon FX library missing. Use DIMENSIONS FRONT/BOLT-7/Prepare imported effects in Edit Mode.");}
                HeroVfxPool.Trace(point,point+Vector3.up*.35f,Color.yellow,.12f,.2f);return;
            }
            if(slot==3)
            {
                if(nullfieldSound==null)nullfieldSound=Resources.Load<AudioClip>("Audio/Heroes/Bolt7/Nullfield_Selected");
                Spawn(library.lightningOrb,point,point,2f,elapsed,owner:actor,selectedSound:nullfieldSound,muteNativeSound:nullfieldSound!=null);
                if(library.lightningNova!=null)Spawn(library.lightningNova,point,point,2f,elapsed,owner:actor,
                    visualScale:Bolt7Balance.UltimateRadius/Mathf.Max(.01f,library.novaRadius),clipRadius:Bolt7Balance.UltimateRadius,muteNativeSound:nullfieldSound!=null);
                Spawn(library.starFog,point,point,1.8f,elapsed,false,.18f,owner:actor,muteNativeSound:nullfieldSound!=null);
            }
            else if(slot==0||slot==2)
            {
                float size=slot==2?GrenadeVisualScale*Bolt7Balance.ImpulseRadius/Bolt7Balance.GrenadeRadius:GrenadeVisualScale;
                Spawn(library.explosion,point,point,3f,elapsed,owner:actor,visualScale:size);
                if(slot==2)HeroVfxPool.Emit(HeroFxShape.PressureWave,point+Vector3.up*.06f,point,Vector3.one*Bolt7Balance.ImpulseRadius,
                    new Color(.45f,.85f,1f,.9f),.35f,HeroFxMotion.RadialPulse,Quaternion.identity,actor);
            }
        }
        private static void Audio(Part part,float age)
        {
            float volume=Alterverse.Profile.PlayerProfile.MasterVolume*Alterverse.Profile.PlayerProfile.SfxVolume*(library!=null?library.nativeAudioGain:1.4f);
            foreach(var sound in part.sounds)if(sound!=null&&Time.timeScale<=0)sound.Pause();
            if(Time.timeScale<=0)return;
            var camera=Camera.main;float attenuation=1f,pan=0;
            if(camera!=null)
            {
                Vector3 screen=camera.WorldToViewportPoint(part.go.transform.position);pan=Mathf.Clamp((screen.x-.5f)*1.1f,-.65f,.65f);
                if(screen.z<=0||screen.x<-.15f||screen.x>1.15f||screen.y<-.15f||screen.y>1.15f)attenuation=0;
            }
            for(int i=0;i<part.sounds.Length;i++)
            {
                var sound=part.sounds[i];if(sound==null||sound.clip==null||!sound.gameObject.activeInHierarchy)continue;
                sound.volume=Mathf.Clamp01(part.gains[i]*volume*attenuation);sound.panStereo=pan;
                if(part.soundStarted[i]){sound.UnPause();continue;}
                var trigger=sound.GetComponent<ParticleSystem>();
                if(trigger!=null&&(!trigger.main.playOnAwake||trigger.main.startDelay.constantMax>0)&&trigger.particleCount==0)continue;
                float soundAge=trigger!=null&&!trigger.main.playOnAwake?0f:Mathf.Max(0,age-(trigger!=null?trigger.main.startDelay.constantMax:0f));
                part.soundStarted[i]=true;if(soundAge>=sound.clip.length&&!sound.loop)continue;
                sound.time=sound.loop?soundAge%Mathf.Max(.01f,sound.clip.length):soundAge;sound.Play();
            }
        }
        private void Update()
        {
            if(Time.timeScale<=0){foreach(var part in active)Audio(part,0);return;}
            for(int i=active.Count-1;i>=0;i--)
            {
                Part p=active[i];
                if(Time.time>=p.until)
                {
                    if(!p.emissionStopped){p.emissionStopped=true;foreach(var ps in p.systems)if(ps.main.playOnAwake)ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);foreach(var a in p.sounds)if(a.loop)a.Stop();}
                    bool alive=false;if(!p.lob&&!p.travel&&Time.time<p.until+8f)
                    {foreach(var ps in p.systems)if(ps.IsAlive(false))alive=true;foreach(var a in p.sounds)if(a.isPlaying&&!a.loop)alive=true;}
                    if(!alive){Return(p);active.RemoveAt(i);continue;}
                }
                float age=Time.time-p.start;if(age<p.delay)continue;
                if(!p.go.activeSelf){p.go.SetActive(true);foreach(var s in p.systems)if(s.main.playOnAwake)s.Play(false);}
                Audio(p,age-p.delay);
                if(p.lob||p.travel){float t=Mathf.Clamp01((age-p.delay)/p.seconds);p.go.transform.position=Vector3.Lerp(p.from,p.to,t)+Vector3.up*(p.lob?4*t*(1-t)*.65f:0);}
            }
        }
    }
}
