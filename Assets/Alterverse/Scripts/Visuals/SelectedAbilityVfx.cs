using System;
using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Selected presentation only. No damage, stun, movement, cooldown or target authority.
    public sealed class SelectedAbilityVfx:MonoBehaviour
    {
        public const string ResourcePath="CombatVfx/SelectedAbilities/Library";
        public const int Smoke=0,Blood=1,Strike=2,Circle=3,Fire=4,Stars=5;
        public const float SizeMultiplier=3.5f;
        [Serializable] public sealed class Entry
        {
            public GameObject prefab;public float nativeRadius=1,emissionSeconds=.1f;
            public string source;public AudioClip sound;
        }
        public Entry smoke,blood,strike,circle,fire,stars;public string sourceSignature;
        private static SelectedAbilityVfx library;
        private static readonly bool[] warned=new bool[6],reported=new bool[6];
        private static readonly List<SelectedAbilityVfx> active=new();
        private struct EchoFrame {public Vector3 origin,direction;public float range,angle,until;}
        private static readonly Dictionary<GameObject,EchoFrame> echoes=new();
        private ParticleSystem[] systems;private Renderer[] renderers;private MaterialPropertyBlock block;
        private GameObject owner;private Health ownerHealth,subject;private HeroCombatStatus status;
        private Transform follow;private Vector3 offset,from,to,clipCenter,clipStart,clipEnd;
        private float started,emissionSeconds,holdUntil,travelSeconds,radius,clipMode,cosine;
        private int kind,delivery;private bool live,held,moving,stopped,rootObserved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {library=null;active.Clear();echoes.Clear();for(int i=0;i<6;i++)warned[i]=reported[i]=false;}
        private static Entry Look(int kind,bool report=true)
        {
            if(kind<0||kind>5)return null;
            if(library==null){var p=Resources.Load<GameObject>(ResourcePath);if(p!=null)library=p.GetComponent<SelectedAbilityVfx>();}
            var e=library==null?null:kind==Smoke?library.smoke:kind==Blood?library.blood:kind==Strike?library.strike:kind==Circle?library.circle:kind==Fire?library.fire:library.stars;
            if(e!=null&&e.prefab!=null&&e.nativeRadius>0&&!float.IsInfinity(e.nativeRadius))return e;
            if(report&&!warned[kind]){Debug.LogWarning("DIMENSIONS FRONT VFX: selected ability kind "+kind+" not prepared; retaining previous presentation.");warned[kind]=true;}
            return null;
        }
        public static bool Ready(int kind)=>Look(kind,false)!=null;
        public static AudioClip StarsClip()=>Look(Stars,false)?.sound;
        private static bool Valid(float v)=>v>=0&&!float.IsNaN(v)&&!float.IsInfinity(v);
        public static bool TryArea(int kind,Vector3 center,float radius,float elapsed=0)
        {
            if(kind!=Circle&&kind!=Fire||!(radius>0)||!Valid(radius)||!Valid(elapsed))return false;
            var e=Look(kind);if(e==null)return false;
            // A magic circle is an enclosing boundary. Its complete rim must fit the area;
            // exploding interiors can be enlarged independently and clipped at the real boundary.
            float visual=kind==Circle?radius:radius*SizeMultiplier;
            Spawn(e,kind,center,Quaternion.identity,visual,radius,elapsed,center,center,1);
            return true;
        }
        public static void RememberEcho(GameObject actor,Vector3 origin,Vector3 direction,float range,float angle)
        {
            if(actor==null)return;direction.y=0;
            if(direction.sqrMagnitude<.0001f)direction=Vector3.forward;
            direction=direction*(1f/Mathf.Max(.0001f,direction.magnitude));
            echoes[actor]=new EchoFrame{origin=origin,direction=direction,range=range,angle=angle,until=Time.time+2};
        }
        public static bool TryEchoImpact(GameObject actor,Vector3 point,float elapsed=0)
        {
            if(actor==null||!Valid(elapsed))return false;var e=Look(Blood);if(e==null)return false;
            if(!echoes.TryGetValue(actor,out var frame)||Time.time>frame.until)return false;
            var p=Spawn(e,Blood,point,Quaternion.identity,Mathf.Min(.8f*SizeMultiplier,frame.range),frame.range,elapsed,frame.origin,frame.origin+frame.direction*frame.range,3);
            p.cosine=(float)Math.Cos(frame.angle*.5f*Math.PI/180f);p.UpdateClip();return true;
        }
        public static bool TryRiftScar(GameObject actor,Vector3 from,Vector3 to,float duration,float width,int delivery,float elapsed=0)
        {
            if(actor==null||!Valid(elapsed)||!Valid(duration)||!(width>0)||!Valid(width))return false;
            var e=Look(Strike);if(e==null)return false;
            if(elapsed>=Mathf.Max(.01f,duration))return true;
            var direction=to-from;var rotation=direction.sqrMagnitude>.0001f?Quaternion.LookRotation(direction):Quaternion.identity;
            var p=Spawn(e,Strike,from,rotation,width*SizeMultiplier,width,elapsed,from,to,2);
            p.owner=actor;p.ownerHealth=actor.GetComponent<Health>();p.moving=true;p.held=true;p.from=from;p.to=to;p.travelSeconds=Mathf.Max(.01f,duration);p.delivery=delivery;
            p.transform.position=Vector3.Lerp(from,to,Mathf.Clamp01(elapsed/p.travelSeconds));return true;
        }
        public static bool JetSmoke(GameObject actor,Transform nozzle,Vector3 fallback,Quaternion rotation,Vector3 pathEnd,float duration,float elapsed=0)
        {
            if(actor==null||!Valid(duration)||!Valid(elapsed))return false;var e=Look(Smoke);if(e==null)return false;
            if(elapsed>=duration)return true;
            var point=nozzle!=null?nozzle.position:fallback;
            var p=Spawn(e,Smoke,point,rotation,.16f*SizeMultiplier,.16f*SizeMultiplier,elapsed,point,pathEnd,2);
            p.owner=actor;p.ownerHealth=actor.GetComponent<Health>();p.follow=nozzle!=null?nozzle:actor.transform;
            p.offset=point-p.follow.position;p.held=true;p.holdUntil=Time.time+duration-elapsed;return true;
        }
        public static bool TimmyHold(GameObject actor,Health target,float duration,float elapsed=0)
        {
            if(actor==null||target==null||!Valid(duration)||!Valid(elapsed))return false;
            for(int i=active.Count-1;i>=0;i--)
            {
                var p=active[i];if(p==null||p.kind!=Stars||p.owner!=actor||p.subject!=target)continue;
                if(duration>0&&!p.held)continue;
                if(duration<=0)p.Release();else p.holdUntil=Time.time+Mathf.Max(0,duration-elapsed);
                return true;
            }
            if(duration<=0||target.IsDead||!target.gameObject.activeInHierarchy||elapsed>=duration)return true;
            var e=Look(Stars);if(e==null)return false;
            var point=target.transform.position+Vector3.up*1.05f;
            var part=Spawn(e,Stars,point,Quaternion.identity,.25f*SizeMultiplier,.25f*SizeMultiplier,elapsed,point,point,1);
            part.owner=actor;part.ownerHealth=actor.GetComponent<Health>();part.subject=target;part.status=target.GetComponent<HeroCombatStatus>();
            part.follow=target.transform;part.offset=Vector3.up*1.05f;part.held=true;part.holdUntil=Time.time+duration-elapsed;
            part.rootObserved=part.status!=null&&(part.status.Rooted||part.status.Stunned);return true;
        }
        public static void CancelSmoke(GameObject actor)
        {for(int i=active.Count-1;i>=0;i--)if(active[i]!=null&&active[i].owner==actor&&active[i].kind==Smoke)active[i].Release();}
        public static void CancelDelivery(int id)
        {if(id>0)for(int i=active.Count-1;i>=0;i--)if(active[i]!=null&&active[i].delivery==id&&active[i].moving)active[i].Release();}
        public static void CancelOwner(GameObject actor)
        {for(int i=active.Count-1;i>=0;i--)if(active[i]!=null&&active[i].owner==actor)active[i].Release();if(actor!=null)echoes.Remove(actor);}
        private static SelectedAbilityVfx Spawn(Entry entry,int kind,Vector3 point,Quaternion rotation,float visualRadius,float clipRadius,float elapsed,Vector3 start,Vector3 end,float clipMode)
        {
            var g=Instantiate(entry.prefab,point,rotation);g.name="DF_Selected_"+kind;
            g.transform.localScale=Vector3.one*(visualRadius/entry.nativeRadius);
            var p=g.AddComponent<SelectedAbilityVfx>();p.live=true;p.kind=kind;p.started=Time.time-elapsed;p.emissionSeconds=Mathf.Max(.05f,entry.emissionSeconds);
            p.radius=clipRadius;p.clipMode=clipMode;p.clipCenter=clipMode==3?start:end;p.clipStart=start;p.clipEnd=end;
            p.systems=g.GetComponentsInChildren<ParticleSystem>(true);p.renderers=g.GetComponentsInChildren<Renderer>(true);p.block=new MaterialPropertyBlock();
            p.UpdateClip();active.Add(p);g.SetActive(true);
            if(elapsed>0)foreach(var system in p.systems)
                if(system.gameObject.activeInHierarchy&&system.isPlaying){system.Simulate(elapsed,false,false,false);if(system.particleCount>0||elapsed<p.emissionSeconds)system.Play(false);}
            if(!reported[kind]){Debug.Log("DIMENSIONS FRONT VFX: selected kind "+kind+" ACTIVE; scale="+SizeMultiplier+"; source="+entry.source);reported[kind]=true;}
            return p;
        }
        private void LateUpdate()
        {
            if(!live)return;float age=Time.time-started;
            if(held&&(owner==null||!owner.activeInHierarchy||ownerHealth!=null&&ownerHealth.IsDead))Release();
            if(held&&kind==Stars)
            {
                if(status!=null&&(status.Rooted||status.Stunned))rootObserved=true;
                if(subject==null||subject.IsDead||!subject.gameObject.activeInHierarchy||rootObserved&&(status==null||!status.Rooted&&!status.Stunned))Release();
            }
            if(moving)
            {
                transform.position=Vector3.Lerp(from,to,Mathf.Clamp01(age/travelSeconds));
                if(age>=travelSeconds)Release();
            }
            else if(follow!=null)
            {
                transform.position=follow.position+offset;
                if(kind==Stars){clipCenter=clipStart=clipEnd=transform.position;UpdateClip();}
                if(held&&Time.time>=holdUntil)Release();
            }
            if(!held&&!stopped&&age>=emissionSeconds)StopEmission();
            if(age<.1f)return;
            foreach(var system in systems)if(system!=null&&system.gameObject.activeInHierarchy&&system.IsAlive(false))return;
            if(held)
            {
                // Repeat only a still-active smoke/held-status composition, never a completed attack.
                foreach(var system in systems)if(system!=null&&system.gameObject.activeInHierarchy&&system.main.playOnAwake)system.Play(false);
                return;
            }
            active.Remove(this);Destroy(gameObject);
        }
        private void Release()
        {
            if(!live)return;held=false;moving=false;follow=null;StopEmission();
            // Status stars must stop indicating a hold immediately; residual smoke/strike tails may fade.
            if(kind==Stars){foreach(var renderer in renderers)renderer.enabled=false;}
        }
        private void StopEmission()
        {if(stopped)return;foreach(var system in systems)if(system!=null&&system.main.playOnAwake)system.Stop(false,ParticleSystemStopBehavior.StopEmitting);stopped=true;}
        private void UpdateClip()
        {
            var delta=clipEnd-clipStart;
            foreach(var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);block.SetVector("_AttackClip",new Vector4(clipCenter.x,clipCenter.z,radius,clipMode));
                block.SetVector("_AttackTrail",new Vector4(clipStart.x,clipStart.z,delta.x,delta.z));
                block.SetVector("_AttackCone",new Vector4(cosine,0,0,0));renderer.SetPropertyBlock(block);
            }
        }
        private void OnDestroy(){active.Remove(this);}
    }
}
