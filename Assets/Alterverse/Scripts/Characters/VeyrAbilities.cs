using System.Collections;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public sealed class VeyrAbilities : MonoBehaviour
    {
        [Header("Cooldowns")]
        [SerializeField] private float shadowStrikeCooldown = 5f;
        [SerializeField] private float soulRendCooldown = 6f;
        [SerializeField] private float nullStepCooldown = 7f;
        [SerializeField] private float endFutureCooldown = 14f;

        private static readonly Color ShadowColor = new(0.42f, 0.10f, 0.72f);
        private static readonly Color SoulColor = new(0.72f, 0.08f, 0.25f);
        private static readonly Color FutureColor = new(0.90f, 0.12f, 0.45f);
        private CharacterController controller;
        private Health selfHealth;
        private float qReady, eReady, rReady, fReady;

        public float QRemaining => Mathf.Max(0f, qReady - Time.time);
        public float ERemaining => Mathf.Max(0f, eReady - Time.time);
        public float RRemaining => Mathf.Max(0f, rReady - Time.time);
        public float FRemaining => Mathf.Max(0f, fReady - Time.time);

        private void Awake() { controller = GetComponent<CharacterController>(); selfHealth = GetComponent<Health>(); }

        private void Update()
        {
            if (Keyboard.current == null || selfHealth.IsDead) return;
            if (Keyboard.current.qKey.wasPressedThisFrame) ShadowStrike();
            if (Keyboard.current.eKey.wasPressedThisFrame) SoulRend();
            if (Keyboard.current.rKey.wasPressedThisFrame) NullStep();
            if (Keyboard.current.fKey.wasPressedThisFrame) EndOfFuture();
        }

        public bool ShadowStrike()
        {
            if (Time.time < qReady) return false;
            Health target = FindNearestEnemy(5.5f);
            if (target == null) return false;
            qReady = Time.time + shadowStrikeCooldown;

            Vector3 start = transform.position;
            Vector3 behind = -target.transform.forward; behind.y = 0f;
            if (behind.sqrMagnitude < 0.01f) behind = (transform.position - target.transform.position).normalized;
            Vector3 destination = target.transform.position + behind.normalized * 1.25f;
            PrototypeVfx.ShadowTrail(start, destination, ShadowColor);
            PrototypeVfx.Ring(start, ShadowColor, 1.25f, 0.18f);
            Teleport(destination);
            Face(target.transform.position);
            PrototypeVfx.Ring(transform.position, ShadowColor, 1.45f, 0.20f);
            PrototypeVfx.Slash(transform, ShadowColor, 2.2f);
            target.TakeDamage(26f);
            PrototypeVfx.Burst(target.transform.position, ShadowColor, 1.05f, 9);
            return true;
        }

        public bool SoulRend()
        {
            if (Time.time < eReady) return false;
            eReady = Time.time + soulRendCooldown;
            PrototypeVfx.Ring(transform.position, SoulColor, 2.8f, 0.38f);
            PrototypeVfx.Burst(transform.position, SoulColor, 1.25f, 10);
            Collider[] hits = Physics.OverlapSphere(transform.position, 2.8f, ~0, QueryTriggerInteraction.Ignore);
            float totalDamage = 0f;
            foreach (Collider hit in hits)
            {
                Health health = hit.GetComponentInParent<Health>();
                if (!IsEnemy(health)) continue;
                health.TakeDamage(30f);
                PrototypeVfx.SoulLink(health.transform.position, transform.position, SoulColor);
                PrototypeVfx.Burst(health.transform.position, SoulColor, 0.9f, 7);
                totalDamage += 30f;
            }
            if (totalDamage > 0f)
            {
                selfHealth.Heal(totalDamage * 0.30f);
                PrototypeVfx.Burst(transform.position, new Color(0.18f, 0.85f, 0.42f), 0.65f, 6);
            }
            return true;
        }

        public bool NullStep()
        {
            if (Time.time < rReady) return false;
            rReady = Time.time + nullStepCooldown;
            Vector3 start = transform.position;
            Vector3 destination = transform.position + transform.forward * 4.2f;
            destination.x = Mathf.Clamp(destination.x, -13f, 13f); destination.z = Mathf.Clamp(destination.z, -13f, 13f);
            PrototypeVfx.ShadowTrail(start, destination, ShadowColor);
            PrototypeVfx.Ring(start, ShadowColor, 0.9f, 0.16f);
            Teleport(destination);
            PrototypeVfx.Ring(transform.position, ShadowColor, 1.5f, 0.24f);
            Health target = FindNearestEnemy(2.2f);
            if (target != null) { target.TakeDamage(16f); PrototypeVfx.Burst(target.transform.position, ShadowColor, 0.75f, 6); }
            return true;
        }

        public bool EndOfFuture()
        {
            if (Time.time < fReady) return false;
            Health target = FindNearestEnemy(7.5f);
            if (target == null) return false;
            fReady = Time.time + endFutureCooldown;
            PrototypeVfx.FutureMark(target.transform, FutureColor, 1.15f);
            StartCoroutine(ResolveUltimate(target));
            return true;
        }

        private IEnumerator ResolveUltimate(Health target)
        {
            yield return new WaitForSeconds(1.15f);
            if (target == null || target.IsDead) yield break;
            float damage = 30f + target.Current * 0.28f;
            PrototypeVfx.Ring(target.transform.position, FutureColor, 3.1f, 0.42f);
            PrototypeVfx.Burst(target.transform.position, FutureColor, 1.7f, 14);
            target.TakeDamage(damage);
        }

        private Health FindNearestEnemy(float radius)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
            Health closest = null; float closestSqr = float.MaxValue;
            foreach (Collider hit in hits)
            {
                Health health = hit.GetComponentInParent<Health>();
                if (!IsEnemy(health)) continue;
                float sqr = (health.transform.position - transform.position).sqrMagnitude;
                if (sqr < closestSqr) { closestSqr = sqr; closest = health; }
            }
            return closest;
        }

        private bool IsEnemy(Health health) => health != null && health != selfHealth && !health.IsDead;
        private void Face(Vector3 point) { point.y = transform.position.y; transform.LookAt(point); }
        private void Teleport(Vector3 destination) { controller.enabled = false; transform.position = destination; controller.enabled = true; }
    }
}
