using System.Collections;
using Alterverse.Audio;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Characters
{
    public sealed partial class HeroAbilityExecution
    {
        private bool timmyScratchPending;
        private HeroCombatStatus timmyHeldStatus;
        private float timmyPassiveReady;

        private void TriggerTimmyPassive()
        {
            if(identity.Hero!=HeroId.Timmy||self.IsDead||Time.time<timmyPassiveReady)return;
            timmyPassiveReady=Time.time+TimmyBalance.PassiveCooldown;
            (GetComponent<HeroCombatStatus>()??gameObject.AddComponent<HeroCombatStatus>()).BoostSpeed(TimmyBalance.PassiveSpeed,TimmyBalance.PassiveSeconds);
        }

        private bool TryCastTimmy(int slot,Health target,AimPose pose,bool manuallyAimed)
        {
            if((slot==3&&timmyScratchPending)||locomotion.OwnsMovementFrame)return false;
            AimSpec aim=AimingSettings.Current.For(HeroId.Timmy,slot);
            Vector3 direction=pose.CastDirection(transform.position);
            if(slot==2)
            {
                Vector3 from=transform.position;
                Vector3 end=GameplayCollision.ClampToRoundedArena(from+direction*aim.castRange,40f,33f);
                if(!locomotion.BeginDash(end,TimmyBalance.JetDashSeconds))return false;
                HeroCombatAudio.Cast(gameObject,slot);
                HeroSkillVfx.Dash(gameObject,from,locomotion.DashDestination,2,TimmyBalance.JetDashSeconds);
                return true;
            }
            if(manuallyAimed)
                target=AimGeometry.CaptureConeTarget(gameObject,direction,aim);
            if(!ValidTarget(target,aim.castRange)||target.GetComponent<HeroIdentity>()==null)target=null;
            if(target!=null&&!SharkSight(target))return false;
            Vector3 jumpPoint=target!=null?target.transform.position:
                transform.position+direction*aim.castRange;
            timmyScratchPending=true;
            HeroCombatAudio.Windup(gameObject,4);
            if(target!=null)HeroSkillVfx.TargetWarning(gameObject,target,TimmyBalance.ScratchWindup);
            StartCoroutine(TimmyScratch(target,jumpPoint,life));
            return true;
        }

        private bool TimmyScratchValid(Health target,int castLife,bool requireContact=true)
        {
            return castLife==life&&isActiveAndEnabled&&!self.IsDead&&
                Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay&&
                GetComponent<HeroCombatStatus>()?.Stunned!=true&&
                HeroCombatRules.Enemy(gameObject,target)&&
                (!requireContact||HeroCombatRules.TargetDistance(transform.position,target)<=TimmyBalance.ScratchContactRange)&&
                !locomotion.IsForcedMoving&&!(target.GetComponent<HeroLocomotion>()?.IsForcedMoving??false)&&
                (target.DamageGate==null||target.DamageGate())&&(!requireContact||SharkSight(target));
        }

        private IEnumerator TimmyScratch(Health target,Vector3 jumpPoint,int castLife)
        {
            yield return new WaitForSeconds(TimmyBalance.ScratchWindup);
            if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))
            {EndTimmyScratch();yield break;}
            Vector3 from=transform.position;
            // An enemy leaving acquisition range does not cancel the committed
            // leap. Continue in the chosen direction, without any target hit.
            if(!ValidTarget(target,TimmyBalance.ScratchRange)||!SharkSight(target))target=null;
            // Refresh once at actual takeoff after the warning. The leap then
            // remains fixed and dodgeable; it never steers after the target.
            Vector3 endPoint=AimGeometry.ClampPoint(from,target!=null?target.transform.position:jumpPoint,TimmyBalance.ScratchRange);
            Vector3 approach=AimGeometry.Flat(endPoint-from);
            if(target!=null)endPoint-=AimGeometry.Direction(approach)*Mathf.Min(TimmyBalance.ScratchStandOff,approach.magnitude*.5f);
            bool touching=target!=null&&TimmyScratchValid(target,castLife);
            if((target!=null&&!SharkSightPoint(from,endPoint,target))||(!touching&&!locomotion.BeginDash(endPoint,TimmyBalance.ScratchLeapSeconds)))
            {EndTimmyScratch();yield break;}
            HeroCombatAudio.Cast(gameObject,3);
            if(!touching)HeroSkillVfx.Dash(gameObject,from,locomotion.DashDestination,3,TimmyBalance.ScratchLeapSeconds);
            float leapDeadline=Time.time+TimmyBalance.ScratchLeapSeconds+.1f;
            while(locomotion.IsDashing&&Time.time<leapDeadline)
            {
                if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))
                {locomotion.ResetMotion();EndTimmyScratch();yield break;}
                // Connect when the selected body is reached along the path,
                // instead of jumping past it and checking only the endpoint.
                if(TimmyScratchValid(target,castLife))
                {locomotion.ResetMotion();break;}
                yield return null;
            }
            if(locomotion.IsDashing){locomotion.ResetMotion();EndTimmyScratch();yield break;}
            if(!TimmyScratchValid(target,castLife)){EndTimmyScratch();yield break;}
            HeroSkillVfx.EndTimmyLeap(gameObject);
            timmyHeldStatus=target.GetComponent<HeroCombatStatus>()??target.gameObject.AddComponent<HeroCombatStatus>();
            if(!timmyHeldStatus.TryRoot(this,TimmyBalance.ScratchHoldSeconds))
            {EndTimmyScratch();yield break;}
            float started=Time.time;int ticks=0;
            while(ticks<TimmyBalance.ScratchTicks)
            {
                // Once contact succeeds, ordinary caster movement and casts do
                // not release the victim or discard the remaining four ticks.
                if(!TimmyScratchValid(target,castLife,false)){EndTimmyScratch();yield break;}
                int due=Mathf.Min(TimmyBalance.ScratchTicks,Mathf.FloorToInt((Time.time-started+.00001f)/TimmyBalance.ScratchTickSeconds));
                for(;ticks<due;ticks++)
                {
                    if(!TimmyScratchValid(target,castLife,false)){EndTimmyScratch();yield break;}
                    float before=target.Current+target.Shield;
                    HeroCombatRules.Damage(gameObject,target,kit.Ultimate.Power/TimmyBalance.ScratchTicks);
                    if(target.Current+target.Shield<before)
                    {
                        TriggerTimmyPassive();HeroSkillVfx.Impact(gameObject,target,3);
                        HeroCombatAudio.Impact(gameObject,3,target.transform.position);
                        HeroSkillVfx.Spell(gameObject,target.transform.position,3);
                        CombatMotion.BeginAimed(gameObject,.001f,target.transform.position);
                    }
                    if(target.IsDead){EndTimmyScratch();yield break;}
                }
                if(ticks<TimmyBalance.ScratchTicks)yield return null;
            }
            EndTimmyScratch();
        }

        private void EndTimmyScratch()
        {
            if(identity!=null&&identity.Hero==HeroId.Timmy)HeroSkillVfx.EndTimmyLeap(gameObject);
            timmyHeldStatus?.ReleaseRoot(this);
            timmyHeldStatus=null;timmyScratchPending=false;
        }
    }
}
