using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Purchased impact visuals only; attack damage, warning radius and audio belong to the callers.
    public sealed class AttackExplosionVfx:MonoBehaviour
    {
        public enum Kind { FluxNuke, MeteorFireball }
        public static string ResourcePath(Kind kind)=>"CombatVfx/AttackExplosions/"+kind;
        public static float SizeMultiplier(Kind kind)=>kind==Kind.FluxNuke?4f/3f:3f; // Flux: one third of the previous 4x size.
        private static readonly List<AttackExplosionVfx>[] pools={new(8),new(32)};
        public string sourcePath,sourceSignature;
        public float emissionSeconds=.1f;
        private ParticleSystem[] systems=System.Array.Empty<ParticleSystem>();
        private Renderer[] renderers=System.Array.Empty<Renderer>();
        private MaterialPropertyBlock block;
        private float started;
        private bool playing,stopped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic(){foreach(var pool in pools)pool.Clear();}

        public static void Prepare(Transform world)
        {
            ResetStatic();
#if !UNITY_SERVER
            for(int index=0;index<pools.Length;index++)
            {
                var kind=(Kind)index;
                var prefab=Resources.Load<GameObject>(ResourcePath(kind));
                if(prefab==null||prefab.GetComponent<AttackExplosionVfx>()==null)
                {Debug.LogWarning("DIMENSIONS FRONT VFX: planet "+kind+" is not prepared. Use DIMENSIONS FRONT/VFX/Prepare attack explosions only.");continue;}
                int capacity=index==0?8:32;
                for(int i=0;i<capacity;i++)
                {
                    var go=Instantiate(prefab,world,false);go.name="DF_AttackExplosion_"+kind;
                    var effect=go.GetComponent<AttackExplosionVfx>();
                    effect.systems=go.GetComponentsInChildren<ParticleSystem>(true);
                    effect.renderers=go.GetComponentsInChildren<Renderer>(true);
                    effect.block=new MaterialPropertyBlock();
                    foreach(var ps in effect.systems)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    go.SetActive(false);pools[index].Add(effect);
                }
            }
#endif
        }

        public static bool TryPlay(Kind kind,Vector3 position)=>Play(kind,position,default,0f,0f);

        public static bool TryMeteor(Vector3 position,Color color,float elapsed=0f,float clipRadius=0f)
            =>Play(Kind.MeteorFireball,position,new Vector4(color.r,color.g,color.b,1f),elapsed,clipRadius);

        private static bool Play(Kind kind,Vector3 position,Vector4 tint,float elapsed,float clipRadius)
        {
            int index=(int)kind;if(index<0||index>=pools.Length)return false;
            if(!(elapsed>=0f)||float.IsInfinity(elapsed))return false;
            var pool=pools[index];
            for(int i=0;i<pool.Count;i++)
            {
                var effect=pool[i];if(effect==null||effect.playing)continue;
                effect.transform.position=position;effect.transform.rotation=Quaternion.identity;
                effect.transform.localScale=Vector3.one*SizeMultiplier(kind);
                effect.started=Time.time-elapsed;effect.stopped=false;effect.playing=true;
                foreach(var renderer in effect.renderers)
                {
                    renderer.GetPropertyBlock(effect.block);
                    effect.block.SetVector("_EffectTint",tint);
                    effect.block.SetVector("_AttackClip",new Vector4(position.x,position.z,clipRadius,clipRadius>0f?1f:0f));
                    renderer.SetPropertyBlock(effect.block);
                }
                effect.gameObject.SetActive(true);
                foreach(var ps in effect.systems)
                {
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    if(ps.main.playOnAwake&&ps.gameObject.activeInHierarchy)ps.Play(false);
                }
                // Hero cue replay uses the same impact age as the authoritative event.
                if(elapsed>0f)foreach(var ps in effect.systems)
                    if(ps.gameObject.activeInHierarchy&&ps.isPlaying)
                    {
                        ps.Simulate(elapsed,false,false,false);
                        if(ps.particleCount>0||elapsed<effect.emissionSeconds)ps.Play(false);
                    }
                return true;
            }
            return false;
        }

        private void LateUpdate()
        {
            if(!playing||Time.time-started<.1f)return;
            if(!stopped&&Time.time-started>=emissionSeconds)
            {
                foreach(var ps in systems)if(ps.main.playOnAwake)ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);
                stopped=true;
            }
            foreach(var ps in systems)if(ps.gameObject.activeInHierarchy&&ps.IsAlive(true))return;
            playing=false;gameObject.SetActive(false);
        }
    }
}
