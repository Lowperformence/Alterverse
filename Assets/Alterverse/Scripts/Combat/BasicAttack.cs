using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Onboarding;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class BasicAttack : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float fallbackRange = 2.2f;
        [SerializeField, Min(0f)] private float fallbackDamage = 20f;
        [SerializeField, Min(.05f)] private float fallbackCooldown = .78f;

        private HeroLocomotion locomotion;
        private HeroMotor motor;
        private Health selfHealth;
        private HeroCombatProfile profile;
        private HeroIdentity identity;
        private SoftTargeting targeting;
        private float nextAttackTime;
        private float nextNetworkAttempt;

        private HeroCombatProfile Profile
        {
            get
            {
                if (profile == null) profile = GetComponent<HeroCombatProfile>();
                return profile;
            }
        }

        private float Range => Profile != null ? Profile.AttackRange : fallbackRange;
        private float Damage => Profile != null ? Profile.Attack : fallbackDamage;
        private float Cooldown => HeroCombatRules.Interval(gameObject, Profile != null ? Profile.AttackInterval : fallbackCooldown);

        // Read-only HUD values; attack timing and execution are unchanged.
        public float RemainingCooldown => Mathf.Max(0, nextAttackTime - Time.time);
        public float AttackCooldown => Cooldown;
        public AimSpec Aim
        {
            get
            {
                return AimingSettings.Current.For(identity != null ? identity.Hero : HeroId.Veyr, -1, Range);
            }
        }

        private void Awake()
        {
            locomotion = GetComponent<HeroLocomotion>() ?? gameObject.AddComponent<HeroLocomotion>();
            motor = GetComponent<HeroMotor>();
            selfHealth = GetComponent<Health>();
            profile = GetComponent<HeroCombatProfile>();
            identity = GetComponent<HeroIdentity>();
            targeting = GetComponent<SoftTargeting>() ?? gameObject.AddComponent<SoftTargeting>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Alterverse.Multiplayer.CoopHeroSlot slot = GetComponent<Alterverse.Multiplayer.CoopHeroSlot>();
            if (slot != null && !slot.IsLocalHuman) return;
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) TryAttack();
        }

        public bool TryAttack()
        {
            AimSpec spec = Aim;
            if (!CanAct() || !spec.allowTapCast) return false;
            // Restore the existing sticky, hero-prioritized selection for basic attacks only.
            Health target = targeting.AcquireBasic(spec.castRange, transform.forward);
            if (target == null)
            {
                // Presentation-only edge tolerance; acquisition and damage keep the
                // exact cast range. Release stops these hold-time refreshes.
                motor?.KeepBasicTargetFacing(spec.castRange);
                return false;
            }
            // Hold calls also arrive during cooldown. Keep facing the eligible target without
            // restarting the attack animation, movement slowdown or cooldown.
            Vector3 direction = target.transform.position - transform.position;
            locomotion.FaceAction(direction, .08f);
            motor?.TrackBasicTarget(target, .08f);
            if (Time.time < nextAttackTime) return false;
            AimPose pose = new AimPose(target.transform.position - transform.position, target.transform.position,
                spec.targetingMode == TargetingMode.Homing || spec.targetingMode == TargetingMode.AutoBasic ? target : null, true);
            bool success = TryAttackAimed(pose);
            if (success) targeting.RegisterOffensiveAction();
            return success;
        }

        public bool TryAttackAimed(AimPose pose)
        {
            if (!CanAttempt() || !AimGeometry.Finite(pose.Direction) || !AimGeometry.Finite(pose.Point)) return false;
            if (Alterverse.Multiplayer.CoopGameplayCommands.ShouldRouteClientCommand(gameObject))
            {
                if (Time.time < nextNetworkAttempt) return false;
                if (pose.Target != null && (!HeroCombatRules.Enemy(gameObject, pose.Target) ||
                    HeroCombatRules.TargetDistance(transform.position, pose.Target) > Aim.castRange)) return false;
                if (!Alterverse.Multiplayer.CoopGameplayCommands.TrySendBasic(gameObject, pose)) return false;
                // An acknowledgement with zero cooldown can mean rejection. Throttle retries
                // independently, without inventing a server gameplay cooldown.
                nextNetworkAttempt = Time.time + Mathf.Min(.12f, Cooldown);
                nextAttackTime = Time.time + Cooldown;
                // Sending intent is not acceptance: the host checks its current range.
                // Its BasicWindup event starts the owner's presentation only after Queue succeeds.
                OnboardingSessionGuard.Attack(this);
                return true;
            }
            return TryAttackAuthoritative(pose);
        }

        public bool TryAttackNetworkAuthoritative(Health requestedTarget)
            => requestedTarget != null && TryAttackNetworkAuthoritative(new AimPose(requestedTarget.transform.position - transform.position, requestedTarget.transform.position, requestedTarget, true));
        public bool TryAttackNetworkAuthoritative(AimPose pose)
            => CanAttempt() && AimGeometry.Finite(pose.Direction) && AimGeometry.Finite(pose.Point) && TryAttackAuthoritative(pose);

        private bool CanAct()
        {
            return isActiveAndEnabled && Time.timeScale > 0f &&
                   selfHealth != null &&
                   !selfHealth.IsDead &&
                   HeroCombatStatus.CanAct(gameObject);
        }

        private bool CanAttempt() => CanAct() && Time.time >= nextAttackTime;

        private bool TryAttackAuthoritative(AimPose pose)
        {
            if (!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay ||
                !HeroAttackDelivery.For(gameObject).BasicAimed(pose, Aim, Damage)) return false;
            float windup = HeroAttackDelivery.Windup(identity != null ? identity.Hero : HeroId.Veyr);
            nextAttackTime = Time.time + Cooldown;
            Vector3 direction = pose.Target != null ? AimGeometry.Direction(pose.Target.transform.position - transform.position)
                : pose.CastDirection(transform.position);
            locomotion.FaceAction(direction);
            motor?.NotifyAttack(Mathf.Max(.18f, windup + .18f), direction);
            motor?.TrackBasicTarget(pose.Target, windup + .18f);
            OnboardingSessionGuard.Attack(this);
            return true;
        }

        internal void ApplyNetworkWindup(Vector3 point, Health target, float windup, float elapsed)
        {
            if (!Alterverse.Multiplayer.CoopGameplayCommands.ShouldRouteClientCommand(gameObject) || !CanAct() ||
                !AimGeometry.Finite(point) || float.IsNaN(windup) || float.IsInfinity(windup) ||
                float.IsNaN(elapsed) || float.IsInfinity(elapsed) || windup <= 0 || elapsed < 0 || elapsed >= windup + .18f) return;
            // Seek the accepted action to the server clock, including its recovery.
            float remaining = Mathf.Max(0, windup - elapsed);
            Vector3 direction = AimGeometry.Direction(point - transform.position);
            locomotion.FaceAction(direction, remaining + .18f);
            motor?.NotifyAttack(remaining + .18f, direction);
            motor?.TrackBasicTarget(target, remaining + .18f);
            CombatMotion.PresentBasicAt(gameObject, windup, point, target, elapsed);
        }

        public void ApplyNetworkCooldown(float remaining,uint acknowledgedCommand=0)
        {
            if (!Alterverse.Multiplayer.CoopAuthority.Active || Alterverse.Multiplayer.CoopAuthority.LocalIsHost) return;
            if(!Alterverse.Multiplayer.CoopGameplayCommands.CanApplyCooldown(gameObject,-1,acknowledgedCommand))return;
            nextAttackTime = Time.time + Mathf.Max(0f, remaining);
        }

        private Color HeroColor() => identity != null
            ? StylizedArtDirection.HeroAccent(identity.Hero)
            : StylizedArtDirection.ZeroEnergy;

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? Range : fallbackRange);
        }
    }
}




