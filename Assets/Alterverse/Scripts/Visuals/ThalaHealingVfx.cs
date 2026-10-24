using System;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Recipient confirmation and Wildsurge centerpiece only. Healing/audio stay in combat code.
    public sealed class ThalaHealingVfx:MonoBehaviour
    {
        public const string ResourcePath="CombatVfx/ThalaHealing/Library";
        private const float SizeMultiplier=3.5f,TargetBaselineRadius=.45f;
        [Serializable] public sealed class Entry
        {
            public GameObject prefab;
            public float nativeRadius=1f,emissionSeconds=.1f;
            public string source;
        }
        public Entry target,area;
        public string sourceSignature;
        private static ThalaHealingVfx library;
        private static bool warnedTarget,warnedArea,reportedTarget,reportedArea;
        private ParticleSystem[] systems;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Transform follow;
        private Health recipientHealth;
        private float clipRadius,started,emissionSeconds;
        private bool live,following,stopped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {library=null;warnedTarget=warnedArea=reportedTarget=reportedArea=false;}
        private static Entry Ready(bool recipient)
        {
            if(library==null){var resource=Resources.Load<GameObject>(ResourcePath);if(resource!=null)library=resource.GetComponent<ThalaHealingVfx>();}
            var entry=library!=null?(recipient?library.target:library.area):null;
            if(entry!=null&&entry.prefab!=null&&entry.nativeRadius>0&&!float.IsInfinity(entry.nativeRadius))return entry;
            bool warned=recipient?warnedTarget:warnedArea;
            if(!warned)Debug.LogWarning("DIMENSIONS FRONT VFX: Thala "+(recipient?"target healing":"Wildsurge healing nova")+" is not prepared; keeping the previous healing visual.");
            if(recipient)warnedTarget=true;else warnedArea=true;
            return null;
        }
        private static bool ValidElapsed(float value)=>value>=0&&!float.IsNaN(value)&&!float.IsInfinity(value);
        public static bool TryTarget(Transform target,float elapsed=0)
        {
            if(!ValidElapsed(elapsed))return false;
            // Consume invalid recipient replays so an old fallback cannot heal-glow a corpse.
            if(target==null||!target.gameObject.activeInHierarchy)return true;
            var health=target.GetComponent<Health>();if(health!=null&&health.IsDead)return true;
            var entry=Ready(true);if(entry==null)return false;
            float radius=TargetBaselineRadius*SizeMultiplier;
            Spawn(entry,"DF_Thala_GrowthVeil_Heal",target.position,radius,radius,target,health,elapsed);
            if(!reportedTarget){Debug.Log("DIMENSIONS FRONT VFX: Thala target healing ACTIVE; source="+entry.source+"; radius="+radius);reportedTarget=true;}
            return true;
        }
        public static bool TryArea(Vector3 center,float radius,float elapsed=0)
        {
            if(!(radius>0)||float.IsInfinity(radius)||!ValidElapsed(elapsed))return false;
            var entry=Ready(false);if(entry==null)return false;
            // Enlarge native geometry only; the actual ability area is never enlarged.
            Spawn(entry,"DF_Thala_Wildsurge_Heal",center,radius*SizeMultiplier,radius,null,null,elapsed);
            if(!reportedArea){Debug.Log("DIMENSIONS FRONT VFX: Thala Wildsurge healing ACTIVE; source="+entry.source+"; actualRadius="+radius);reportedArea=true;}
            return true;
        }
        private static void Spawn(Entry entry,string name,Vector3 center,float visualRadius,float clipRadius,
            Transform follow,Health health,float elapsed)
        {
            var instance=Instantiate(entry.prefab,center,Quaternion.identity);instance.name=name;
            instance.transform.localScale=Vector3.one*(visualRadius/entry.nativeRadius);
            var player=instance.AddComponent<ThalaHealingVfx>();player.live=true;player.follow=follow;
            player.following=follow!=null;player.recipientHealth=health;player.clipRadius=clipRadius;
            player.started=Time.time-elapsed;player.emissionSeconds=Mathf.Max(.05f,entry.emissionSeconds);
            player.systems=instance.GetComponentsInChildren<ParticleSystem>(true);
            player.renderers=instance.GetComponentsInChildren<Renderer>(true);player.block=new MaterialPropertyBlock();
            player.UpdateClip();instance.SetActive(true);
            if(elapsed>0)
                foreach(var system in player.systems)
                {
                    if(!system.gameObject.activeInHierarchy||!system.isPlaying)continue;
                    system.Simulate(elapsed,false,false,false);
                    if(system.particleCount>0||elapsed<player.emissionSeconds)system.Play(false);
                }
        }
        private void LateUpdate()
        {
            if(!live)return;
            if(following)
            {
                if(follow!=null&&follow.gameObject.activeInHierarchy&&(recipientHealth==null||!recipientHealth.IsDead))
                {transform.position=follow.position;UpdateClip();}
                else
                {
                    // Recipient vanished: stop new emission, but do not clear a living tail.
                    follow=null;following=false;StopEmission();
                }
            }
            if(Time.time-started<.1f)return;
            if(!stopped&&Time.time-started>=emissionSeconds)StopEmission();
            foreach(var system in systems)
                if(system!=null&&system.gameObject.activeInHierarchy&&system.IsAlive(false))return;
            Destroy(gameObject);
        }
        private void StopEmission()
        {
            if(stopped)return;
            foreach(var system in systems)
                if(system!=null&&system.main.playOnAwake)system.Stop(false,ParticleSystemStopBehavior.StopEmitting);
            stopped=true;
        }
        private void UpdateClip()
        {
            Vector3 center=transform.position;
            foreach(var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);block.SetVector("_AttackClip",new Vector4(center.x,center.z,clipRadius,1f));renderer.SetPropertyBlock(block);
            }
        }
    }
}
