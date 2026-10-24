using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    // The library prefab stores the three native parts; live sessions only present a shot.
    public sealed class SmallBulletVfx : MonoBehaviour
    {
        public const string ResourcePath="CombatVfx/SmallBullet/Library";
        private const float SizeMultiplier=2.5f;
        public GameObject muzzle,projectile,impact;
        public float muzzleRadius=1,projectileRadius=1,impactRadius=1;
        public string sourceSignature;
        private static SmallBulletVfx library;
        private static bool warned,reported;
        private static readonly List<SmallBulletVfx> active=new();
        private GameObject owner,flight;
        private Health ownerHealth;
        private Transform target;
        private Vector3 from,to;
        private float started,duration,maxDistance;
        private int delivery;
        private bool confirmed,arrived,tail,finished,held;
        private ParticleSystem[] systems;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private SmallBulletVfx flightVisual;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic(){library=null;warned=false;reported=false;active.Clear();}
        private static bool Ready()
        {
            if(library==null){var asset=Resources.Load<GameObject>(ResourcePath);if(asset!=null)library=asset.GetComponent<SmallBulletVfx>();}
            bool ready=library!=null&&library.muzzle!=null&&library.projectile!=null&&library.impact!=null&&
                ValidRadius(library.muzzleRadius)&&ValidRadius(library.projectileRadius)&&ValidRadius(library.impactRadius);
            if(!ready&&!warned)
            {
                Debug.LogWarning("DIMENSIONS FRONT VFX: Small Bullet chain is not prepared; retaining the original attack visual.");
                warned=true;
            }
            return ready;
        }
        private static bool ValidRadius(float value)=>value>0&&!float.IsInfinity(value);
        public static bool Launch(GameObject actor,Vector3 start,Vector3 end,float seconds,Transform follow=null,
            int deliveryId=0,float elapsed=0,bool confirmedHit=false,float delay=0)
        {
            if(actor==null||!Ready())return false;
            if(!reported){Debug.Log("DIMENSIONS FRONT VFX: Small Bullet chain ACTIVE; "+library.sourceSignature);reported=true;}
            var root=new GameObject("DF_SmallBullet_Shot");var shot=root.AddComponent<SmallBulletVfx>();
            shot.owner=actor;shot.ownerHealth=actor.GetComponent<Health>();shot.from=start;shot.to=end;shot.target=follow;
            shot.maxDistance=Vector3.Distance(start,end);shot.duration=Mathf.Max(.01f,seconds);
            shot.started=Time.time+delay-Mathf.Max(0,elapsed);shot.delivery=deliveryId;shot.confirmed=confirmedHit;
            active.Add(shot);
            // Present immediately on release; delayed guard burst rounds are started by Update.
            shot.Update();return true;
        }
        public static bool ConfirmHit(GameObject actor,Vector3 point)
        {
            if(!Ready())return false;
            SmallBulletVfx match=null;float closest=float.PositiveInfinity;
            for(int i=active.Count-1;i>=0;i--)
            {
                var shot=active[i];if(shot==null||shot.tail||shot.owner!=actor||shot.confirmed)continue;
                Vector3 endpoint=shot.target!=null?shot.target.position+Vector3.up*.25f:shot.to;
                float distance=(endpoint-point).sqrMagnitude;
                if(distance>=closest)continue;
                closest=distance;match=shot;
            }
            if(match!=null)
            {
                match.confirmed=true;
                match.to=match.from+Vector3.ClampMagnitude(point-match.from,match.maxDistance);
                if(match.arrived)match.FinishHit();return true;
            }
            // A confirmed contact can be replayed even when its release packet was lost.
            Part(library.impact,point,Quaternion.identity,.18f/library.impactRadius,0,point,point,.18f);
            return true;
        }
        public static void CancelDelivery(int id)
        {if(id>0)for(int i=active.Count-1;i>=0;i--)if(active[i]!=null&&!active[i].tail&&active[i].delivery==id)active[i].Cancel();}
        public static void CancelOwner(GameObject actor)
        {for(int i=active.Count-1;i>=0;i--)if(active[i]!=null&&!active[i].tail&&active[i].owner==actor)active[i].Cancel();}
        private void Update()
        {
            if(finished)return;
            if(tail){if(held||Time.time<started)return;foreach(var ps in systems)if(ps!=null&&ps.gameObject.activeInHierarchy&&ps.IsAlive(false))return;Destroy(gameObject);return;}
            float age=Time.time-started;if(age<0)return;
            if(owner==null||ownerHealth!=null&&ownerHealth.IsDead){Cancel();return;}
            Vector3 end=target!=null?target.position+Vector3.up*.25f:to;
            end=from+Vector3.ClampMagnitude(end-from,maxDistance);
            Quaternion direction=(end-from).sqrMagnitude>.0001f?Quaternion.LookRotation(end-from):Quaternion.identity;
            if(!arrived&&flight==null)
            {
                Part(library.muzzle,from,direction,.12f/library.muzzleRadius,age,from,from,.12f);
                flight=Part(library.projectile,from,direction,.08f/library.projectileRadius,age,from,end,.10f,true);
                flightVisual=flight.GetComponent<SmallBulletVfx>();
            }
            if(!arrived)
            {
                flight.transform.position=Vector3.Lerp(from,end,Mathf.Clamp01(age/duration));
                flight.transform.rotation=direction;
                flightVisual.Clip(from,end,.10f,true);
                if(age<duration)return;
                arrived=true;to=end;StopFlight();
            }
            if(confirmed)FinishHit();else if(age>duration+.5f)Cancel();
        }
        private void FinishHit()
        {
            if(finished)return;finished=true;
            Part(library.impact,to,Quaternion.identity,.18f/library.impactRadius,Mathf.Max(0,Time.time-started-duration),to,to,.18f);
            active.Remove(this);Destroy(gameObject);
        }
        private void StopFlight()
        {
            if(flight==null)return;
            flightVisual.held=false;
            foreach(var ps in flightVisual.systems)ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);
            flight=null;flightVisual=null;
        }
        private void Cancel(){if(finished)return;finished=true;StopFlight();active.Remove(this);Destroy(gameObject);}
        private void OnDestroy(){if(!tail)StopFlight();active.Remove(this);}
        private static GameObject Part(GameObject prefab,Vector3 point,Quaternion rotation,float scale,float elapsed,
            Vector3 start,Vector3 end,float radius,bool moving=false)
        {
            var instance=Instantiate(prefab,point,rotation);instance.transform.localScale=Vector3.one*(scale*SizeMultiplier);
            var life=instance.AddComponent<SmallBulletVfx>();life.tail=true;life.held=moving;life.started=Time.time+.1f;
            life.systems=instance.GetComponentsInChildren<ParticleSystem>(true);
            life.renderers=instance.GetComponentsInChildren<Renderer>(true);
            life.block=new MaterialPropertyBlock();life.Clip(start,end,radius,moving);
            instance.SetActive(true);
            // Seek late network playback without compressing the native animation timeline.
            if(elapsed>0)foreach(var ps in life.systems)
                if(ps.gameObject.activeInHierarchy&&ps.isPlaying){ps.Simulate(elapsed,false,false,false);ps.Play(false);}
            return instance;
        }
        private void Clip(Vector3 start,Vector3 end,float radius,bool moving)
        {
            radius*=SizeMultiplier;
            foreach(var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetVector("_AttackClip",new Vector4(end.x,end.z,radius,moving?2:1));
                block.SetVector("_AttackTrail",new Vector4(start.x,start.z,end.x-start.x,end.z-start.z));
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
