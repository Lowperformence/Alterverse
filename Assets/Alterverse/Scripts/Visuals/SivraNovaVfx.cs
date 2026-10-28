using System;
using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Two explicitly selected Nova families. No damage, collision or audio playback lives here.
    public sealed class SivraNovaVfx:MonoBehaviour
    {
        public const string ResourcePath="CombatVfx/SivraNova/Library";
        private const float SizeMultiplier=5f;
        [Serializable] public sealed class Effect
        {
            public GameObject prefab;
            public float radius=1f,emissionSeconds=.1f;
        }
        [Serializable] public sealed class Chain
        {
            public Effect muzzle,projectile,impact,status;
            public AudioClip castClip,impactClip,windupClip;
            public string sources;
        }
        public Chain novaSmall,nova;
        public string sourceSignature;
        private static SivraNovaVfx library;
        private static readonly List<SivraNovaVfx> shots=new(),statuses=new();
        private static bool warnedSmall,warnedNova,reportedSmall,reportedNova;
        private Chain chain;
        private GameObject owner,flight;
        private Health ownerHealth,subject;
        private HeroCombatStatus subjectStatus;
        private Transform target,follow;
        private Vector3 from,to,offset;
        private int slot,delivery;
        private float started,duration,maxDistance,stopEmissionAt,statusUntil,cycleStarted;
        private bool part,held,confirmed,arrived,finished,stopped,statusVisual;
        private ParticleSystem[] systems;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private SivraNovaVfx flightPart;
        private Action impactAudio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {library=null;shots.Clear();statuses.Clear();warnedSmall=warnedNova=reportedSmall=reportedNova=false;}
        private static Chain Look(int skill,bool report)
        {
            if(skill!=2&&skill!=3)return null;
            if(library==null){var asset=Resources.Load<GameObject>(ResourcePath);if(asset!=null)library=asset.GetComponent<SivraNovaVfx>();}
            Chain look=library!=null?(skill==2?library.novaSmall:library.nova):null;
            if(look!=null&&Valid(look.muzzle)&&Valid(look.projectile)&&Valid(look.impact))return look;
            bool warned=skill==2?warnedSmall:warnedNova;
            if(report&&!warned)
            {
                Debug.LogWarning("DIMENSIONS FRONT VFX: blue "+(skill==2?"Nova Small":"Nova")+" chain is not prepared; retaining the previous skill visual.");
                if(skill==2)warnedSmall=true;else warnedNova=true;
            }
            return null;
        }
        private static bool Valid(Effect effect)=>effect!=null&&effect.prefab!=null&&effect.radius>0&&!float.IsInfinity(effect.radius);
        public static AudioClip AudioFor(int skill,int phase)
        {
            var look=Look(skill,false);if(look==null)return null;
            return phase==0?look.castClip:phase==1?look.impactClip:phase==2?look.windupClip:null;
        }
        public static bool Cast(GameObject actor,int skill,Vector3 muzzle,float windup,float elapsed=0)
        {
            var look=Look(skill,true);if(actor==null||look==null)return false;
            // Cast is called once. Ultimate's three actual releases come from the delivery system.
            float radius=skill==3?.34f:.22f;
            Part(look.muzzle,muzzle,actor.transform.rotation,radius,elapsed,muzzle,muzzle,radius,false,actor.transform);
            return true;
        }
        public static bool Launch(GameObject actor,int skill,Vector3 start,Vector3 end,float seconds,Transform follow=null,
            int deliveryId=0,float elapsed=0)
        {
            var look=Look(skill,true);if(actor==null||look==null)return false;
            if(skill==2&&!reportedSmall||skill==3&&!reportedNova)
            {
                Debug.Log("DIMENSIONS FRONT VFX: blue "+(skill==2?"Nova Small":"Nova")+" ACTIVE; visualScale="+SizeMultiplier+
                    "; nativeProjectileRadius="+look.projectile.radius+"; "+look.sources);
                if(skill==2)reportedSmall=true;else reportedNova=true;
            }
            var root=new GameObject(skill==2?"DF_Sivra_NovaSmall_Shot":"DF_Sivra_Nova_Shot");
            var shot=root.AddComponent<SivraNovaVfx>();
            shot.chain=look;shot.slot=skill;shot.owner=actor;shot.ownerHealth=actor.GetComponent<Health>();
            shot.from=start;shot.to=end;shot.target=follow;shot.maxDistance=Vector3.Distance(start,end);
            shot.started=Time.time-Mathf.Max(0,elapsed);shot.duration=Mathf.Max(.01f,seconds);shot.delivery=deliveryId;
            shots.Add(shot);shot.Update();return true;
        }
        public static bool ConfirmHit(GameObject actor,Health victim,int skill,float elapsed=0)
        {
            var look=Look(skill,true);if(actor==null||victim==null||look==null)return false;
            Vector3 point=victim.transform.position+Vector3.up*.35f;
            SivraNovaVfx match=null;float closest=float.PositiveInfinity;
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];if(shot==null||shot.owner!=actor||shot.slot!=skill||shot.confirmed||shot.finished)continue;
                Vector3 endpoint=shot.target!=null?shot.target.position+Vector3.up*.25f:shot.to;
                float distance=(endpoint-point).sqrMagnitude;if(distance>=closest)continue;
                closest=distance;match=shot;
            }
            if(match!=null)
            {
                match.confirmed=true;match.to=match.from+Vector3.ClampMagnitude(point-match.from,match.maxDistance);
                if(match.arrived)match.FinishHit();return true;
            }
            float radius=skill==3?.44f:.30f;
            Part(look.impact,point,Quaternion.identity,radius,elapsed,point,point,radius);
            return true;
        }
        public static bool ScheduleImpactAudio(GameObject actor,int skill,Vector3 point,Action play)
        {
            if(actor==null||play==null||Look(skill,false)==null)return false;
            SivraNovaVfx match=null;float closest=float.PositiveInfinity;
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];
                if(shot==null||shot.owner!=actor||shot.slot!=skill||!shot.confirmed||shot.arrived||shot.finished||shot.impactAudio!=null)continue;
                float distance=(shot.to-point).sqrMagnitude;if(distance>=closest)continue;
                closest=distance;match=shot;
            }
            if(match==null)return false;match.impactAudio=play;return true;
        }
        public static bool SnareStatus(GameObject actor,Health victim,float elapsed=0)
        {
            var look=Look(2,true);if(actor==null||victim==null||look==null)return false;
            if(victim.IsDead||elapsed>=1.8f)return true;
            var status=victim.GetComponent<HeroCombatStatus>();
            // The gameplay hook runs after the actual slow was accepted. Buildings do not gain a cage.
            if(status==null||status.MoveMultiplier>=.999f)return true;
            bool found=false;
            foreach(var existing in statuses)
                if(existing!=null&&existing.subject==victim&&!existing.finished)
                {existing.statusUntil=Mathf.Max(existing.statusUntil,Time.time+1.8f-Mathf.Max(0,elapsed));found=true;}
            if(found)return true;
            Effect effect=Valid(look.status)?look.status:look.muzzle;
            Vector3 point=victim.transform.position+Vector3.up*.65f;
            for(int i=0;i<2;i++)
            {
                var root=Part(effect,point,Quaternion.Euler(65f,i==0?15f:105f,i==0?28f:-28f),.30f,elapsed,point,point,.32f,false,victim.transform);
                var visual=root.GetComponent<SivraNovaVfx>();visual.statusVisual=true;visual.subject=victim;visual.subjectStatus=status;
                visual.statusUntil=Time.time+1.8f-Mathf.Max(0,elapsed);statuses.Add(visual);
            }
            return true;
        }
        public static void CancelDelivery(int id)
        {if(id>0)for(int i=shots.Count-1;i>=0;i--)if(shots[i]!=null&&shots[i].delivery==id)shots[i].Cancel();}
        public static void CancelOwner(GameObject actor)
        {for(int i=shots.Count-1;i>=0;i--)if(shots[i]!=null&&shots[i].owner==actor)shots[i].Cancel();}
        private void Update()
        {
            if(finished)return;
            if(part){UpdatePart();return;}
            if(owner==null||ownerHealth!=null&&ownerHealth.IsDead){Cancel();return;}
            float age=Time.time-started;
            Vector3 endpoint=target!=null?target.position+Vector3.up*.25f:to;
            endpoint=from+Vector3.ClampMagnitude(endpoint-from,maxDistance);
            Quaternion heading=(endpoint-from).sqrMagnitude>.0001f?Quaternion.LookRotation(endpoint-from):Quaternion.identity;
            float radius=slot==3?.38f:.25f;
            if(!arrived&&flight==null)
            {
                Part(chain.muzzle,from,heading,slot==3?.38f:.24f,age,from,from,slot==3?.38f:.24f);
                flight=Part(chain.projectile,from,heading,radius,age,from,endpoint,radius,true);
                flightPart=flight.GetComponent<SivraNovaVfx>();
            }
            if(!arrived)
            {
                flight.transform.position=Vector3.Lerp(from,endpoint,Mathf.Clamp01(age/duration));flight.transform.rotation=heading;
                flightPart.Clip(from,endpoint,radius,true);
                if(age<duration)return;
                arrived=true;to=endpoint;StopFlight();
            }
            if(confirmed)FinishHit();else if(age>duration+.5f)Cancel();
        }
        private void UpdatePart()
        {
            if(follow!=null)
            {
                transform.position=follow.position+offset;
                if(statusVisual)Clip(transform.position,transform.position,.32f,false);
            }
            if(statusVisual&&(subject==null||subject.IsDead||Time.time>=statusUntil||subjectStatus==null||subjectStatus.MoveMultiplier>=.999f))
            {statusVisual=false;statuses.Remove(this);finished=true;Destroy(gameObject);return;}
            if(!held&&!stopped&&Time.time-cycleStarted>=stopEmissionAt)StopScheduledEmission();
            if(held||Time.time-started<.1f)return;
            foreach(var system in systems)if(system!=null&&system.gameObject.activeInHierarchy&&system.IsAlive(false))return;
            if(statusVisual)
            {
                foreach(var system in systems)if(system.gameObject.activeInHierarchy&&system.main.playOnAwake)system.Play(false);
                cycleStarted=Time.time;stopped=false;return;
            }
            Destroy(gameObject);
        }
        private void StopScheduledEmission()
        {
            foreach(var system in systems)if(system!=null&&system.main.playOnAwake)system.Stop(false,ParticleSystemStopBehavior.StopEmitting);
            stopped=true;
        }
        private void FinishHit()
        {
            if(finished)return;finished=true;
            float radius=slot==3?.44f:.30f;
            Part(chain.impact,to,Quaternion.identity,radius,Mathf.Max(0,Time.time-started-duration),to,to,radius);
            var play=impactAudio;impactAudio=null;
            try{play?.Invoke();}finally{shots.Remove(this);Destroy(gameObject);}
        }
        private void StopFlight()
        {
            if(flight==null)return;
            flightPart.held=false;flightPart.StopScheduledEmission();flight=null;flightPart=null;
        }
        private void Cancel(){if(finished)return;finished=true;impactAudio=null;StopFlight();shots.Remove(this);Destroy(gameObject);}
        private void OnDestroy(){if(!part)StopFlight();shots.Remove(this);statuses.Remove(this);}
        private static GameObject Part(Effect effect,Vector3 point,Quaternion rotation,float radius,float elapsed,
            Vector3 segmentStart,Vector3 segmentEnd,float clipRadius,bool moving=false,Transform follow=null)
        {
            var root=Instantiate(effect.prefab,point,rotation);root.transform.localScale=Vector3.one*(radius*SizeMultiplier/effect.radius);
            var visual=root.AddComponent<SivraNovaVfx>();visual.part=true;visual.held=moving;
            visual.started=Time.time;visual.cycleStarted=Time.time-Mathf.Max(0,elapsed);visual.stopEmissionAt=Mathf.Max(.05f,effect.emissionSeconds);
            visual.follow=follow;visual.offset=follow!=null?point-follow.position:Vector3.zero;
            visual.systems=root.GetComponentsInChildren<ParticleSystem>(true);visual.renderers=root.GetComponentsInChildren<Renderer>(true);
            visual.block=new MaterialPropertyBlock();visual.Clip(segmentStart,segmentEnd,clipRadius,moving);root.SetActive(true);
            if(elapsed>0)foreach(var system in visual.systems)
                if(system.gameObject.activeInHierarchy&&system.isPlaying){system.Simulate(elapsed,false,false,false);system.Play(false);}
            return root;
        }
        private void Clip(Vector3 start,Vector3 end,float radius,bool moving)
        {
            radius*=SizeMultiplier;
            foreach(var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetVector("_AttackClip",new Vector4(end.x,end.z,radius,moving?2f:1f));
                block.SetVector("_AttackTrail",new Vector4(start.x,start.z,end.x-start.x,end.z-start.z));
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
