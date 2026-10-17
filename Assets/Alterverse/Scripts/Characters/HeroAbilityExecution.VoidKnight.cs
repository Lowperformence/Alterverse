using System.Collections;
using System.Collections.Generic;
using Alterverse.Audio;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;
namespace Alterverse.Characters
{
    public sealed partial class HeroAbilityExecution
    {
        private bool voidPending;
        private VoidKnightState VoidState=>GetComponent<VoidKnightState>()??gameObject.AddComponent<VoidKnightState>();
        private bool VoidValid(int castLife)=>castLife==life&&isActiveAndEnabled&&!self.IsDead&&CoopAuthority.CanSimulateHostGameplay&&GetComponent<HeroCombatStatus>()?.Stunned!=true;
        private void CancelVoidKnight()
        {
            if(identity==null||identity.Hero!=HeroId.VoidKnight)return;
            voidPending=false;GetComponent<HeroCombatStatus>()?.ReleaseRoot(this);GetComponent<VoidKnightState>()?.CancelGuard();HeroSkillVfx.CancelVoidKnightWarnings(gameObject);
            if(locomotion!=null&&locomotion.IsDashing)locomotion.ResetMotion();
        }
        private bool TryCastVoidKnight(int slot,AimPose pose)
        {
            if(voidPending||locomotion.OwnsMovementFrame)return false;
            Vector3 direction=slot==3?AimGeometry.Direction(pose.Direction):pose.CastDirection(transform.position);
            if(slot==1){if(!VoidState.BeginGuard())return false;HeroCombatAudio.Cast(gameObject,1);return true;}
            var status=GetComponent<HeroCombatStatus>()??gameObject.AddComponent<HeroCombatStatus>();
            if(!status.TryRoot(this,kit.Spec(slot).Windup,true))return false;
            voidPending=true;locomotion.FaceAction(direction,kit.Spec(slot).Windup+.35f);HeroCombatAudio.Windup(gameObject,slot+1);
            HeroSkillVfx.VoidKnightField(gameObject,direction,kit.Spec(slot).Range,kit.Spec(slot).Windup,true,slot==2);
            StartCoroutine(slot==2?VoidLunge(direction,life):VoidCleave(direction,life));return true;
        }
        private IEnumerator VoidCleave(Vector3 direction,int castLife)
        {
            yield return new WaitForSeconds(kit.Ultimate.Windup);GetComponent<HeroCombatStatus>()?.ReleaseRoot(this);
            if(!VoidValid(castLife)){voidPending=false;yield break;}
            HeroCombatAudio.Cast(gameObject,3);HeroSkillVfx.VoidKnightField(gameObject,direction,kit.Ultimate.Range,.3f,false);
            bool confirmed=false;
            Vector3 fieldOrigin=transform.position;
            float activeUntil=Time.time+.30f;
            // The cleave's visible 0.30-second cone IS its active hitbox. Units
            // entering from either side during this interval are eligible; each
            // receives the ultimate at most once, regardless of frame rate.
            HashSet<Health> struck=new HashSet<Health>();
            do
            {
                if(!VoidValid(castLife))break;
                for(int i=Health.Active.Count-1;i>=0;i--)
                {
                    Health target=Health.Active[i];
                    if(!HeroCombatRules.Enemy(gameObject,target)||struck.Contains(target)||
                        !AimGeometry.ConeBody(target,fieldOrigin,direction,kit.Ultimate.Range,80f,true)||
                        !SharkSight(target))continue;
                    struck.Add(target);
                    float before=target.Current+target.Shield,healthBefore=target.Current;
                    HeroCombatRules.Damage(gameObject,target,kit.Ultimate.Power+VoidKnightBalance.UltimateLifeDrain);
                    if(target.Current+target.Shield>=before)continue;
                    float absorbed=Mathf.Min(VoidKnightBalance.UltimateLifeDrain,Mathf.Max(0,healthBefore-target.Current));
                    if(absorbed>0)self.Heal(absorbed,false);
                    confirmed=true;HeroCombatStatus.Apply(target,.75f,1.2f);HeroSkillVfx.Impact(gameObject,target,3);
                }
                if(Time.time>=activeUntil)break;
                yield return null;
            }
            while(Time.time<=activeUntil);
            if(confirmed){VoidState.RecordSkillHit();HeroCombatAudio.Impact(gameObject,3,fieldOrigin+direction*2f);}voidPending=false;
        }
        private IEnumerator VoidLunge(Vector3 direction,int castLife)
        {
            yield return new WaitForSeconds(kit.E.Windup);GetComponent<HeroCombatStatus>()?.ReleaseRoot(this);
            if(!VoidValid(castLife)){voidPending=false;yield break;}
            Vector3 from=transform.position;
            if(!locomotion.BeginDash(GameplayCollision.ClampToRoundedArena(from+direction*kit.E.Range,40f,33f),VoidKnightBalance.DashSeconds)){voidPending=false;yield break;}
            HeroCombatAudio.Cast(gameObject,2);HeroSkillVfx.Dash(gameObject,from,locomotion.DashDestination,2,VoidKnightBalance.DashSeconds);
            Vector3 previous=from;float deadline=Time.time+VoidKnightBalance.DashSeconds+.12f;
            while(VoidValid(castLife)&&Time.time<=deadline)
            {
                Vector3 current=transform.position;Health first=null;float earliest=float.MaxValue;
                for(int i=0;i<Health.Active.Count;i++)
                {
                    Health target=Health.Active[i];if(!HeroCombatRules.Enemy(gameObject,target)||!SharkSight(target))continue;
                    float fraction;bool contact=AimGeometry.Sweep(previous,current,target.transform.position,target.transform.position,VoidKnightBalance.DashContactRadius,out fraction);
                    if(!contact&&target.GetComponent<HeroIdentity>()==null&&target.GetComponent<LaneMinion>()==null&&HeroCombatRules.TargetDistance(current,target)<=VoidKnightBalance.DashContactRadius){contact=true;fraction=1f;}
                    if(!contact||fraction>=earliest)continue;first=target;earliest=fraction;
                }
                if(first!=null)
                {
                    Vector3 contact=Vector3.Lerp(previous,current,Mathf.Clamp01(earliest));locomotion.ResetMotion();locomotion.Move(contact-current,40f,33f,false);Hit(2,first,true,true);break;
                }
                previous=current;if(!locomotion.IsDashing)break;yield return null;
            }
            if(locomotion.IsDashing)locomotion.ResetMotion();voidPending=false;
        }
        internal void VoidKnightCounter(Vector3 direction)
        {
            if(identity.Hero!=HeroId.VoidKnight||!VoidValid(life)||voidPending)return;
            voidPending=true;CombatMotion.BeginAimed(gameObject,VoidKnightBalance.CounterWindup,transform.position+direction*VoidKnightBalance.CounterRange);GetComponent<VoidKnightPresentation>()?.BeginCounter();StartCoroutine(VoidCounterStrike(direction,life));
        }
        private IEnumerator VoidCounterStrike(Vector3 direction,int castLife)
        {
            HeroCombatAudio.Windup(gameObject,1);yield return new WaitForSeconds(VoidKnightBalance.CounterWindup);
            if(VoidValid(castLife))
            {
                HeroCombatAudio.Cast(gameObject,0);Health target=AimGeometry.Select(gameObject,direction,VoidKnightBalance.CounterRange,40f,false);
                if(target!=null&&SharkSight(target))
                {
                    float before=target.Current+target.Shield;HeroCombatRules.Damage(gameObject,target,VoidKnightBalance.CounterDamage);
                    if(target.Current+target.Shield<before){VoidState.RecordSkillHit();HeroSkillVfx.Impact(gameObject,target,0);HeroCombatAudio.Impact(gameObject,0,target.transform.position);}
                }
            }
            voidPending=false;
        }
    }
}
