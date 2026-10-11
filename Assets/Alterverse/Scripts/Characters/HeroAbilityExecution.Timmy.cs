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
        private HeroCombatStatus timmyHeldStatus, timmyChannelStatus;
        private float timmyPassiveReady;

        private void TriggerTimmyPassive()
        {
            if(identity.Hero!=HeroId.Timmy||self.IsDead||Time.time<timmyPassiveReady)return;
            timmyPassiveReady=Time.time+TimmyBalance.PassiveCooldown;
            (GetComponent<HeroCombatStatus>()??gameObject.AddComponent<HeroCombatStatus>()).BoostSpeed(TimmyBalance.PassiveSpeed,TimmyBalance.PassiveSeconds);
        }

        private bool TryCastTimmy(int slot,Health target,AimPose pose,bool manuallyAimed)
        {
            if(timmyScratchPending||locomotion.OwnsMovementFrame)return false;
            AimSpec aim=AimingSettings.Current.For(HeroId.Timmy,slot);
            Vector3 direction=pose.CastDirection(transform.position);
            if(slot==2)
            {
                Vector3 from=transform.position;
                Vector3 end=GameplayCollision.ClampToRoundedArena(from+direction*aim.castRange,40f,33f);
                if(!locomotion.BeginDash(end))return false;
                HeroCombatAudio.Cast(gameObject,slot);
                HeroSkillVfx.Dash(gameObject,from,locomotion.DashDestination);
                return true;
            }
            if(manuallyAimed)
                target=AimGeometry.CaptureConeTarget(gameObject,direction,aim);
            if(!ValidTarget(target,aim.castRange)||target.GetComponent<HeroIdentity>()==null||!SharkSight(target))return false;
            // The endpoint is captured once. Neither the leap nor damage follows an escaping target.
            Vector3 endPoint=AimGeometry.ClampPoint(transform.position,target.transform.position,aim.castRange);
            Vector3 approach=AimGeometry.Flat(endPoint-transform.position);
            endPoint-=AimGeometry.Direction(approach)*Mathf.Min(TimmyBalance.ScratchStandOff,approach.magnitude*.5f);
            timmyScratchPending=true;
            HeroCombatAudio.Windup(gameObject,4);
            HeroSkillVfx.TargetWarning(gameObject,target,TimmyBalance.ScratchWindup);
            StartCoroutine(TimmyScratch(target,endPoint,life));
            return true;
        }

        private bool TimmyChannelValid(Health target,int castLife)
        {
            return castLife==life&&isActiveAndEnabled&&!self.IsDead&&
                Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay&&
                GetComponent<HeroCombatStatus>()?.Stunned!=true&&
                HeroCombatRules.Enemy(gameObject,target)&&
                HeroCombatRules.TargetDistance(transform.position,target)<=TimmyBalance.ScratchContactRange&&
                !locomotion.IsForcedMoving&&!(target.GetComponent<HeroLocomotion>()?.IsForcedMoving??false)&&
                (target.DamageGate==null||target.DamageGate())&&SharkSight(target);
        }

        private IEnumerator TimmyScratch(Health target,Vector3 endPoint,int castLife)
        {
            yield return new WaitForSeconds(TimmyBalance.ScratchWindup);
            if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject)||!ValidTarget(target,TimmyBalance.ScratchRange))
            {EndTimmyScratch();yield break;}
            Vector3 from=transform.position;
            if(!SharkSightPoint(from,endPoint,target)||!locomotion.BeginDash(endPoint,TimmyBalance.ScratchLeapSeconds))
            {EndTimmyScratch();yield break;}
            HeroCombatAudio.Cast(gameObject,3);
            HeroSkillVfx.Dash(gameObject,from,locomotion.DashDestination);
            float leapDeadline=Time.time+TimmyBalance.ScratchLeapSeconds+.1f;
            while(locomotion.IsDashing&&Time.time<leapDeadline)
            {
                if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))
                {locomotion.ResetMotion();EndTimmyScratch();yield break;}
                yield return null;
            }
            if(locomotion.IsDashing){locomotion.ResetMotion();EndTimmyScratch();yield break;}
            if(!TimmyChannelValid(target,castLife)){EndTimmyScratch();yield break;}
            timmyHeldStatus=target.GetComponent<HeroCombatStatus>()??target.gameObject.AddComponent<HeroCombatStatus>();
            timmyChannelStatus=GetComponent<HeroCombatStatus>()??gameObject.AddComponent<HeroCombatStatus>();
            if(!timmyHeldStatus.TryRoot(this,TimmyBalance.ScratchHoldSeconds)||
                !timmyChannelStatus.TryRoot(this,TimmyBalance.ScratchHoldSeconds,true))
            {EndTimmyScratch();yield break;}
            GetComponent<HeroAttackDelivery>()?.CancelWindups();
            locomotion.FaceAction(target.transform.position-transform.position,TimmyBalance.ScratchHoldSeconds);
            float started=Time.time;int ticks=0;
            while(ticks<TimmyBalance.ScratchTicks)
            {
                if(!TimmyChannelValid(target,castLife)){EndTimmyScratch();yield break;}
                int due=Mathf.Min(TimmyBalance.ScratchTicks,Mathf.FloorToInt((Time.time-started+.00001f)/TimmyBalance.ScratchTickSeconds));
                for(;ticks<due;ticks++)
                {
                    if(!TimmyChannelValid(target,castLife)){EndTimmyScratch();yield break;}
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
            timmyHeldStatus?.ReleaseRoot(this);timmyChannelStatus?.ReleaseRoot(this);
            timmyHeldStatus=timmyChannelStatus=null;timmyScratchPending=false;
        }
    }
}
