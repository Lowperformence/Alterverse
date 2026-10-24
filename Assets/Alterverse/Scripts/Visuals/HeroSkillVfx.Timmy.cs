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
            Fx(HeroFxShape.Claw,point+Vector3.up*.12f,Accent(actor),slot<0?.50f:.72f,.20f,
                HeroFxMotion.Hold,Face(actor.transform.position,point),actor);
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
        }
        private static void TimmyDust(GameObject actor,Vector3 center,float radius,float duration,bool warning,float elapsed)
        {
            Vector3 p=Ground(center);
            HeroVfxPool.Emit(warning?HeroFxShape.Ring:HeroFxShape.Mist,p,p,Vector3.one*radius,
                warning?new Color(1f,.48f,.16f,.70f):new Color(.16f,.65f,.94f,.54f),duration,
                warning?HeroFxMotion.Hold:HeroFxMotion.Sustained,owner:actor,warning:warning,elapsed:elapsed);
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
                HeroVfxPool.Emit(HeroFxShape.Streak,p,p,new Vector3(.14f,.14f,.72f),cyan,jetLife,
                    HeroFxMotion.AttachedJet,exhaust,actor,follow:follow,elapsed:elapsed,cancelOnOwnerDeath:true);
            }
            if(scratch)
            {
                // Stay with the actual actor as Scratch steers toward its victim.
                Vector3 offset=Vector3.up*.36f+heading*.42f;
                HeroVfxPool.Emit(HeroFxShape.Claw,from+offset,to+offset,Vector3.one*.72f,cyan,
                    duration,HeroFxMotion.AttachedJet,facing,actor,follow:actor.transform,elapsed:elapsed,cancelOnOwnerDeath:true);
            }
            if(elapsed<duration)actor.GetComponent<HeroVisualMotion>()?.PulseDash(Mathf.Max(.04f,duration-elapsed));
        }
    }
}

