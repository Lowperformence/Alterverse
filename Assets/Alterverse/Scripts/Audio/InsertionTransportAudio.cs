using Alterverse.Match;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.Audio
{
    /// <summary>The selected thepodcastdoctor recording, preloaded under the loading curtain.</summary>
    public sealed class InsertionTransportAudio : MonoBehaviour
    {
        private AudioSource engine;
        private bool started, stopped;

        public void Prepare()
        {
            if(engine!=null)return;
            engine=gameObject.AddComponent<AudioSource>();
            engine.playOnAwake=false;engine.loop=false;engine.spatialBlend=0f;
            engine.priority=72;engine.dopplerLevel=0f;engine.volume=0f;engine.pitch=1f;
            engine.clip=Resources.Load<AudioClip>("Audio/SFX/InsertionTransport/R35Arrival");
            if(engine.clip!=null)engine.clip.LoadAudioData();
            else Debug.LogWarning("DIMENSIONS FRONT: selected R35 arrival recording missing.");
        }

        public void Advance(float progress)
        {
            if(stopped||engine==null)return;
            if(MatchClock.InBackground){engine.Pause();return;}
            engine.UnPause();
            if(progress>=4f){Stop();return;}
            float gain=Mathf.Clamp01(PlayerProfile.MasterVolume)*Mathf.Clamp01(PlayerProfile.SfxVolume);
            float arrival=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress));
            float leaving=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress-3f));
            float envelope=progress<1f?Mathf.Lerp(.24f,.82f,arrival)
                :progress<3f?Mathf.Lerp(.82f,.68f,Mathf.Clamp01((progress-1f)/.5f))
                :Mathf.Lerp(.82f,0f,leaving);
            engine.volume=envelope*gain;
            if(!started&&engine.clip!=null)
            {
                started=true;
                // A late client enters the existing recording instead of replaying the approach.
                float elapsed=progress<1f?progress*2.2f:2.2f+(progress-1f)*.72f;
                engine.time=Mathf.Min(elapsed,Mathf.Max(0f,engine.clip.length-.05f));
                engine.Play();
            }
        }

        public void Stop()
        {
            if(stopped)return;stopped=true;
            if(engine!=null)engine.Stop();
        }
        private void OnDisable(){Stop();}
    }
}
