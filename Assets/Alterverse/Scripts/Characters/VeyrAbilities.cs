using System.Collections;
using Alterverse.Combat;
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

        private CharacterController controller;
        private Health selfHealth;
        private float qReady;
        private float wReady;
        private float eReady;
        private float rReady;

        public float QRemaining => Mathf.Max(0f, qReady - Time.time);
        public float WRemaining => Mathf.Max(0f, wReady - Time.time);
        public float ERemaining => Mathf.Max(0f, eReady - Time.time);
        public float RRemaining => Mathf.Max(0f, rReady - Time.time);

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            selfHealth = GetComponent<Health>();
        }

        private void Update()
        {
            if (Keyboard.current == null || selfHealth.IsDead) return;
            if (Keyboard.current.qKey.wasPressedThisFrame) ShadowStrike();
            if (Keyboard.current.wKey.wasPressedThisFrame) SoulRend();
            if (Keyboard.current.eKey.wasPressedThisFrame) NullStep();
            if (Keyboard.current.rKey.wasPressedThisFrame) EndOfFuture();
        }

        public bool ShadowStrike()
        {
            if (Time.time < qReady) return false;
            Health target = FindNearestEnemy(5.5f);
            if (target == null) return false;

            qReady = Time.time + shadowStrikeCooldown;
            Vector3 away = (transform.position - target.transform.position);
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -target.transform.forward;
            Vector3 destination = target.transform.position + away.normalized * 1.2f;
            Teleport(destination);
            transform.LookAt(new Vector3(target.transform.position.x, transform.position.y, target.transform.position.z));
            target.TakeDamage(26f);
            Debug.Log("Veyr used Schattenstich.");
            return true;
        }

        public bool SoulRend()
        {
            if (Time.time < wReady) return false;
            wReady = Time.time + soulRendCooldown;

            Collider[] hits = Physics.OverlapSphere(transform.position, 2.8f, ~0, QueryTriggerInteraction.Ignore);
            float totalDamage = 0f;
            foreach (Collider hit in hits)
            {
                Health health = hit.GetComponentInParent<Health>();
                if (!IsEnemy(health)) continue;
                health.TakeDamage(30f);
                totalDamage += 30f;
            }

            if (totalDamage > 0f) selfHealth.Heal(totalDamage * 0.30f);
            Debug.Log("Veyr used Seelenriss.");
            return true;
        }

        public bool NullStep()
        {
            if (Time.time < eReady) return false;
            eReady = Time.time + nullStepCooldown;

            Vector3 destination = transform.position + transform.forward * 4.2f;
            destination.x = Mathf.Clamp(destination.x, -13f, 13f);
            destination.z = Mathf.Clamp(destination.z, -13f, 13f);
            Teleport(destination);

            Health target = FindNearestEnemy(2.2f);
            if (target != null) target.TakeDamage(16f);
            Debug.Log("Veyr used Nullschritt.");
            return true;
        }

        public bool EndOfFuture()
        {
            if (Time.time < rReady) return false;
            Health target = FindNearestEnemy(7.5f);
            if (target == null) return false;

            rReady = Time.time + endFutureCooldown;
            StartCoroutine(ResolveUltimate(target));
            Debug.Log($"Veyr marked {target.name} with Ende der Zukunft.");
            return true;
        }

        private IEnumerator ResolveUltimate(Health target)
        {
            yield return new WaitForSeconds(1.15f);
            if (target == null || target.IsDead) yield break;
            float damage = 30f + target.Current * 0.28f;
            target.TakeDamage(damage);
        }

        private Health FindNearestEnemy(float radius)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
            Health closest = null;
            float closestSqr = float.MaxValue;

            foreach (Collider hit in hits)
            {
                Health health = hit.GetComponentInParent<Health>();
                if (!IsEnemy(health)) continue;
                float sqr = (health.transform.position - transform.position).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closest = health;
                }
            }
            return closest;
        }

        private bool IsEnemy(Health health)
        {
            return health != null && health != selfHealth && !health.IsDead;
        }

        private void Teleport(Vector3 destination)
        {
            controller.enabled = false;
            transform.position = destination;
            controller.enabled = true;
        }
    }
}
