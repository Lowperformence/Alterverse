using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class LaneMinion : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2.6f;
        [SerializeField] private float detectionRange = 4.2f;
        [SerializeField] private float attackRange = 1.25f;
        [SerializeField] private float attackDamage = 7f;
        [SerializeField] private float attackCooldown = 1.15f;

        private Health health;
        private int team;
        private Vector3 destination;
        private float nextAttackTime;

        public int Team => team;

        private void Awake() => health = GetComponent<Health>();

        public void Initialize(int teamId, Vector3 laneDestination)
        {
            team = teamId;
            destination = laneDestination;
        }

        private void Update()
        {
            if (health == null || health.IsDead) return;

            Health target = FindTarget();
            if (target != null)
            {
                Vector3 delta = target.transform.position - transform.position;
                delta.y = 0f;
                if (delta.magnitude <= attackRange)
                {
                    Face(delta);
                    if (Time.time >= nextAttackTime)
                    {
                        nextAttackTime = Time.time + attackCooldown;
                        target.TakeDamage(attackDamage);
                    }
                    return;
                }
            }

            MoveTowards(target != null ? target.transform.position : destination);
        }

        private Health FindTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, ~0, QueryTriggerInteraction.Ignore);
            Health closest = null;
            float closestSqr = float.MaxValue;
            HashSet<Health> checkedHealth = new();

            foreach (Collider hit in hits)
            {
                Health candidate = hit.GetComponentInParent<Health>();
                if (candidate == null || candidate == health || candidate.IsDead || !checkedHealth.Add(candidate)) continue;

                LaneMinion otherMinion = candidate.GetComponent<LaneMinion>();
                if (otherMinion != null && otherMinion.team == team) continue;

                float sqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closest = candidate;
                }
            }
            return closest;
        }

        private void MoveTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.04f) return;
            direction.Normalize();
            transform.position += direction * moveSpeed * Time.deltaTime;
            Face(direction);
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
        }
    }
}
