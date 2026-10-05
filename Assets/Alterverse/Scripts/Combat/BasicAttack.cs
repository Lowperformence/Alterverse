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
        private Health selfHealth;
        private HeroCombatProfile profile;
        private HeroIdentity identity;
        private SoftTargeting targeting;
        private float nextAttackTime;

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

        public float RemainingCooldown => Mathf.Max(0, nextAttackTime - Time.time);
        public float AttackCooldown => Cooldown;

        private void Awake()
        {
            locomotion = GetComponent<HeroLocomotion>() ?? gameObject.AddComponent<HeroLocomotion>();
            selfHealth = GetComponent<Health>();
            profile = GetComponent<HeroCombatProfile>();
            identity = GetComponent<HeroIdentity>();
            targeting = GetComponent<SoftTargeting>() ?? gameObject.AddComponent<SoftTargeting>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Alterverse.Multiplayer.CoopHeroSlot slot = GetComponent<Alterverse.Multiplayer.CoopHeroSlot>();
            if (slot != null && slot.IsHuman && !slot.IsLocalHuman) return;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) TryAttack();
        }

        public bool TryAttack()
        {
            if (!CanAttempt()) return false;

            Health target = targeting.Acquire(Range, transform.forward, true);
            if (target == null) return false;

            if (Alterverse.Multiplayer.CoopGameplayCommands.ShouldRouteClientCommand(gameObject))
            {
                if (!Alterverse.Multiplayer.CoopGameplayCommands.TrySendBasic(gameObject, target)) return false;
                nextAttackTime = Time.time + Cooldown;
                targeting.RegisterOffensiveAction();
                Vector3 predictedLook = target.transform.position - transform.position;
                predictedLook.y = 0;
                locomotion.FaceAction(predictedLook);
                GetComponent<HeroVisualMotion>()?.PulseBasic(HeroAttackDelivery.Windup(identity != null ? identity.Hero : HeroId.Veyr));
                OnboardingSessionGuard.Attack(this);
                return true;
            }

            return TryAttackAuthoritative(target);
        }

        public bool TryAttackNetworkAuthoritative(Health requestedTarget)
        {
            if (!CanAttempt()) return false;
            return TryAttackAuthoritative(requestedTarget);
        }

        private bool CanAttempt()
        {
            return Time.timeScale > 0f &&
                   selfHealth != null &&
                   !selfHealth.IsDead &&
                   HeroCombatStatus.CanAct(gameObject) &&
                   Time.time >= nextAttackTime;
        }

        private bool TryAttackAuthoritative(Health target)
        {
            if (target == null ||
                !HeroCombatRules.Enemy(gameObject, target) ||
                HeroCombatRules.BasicDistance(transform.position, target) > Range)
                return false;

            nextAttackTime = Time.time + Cooldown;
            targeting.RegisterOffensiveAction();
            Vector3 look = target.transform.position - transform.position;
            look.y = 0;
            locomotion.FaceAction(look);
            HeroCombatRules.BasicHit(gameObject, target, Damage);
            OnboardingSessionGuard.Attack(this);
            return true;
        }

        public void ApplyNetworkCooldown(float remaining)
        {
            if (!Alterverse.Multiplayer.CoopAuthority.Active || Alterverse.Multiplayer.CoopAuthority.LocalIsHost) return;
            nextAttackTime = Time.time + Mathf.Max(0f, remaining);
        }

        private Color HeroColor() => identity != null ? StylizedArtDirection.HeroAccent(identity.Hero) : StylizedArtDirection.ZeroEnergy;

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? Range : fallbackRange);
        }
    }
}
