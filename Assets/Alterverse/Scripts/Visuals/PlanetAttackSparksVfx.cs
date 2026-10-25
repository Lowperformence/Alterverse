using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Additive decoration for buff-planet impacts; no damage, targeting or audio.
    public sealed class PlanetAttackSparksVfx:MonoBehaviour
    {
        public const string ResourcePath="CombatVfx/PlanetAttackSparks/Sparks";
        public const float SizeMultiplier=4f; // Original size plus 300 percent.
        private const int Capacity=32;
        public string sourcePath,sourceSignature;
        public float emissionSeconds=.1f;
        private static readonly List<PlanetAttackSparksVfx> pool=new(Capacity);
        private ParticleSystem[] systems=System.Array.Empty<ParticleSystem>();
        private float started;
        private bool playing,stopped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()=>pool.Clear();

        public static void Prepare(Transform world)
        {
            pool.Clear();
#if !UNITY_SERVER
            var prefab=Resources.Load<GameObject>(ResourcePath);
            if(prefab==null||prefab.GetComponent<PlanetAttackSparksVfx>()==null)
            {Debug.LogWarning("DIMENSIONS FRONT VFX: planet attack Sparks is not prepared. Use DIMENSIONS FRONT/VFX/Prepare planet attack Sparks only.");return;}
            for(int i=0;i<Capacity;i++)
            {
                var go=Instantiate(prefab,world,false);go.name="DF_PlanetAttackSparks";
                var effect=go.GetComponent<PlanetAttackSparksVfx>();
                effect.systems=go.GetComponentsInChildren<ParticleSystem>(true);
                foreach(var ps in effect.systems)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                go.SetActive(false);pool.Add(effect);
            }
#endif
        }

        public static bool TryPlay(Vector3 position)
        {
            for(int i=0;i<pool.Count;i++)
            {
                var effect=pool[i];if(effect==null||effect.playing)continue;
                effect.transform.position=position;effect.transform.rotation=Quaternion.identity;
                effect.transform.localScale=Vector3.one*SizeMultiplier;
                effect.started=Time.time;effect.stopped=false;effect.playing=true;
                effect.gameObject.SetActive(true);
                foreach(var ps in effect.systems)
                {
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    if(ps.main.playOnAwake&&ps.gameObject.activeInHierarchy)ps.Play(false);
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
