using System;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Detonation-only presentation. Casts, warnings, shots, damage and sound stay with their owners.
    public sealed class UltimateLightningVfx:MonoBehaviour
    {
        public const int Ione=0,Traveler=1;
        public const string ResourcePath="CombatVfx/UltimateLightning/Library";
        private const float VisualSizeMultiplier=2f;
        [Serializable] public sealed class Entry
        {
            public GameObject prefab;
            public float nativeRadius=1f,emissionSeconds=.1f;
            public string source;
        }
        public Entry ione,traveler;
        public string sourceSignature;
        private static UltimateLightningVfx library;
        private static bool warnedIone,warnedTraveler,reportedIone,reportedTraveler;
        private ParticleSystem[] systems;
        private float started,emissionSeconds;
        private bool live,stopped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {library=null;warnedIone=warnedTraveler=reportedIone=reportedTraveler=false;}
        public static bool TryPlay(int kind,Vector3 center,float radius,float elapsed=0)
        {
            if(kind!=Ione&&kind!=Traveler||!(radius>0f)||float.IsInfinity(radius)||elapsed<0||float.IsNaN(elapsed)||float.IsInfinity(elapsed))return false;
            if(library==null){var resource=Resources.Load<GameObject>(ResourcePath);if(resource!=null)library=resource.GetComponent<UltimateLightningVfx>();}
            var entry=library!=null?(kind==Ione?library.ione:library.traveler):null;
            if(entry==null||entry.prefab==null||!(entry.nativeRadius>0)||float.IsInfinity(entry.nativeRadius))
            {
                bool warned=kind==Ione?warnedIone:warnedTraveler;
                if(!warned)Debug.LogWarning("DIMENSIONS FRONT VFX: "+(kind==Ione?"Ione Lightning Nova":"Traveler Lightning Explosion")+" is not prepared; keeping the previous detonation.");
                if(kind==Ione)warnedIone=true;else warnedTraveler=true;
                return false;
            }
            // Enlarge the authored composition; the fragment clip below still uses the
            // actual ability radius, independently of this visual size adjustment.
            var instance=Instantiate(entry.prefab,center,Quaternion.identity);
            instance.name=kind==Ione?"DF_Ione_LightningNova":"DF_Traveler_LightningExplosion";
            instance.transform.localScale=Vector3.one*(radius*VisualSizeMultiplier/entry.nativeRadius);
            var block=new MaterialPropertyBlock();
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);block.SetVector("_AttackClip",new Vector4(center.x,center.z,radius,1f));renderer.SetPropertyBlock(block);
            }
            var player=instance.AddComponent<UltimateLightningVfx>();player.live=true;
            player.systems=instance.GetComponentsInChildren<ParticleSystem>(true);
            player.started=Time.time-elapsed;player.emissionSeconds=Mathf.Max(.05f,entry.emissionSeconds);
            instance.SetActive(true);
            if(elapsed>0)
                foreach(var system in player.systems)
                {
                    if(!system.gameObject.activeInHierarchy||!system.isPlaying)continue;
                    system.Simulate(elapsed,false,false,false);
                    // Simulate pauses the system. Resume only live particles or pending emission;
                    // do not restart a completed late explosion as a fresh full-bright blast.
                    if(system.particleCount>0||elapsed<player.emissionSeconds)system.Play(false);
                }
            bool reported=kind==Ione?reportedIone:reportedTraveler;
            if(!reported)
            {
                Debug.Log("DIMENSIONS FRONT VFX: "+instance.name+" ACTIVE; source="+entry.source+"; actualRadius="+radius);
                if(kind==Ione)reportedIone=true;else reportedTraveler=true;
            }
            return true;
        }
        private void LateUpdate()
        {
            if(!live||Time.time-started<.1f)return;
            if(!stopped&&Time.time-started>=emissionSeconds)
            {
                // Leave death-triggered non-autoplay sub-emitters and current smoke/trails alive.
                foreach(var system in systems)
                    if(system!=null&&system.main.playOnAwake)system.Stop(false,ParticleSystemStopBehavior.StopEmitting);
                stopped=true;
            }
            foreach(var system in systems)
                if(system!=null&&system.gameObject.activeInHierarchy&&system.IsAlive(false))return;
            Destroy(gameObject);
        }
    }
}
