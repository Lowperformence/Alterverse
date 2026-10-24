using System;
using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Visuals
{
    // Temporal Beam and Chrono Snare share the selected native lightning chain.
    // The existing cone/AoE still decides every hit; a travelling visual never applies damage.
    public sealed class TravelerLightningVfx:MonoBehaviour
    {
        public const string ResourcePath="CombatVfx/TravelerLightning/Library";
        public const float SizeMultiplier=3.5f;
        [Serializable] public sealed class Effect {public GameObject prefab;public float radius=1,emissionSeconds=.1f;}
        [Serializable] public sealed class Chain {public Effect muzzle,projectile,impact,snareNova;public AudioClip castClip,impactClip,windupClip;public string sources;}
        public Chain lightning;public string sourceSignature;
        private static TravelerLightningVfx library;private static bool warned,reported;
        private static readonly List<TravelerLightningVfx> shots=new();
        private Chain chain;private GameObject owner,flight;private Health ownerHealth;
        private TravelerLightningVfx flightPart;private ParticleSystem[] systems;private Renderer[] renderers;private MaterialPropertyBlock block;
        private Vector3 from,to,areaCenter;private float started,duration,maxDistance,startedCycle,emissionSeconds,shotWidth,areaRadius;
        private int slot,delivery;private bool part,held,stopped,arrived,confirmed,finished;private Action impactAudio;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic(){library=null;shots.Clear();warned=reported=false;}
        private static bool Selected(int skill)=>skill==0||skill==2;
        private static bool Valid(Effect e)=>e!=null&&e.prefab!=null&&e.radius>0&&!float.IsInfinity(e.radius);
        private static Chain Look(int skill,bool report=true)
        {
            if(!Selected(skill))return null;
            if(library==null){var p=Resources.Load<GameObject>(ResourcePath);if(p!=null)library=p.GetComponent<TravelerLightningVfx>();}
            var c=library?.lightning;
            if(c!=null&&Valid(c.muzzle)&&Valid(c.projectile)&&Valid(c.impact)&&c.castClip!=null&&c.impactClip!=null&&(skill!=2||Valid(c.snareNova)))return c;
            if(report&&!warned){warned=true;Debug.LogWarning("DIMENSIONS FRONT VFX: Traveler complete lightning chain is not prepared; retaining previous visuals and sounds.");}return null;
        }
        public static bool Ready(int skill)=>Look(skill,false)!=null;
        public static AudioClip AudioFor(int skill,int phase)
        {var c=Look(skill,false);return c==null?null:phase==0?c.castClip:phase==1?c.impactClip:phase==2?c.windupClip:null;}
        // The native muzzle belongs to Launch, not also to the preparation cue.
        public static bool Cast(GameObject actor,int skill)=>actor!=null&&Look(skill)!=null;
        public static bool Launch(GameObject actor,int skill,Vector3 start,Vector3 end,float seconds,float width,int deliveryId=0,float elapsed=0)
        {
            var c=Look(skill);if(actor==null||c==null||!(seconds>=0)||float.IsInfinity(seconds)||!(width>0)||float.IsInfinity(width)||!(elapsed>=0)||float.IsInfinity(elapsed))return false;
            var root=new GameObject(skill==0?"DF_Traveler_TemporalBeam_Lightning":"DF_Traveler_ChronoSnare_Lightning");var p=root.AddComponent<TravelerLightningVfx>();
            p.owner=actor;p.ownerHealth=actor.GetComponent<Health>();p.slot=skill;p.chain=c;p.from=start;p.to=end;p.maxDistance=Vector3.Distance(start,end);
            p.started=Time.time-elapsed;p.duration=Mathf.Max(.01f,seconds);p.shotWidth=width;p.delivery=deliveryId;shots.Add(p);p.Update();
            if(!reported){reported=true;Debug.Log("DIMENSIONS FRONT VFX: Traveler full lightning chain ACTIVE; visualScale="+SizeMultiplier+"; "+c.sources);}return true;
        }
        public static bool ConfirmHit(GameObject actor,Health victim,int skill,float elapsed=0)
        {
            var c=Look(skill);if(actor==null||victim==null||c==null)return false;
            // Chrono Snare has one central resolution from Area, not one explosion per victim.
            if(skill==2)return true;
            Vector3 point=victim.transform.position+Vector3.up*.35f;var match=Match(actor,skill,point,false);
            if(match!=null)
            {
                match.confirmed=true;match.to=match.from+Vector3.ClampMagnitude(point-match.from,match.maxDistance);
                if(match.arrived)match.FinishHit();return true;
            }
            // Confirmed damage may reach a client after the shot has already expired.
            Part(c.impact,point,Quaternion.identity,.22f*SizeMultiplier,.3f,elapsed,point,point,false);return true;
        }
        public static bool ResolveArea(GameObject actor,Vector3 center,float radius,Action playImpact,float elapsed=0)
        {
            var c=Look(2);if(actor==null||c==null||!(radius>0)||float.IsInfinity(radius)||!(elapsed>=0)||float.IsInfinity(elapsed))return false;
            var match=Match(actor,2,center,false);
            if(match!=null)
            {
                match.confirmed=true;match.areaCenter=center;match.areaRadius=radius;match.impactAudio=playImpact;
                if(match.arrived)match.FinishHit();return true;
            }
            Part(c.snareNova,center,Quaternion.identity,radius*SizeMultiplier,radius,elapsed,center,center,false);playImpact?.Invoke();return true;
        }
        private static TravelerLightningVfx Match(GameObject actor,int skill,Vector3 point,bool audio)
        {
            TravelerLightningVfx match=null;float nearest=float.PositiveInfinity;
            for(int i=shots.Count-1;i>=0;i--)
            {
                var p=shots[i];if(p==null||p.owner!=actor||p.slot!=skill||p.finished||(!audio&&p.confirmed)||(audio&&(!p.confirmed||p.arrived||p.impactAudio!=null)))continue;
                float distance=(p.to-point).sqrMagnitude;if(distance<nearest){nearest=distance;match=p;}
            }
            return match;
        }
        public static bool ScheduleImpactAudio(GameObject actor,int skill,Vector3 point,Action play)
        {
            if(skill!=0||actor==null||play==null||Look(skill,false)==null)return false;
            var match=Match(actor,skill,point,true);if(match==null)return false;match.impactAudio=play;return true;
        }
        public static void CancelDelivery(int id)
        {if(id>0)for(int i=shots.Count-1;i>=0;i--)if(shots[i]!=null&&shots[i].delivery==id)shots[i].Cancel();}
        public static void CancelOwner(GameObject actor)
        {for(int i=shots.Count-1;i>=0;i--)if(shots[i]!=null&&shots[i].owner==actor)shots[i].Cancel();}
        private void Update()
        {
            if(finished)return;
            if(part){UpdatePart();return;}
            if(owner==null||!owner.activeInHierarchy||ownerHealth!=null&&ownerHealth.IsDead){Cancel();return;}
            float age=Time.time-started;Quaternion heading=(to-from).sqrMagnitude>.0001f?Quaternion.LookRotation(to-from):Quaternion.identity;
            if(!arrived&&flight==null&&age<duration)
            {
                float muzzleRadius=(slot==0?.18f:.30f)*SizeMultiplier;
                Part(chain.muzzle,from,heading,muzzleRadius,muzzleRadius,age,from,from,false);
                float visual=(slot==0?.18f:.24f)*SizeMultiplier;
                flight=Part(chain.projectile,from,heading,visual,shotWidth,age,from,to,true);flightPart=flight.GetComponent<TravelerLightningVfx>();
            }
            if(!arrived)
            {
                if(flight!=null)
                {
                    flight.transform.position=Vector3.Lerp(from,to,Mathf.Clamp01(age/duration));
                    flight.transform.rotation=heading;flightPart.SetClip(from,to,shotWidth,true);
                }
                if(age<duration)return;arrived=true;StopFlight();
            }
            if(confirmed)FinishHit();else if(age>duration+.5f)Cancel();
        }
        private void UpdatePart()
        {
            if(!held&&!stopped&&Time.time-startedCycle>=emissionSeconds)StopEmission();
            if(held)
            {
                bool alive=false;foreach(var ps in systems)if(ps!=null&&ps.gameObject.activeInHierarchy&&ps.IsAlive(false)){alive=true;break;}
                if(!alive)foreach(var ps in systems)if(ps!=null&&ps.gameObject.activeInHierarchy&&ps.main.playOnAwake)ps.Play(false);
                return;
            }
            if(Time.time-started<.1f)return;
            foreach(var p in systems)if(p!=null&&p.gameObject.activeInHierarchy&&p.IsAlive(false))return;
            Destroy(gameObject);
        }
        private void FinishHit()
        {
            if(finished)return;finished=true;
            var point=slot==2?areaCenter:to;float visual=slot==2?areaRadius*SizeMultiplier:.22f*SizeMultiplier;
            float clip=slot==2?areaRadius:shotWidth;
            Part(slot==2?chain.snareNova:chain.impact,point,Quaternion.identity,visual,clip,Mathf.Max(0,Time.time-started-duration),point,point,false);
            var audio=impactAudio;impactAudio=null;try{audio?.Invoke();}finally{shots.Remove(this);Destroy(gameObject);}
        }
        private void StopFlight(){if(flight==null)return;flightPart.held=false;flightPart.StopEmission();flight= null;flightPart=null;}
        private void StopEmission(){foreach(var p in systems)if(p!=null&&p.main.playOnAwake)p.Stop(false,ParticleSystemStopBehavior.StopEmitting);stopped=true;}
        private void Cancel(){if(finished)return;finished=true;impactAudio=null;StopFlight();shots.Remove(this);Destroy(gameObject);}
        private void OnDestroy(){if(!part)StopFlight();shots.Remove(this);}
        private void SetClip(Vector3 start,Vector3 end,float radius,bool moving)
        {
            foreach(var r in renderers)
            {
                r.GetPropertyBlock(block);block.SetVector("_AttackClip",new Vector4(end.x,end.z,radius,moving?2:1));
                block.SetVector("_AttackTrail",new Vector4(start.x,start.z,end.x-start.x,end.z-start.z));r.SetPropertyBlock(block);
            }
        }
        private static GameObject Part(Effect e,Vector3 point,Quaternion rotation,float visualRadius,float clipRadius,float elapsed,Vector3 start,Vector3 end,bool moving)
        {
            var root=Instantiate(e.prefab,point,rotation);root.name="DF_Traveler_Lightning_"+(moving?"Flight":"Burst");root.transform.localScale=Vector3.one*(visualRadius/e.radius);
            var p=root.AddComponent<TravelerLightningVfx>();p.part=true;p.held=moving;p.started=Time.time;p.startedCycle=Time.time-Mathf.Max(0,elapsed);p.emissionSeconds=Mathf.Max(.05f,e.emissionSeconds);
            p.systems=root.GetComponentsInChildren<ParticleSystem>(true);p.renderers=root.GetComponentsInChildren<Renderer>(true);p.block=new MaterialPropertyBlock();
            p.SetClip(start,end,clipRadius,moving);
            root.SetActive(true);
            if(elapsed>0)foreach(var system in p.systems)
                if(system.gameObject.activeInHierarchy&&system.isPlaying){system.Simulate(elapsed,false,false,false);if(system.particleCount>0||moving||elapsed<p.emissionSeconds)system.Play(false);}
            return root;
        }
    }
}
