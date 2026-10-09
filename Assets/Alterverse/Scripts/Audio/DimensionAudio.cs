using Alterverse.Performance;
using Alterverse.Profile;
using UnityEngine;
namespace Alterverse.Audio
{
    public enum DimensionSound{HitLight,HitHeavy,RecallStart,RecallComplete,RecallCancel,RealityShatter,FrontShift,Overtime,SuddenDeath,Victory,Defeat,UiConfirm,UiBack,Attack,AbilityCast,Shield,Heal,HeroDeath,Kill,TowerTarget,TowerShot,TowerImpact,CoreHit,PlanetArrival,MeteorImpact,SiegeArrival,Purchase,MinionDeath,AbilityImpact,Explosion}
    public sealed class DimensionAudio:MonoBehaviour
    {
        static DimensionAudio instance;
        const int VoiceCount=8;
        AudioClip[] clips;AudioClip[][] heroClips;float[] nextCue;int variation;
        int[] heroVariation;
        readonly int[] actorKeys=new int[128],cueKeys=new int[128];
        readonly float[] actorReady=new float[128];int actorCursor;float warningUntil,duckGain=1f;
        readonly int[] importance=new int[VoiceCount],activeCue=new int[VoiceCount];
        readonly AudioSource[] voices=new AudioSource[VoiceCount];readonly float[] gains=new float[VoiceCount];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create(){if(FindAnyObjectByType<DimensionAudio>()==null)new GameObject("DF_Audio").AddComponent<DimensionAudio>();}
        void Awake()
        {
            if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;
            int count=System.Enum.GetValues(typeof(DimensionSound)).Length;clips=new AudioClip[count];nextCue=new float[count+HeroCombatAudio.CueCount];heroClips=HeroCombatAudio.Load();int missing=0;
            for(int i=0;i<count;i++){clips[i]=Resources.Load<AudioClip>("Audio/SFX/"+(DimensionSound)i);if(clips[i]==null)missing++;}
            heroVariation=new int[HeroCombatAudio.CueCount];
            for(int i=0;i<VoiceCount;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;}
            if(missing>0)Debug.LogWarning("DIMENSIONS FRONT: FINAL SFX ASSETS REQUIRED — "+missing+" missing cues. Synthetic prototype tones are disabled.");
            Alterverse.Match.DimensionPlanetDirector.PlanetArrived+=Planet;
        }
        static void Planet(Vector3 position)=>Play(DimensionSound.PlanetArrival,.65f);
        public static void PlayAt(DimensionSound sound,Vector3 position,float volume=.5f)
        {
            Camera camera=Camera.main;if(camera==null)return;Vector3 p=camera.WorldToViewportPoint(position);
            if(p.z<=0||p.x<-.15f||p.x>1.15f||p.y<-.15f||p.y>1.15f)return;
            Ray ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            float attenuation=1f;
            if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))
            {
                Vector3 delta=position-ray.GetPoint(distance);delta.y=0;
                attenuation=Mathf.Lerp(1f,.15f,Mathf.InverseLerp(3,28,delta.magnitude));
            }
            if(instance==null)return;int cue=(int)sound;if(cue<0||cue>=instance.clips.Length)return;
            if(instance.clips[cue]!=null)PlayCue(instance.clips[cue],cue,volume*Gain(sound)*attenuation,1,Priority(sound),Cooldown(sound),Mathf.Clamp((p.x-.5f)*1.1f,-.65f,.65f));
        }
        public static void Play(DimensionSound sound,float volume=1,float pitch=1)
        {
            if(instance==null)return;int cue=(int)sound;if(cue<0||cue>=instance.clips.Length||instance.clips[cue]==null)return;
            PlayCue(instance.clips[cue],cue,volume*Gain(sound),pitch,Priority(sound),Cooldown(sound));
        }
        public static void PlayHero(int cue,float gain,int priority,float cooldown,float pan=0,float pitch=1f,int actorId=0)
        {
            if(instance==null||cue<0||cue>=instance.heroClips.Length)return;
            var bank=instance.heroClips[cue];if(bank==null||bank.Length==0)return;
            int key=-1;
            for(int i=0;i<instance.actorKeys.Length;i++)
                if(instance.actorKeys[i]==actorId&&instance.cueKeys[i]==cue){key=i;break;}
            if(key>=0&&Time.unscaledTime<instance.actorReady[key])return;
            if(key<0){key=instance.actorCursor++%instance.actorKeys.Length;instance.actorKeys[key]=actorId;instance.cueKeys[key]=cue;}
            int variant=instance.heroVariation[cue]%bank.Length;
            if(bank[variant]==null)return;
            if(PlayCue(bank[variant],instance.clips.Length+cue,gain*.7f,pitch,priority,0,pan,false))
            {instance.actorReady[key]=Time.unscaledTime+cooldown;instance.heroVariation[cue]=(variant+1)%bank.Length;}
        }
        static bool PlayCue(AudioClip clip,int cue,float volume,float pitch,int priority,float cooldown,float pan=0,bool throttle=true)
        {
            if(throttle&&Time.unscaledTime<instance.nextCue[cue])return false;
            int budget=Mathf.Clamp(AudioStressBudget.RecommendedVoices,1,VoiceCount),slot=-1,overlap=0;
            for(int i=0;i<budget;i++)
            {
                if(!instance.voices[i].isPlaying){if(slot<0)slot=i;continue;}
                if(instance.activeCue[i]==cue)overlap++;
            }
            if(priority==0&&overlap>=2)return false;
            if(cue==(int)DimensionSound.Explosion&&overlap>=3)return false;
            // Repeated combat must not steal a warning or objective voice.
            if(slot<0)
                for(int i=0;i<budget;i++)
                    if(instance.importance[i]<=priority&&(slot<0||instance.importance[i]<instance.importance[slot]||
                       instance.importance[i]==instance.importance[slot]&&instance.voices[i].time>instance.voices[slot].time))slot=i;
            if(slot<0)return false;
            if(throttle)instance.nextCue[cue]=Time.unscaledTime+cooldown;
            if(priority==3)instance.warningUntil=Time.unscaledTime+Mathf.Min(clip.length,.8f);
            var voice=instance.voices[slot];voice.Stop();voice.clip=clip;
            // Reset on every pooled use, including centered UI/objective cues.
            voice.panStereo=Mathf.Clamp(pan,-.65f,.65f);
            // Deterministic presentation variation does not consume gameplay Random state.
            float variation=priority==0?Mathf.Sin(++instance.variation*2.39996f):0;
            voice.pitch=Mathf.Clamp(pitch*(1+variation*.018f),.8f,1.2f);
            instance.importance[slot]=priority;instance.activeCue[slot]=cue;voice.priority=priority==3?32:priority==2?64:priority==1?112:160;
            instance.gains[slot]=Mathf.Clamp01(volume)*(1+variation*.035f);
            voice.volume=instance.gains[slot]*PlayerProfile.MasterVolume*PlayerProfile.SfxVolume*(priority<3?instance.duckGain:1f);voice.Play();return true;
        }
        static int Priority(DimensionSound sound)=>sound switch
        {
            DimensionSound.TowerTarget or DimensionSound.CoreHit or DimensionSound.MeteorImpact or
            DimensionSound.RealityShatter or DimensionSound.Overtime or DimensionSound.SuddenDeath or
            DimensionSound.Kill or DimensionSound.HeroDeath or DimensionSound.Victory or DimensionSound.Defeat=>3,
            DimensionSound.Explosion or DimensionSound.PlanetArrival or DimensionSound.SiegeArrival or
            DimensionSound.AbilityCast or DimensionSound.RecallComplete or DimensionSound.Purchase=>1,
            _=>0
        };
        static float Cooldown(DimensionSound sound)=>sound switch
        {
            DimensionSound.Explosion=>.12f,DimensionSound.Attack=>.12f,DimensionSound.HitLight=>.1f,DimensionSound.AbilityImpact=>.14f,
            DimensionSound.TowerShot or DimensionSound.TowerImpact=>.2f,
            DimensionSound.TowerTarget=>.65f,DimensionSound.UiConfirm or DimensionSound.UiBack=>.16f,
            DimensionSound.Heal or DimensionSound.Shield=>.4f,_=>.075f
        };
        static float Gain(DimensionSound sound)=>sound switch
        {
            DimensionSound.Attack=>.78f,DimensionSound.HitLight=>.82f,DimensionSound.HitHeavy=>.9f,
            DimensionSound.AbilityImpact=>.8f,DimensionSound.TowerShot=>.72f,DimensionSound.TowerImpact=>.8f,
            DimensionSound.UiConfirm or DimensionSound.UiBack=>.8f,_=>1f
        };

        void Update()
        {
            duckGain=AudioDuckEnvelope.Advance(duckGain,Time.unscaledTime<warningUntil,Time.unscaledDeltaTime);
            float volume=PlayerProfile.MasterVolume*PlayerProfile.SfxVolume;
            for(int i=0;i<VoiceCount;i++)if(voices[i]!=null)
                voices[i].volume=gains[i]*volume*(importance[i]<3?duckGain:1f);
        }
        void OnDestroy()
        {
            if(instance!=this)return;instance=null;
            Alterverse.Match.DimensionPlanetDirector.PlanetArrived-=Planet;
            // Remaining resource clips are shared assets; never Destroy them.
        }
    }
}
