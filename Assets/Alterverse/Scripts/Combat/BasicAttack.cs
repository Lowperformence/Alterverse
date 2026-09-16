using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.Combat
{
    public sealed class BasicAttack : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float range = 2.2f;
        [SerializeField, Min(0f)] private float damage = 20f;
        [SerializeField, Min(0.05f)] private float cooldown = 0.7f;
        [SerializeField] private LayerMask targetMask = ~0;

        private float nextAttackTime;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                TryAttack();
        }

        public bool TryAttack()
        {
            if (Time.time < nextAttackTime) return false;
            nextAttackTime = Time.time + cooldown;

            Collider[] hits = Physics.OverlapSphere(transform.position, range, targetMask, QueryTriggerInteraction.Ignore);
            Health closest = null;
            float closestSqr = float.MaxValue;

            foreach (Collider hit in hits)
            {
                Health health = hit.GetComponentInParent<Health>();
                if (health == null || health.gameObject == gameObject || health.IsDead) continue;

                float sqr = (health.transform.position - transform.position).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closest = health;
                }
            }

            if (closest == null) return false;
            closest.TakeDamage(damage);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
