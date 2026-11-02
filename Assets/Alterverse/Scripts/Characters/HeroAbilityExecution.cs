using System.Collections;
using Alterverse.Audio;
using Alterverse.AI;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
namespace Alterverse.Characters
{
    [RequireComponent(typeof(Health), typeof(HeroIdentity), typeof(HeroAbilityKit))]
    public sealed partial class HeroAbilityExecution : MonoBehaviour
    {
        private const int SightlineBreakShots=3;
        private Health self;
        private HeroLocomotion locomotion;
        private HeroIdentity identity;
        private HeroAbilityKit kit;
        private readonly float[] ready = new float[4];
        private readonly System.Collections.Generic.HashSet<Health> resolvedTargets=new();
        private int life;
        private bool wasStunned;
        private bool sivraDashPassivePending;
        private readonly RaycastHit[] sightHits=new RaycastHit[16];
        private void Awake()
        {
            locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();
            self = GetComponent<Health>(); identity = GetComponent<HeroIdentity>(); kit = GetComponent<HeroAbilityKit>();
            ready[3] = Time.time + HeroAbilityKit.UltimateInitialLock;
            self.Died += CancelPending;
        }
        private void Update()
        {
            bool stunned=GetComponent<HeroCombatStatus>()?.Stunned==true;
            if(stunned&&!wasStunned)
            {
                // Interrupt preparation/channels, not released cone attacks or an
                // incoming forced displacement owned by another hero (for example the hook).
                GetComponent<HeroAttackDelivery>()?.CancelWindups();
                sivraDashPassivePending=false;
                EndTimmyScratch();CancelVoidKnight();life++;HeroSkillVfx.CancelWarnings(gameObject);StopAllCoroutines();
            }
            wasStunned=stunned;
            // Only a completed, authoritative Vector Shift activates Sivra's
            // post-dash run speed. This does not alter dash distance or travel speed.
            if(sivraDashPassivePending&&!locomotion.IsDashing)
            {
                sivraDashPassivePending=false;
                if(isActiveAndEnabled&&!self.IsDead&&!stunned&&
                    Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
                    (GetComponent<HeroCombatStatus>()??gameObject.AddComponent<HeroCombatStatus>())
                        .BoostSpeed(HeroAbilityKit.SivraDashSpeedMultiplier,
                            HeroAbilityKit.SivraDashSpeedSeconds);
            }
        }
        private void CancelPending() { sivraDashPassivePending=false; EndTimmyScratch();CancelVoidKnight();GetComponent<HeroAttackDelivery>()?.Cancel(); life++; HeroSkillVfx.CancelWarnings(gameObject); StopAllCoroutines(); if(locomotion!=null)locomotion.ResetMotion(); }
        private void OnDisable() => CancelPending();
        private void OnDestroy() { if (self != null) self.Died -= CancelPending; }
        public float Remaining(int slot) => Mathf.Max(0, ready[Mathf.Clamp(slot, 0, 3)] - Time.time);
        public float Cooldown(int slot) => HeroCombatRules.Interval(gameObject, kit.Spec(slot).Cooldown);
        public void MarkNetworkRequestCooldown(int slot)
        {
            slot=Mathf.Clamp(slot,0,3);
            ready[slot]=Time.time+Cooldown(slot);
            GetComponent<HeroVisualMotion>()?.PulseAbility(slot,HeroAttackDelivery.AbilityWindup(identity.Hero,slot,kit.Spec(slot)));
        }

        public void ApplyNetworkCooldown(int slot,float remaining,uint acknowledgedCommand=0)
        {
            if(!Alterverse.Multiplayer.CoopAuthority.Active||Alterverse.Multiplayer.CoopAuthority.LocalIsHost)return;
            slot=Mathf.Clamp(slot,0,3);
            if(!Alterverse.Multiplayer.CoopGameplayCommands.CanApplyCooldown(gameObject,slot,acknowledgedCommand))return;
            ready[slot]=Time.time+Mathf.Max(0f,remaining);
        }

        public Health WoundedAlly(float range, bool shield = false)
        {
            TeamMember team = GetComponent<TeamMember>(); if (team == null) return null;
            Health best = null; float lowest = shield ? 1.01f : .95f;
            float rangeSqr=range*range;
            // Support targeting must not depend on a physics collider. Heroes are valid combat
            // targets through Health/TeamMember even while a collider is absent or temporarily disabled.
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health h=Health.Active[i];
                if(h==null||h.IsDead||!h.isActiveAndEnabled||h.GetComponent<HeroIdentity>()==null||h.Normalized>=lowest)continue;
                TeamMember member=h.GetComponent<TeamMember>();
                if(member==null||member.Team!=team.Team||(shield&&h.TemporaryShield>5f))continue;
                Vector3 delta=h.transform.position-transform.position;delta.y=0;
                if(delta.sqrMagnitude>rangeSqr)continue;
                lowest=h.Normalized;best=h;
            }
            return best;
        }
        public bool TryCast(int slot, Health target, Vector3 movement)
            => TryCastCore(slot, target, movement, null);
        public bool TryCastAimed(int slot, AimPose pose)
        {
            if (slot < 0 || slot > 3 || !AimGeometry.Finite(pose.Direction) || !AimGeometry.Finite(pose.Point)) return false;
            AimSpec aim = AimingSettings.Current.For(identity.Hero, slot);
            pose = new AimPose(pose.Direction, AimGeometry.ClampPoint(transform.position, pose.Point, aim.castRange), pose.Target, pose.HasAimPoint, pose.AllowDashTargetCapture);
            return TryCastCore(slot, pose.Target, pose.Direction, pose);
        }
        private bool TryCastCore(int slot, Health target, Vector3 movement, AimPose? aimed)
        {
            if (!isActiveAndEnabled || !Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay || slot < 0 || slot > 3 || Time.timeScale <= 0 || self.IsDead || !HeroCombatStatus.CanAct(gameObject) || Remaining(slot) > 0) return false;
            if(identity.Hero==HeroId.Bolt7&&!Bolt7Balance.HasAbility(slot))return false;
            if(identity.Hero==HeroId.VoidKnight&&voidPending)return false;
            AbilitySpec spec = kit.Spec(slot); HeroId hero = identity.Hero;
            float presentationWindup=spec.Windup;
            AimSpec aimSpec = AimingSettings.Current.For(hero, slot);
            AimPose attackAim = aimed ?? new AimPose(target != null ? target.transform.position - transform.position : movement,
                target != null ? target.transform.position : transform.position, null, target != null);
            bool usesDelivery = hero != HeroId.Bolt7 && (aimSpec.aimType == AimType.Melee || aimSpec.aimType == AimType.Projectile ||
                aimSpec.aimType == AimType.Beam || aimSpec.aimType == AimType.Homing);
            if (usesDelivery && !HeroAttackDelivery.For(gameObject).HasCapacity(hero == HeroId.Nyra && slot == 3 ? SightlineBreakShots : 1)) return false;
            bool mobility = (hero == HeroId.Veyr && slot == 2) || (hero == HeroId.Nyra && slot == 1) || (hero == HeroId.Elyra && slot == 2) || (hero == HeroId.Traveler && slot == 1);
            if(hero==HeroId.Bolt7){if(!TryCastBolt7(slot,attackAim))return false;}
            else if(hero==HeroId.VoidKnight&&(slot==1||slot==2||slot==3)){if(!TryCastVoidKnight(slot,attackAim))return false;}
            else if (hero == HeroId.Timmy && (slot == 2 || slot == 3))
            {
                if(!TryCastTimmy(slot,target,attackAim,aimed.HasValue && attackAim.AllowDashTargetCapture))return false;
            }
            else if (hero == HeroId.Nerok)
            {
                if(!TryCastShark(slot,target,ref presentationWindup,aimed))return false;
            }
            else if (mobility)
            {
                movement.y = 0; if (movement.sqrMagnitude < .01f) movement = transform.forward;
                Vector3 from = transform.position, end = from + movement.normalized * aimSpec.castRange;
                // An assassin gap-close should not jump beyond its intended opponent.
                MobaBotMover mover = GetComponent<MobaBotMover>();
                if (mover != null && target != null && Vector3.Dot(movement, target.transform.position - from) > 0)
                    end = from + movement.normalized * Mathf.Min(spec.Range, Mathf.Max(0, HeroCombatRules.TargetDistance(from, target) - 1.5f));
                end = GameplayCollision.ClampToRoundedArena(end,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight);
                if ((end-from).sqrMagnitude < .01f || (mover != null && !mover.CanRepositionTo(end))) return false;
                if(!locomotion.BeginDash(end))return false;
                // Apply the speed passive after Vector Shift ends, not during dash
                // travel; cancelled or interrupted dashes cannot grant it.
                if(hero==HeroId.Nyra&&slot==1)sivraDashPassivePending=true;
                HeroCombatAudio.Cast(gameObject,slot);
                mover?.ResetSteering(); HeroSkillVfx.Dash(gameObject, from, locomotion.DashDestination);
                // Zhaev is intentionally still a low-HP assassin. The short shield protects only
                // the committed entry window instead of inflating his permanent durability.
                if(hero==HeroId.Veyr&&slot==2)
                {
                    self.AddShield(30f,2.25f,false);
                    HeroSkillVfx.Support(gameObject,self,true,2.25f);
                }
                else if(hero==HeroId.Elyra&&slot==2)
                {
                    self.AddShield(HeroAbilityKit.PhaseFoldShield,HeroAbilityKit.PhaseFoldShieldSeconds,false);
                    HeroSkillVfx.Support(gameObject,self,true,HeroAbilityKit.PhaseFoldShieldSeconds);
                }
            }
            else if (hero == HeroId.Mira && (slot == 1 || slot == 2))
            {
                Health ally;
                if (aimed.HasValue)
                {
                    ally = aimed.Value.Target;
                    if (ally == null || ally == self)
                        ally = AimGeometry.Select(gameObject, attackAim.Direction, aimSpec.castRange, aimSpec.coneAngle * .5f, true) ?? self;
                }
                else ally = WoundedAlly(spec.Range, slot == 2) ?? self;
                if (ally == null || ally.IsDead || !ally.isActiveAndEnabled || ally.GetComponent<HeroIdentity>() == null) return false;
                TeamMember team = GetComponent<TeamMember>(), member = ally.GetComponent<TeamMember>();
                if (team == null || member == null || team.Team != member.Team ||
                    HeroCombatRules.TargetDistance(transform.position, ally) > aimSpec.castRange ||
                    (slot == 2 ? ally.TemporaryShield > 5f : ally.Normalized >= .95f)) return false;
                if (aimed.HasValue && !AimGeometry.Cone(transform.position, attackAim.Direction, ally.transform.position, aimSpec.castRange, aimSpec.coneAngle)) return false;
                HeroCombatAudio.Cast(gameObject,slot);
                if (slot == 1) ally.Heal(spec.Power,false,gameObject); else ally.AddShield(spec.Power, 4f,false,gameObject);
                HeroSkillVfx.SupportLink(gameObject, ally);
                HeroSkillVfx.Support(gameObject,ally,slot==2,4f);
            }
            else if ((hero == HeroId.Kaelon || hero == HeroId.Rook) && slot == 1)
            {
                if (hero == HeroId.Kaelon) { if (self.TemporaryShield > 5f) return false; HeroCombatAudio.Cast(gameObject,slot);self.AddShield(spec.Power, 4f,false); }
                else { if (self.Normalized >= .95f) return false; HeroCombatAudio.Cast(gameObject,slot);self.Heal(spec.Power,false); self.AddShield(30, 3,false); }
                HeroSkillVfx.Support(gameObject,self,true,hero==HeroId.Kaelon?4f:3f);
                if(hero==HeroId.Rook)HeroSkillVfx.Support(gameObject,self,false,.45f);
            }
            else if (aimSpec.aimType == AimType.Area || aimSpec.aimType == AimType.SelfArea || aimSpec.aimType == AimType.AreaCone)
            {
                Vector3 center = transform.position;
                if (aimSpec.aimType == AimType.Area)
                {
                    if (aimed.HasValue) center = aimed.Value.Point;
                    else
                    {
                        if (!ValidTarget(target, spec.Range)) return false;
                        center = PredictedAreaCenter(target,spec);
                    }
                }
                if (hero == HeroId.Mira && WoundedAlly(spec.Radius) == null) return false;
                HeroCombatAudio.Windup(gameObject,slot+1);
                HeroSkillVfx.Area(gameObject, center, aimSpec.aoeRadius, spec.Windup, true, hero==HeroId.Mira,slot);
                if(spec.Range>0)HeroSkillVfx.Spell(gameObject,center,slot,spec.Windup);
                StartCoroutine(ResolveArea(slot, center, life, attackAim.Direction));
            }
            else
            {
                if (!aimed.HasValue && !ValidTarget(target, spec.Range)) return false;
                presentationWindup = HeroAttackDelivery.AbilityWindup(hero, slot, spec);
                if (hero == HeroId.Nyra && slot == 3)
                {
                    HeroCombatAudio.Windup(gameObject, slot + 1);
                    QueueVolley(attackAim);
                }
                else if (!HeroAttackDelivery.For(gameObject).AbilityAimed(this, slot, attackAim)) return false;
            }
            // Grant the entry shield only after Null Cut has been accepted.
            // Cooldown, invalid aim and rejected delivery paths return before this point.
            if(hero==HeroId.Veyr&&slot==0)
            {
                self.AddShield(20f,2f,false);
                HeroSkillVfx.Support(gameObject,self,true,2f);
            }
            HeroSkillVfx.Cast(gameObject,slot,presentationWindup);
            if (!mobility) locomotion.FaceAction(aimSpec.targetingMode == TargetingMode.Cone ?
                attackAim.Direction : attackAim.CastDirection(transform.position));
            ready[slot] = Time.time + Cooldown(slot);
            GetComponent<HeroVisualMotion>()?.PulseAbility(slot,presentationWindup);
            return true;
        }
        public Vector3 PredictedAreaCenter(Health target,AbilitySpec spec)
        {
            Vector3 position=target.transform.position,velocity=Vector3.zero;
            float quality=.55f;
            BotTargetSensor sensor=GetComponent<BotTargetSensor>();
            if(sensor!=null)
            {
                if(!sensor.CanObserve(target)||!sensor.TryObservedVelocity(target,out velocity))return position;
                position=sensor.ObservedTargetPosition;
                MobaBotBrain brain=GetComponent<MobaBotBrain>();
                quality=brain==null?.35f:brain.DifficultyLevel switch
                {BotDifficulty.VeryEasy=>0f,BotDifficulty.Easy=>.05f,BotDifficulty.Normal=>.35f,BotDifficulty.Hard=>.65f,_=>.8f};
            }
            else
            {
                // No player input or hidden movement: verify the current line of sight first.
                Vector3 from=transform.position+Vector3.up*.4f,to=position+Vector3.up*.4f;
                Vector3 ray=to-from;
                int count=Physics.RaycastNonAlloc(from,ray.normalized,sightHits,ray.magnitude,
                    GameplayCollision.SightMask,QueryTriggerInteraction.Ignore);
                if(count==sightHits.Length)return position;
                for(int i=0;i<count;i++)
                {
                    Health owner=sightHits[i].collider.GetComponentInParent<Health>();
                    if(owner!=self&&owner!=target)return position;
                }
                HeroLocomotion body=target.GetComponent<HeroLocomotion>();
                if(body!=null)velocity=body.Velocity;
            }
            return HeroAimPrediction.Point(transform.position,position,velocity,spec.Windup,quality,spec.Range);
        }
        private bool ValidTarget(Health target, float range) => HeroCombatRules.Enemy(gameObject, target) && HeroCombatRules.TargetDistance(transform.position, target) <= range;
        private void QueueVolley(AimPose pose)
        {
            // Reserve all three deliveries immediately; another fast cast cannot consume a later shot's slot.
            for (int i = 0; i < SightlineBreakShots; i++)
                HeroAttackDelivery.For(gameObject).AbilityAimed(this,3,pose,true,HeroAttackDelivery.Windup(identity.Hero,3)+i*.18f);
        }
        internal void ResolvePresentedHit(int slot,Health target,bool volley)
        {
            if(!volley){Hit(slot,target,true,true);return;}
            float before=target.Current+target.Shield;
            HeroCombatRules.Damage(gameObject,target,kit.Ultimate.Power/SightlineBreakShots);
            if(target.Current+target.Shield<before)
            {
                HeroSkillVfx.Impact(gameObject,target,3);
                HeroCombatAudio.Impact(gameObject,3,target.transform.position);
            }
        }
        private IEnumerator ResolveArea(int slot, Vector3 center, int castLife, Vector3 direction = default)
        {
            AbilitySpec spec = kit.Spec(slot);
            yield return new WaitForSeconds(spec.Windup);
            if (castLife != life || self.IsDead || !HeroCombatStatus.CanAct(gameObject)) yield break;
            HeroCombatAudio.Cast(gameObject,slot);
            bool confirmed=false;
            TeamMember own = GetComponent<TeamMember>();
            // Health.Active is the combat target registry. Visual and movement colliders are
            // optional for heroes, so physics overlap cannot be the source of truth here.
            resolvedTargets.Clear();
            AimSpec areaAim = AimingSettings.Current.For(identity.Hero, slot);
            for(int i=Health.Active.Count-1;i>=0;i--)
            {
                Health h=Health.Active[i];
                if(h==null||h.IsDead||!h.isActiveAndEnabled||!resolvedTargets.Add(h))continue;
                Vector3 delta=h.transform.position-center;delta.y=0;
                bool inArea = areaAim.aimType == AimType.AreaCone
                    ? AimGeometry.Cone(center, direction, h.transform.position, areaAim.castRange, areaAim.coneAngle)
                    : AimGeometry.Circle(center, h.transform.position, areaAim.aoeRadius);
                if (!inArea) continue;
                TeamMember member=h.GetComponent<TeamMember>();
                bool ally=own!=null&&member!=null&&member.Team==own.Team&&h.GetComponent<HeroIdentity>()!=null;
                if(identity.Hero==HeroId.Mira)
                {
                    if(ally){float supportBefore=h.Current+h.Shield;h.Heal(spec.Power,false,gameObject);h.AddShield(25,3,false,gameObject);confirmed|=h.Current+h.Shield>supportBefore;HeroSkillVfx.Support(gameObject,h,false,.45f);HeroSkillVfx.Support(gameObject,h,true,3);}
                }
                else
                {
                    if(identity.Hero==HeroId.Kaelon&&slot==3&&ally){h.AddShield(60,3,false,gameObject);HeroSkillVfx.Support(gameObject,h,true,3);}
                    if(HeroCombatRules.Enemy(gameObject,h)){float before=h.Current+h.Shield;Hit(slot,h,false);confirmed|=h.Current+h.Shield<before;}
                }
            }
            if (identity.Hero == HeroId.Kaelon && slot == 3) { self.Heal(50, false, gameObject);HeroSkillVfx.Support(gameObject,self,false,.45f); }
            if (identity.Hero == HeroId.Rook && slot == 3) { self.AddShield(65, 4,false);HeroSkillVfx.Support(gameObject,self,true,4); }
            if(confirmed)HeroCombatAudio.Impact(gameObject,slot,center);
            HeroSkillVfx.Area(gameObject, center, areaAim.aoeRadius, .3f, false,identity.Hero==HeroId.Mira,slot);
        }
        private void Hit(int slot, Health target, bool visual = true,bool launched=false)
        {
            if(!HeroCombatRules.Enemy(gameObject,target))return;
            float damage = kit.Spec(slot).Power;
            if (identity.Hero == HeroId.Veyr && slot == 3 && target.GetComponent<HeroIdentity>() != null)
                damage *= 1f + (1f - target.Normalized) * .35f;
            // Resolve only at confirmed contact, before normal damage consumes a small
            // remaining shield. Health owns authority, invulnerability and pool limits.
            bool prism=identity.Hero==HeroId.Nyra&&slot==0;
            bool invert=identity.Hero==HeroId.Elyra&&slot==1;
            float shieldSeconds=invert?HeroAbilityKit.IoneInvertOvershieldSeconds:HeroAbilityKit.SivraPrismOvershieldSeconds;
            float absorbed=prism||invert
                ? target.TransferShieldTo(self,invert?HeroAbilityKit.IoneInvertShieldAbsorb:HeroAbilityKit.SivraPrismShieldAbsorb,
                    shieldSeconds) : 0;
            if(absorbed>0)
            {
                Alterverse.Multiplayer.CoopGameplayCommands.RecordAuthoritativeDamage(gameObject,absorbed);
                HeroSkillVfx.Support(gameObject,self,true,shieldSeconds);
            }
            float before=target.Current+target.Shield;
            HeroCombatRules.Damage(gameObject, target, damage);
            bool damaged=absorbed>0||target!=null&&target.Current+target.Shield<before;
            // A rejected hit (for example an invulnerable objective) cannot apply control either.
            if(!damaged)return;
            GetComponent<VoidKnightState>()?.RecordSkillHit();
            if(damaged)
            {
                HeroSkillVfx.Impact(gameObject,target,slot);
                if(visual)HeroCombatAudio.Impact(gameObject,slot,target.transform.position);
                switch (identity.Hero)
                {
                    case HeroId.VoidKnight:
                        if(slot==2)HeroCombatStatus.Apply(target,VoidKnightBalance.DashSlow,VoidKnightBalance.DashSlowSeconds);
                        break;
                    case HeroId.Timmy:
                        TriggerTimmyPassive();
                        if(slot==1)HeroCombatStatus.Apply(target,TimmyBalance.DustSlow,TimmyBalance.DustSlowSeconds);
                        break;
                    case HeroId.Veyr: if (slot == 1) HeroCombatStatus.Apply(target, .8f, 1.2f); break;
                    case HeroId.Kaelon: HeroCombatStatus.Apply(target, slot == 3 ? .5f : .65f, slot == 3 ? 2f : 1.5f, slot == 0 ? .55f : 0); break;
                    case HeroId.Nyra: if (slot == 2) HeroCombatStatus.Apply(target, .7f, 1.8f); break;
                    case HeroId.Elyra: if (slot == 1 || slot == 3) HeroCombatStatus.Apply(target, slot == 3 ? .55f : .65f, 2f); break;
                    case HeroId.Mira: HeroCombatStatus.Apply(target, .75f, 1.5f); break;
                    case HeroId.Rook: if (slot == 0) HeroCombatStatus.Apply(target, .8f, 1f); break;
                    case HeroId.Nerok: if(slot==2)HeroCombatStatus.Apply(target,.75f,1.2f); break;
                    case HeroId.Traveler: if (slot == 2 || slot == 3) HeroCombatStatus.Apply(target, slot == 3 ? .62f : .72f, slot == 3 ? 1.8f : 1.5f); break;
                }
            }
            if (visual&&!launched) HeroSkillVfx.Spell(gameObject, target.transform.position, slot);
            if(damaged&&(identity.Hero==HeroId.Kaelon||identity.Hero==HeroId.Mira||identity.Hero==HeroId.Veyr&&slot==1||
                identity.Hero==HeroId.Nyra&&slot==2||identity.Hero==HeroId.Elyra&&(slot==1||slot==3)||identity.Hero==HeroId.Rook&&slot==0||identity.Hero==HeroId.Traveler&&(slot==2||slot==3)))
                HeroSkillVfx.Debuff(gameObject,target,slot);
        }
    }
}

