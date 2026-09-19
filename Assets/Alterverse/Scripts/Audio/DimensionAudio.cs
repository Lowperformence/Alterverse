using Alterverse.Performance;
using Alterverse.Profile;
using UnityEngine;
namespace Alterverse.Audio
{
    public enum DimensionSound{HitLight,HitHeavy,RecallStart,RecallComplete,RecallCancel,RealityShatter,FrontShift,Overtime,SuddenDeath,Victory,Defeat,UiConfirm,UiBack,Attack,AbilityCast,Shield,Heal,HeroDeath,Kill,TowerTarget,TowerShot,TowerImpact,CoreHit,PlanetArrival,MeteorImpact,SiegeArrival,Purchase,MinionDeath,AbilityImpact}
    public sealed class DimensionAudio:MonoBehaviour
    {
        static DimensionAudio instance;
        const int VoiceCount=8;
        AudioClip[] clips,heroClips;AudioClip heroDeathSignature;float[] nextCue;int variation;
        readonly int[] importance=new int[VoiceCount],activeCue=new int[VoiceCount];
        readonly AudioSource[] voices=new AudioSource[VoiceCount];readonly float[] gains=new float[VoiceCount];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create(){if(FindAnyObjectByType<DimensionAudio>()==null)new GameObject("DF_Audio").AddComponent<DimensionAudio>();}
        void Awake()
        {
            if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;
            int count=System.Enum.GetValues(typeof(DimensionSound)).Length;clips=new AudioClip[count];nextCue=new float[count+HeroCombatAudio.CueCount];heroClips=HeroCombatAudio.Load();int missing=0;
            for(int i=0;i<count;i++){clips[i]=Resources.Load<AudioClip>("Audio/SFX/"+(DimensionSound)i);if(clips[i]==null)missing++;}
            // Hero deaths use an original DIMENSIONS FRONT signature instead of the old electronic placeholder.
            // It is intentionally layered from a dry crack, low body hit and short noisy dimensional tail:
            // no borrowed sample and no repeating UI-style beep.
            heroDeathSignature=CreateHeroDeathSignature();
            clips[(int)DimensionSound.HeroDeath]=heroDeathSignature;
            for(int i=0;i<VoiceCount;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;}
            if(missing>0)Debug.LogWarning("DIMENSIONS FRONT: FINAL SFX ASSETS REQUIRED — "+missing+" missing cues. Synthetic prototype tones are disabled.");
            Alterverse.Combat.LaneWaveSpawner.SiegeWaveSpawned+=Siege;
            Alterverse.Match.DimensionPlanetDirector.PlanetArrived+=Planet;
        }
        static AudioClip CreateHeroDeathSignature()
        {
            const int rate=44100;
            const float duration=.42f;
            int length=Mathf.CeilToInt(rate*duration);
            float[] samples=new float[length];
            uint noiseState=0x6D2B79F5u;
            float phase=0f;
            for(int i=0;i<length;i++)
            {
                float t=i/(float)rate;
                noiseState^=noiseState<<13;noiseState^=noiseState>>17;noiseState^=noiseState<<5;
                float noise=((noiseState&0xFFFF)/32767.5f)-1f;

                // Immediate dry fracture: short, noisy and non-tonal.
                float crackEnv=Mathf.Exp(-t*72f);
                float crack=noise*crackEnv*.66f;

                // A second tiny splinter makes the event read as a decisive elimination rather than a click.
                float dt=t-.038f;
                float splinter=dt>0?noise*Mathf.Exp(-dt*95f)*.28f:0f;

                // Low impact body with a fast downward sweep. Kept low enough to avoid "beep" character.
                float freq=118f-42f*Mathf.Clamp01(t/.18f);
                phase+=Mathf.PI*2f*freq/rate;
                float bodyEnv=(1f-Mathf.Exp(-t*130f))*Mathf.Exp(-t*9.5f);
                float body=Mathf.Sin(phase)*bodyEnv*.5f;

                // Brief gritty dimensional tail: mostly noise, lightly pulsed, no pure high-pitched tone.
                float tailStart=Mathf.Max(0,t-.055f);
                float tailEnv=tailStart>0?Mathf.Exp(-tailStart*10.5f):0f;
                float grit=noise*tailEnv*(.13f+.05f*Mathf.Sin(t*Mathf.PI*2f*310f));

                float mixed=crack+splinter+body+grit;
                samples[i]=Mathf.Clamp(mixed/(1f+Mathf.Abs(mixed)*.42f),-1f,1f);
            }
            AudioClip clip=AudioClip.Create("DF_HeroDeathSignature",length,1,rate,false);
            clip.SetData(samples,0);
            return clip;
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
            PlayCue(instance.clips[cue],cue,volume*Gain(sound),pitch,Priority(sound),Cooldown(sound));
        }
        public static void PlayHero(int cue,float gain,int priority,float cooldown)
        {
            if(instance==null||cue<0||cue>=instance.heroClips.Length||instance.heroClips[cue]==null)return;
            PlayCue(instance.heroClips[cue],instance.clips.Length+cue,gain,1,priority,cooldown);
        }
        static void PlayCue(AudioClip clip,int cue,float volume,float pitch,int priority,float cooldown)
        {
            if(Time.unscaledTime<instance.nextCue[cue])return;
            int budget=Mathf.Clamp(AudioStressBudget.RecommendedVoices,1,VoiceCount),slot=-1,overlap=0;
            for(int i=0;i<budget;i++)
            {
                if(!instance.voices[i].isPlaying){if(slot<0)slot=i;continue;}
                if(instance.activeCue[i]==cue)overlap++;
            }
            if(priority==0&&overlap>=2)return;
            // Repeated combat must not steal a warning or objective voice.
            if(slot<0)
                for(int i=0;i<budget;i++)
                    if(instance.importance[i]<=priority&&(slot<0||instance.importance[i]<instance.importance[slot]||
                       instance.importance[i]==instance.importance[slot]&&instance.voices[i].time>instance.voices[slot].time))slot=i;
            if(slot<0)return;
            instance.nextCue[cue]=Time.unscaledTime+cooldown;
            var voice=instance.voices[slot];voice.Stop();voice.clip=clip;
            // Deterministic presentation variation does not consume gameplay Random state.
            float variation=priority==0?Mathf.Sin(++instance.variation*2.39996f):0;
            voice.pitch=Mathf.Clamp(pitch*(1+variation*.018f),.8f,1.2f);
            instance.importance[slot]=priority;instance.activeCue[slot]=cue;voice.priority=priority==2?64:priority==1?112:160;
            instance.gains[slot]=Mathf.Clamp01(volume)*(1+variation*.035f);
            voice.volume=instance.gains[slot]*PlayerProfile.MasterVolume*PlayerProfile.SfxVolume;voice.Play();
        }
        static int Priority(DimensionSound sound)=>sound switch
        {
            DimensionSound.TowerTarget or DimensionSound.CoreHit or DimensionSound.MeteorImpact or
            DimensionSound.RealityShatter or DimensionSound.Overtime or DimensionSound.SuddenDeath or
            DimensionSound.HeroDeath or DimensionSound.Victory or DimensionSound.Defeat=>2,
            DimensionSound.PlanetArrival or DimensionSound.SiegeArrival or DimensionSound.Kill or
            DimensionSound.AbilityCast or DimensionSound.RecallComplete or DimensionSound.Purchase=>1,
            _=>0
        };
        static float Cooldown(DimensionSound sound)=>sound switch
        {
            DimensionSound.Attack=>.12f,DimensionSound.HitLight=>.1f,DimensionSound.AbilityImpact=>.14f,
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

        void Update(){float volume=PlayerProfile.MasterVolume*PlayerProfile.SfxVolume;for(int i=0;i<VoiceCount;i++)if(voices[i]!=null)voices[i].volume=gains[i]*volume;}
        void OnDestroy()
        {
            if(instance!=this)return;instance=null;
            Alterverse.Combat.LaneWaveSpawner.SiegeWaveSpawned-=Siege;Alterverse.Match.DimensionPlanetDirector.PlanetArrived-=Planet;
            if(heroDeathSignature!=null)Destroy(heroDeathSignature);
            // Remaining resource clips are shared assets; never Destroy them.
        }
    }
}
