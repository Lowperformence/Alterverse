using Alterverse.Characters;
using Alterverse.Multiplayer;
using UnityEngine;
namespace Alterverse.Audio
{
    public static class HeroCombatAudio
    {
        // Preserve original cast/impact cue indices; preparation occupies the appended bank.
        public const int CueCount=HeroIdentity.HeroCount*15;
        static readonly string[] Heroes={"Veyr","Kaelon","Nyra","Elyra","Mira","Rook","Traveler","Nerok"};
        static readonly string[] Actions={"Basic","Q","W","E","R"};
        // User-selected recordings are complete cues. Play them once at ability release,
        // not over the former synthesized preparation/impact layers (or once per AoE victim).
        public static bool UsesSelectedRecording(int hero,int action)=>hero switch
        {
            0=>action>=1&&action<=3,
            1=>action>=1&&action<=3,
            2=>action>=1&&action<=3,
            3=>action>=1&&action<=3,
            4=>action>=1&&action<=3,
            5=>action>=1&&action<=3,
            6=>action>=1&&action<=3,
            7=>action>=1&&action<=4,
            _=>false
        };
        public static bool HasImpact(int hero,int action)=>!UsesSelectedRecording(hero,action)&&(action==0||(hero switch
        {
            0=>action!=3,1=>action!=2,2=>action!=2,3=>action!=3,4=>action==1||action==4,5=>action!=2,6=>action!=2,7=>action!=2,_=>false
        }));
        public static bool HasWindup(int hero,int action)=>!UsesSelectedRecording(hero,action)&&(action==0||HasImpact(hero,action));
        public static int VariantCount(int hero,int action,int phase)=>hero==7?1:phase!=2&&(action==0||phase==1&&hero==2&&action==4)?3:1;
        public static int Cue(int hero,int action,int phase)=>hero==7?105+action*3+phase:phase==2?70+hero*5+action:hero*10+action*2+phase;
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
                    clips[cue][v]=Resources.Load<AudioClip>(path);
                    if(clips[cue][v]==null)Debug.LogWarning("DIMENSIONS FRONT: missing combat audio: "+path);
                }
            }
            return clips;
        }
        public static bool IsLocal(GameObject actor)
        {
            var slot=actor.GetComponent<CoopHeroSlot>();
            return slot!=null?slot.IsLocalHuman:actor.GetComponent<Alterverse.Combat.BasicAttack>()!=null;
        }
        public static void Play(GameObject actor,int action,bool impact,Vector3 position)=>PlayPhase(actor,action,impact?1:0,position);
        static void PlayPhase(GameObject actor,int action,int phase,Vector3 position,bool relay=true)
        {
            if(actor==null||action<0||action>4)return;
            var identity=actor.GetComponent<HeroIdentity>();if(identity==null)return;
            int hero=(int)identity.Hero;
            if(hero==(int)HeroId.Nerok&&action==0&&phase!=1)return;
            if(hero<0||hero>=Heroes.Length||phase==1&&!HasImpact(hero,action)||phase==2&&!HasWindup(hero,action))return;
            if(relay)CoopHeroStateSync.BroadcastCombatSound(actor,action,phase,position);
            bool local=IsLocal(actor),ultimate=action==4;
            Camera camera=Camera.main;if(camera==null)return;
            Vector3 p=camera.WorldToViewportPoint(position);
            if(!local&&(p.z<=0||p.x<-.15f||p.x>1.15f||p.y<-.15f||p.y>1.15f))return;
            float gain=phase==2?.32f:action==0?(phase==1?.48f:.58f):ultimate?(phase==1?.84f:.9f):(phase==1?.62f:.7f);
            gain*=CombatSoundPalette.For(identity.Hero).Gain;
            // Traveler Q = Temporal Beam (+20%); E = Chrono Snare (-30%); R = Paradox Collapse (+20%).
            if(identity.Hero==HeroId.Traveler)gain*=action==1||action==4?1.20f:action==3?.70f:1f;
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
            // Sivra fires four shots in .54s; the selected ultimate recording starts only once.
            float cooldown=ultimate&&phase==0?1f:phase==2?.045f:ultimate&&hero==2?.06f:phase==1?.065f:.08f;
            int cue=Cue(hero,action,phase);
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
