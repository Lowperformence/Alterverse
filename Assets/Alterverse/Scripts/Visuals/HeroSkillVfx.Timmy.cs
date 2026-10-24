using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class HeroSkillVfx
    {
        private Transform timmyJetL,timmyJetR;
        private void FindTimmyJets()
        {
            // The imported model can appear after the first dash cue. Cache only
            // complete live sockets, so later Scratch/Jet Pounce cues recover.
            if(timmyJetL!=null&&timmyJetR!=null)return;
            foreach(Transform part in GetComponentsInChildren<Transform>(true))
            {
                if(part.name=="DF_TimmyJetL")timmyJetL=part;
                if(part.name=="DF_TimmyJetR")timmyJetR=part;
            }
        }
        private static void TimmyClaws(GameObject actor,Vector3 point,int slot)
        {
            Color cyan=Accent(actor);Quaternion facing=Face(actor.transform.position,point);
            Vector3 center=point+Vector3.up*.12f;
            float size=slot<0?.40f:slot==3?.60f:.55f;
            Vector3 right=facing*Vector3.right;
            for(int i=-1;i<=1;i++)
                Fx(HeroFxShape.Claw,center+right*(i*.11f),
                    i==0?Color.Lerp(cyan,Color.white,.75f):cyan,size,.16f,
                    HeroFxMotion.Sweep,facing,actor);
        }
        private static void TimmyCast(GameObject actor,int slot)
        {
            Color cyan=Accent(actor);Vector3 p=Chest(actor);
            if(slot==0)TimmyClaws(actor,p+actor.transform.forward*.5f,slot);
            else if(slot==1)
                Fx(HeroFxShape.Shard,p,Color.Lerp(cyan,Color.white,.55f),.18f,.14f,HeroFxMotion.Contract,actor:actor);
        }
        // Only presentation; host gameplay remains responsible for the one area hit/leap.
        public static void TimmyEffect(GameObject actor,int kind,Vector3 from,Vector3 to,float radius,float duration,
            bool relay=true,float elapsed=0)
        {
            if(actor==null||Hero(actor)!=HeroId.Timmy)return;
            using var original=HeroVfxPool.PreserveNerokOriginal();
            if(kind==4||kind==6)
            {
                HeroVfxPool.CancelShape(actor,HeroFxShape.Claw,HeroFxMotion.TravelFixedFrame);
                HeroVfxPool.CancelShape(actor,HeroFxShape.Claw,HeroFxMotion.AttachedJet);
                HeroVfxPool.CancelShape(actor,HeroFxShape.Streak,HeroFxMotion.AttachedJet);
                HeroVfxPool.CancelShape(actor,HeroFxShape.TexturedQuad,HeroFxMotion.AttachedJet);
                if(kind==4)
                {
                    HeroVfxPool.CancelShape(actor,HeroFxShape.TimmyGrenade);
                    HeroVfxPool.CancelShape(actor,HeroFxShape.Halo);
                }
            }
            else if(kind==0||kind==1)TimmyDust(actor,to,radius,duration,kind==0,elapsed);
            else if(kind==2||kind==3)TimmyJet(actor,from,to,duration,kind==3,elapsed);
            else if(kind==5)TimmyGrenade(actor,from,to,duration,elapsed);
            else return;
            if(relay)Alterverse.Multiplayer.CoopHeroStateSync.BroadcastTimmyEffect(actor,kind,from,to,radius,duration);
        }
        public static void CancelTimmyDash(GameObject actor)
        {if(actor!=null&&Hero(actor)==HeroId.Timmy)TimmyEffect(actor,4,Vector3.zero,Vector3.zero,0,0);}
        public static void EndTimmyLeap(GameObject actor)
        {if(actor!=null&&Hero(actor)==HeroId.Timmy)TimmyEffect(actor,6,Vector3.zero,Vector3.zero,0,0);}
        private static void TimmyGrenade(GameObject actor,Vector3 from,Vector3 to,float duration,float elapsed)
        {
            // Reach the locked ground point at the existing W windup deadline.
            // ResolveArea still decides damage; this canister never follows a target.
            Vector3 landing=Ground(to)+Vector3.up*.08f;
            Quaternion facing=Face(from,landing);
            HeroVfxPool.Emit(HeroFxShape.TimmyGrenade,from,landing,Vector3.one*.23f,
                new Color(.80f,.85f,.90f,1),duration,HeroFxMotion.Lob,facing,actor,
                elapsed:elapsed,cancelOnOwnerDeath:true);
            HeroVfxPool.Emit(HeroFxShape.Halo,from,landing,Vector3.one*.115f,
                new Color(.13f,.85f,1f,.92f),duration,HeroFxMotion.Lob,facing,actor,
                elapsed:elapsed,cancelOnOwnerDeath:true);
        }
        private static void TimmyDust(GameObject actor,Vector3 center,float radius,float duration,bool warning,float elapsed)
        {
            Vector3 p=Ground(center);
            if(warning)
            {
                HeroVfxPool.Emit(HeroFxShape.Ring,p,p,Vector3.one*radius,new Color(1f,.48f,.16f,.70f),
                    duration,HeroFxMotion.Hold,owner:actor,warning:true,elapsed:elapsed);
                return;
            }
            Color mist=new Color(.16f,.65f,.94f,.54f);
            // Sustained keeps its true radius and opacity until the final 0.2s fade.
            HeroVfxPool.Emit(HeroFxShape.Mist,p+Vector3.up*.04f,p,Vector3.one*radius,mist,duration,
                HeroFxMotion.Sustained,owner:actor,elapsed:elapsed);
            HeroVfxPool.Emit(HeroFxShape.Mist,p+Vector3.up*.26f,p,Vector3.one*(radius*.92f),
                new Color(.42f,.27f,.82f,.30f),duration,HeroFxMotion.Sustained,
                Quaternion.Euler(0,55,0),actor,elapsed:elapsed);
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f;
                Vector3 star=p+new Vector3(Mathf.Cos(a)*radius*.55f,.32f,Mathf.Sin(a)*radius*.55f);
                HeroVfxPool.Emit(HeroFxShape.ContactStar,star,star,Vector3.one*.08f,
                    new Color(1f,.73f,.35f,.65f),duration,HeroFxMotion.Sustained,
                    Quaternion.Euler(90,0,0),actor,decorative:true,elapsed:elapsed);
            }
        }
        private static void TimmyJet(GameObject actor,Vector3 from,Vector3 to,float duration,bool scratch,float elapsed)
        {
            Vector3 heading=to-from;heading.y=0;
            if(heading.sqrMagnitude<.0001f)return;
            heading.Normalize();Quaternion facing=Quaternion.LookRotation(heading);
            Color cyan=Accent(actor);float jetLife=duration+.12f;
            var sockets=actor.GetComponent<HeroSkillVfx>()??actor.AddComponent<HeroSkillVfx>();
            sockets.FindTimmyJets();
            for(int side=-1;side<=1;side+=2)
            {
                Transform nozzle=side<0?sockets.timmyJetL:sockets.timmyJetR;
                Vector3 p=nozzle!=null?nozzle.position:Chest(actor)-heading*.22f+facing*Vector3.right*(side*.12f);
                Transform follow=nozzle!=null?nozzle:actor.transform;
                Quaternion exhaust=Quaternion.LookRotation((heading+Vector3.up*.35f).normalized);
                // One purchased flamethrower per live nozzle replaces the former stacked
                // flame sprite and two cores. Scratch's original claws below stay intact.
                HeroVfxPool.Emit(HeroFxShape.Streak,p,p,new Vector3(.055f,.055f,.38f),
                    Color.white,jetLife,HeroFxMotion.AttachedJet,exhaust*Quaternion.Euler(0,180f,0),actor,
                    follow:follow,elapsed:elapsed,cancelOnOwnerDeath:true,epicKey:"TimmyJet",effectRadius:.55f);
            }
            if(scratch)
            {
                Vector3 right=facing*Vector3.right;
                // Stay with the actual actor as Scratch steers toward its victim.
                for(int i=-1;i<=1;i++)
                {
                    Vector3 offset=right*(i*.20f)+Vector3.up*.36f+heading*.42f;
                    HeroVfxPool.Emit(HeroFxShape.Claw,from+offset,to+offset,
                        new Vector3(.72f,.72f,.60f),i==0?Color.Lerp(cyan,Color.white,.65f):cyan,
                        duration,HeroFxMotion.AttachedJet,facing,actor,follow:actor.transform,elapsed:elapsed,cancelOnOwnerDeath:true);
                }
            }
            if(elapsed<duration)actor.GetComponent<HeroVisualMotion>()?.PulseDash(Mathf.Max(.04f,duration-elapsed));
        }
    }
}

