using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // The imported Magic Aura is loaded and instantiated during match preparation.
    // Spawn only places and restarts a reserved instance; it never loads assets.
    public sealed class HotZoneSpawnAura : MonoBehaviour
    {
        public const string ResourcePath="CombatVfx/HotZoneSpawnAura/Aura";
        public float nativeRadius=1f,emissionSeconds=1.2f;
        public string sourceSignature,sourcePath;
        private static readonly List<HotZoneSpawnAura> pool=new(2);
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
            var definition=prefab!=null?prefab.GetComponent<HotZoneSpawnAura>():null;
            if(definition==null||!(definition.nativeRadius>0f)||float.IsInfinity(definition.nativeRadius))
            {Debug.LogWarning("DIMENSIONS FRONT VFX: Hot Zone Magic Aura is not prepared. Use DIMENSIONS FRONT/VFX/Prepare Hot Zone Magic Aura only.");return;}
            for(int i=0;i<2;i++)
            {
                var go=Instantiate(prefab,world,false);go.name="DF_HotZoneSpawnMagicAura";
                var aura=go.GetComponent<HotZoneSpawnAura>();
                aura.systems=go.GetComponentsInChildren<ParticleSystem>(true);
                foreach(var ps in aura.systems)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                go.SetActive(false);pool.Add(aura);
            }
#endif
        }

        public static bool TryPlay(Vector3 position,float radius)
        {
            if(!(radius>0f)||float.IsInfinity(radius))return false;
            for(int i=0;i<pool.Count;i++)
            {
                var aura=pool[i];if(aura==null||aura.playing)continue;
                aura.transform.SetPositionAndRotation(position,Quaternion.identity);
                aura.transform.localScale=Vector3.one*(radius/aura.nativeRadius);
                aura.started=Time.time;aura.stopped=false;aura.playing=true;
                aura.gameObject.SetActive(true);
                foreach(var ps in aura.systems)
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
