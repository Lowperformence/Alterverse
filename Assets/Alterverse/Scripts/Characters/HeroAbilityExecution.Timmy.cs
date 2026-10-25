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
            if (!pose.AllowDashTargetCapture) target = null;
            if((slot==3&&timmyScratchPending)||locomotion.OwnsMovementFrame)return false;
            AimSpec aim=AimingSettings.Current.For(HeroId.Timmy,slot);
            Vector3 direction=pose.CastDirection(transform.position);
            if(slot==2)
            {
                Vector3 from=transform.position;
                Vector3 end=GameplayCollision.ClampToRoundedArena(from+direction*aim.castRange,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight);
                if(!locomotion.BeginDash(end,TimmyBalance.JetDashSeconds))return false;
                self.AddShield(TimmyBalance.JetShield,TimmyBalance.JetShieldSeconds,false);
                HeroCombatAudio.Cast(gameObject,slot);
                HeroSkillVfx.Dash(gameObject,from,locomotion.DashDestination,2,TimmyBalance.JetDashSeconds);
                return true;
            }
            // The retained cone is the one the player actually saw. A captured
            // target point must not rotate/narrow that field at release.
            direction=AimGeometry.Direction(pose.Direction);
            Vector3 fieldOrigin=transform.position;
            if(manuallyAimed&&target==null)
                target=AimGeometry.CaptureConeTarget(gameObject,direction,aim);
            if(!TimmyScratchInField(target,fieldOrigin,direction,aim))target=null;
            if(target!=null&&!SharkSight(target))return false;
            Vector3 jumpPoint=target!=null?target.transform.position:
                transform.position+direction*aim.castRange;
            timmyScratchPending=true;
            HeroCombatAudio.Windup(gameObject,4);
            if(target!=null)HeroSkillVfx.TargetWarning(gameObject,target,TimmyBalance.ScratchWindup);
            StartCoroutine(TimmyScratch(target,jumpPoint,fieldOrigin,direction,aim,life,manuallyAimed));
            return true;
        }

        private bool TimmyScratchInField(Health target,Vector3 origin,Vector3 direction,AimSpec aim)
            =>HeroCombatRules.Enemy(gameObject,target)&&target.GetComponent<HeroIdentity>()!=null&&
                AimGeometry.Cone(origin,direction,target.transform.position,aim.castRange,aim.coneAngle);

        private bool TimmyScratchContact(Health target,int castLife)
            =>TimmyScratchContact(target,castLife,out _);

        private bool TimmyScratchContact(Health target,int castLife,out float fraction)
        {
            fraction=1f;
            if(!TimmyScratchValid(target,castLife,false)||!SharkSight(target))return false;
            HeroLocomotion other=target.GetComponent<HeroLocomotion>();
            if(AimGeometry.Sweep(locomotion.FrameStartPosition,transform.position,
                other!=null?other.FrameStartPosition:target.transform.position,target.transform.position,
                TimmyBalance.ScratchContactRange,out fraction))return true;
            fraction=1f;
            return HeroCombatRules.TargetDistance(transform.position,target)<=TimmyBalance.ScratchContactRange;
        }

        private Health TimmyScratchPathContact(Vector3 fieldOrigin,Vector3 fieldDirection,AimSpec aim,int castLife)
        {
            Health first=null;float earliest=float.MaxValue;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health candidate=Health.Active[i];
                if(!HeroCombatRules.Enemy(gameObject,candidate)||candidate.GetComponent<HeroIdentity>()==null||
                    !TimmyScratchContact(candidate,castLife,out float fraction)||fraction>=earliest)continue;
                HeroLocomotion other=candidate.GetComponent<HeroLocomotion>();
                Vector3 contact=Vector3.Lerp(other!=null?other.FrameStartPosition:candidate.transform.position,
                    candidate.transform.position,fraction);
                if(!AimGeometry.Cone(fieldOrigin,fieldDirection,contact,aim.castRange,aim.coneAngle))continue;
                first=candidate;earliest=fraction;
            }
            return first;
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

        private IEnumerator TimmyScratch(Health target,Vector3 jumpPoint,Vector3 fieldOrigin,Vector3 fieldDirection,AimSpec aim,int castLife,bool manuallyAimed)
        {
            // An empty-field cast is still a physical attack: enemies can enter its
            // fixed leap path later. A selected victim retains the existing dodge
            // rules and must never be silently replaced after leaving the field.
            bool freeFlight=target==null;
            float warningEnds=Time.time+TimmyBalance.ScratchWindup;
            while(Time.time<warningEnds)
            {
                // Leaving the original field revokes this victim for the whole
                // cast, including a later return. The directional leap remains.
                if(target!=null&&!TimmyScratchInField(target,fieldOrigin,fieldDirection,aim))target=null;
                yield return null;
            }
            if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))
            {EndTimmyScratch();yield break;}
            if(freeFlight&&manuallyAimed)
            {
                // The displayed field also accepts heroes who enter during its warning.
                // Movement taps stay directional; an earlier selected victim is never replaced.
                Health entrant=AimGeometry.Select(gameObject,fieldOrigin,fieldDirection,aim.castRange,aim.coneAngle*.5f,heroesOnly:true);
                if(entrant!=null&&SharkSight(entrant)){target=entrant;freeFlight=false;}
            }
            Vector3 from=transform.position;
            // An enemy leaving acquisition range does not cancel the committed
            // leap. Continue in the chosen direction, without any target hit.
            if(!TimmyScratchInField(target,fieldOrigin,fieldDirection,aim)||!SharkSight(target))target=null;
            Vector3 endPoint=AimGeometry.ClampPoint(from,target!=null?target.transform.position:jumpPoint,aim.castRange);
            Vector3 approach=AimGeometry.Flat(endPoint-from);
            if(target!=null)endPoint-=AimGeometry.Direction(approach)*Mathf.Min(TimmyBalance.ScratchStandOff,approach.magnitude*.5f);
            bool touching=target!=null&&TimmyScratchValid(target,castLife);
            if((target!=null&&!SharkSightPoint(from,endPoint,target))||(!touching&&!locomotion.BeginDash(endPoint,TimmyBalance.ScratchLeapSeconds)))
            {EndTimmyScratch();yield break;}
            HeroCombatAudio.Cast(gameObject,3);
            if(!touching)HeroSkillVfx.Dash(gameObject,from,locomotion.DashDestination,3,TimmyBalance.ScratchLeapSeconds);
            float leapEnds=Time.time+TimmyBalance.ScratchLeapSeconds;
            float leapDeadline=leapEnds+.1f;
            bool connected=touching;
            while(locomotion.IsDashing&&Time.time<leapDeadline)
            {
                if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))
                {locomotion.ResetMotion();EndTimmyScratch();yield break;}
                if(target!=null&&!TimmyScratchInField(target,fieldOrigin,fieldDirection,aim))target=null;
                if(freeFlight&&target==null)target=TimmyScratchPathContact(fieldOrigin,fieldDirection,aim,castLife);
                if(TimmyScratchContact(target,castLife))
                {connected=true;locomotion.ResetMotion();break;}
                if(target!=null&&SharkSight(target))
                {
                    // Follow the chosen victim only while it remains inside
                    // the original release field. Keep the original deadline.
                    Vector3 delta=AimGeometry.Flat(target.transform.position-transform.position);
                    Vector3 destination=target.transform.position-AimGeometry.Direction(delta)*Mathf.Min(TimmyBalance.ScratchStandOff,delta.magnitude*.5f);
                    locomotion.RetargetDash(destination,Mathf.Max(0f,leapEnds-Time.time));
                }
                yield return null;
            }
            if(locomotion.IsDashing){locomotion.ResetMotion();EndTimmyScratch();yield break;}
            if(target!=null&&!TimmyScratchInField(target,fieldOrigin,fieldDirection,aim))target=null;
            if(freeFlight&&target==null)target=TimmyScratchPathContact(fieldOrigin,fieldDirection,aim,castLife);
            if(!connected)connected=TimmyScratchContact(target,castLife);
            if(!connected&&Time.time+.000001f>=leapEnds&&TimmyScratchValid(target,castLife,false)&&SharkSight(target))
            {
                // A long landing frame can move the victim after locomotion
                // reached the previous sample. Finish at the live victim using
                // the normal collision sweep; never teleport through a wall.
                Vector3 delta=AimGeometry.Flat(target.transform.position-transform.position);
                Vector3 step=delta-AimGeometry.Direction(delta)*Mathf.Min(TimmyBalance.ScratchStandOff,delta.magnitude*.5f);
                locomotion.Move(step,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight,false);
                connected=TimmyScratchContact(target,castLife);
            }
            if(!connected||!TimmyScratchValid(target,castLife,false)){EndTimmyScratch();yield break;}
            HeroSkillVfx.EndTimmyLeap(gameObject);
            timmyHeldStatus=target.GetComponent<HeroCombatStatus>()??target.gameObject.AddComponent<HeroCombatStatus>();
            if(!timmyHeldStatus.TryRoot(this,TimmyBalance.ScratchHoldSeconds))
            {EndTimmyScratch();yield break;}
            HeroSkillVfx.TimmyHold(gameObject,target,TimmyBalance.ScratchHoldSeconds);
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
            if(timmyHeldStatus!=null)HeroSkillVfx.TimmyHold(gameObject,timmyHeldStatus.GetComponent<Health>(),0);
            timmyHeldStatus?.ReleaseRoot(this);
            timmyHeldStatus=null;timmyScratchPending=false;
        }
    }
}


