using Alterverse.Performance;
using Alterverse.Profile;
using UnityEngine;
namespace Alterverse.Audio
{
    public enum DimensionSound{HitLight,HitHeavy,RecallStart,RecallComplete,RecallCancel,RealityShatter,FrontShift,Overtime,SuddenDeath,Victory,Defeat,UiConfirm,UiBack,Attack,AbilityCast,Shield,Heal,HeroDeath,Kill,TowerTarget,TowerShot,TowerImpact,CoreHit,PlanetArrival,MeteorImpact,SiegeArrival}
    public sealed class DimensionAudio:MonoBehaviour
    {
        static DimensionAudio instance;
        const int VoiceCount=8;
        AudioClip[] clips;float[] nextCue;
        readonly AudioSource[] voices=new AudioSource[VoiceCount];readonly float[] gains=new float[VoiceCount];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create(){if(FindAnyObjectByType<DimensionAudio>()==null)new GameObject("DF_Audio").AddComponent<DimensionAudio>();}
        void Awake()
        {
            if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;
            int count=System.Enum.GetValues(typeof(DimensionSound)).Length;clips=new AudioClip[count];nextCue=new float[count];int missing=0;
            for(int i=0;i<count;i++){clips[i]=Resources.Load<AudioClip>("Audio/SFX/"+(DimensionSound)i);if(clips[i]==null)missing++;}
            for(int i=0;i<VoiceCount;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;}
            if(missing>0)Debug.LogWarning("DIMENSIONS FRONT: FINAL SFX ASSETS REQUIRED — "+missing+" missing cues. Synthetic prototype tones are disabled.");
            Alterverse.Combat.LaneWaveSpawner.SiegeWaveSpawned+=Siege;
            Alterverse.Match.DimensionPlanetDirector.PlanetArrived+=Planet;
        }
        static void Siege()=>Play(DimensionSound.SiegeArrival,.65f);
        static void Planet(Vector3 position)=>Play(DimensionSound.PlanetArrival,.65f);
        public static void PlayAt(DimensionSound sound,Vector3 position,float volume=.5f)
        {
            Camera camera=Camera.main;if(camera==null)return;Vector3 p=camera.WorldToViewportPoint(position);
            if(p.z<=0||p.x<-.15f||p.x>1.15f||p.y<-.15f||p.y>1.15f)return;
            Play(sound,volume);
        }
        public static void Play(DimensionSound sound,float volume=1,float pitch=1)
        {
            if(instance==null)return;int cue=(int)sound;if(cue<0||cue>=instance.clips.Length||instance.clips[cue]==null)return;
            if(Time.unscaledTime<instance.nextCue[cue])return;instance.nextCue[cue]=Time.unscaledTime+(sound==DimensionSound.Heal||sound==DimensionSound.Shield?.4f:.075f);
            int budget=Mathf.Clamp(AudioStressBudget.RecommendedVoices,1,VoiceCount),slot=-1;
            for(int i=0;i<budget;i++)if(!instance.voices[i].isPlaying){slot=i;break;}
            if(slot<0){slot=0;for(int i=1;i<budget;i++)if(instance.voices[i].time>instance.voices[slot].time)slot=i;}
            var voice=instance.voices[slot];voice.Stop();voice.clip=instance.clips[cue];voice.pitch=Mathf.Clamp(pitch,.8f,1.2f);
            instance.gains[slot]=Mathf.Clamp01(volume);voice.volume=instance.gains[slot]*PlayerProfile.MasterVolume*PlayerProfile.SfxVolume;voice.Play();
        }
        void Update(){float volume=PlayerProfile.MasterVolume*PlayerProfile.SfxVolume;for(int i=0;i<VoiceCount;i++)if(voices[i]!=null)voices[i].volume=gains[i]*volume;}
        void OnDestroy()
        {
            if(instance!=this)return;instance=null;
            Alterverse.Combat.LaneWaveSpawner.SiegeWaveSpawned-=Siege;Alterverse.Match.DimensionPlanetDirector.PlanetArrived-=Planet;
            // Resource clips are shared assets; never Destroy them.
        }
    }
}
