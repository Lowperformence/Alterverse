using Alterverse.Match;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Audio
{
    /// <summary>One local cinematic mix, using authored assets preloaded behind the loading curtain.</summary>
    public sealed class InsertionTransportAudio : MonoBehaviour
    {
        private AudioSource turbine, mechanisms;
        private AudioClip bayRelease, hydraulics, departure;
        private float previous=-1f;
        private bool started, stopped;

        public void Prepare()
        {
            turbine=gameObject.AddComponent<AudioSource>();
            mechanisms=gameObject.AddComponent<AudioSource>();
            foreach(var source in new[]{turbine,mechanisms})
            {
                source.playOnAwake=false;source.spatialBlend=0f;
                source.priority=72;source.dopplerLevel=0f;source.volume=0f;
            }
            turbine.loop=true;
            turbine.clip=Load("TurbineLoop");
            bayRelease=Load("BayRelease");hydraulics=Load("CargoHydraulics");departure=Load("Departure");
        }

        private static AudioClip Load(string name)
        {
            var clip=Resources.Load<AudioClip>("Audio/SFX/InsertionTransport/"+name);
            if(clip!=null)clip.LoadAudioData();
            else Debug.LogWarning("DIMENSIONS FRONT: ATLAS audio missing: "+name);
            return clip;
        }

        public void Advance(float progress)
        {
            if(stopped||turbine==null)return;
            if(MatchClock.InBackground)
            {
                turbine.Pause();mechanisms.Pause();return;
            }
            turbine.UnPause();mechanisms.UnPause();
            if(progress>=4f){Stop();return;}
            float gain=Mathf.Clamp01(PlayerProfile.MasterVolume)*Mathf.Clamp01(PlayerProfile.SfxVolume);
            float arrival=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress));
            float leaving=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress-3f));
            float engine=progress<1f?Mathf.Lerp(.10f,.68f,arrival)
                :progress<3f?Mathf.Lerp(.68f,.52f,Mathf.Clamp01((progress-1f)/.4f))
                :Mathf.Lerp(.74f,0f,leaving);
            turbine.volume=engine*gain;
            turbine.pitch=progress<1f?Mathf.Lerp(.72f,.97f,arrival)
                :progress<3f?Mathf.Lerp(.97f,.86f,Mathf.Clamp01((progress-1f)/.4f))
                :Mathf.Lerp(.85f,1.35f,leaving);
            mechanisms.volume=.68f*gain;
            if(!started)
            {
                started=true;
                if(turbine.clip!=null)turbine.Play();
            }
            // Only crossings close to the visible event play a cue; late joins never replay earlier mechanisms.
            if(previous<2f&&progress>=2f&&progress<2.35f&&bayRelease!=null)
                mechanisms.PlayOneShot(bayRelease,.74f);
            if(previous<1.1f&&progress>=1.1f&&progress<1.5f&&hydraulics!=null)
                mechanisms.PlayOneShot(hydraulics,.56f);
            if(previous<3f&&progress>=3f&&progress<3.35f&&departure!=null)
                mechanisms.PlayOneShot(departure,.72f);
            previous=Mathf.Max(previous,progress);
        }

        public void Stop()
        {
            if(stopped)return;stopped=true;
            if(turbine!=null)turbine.Stop();
            if(mechanisms!=null)mechanisms.Stop();
        }
        private void OnDisable(){Stop();}
    }
}
