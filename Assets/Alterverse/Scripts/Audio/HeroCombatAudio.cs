using Alterverse.Characters;
using Alterverse.Multiplayer;
using UnityEngine;
namespace Alterverse.Audio
{
    public static class HeroCombatAudio
    {
        static bool UsesNovaAudio(int hero,int action)=>hero==(int)HeroId.Nyra&&(action==3||action==4)&&
            Alterverse.Visuals.SivraNovaVfx.AudioFor(action-1,0)!=null;
        static bool UsesLightningAudio(int hero,int action)=>hero==(int)HeroId.Traveler&&(action==1||action==3)&&
            Alterverse.Visuals.TravelerLightningVfx.AudioFor(action-1,0)!=null;
        static bool UsesImportedAudio(int hero,int action)=>UsesNovaAudio(hero,action)||UsesLightningAudio(hero,action);
        static AudioClip ImportedAudio(int hero,int action,int phase)=>UsesLightningAudio(hero,action)?
            Alterverse.Visuals.TravelerLightningVfx.AudioFor(action-1,phase):Alterverse.Visuals.SivraNovaVfx.AudioFor(action-1,phase);
        // Preserve original cast/impact cue indices; preparation occupies the appended bank.
        public const int ScratchHoldCue=HeroIdentity.HeroCount*15;
        public const int CueCount=ScratchHoldCue+1;
        static readonly string[] Heroes={"Veyr","Kaelon","Nyra","Elyra","Mira","Rook","Traveler","Nerok"};
        static readonly string[] Actions={"Basic","Q","W","E","R"};
        // Timmy has selected Q/W/R recordings; basic/E share the existing fast-cut bank.
        // Keep his gameplay/network identity intact and preserve every existing cue index.
        static int RecordingHero(int hero,int action)=>hero==(int)HeroId.Timmy&&action!=1&&action!=2&&action!=4?(int)HeroId.Veyr:hero;
        // User-selected recordings are complete cues. Play them once at ability release,
        // not over the former synthesized preparation/impact layers (or once per AoE victim).
        public static bool UsesSelectedRecording(int hero,int action)=>RecordingHero(hero,action) switch
        {
            0=>action>=1&&action<=3,
            1=>action>=1&&action<=3,
            2=>action>=1&&action<=3,
            3=>action>=1&&action<=3,
            4=>action>=1&&action<=3,
            5=>action>=1&&action<=3,
            6=>action>=1&&action<=3,
            7=>action>=1&&action<=4,
            8=>action==1||action==2||action==4,
            _=>false
        };
        public static bool HasImpact(int hero,int action)=>UsesImportedAudio(hero,action)?ImportedAudio(hero,action,1)!=null:hero==(int)HeroId.VoidKnight||!UsesSelectedRecording(hero,action)&&(action==0||(RecordingHero(hero,action) switch
        {
            0=>action!=3,1=>action!=2,2=>action!=2,3=>action!=3,4=>action==1||action==4,5=>action!=2,6=>action!=2,7=>action!=2,_=>false
        }));
        public static bool HasWindup(int hero,int action)=>UsesImportedAudio(hero,action)?ImportedAudio(hero,action,2)!=null:hero==(int)HeroId.VoidKnight?action!=2:!UsesSelectedRecording(hero,action)&&(action==0||HasImpact(hero,action));
        public static int VariantCount(int hero,int action,int phase)=>UsesImportedAudio(hero,action)?1:hero==(int)HeroId.VoidKnight?(action==0&&phase==0?3:1):hero==7?1:phase!=2&&(action==0||phase==1&&hero==2&&action==4)?3:1;
        public static int Cue(int hero,int action,int phase)
        {hero=RecordingHero(hero,action);return hero==(int)HeroId.VoidKnight?135+action*3+phase:hero==(int)HeroId.Timmy?120+action*3+phase:hero==7?105+action*3+phase:phase==2?70+hero*5+action:hero*10+action*2+phase;}
        public static AudioClip[][] Load()
        {
            var clips=new AudioClip[CueCount][];
            for(int h=0;h<Heroes.Length;h++)for(int a=0;a<5;a++)for(int phase=0;phase<3;phase++)
            {
                // The selected Nerok recording already contains swing and contact.
                if(h==(int)HeroId.Nerok&&a==0&&phase!=1)continue;
                if(phase==1&&!HasImpact(h,a)||phase==2&&!HasWindup(h,a))continue;
                int cue=Cue(h,a,phase);
                clips[cue]=new AudioClip[VariantCount(h,a,phase)];
                for(int v=0;v<clips[cue].Length;v++)
                {
                    string path="Audio/Heroes/"+Heroes[h]+"/"+Actions[a]+(phase==0?"_Cast":phase==1?"_Impact":"_Windup")+(v==0?"":"_v"+(v+1));
                    if(a==4&&phase==0)path="Audio/Heroes/"+Heroes[h]+"/Ultimate_Selected";
                    clips[cue][v]=UsesImportedAudio(h,a)?ImportedAudio(h,a,phase):Resources.Load<AudioClip>(path);
                    if(clips[cue][v]==null)Debug.LogWarning("DIMENSIONS FRONT: missing combat audio: "+path);
                }
            }
            LoadTimmyRecording(clips,1,"Audio/Heroes/Timmy/Q_Cast");
            LoadTimmyRecording(clips,2,"Audio/Heroes/Timmy/W_Cast");
            LoadTimmyRecording(clips,4,"Audio/Heroes/Timmy/Ultimate_Selected");
            for(int action=0;action<5;action++)for(int phase=0;phase<3;phase++)
            {
                if(phase==2&&!HasWindup((int)HeroId.VoidKnight,action))continue;
                string path="Audio/Heroes/VoidKnight/"+Actions[action]+(phase==0?"_Cast":phase==1?"_Impact":"_Windup");
                int cue=Cue((int)HeroId.VoidKnight,action,phase);clips[cue]=new AudioClip[VariantCount((int)HeroId.VoidKnight,action,phase)];
                for(int v=0;v<clips[cue].Length;v++)clips[cue][v]=Resources.Load<AudioClip>(path+(v==0?"":"_v"+(v+1)));
            }
            clips[ScratchHoldCue]=new[]{Alterverse.Visuals.SelectedAbilityVfx.StarsClip()};
            return clips;
        }
        static void LoadTimmyRecording(AudioClip[][] clips,int action,string path)
        {
            var recording=Resources.Load<AudioClip>(path);
            clips[Cue((int)HeroId.Timmy,action,0)]=new[]{recording};
            if(recording==null)Debug.LogWarning("DIMENSIONS FRONT: missing combat audio: "+path);
        }
        public static void PlayScratchHold(GameObject actor,Vector3 position,float elapsed)
        {
            if(actor==null||elapsed>.15f||Alterverse.Visuals.SelectedAbilityVfx.StarsClip()==null)return;
            var camera=Camera.main;if(camera==null)return;var p=camera.WorldToViewportPoint(position);
            bool local=IsLocal(actor);if(!local&&(p.z<=0||p.x<-.15f||p.x>1.15f||p.y<-.15f||p.y>1.15f))return;
            DimensionAudio.PlayHero(ScratchHoldCue,local?.65f:.45f,local?2:0,.3f,
                local?0:Mathf.Clamp((p.x-.5f)*1.1f,-.65f,.65f),1f,actor.GetEntityId().GetHashCode());
        }
        public static bool IsLocal(GameObject actor)
        {
            var slot=actor.GetComponent<CoopHeroSlot>();
            return slot!=null?slot.IsLocalHuman:actor.GetComponent<Alterverse.Combat.BasicAttack>()!=null;
        }
        public static void Play(GameObject actor,int action,bool impact,Vector3 position)=>PlayPhase(actor,action,impact?1:0,position);
        public static void PlayLightningGroundImpact(GameObject actor,Vector3 position)=>PlayPhase(actor,3,1,position,false,true);
        static void PlayPhase(GameObject actor,int action,int phase,Vector3 position,bool relay=true,bool groundImpact=false)
        {
            if(actor==null||action<0||action>4)return;
            var identity=actor.GetComponent<HeroIdentity>();if(identity==null)return;
            int hero=RecordingHero((int)identity.Hero,action);
            bool lightningAudio=UsesLightningAudio(hero,action);
            if(lightningAudio&&action==3)
            {
                // Chrono's existing warning cue launches its visual projectile. Move only
                // the selected shot sound to that launch; ResolveArea's later Cast is not a second shot.
                if(phase==0&&relay)return;
                if(phase==1&&!groundImpact)return;
                if(phase==2)phase=0;
            }
            if(hero==(int)HeroId.Nerok&&action==0&&phase!=1)return;
            if(hero<0||hero>=HeroIdentity.HeroCount||phase==1&&!HasImpact(hero,action)||phase==2&&!HasWindup(hero,action))return;
            if(relay)CoopHeroStateSync.BroadcastCombatSound(actor,action,phase,position);
            bool local=IsLocal(actor),ultimate=action==4;
            Camera camera=Camera.main;if(camera==null)return;
            Vector3 p=camera.WorldToViewportPoint(position);
            if(!local&&(p.z<=0||p.x<-.15f||p.x>1.15f||p.y<-.15f||p.y>1.15f))return;
            float gain=phase==2?.32f:action==0?(phase==1?.48f:.58f):ultimate?(phase==1?.84f:.9f):(phase==1?.62f:.7f);
            gain*=CombatSoundPalette.For(identity.Hero).Gain;
            // Traveler Q = Temporal Beam (+20%); E = Chrono Snare (-30%); R = Paradox Collapse (+20%).
            if(identity.Hero==HeroId.Traveler)gain*=action==1||action==4?1.20f:action==3?.70f:1f;
            // The reduced Nerok basic is the reference for all basic recordings.
            if(hero==(int)HeroId.Nerok&&action==0)gain*=.70f;
            if(hero==(int)HeroId.Nerok&&ultimate)gain*=.60f;
            // Other heroes start 30% quieter; distance attenuation is applied afterwards.
            float attenuation=local?1f:.70f;
            Ray ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if(!local&&new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))
            {
                Vector3 delta=position-ray.GetPoint(distance);delta.y=0;
                attenuation*=Mathf.Lerp(1f,ultimate?.28f:.12f,Mathf.InverseLerp(3,ultimate?30:22,delta.magnitude));
            }
            float pan=local?0:Mathf.Clamp((p.x-.5f)*1.1f,-.65f,.65f);
            // Per-actor throttling: other actors of the same class cannot silence the player.
            // The selected ultimate recording starts once; native Nova audio follows
            // Sivra's three existing releases without the one-second voice throttle.
            bool novaAudio=UsesNovaAudio(hero,action);
            float cooldown=novaAudio&&ultimate&&phase==0?.06f:ultimate&&phase==0?1f:phase==2?.045f:ultimate&&hero==2?.06f:phase==1?.065f:.08f;
            int cue=Cue(hero,action,phase);
            if(novaAudio&&phase==1&&Alterverse.Visuals.SivraNovaVfx.ScheduleImpactAudio(actor,action-1,position,
                ()=>DimensionAudio.PlayHero(cue,gain*attenuation,local?2:ultimate?1:0,cooldown,pan,1f,actor.GetEntityId().GetHashCode())))return;
            if(lightningAudio&&phase==1&&Alterverse.Visuals.TravelerLightningVfx.ScheduleImpactAudio(actor,action-1,position,
                ()=>DimensionAudio.PlayHero(cue,gain*attenuation,local?2:ultimate?1:0,cooldown,pan,1f,actor.GetEntityId().GetHashCode())))return;
            DimensionAudio.PlayHero(cue,gain*attenuation,local?2:ultimate?1:0,cooldown,pan,1f,actor.GetEntityId().GetHashCode());
        }
        // A confirmed hero kill is feedback for its owner, independent of camera visibility.
        public static void ConfirmedKill(GameObject killer,bool relay=true)
        {
            if(killer==null)return;
            if(relay)CoopHeroStateSync.BroadcastKillSound(killer);
            if(IsLocal(killer))DimensionAudio.Play(DimensionSound.Kill,.85f);
        }
        public static void Receive(GameObject actor,int action,int phase,Vector3 position)
        {if(phase>=0&&phase<=2)PlayPhase(actor,action,phase,position,false);}
        public static void Windup(GameObject actor,int action)=>PlayPhase(actor,action,2,actor.transform.position);
        public static void Cast(GameObject actor,int slot)=>Play(actor,slot+1,false,actor.transform.position);
        public static void Impact(GameObject actor,int slot,Vector3 point)=>Play(actor,slot+1,true,point);
    }
}
