using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class LaneTower : MonoBehaviour
    {
        [SerializeField] private float attackRange = 6.5f;
        [SerializeField] private float attackDamage = 18f;
        [SerializeField] private float attackCooldown = 1.2f;

        private Health health;
        private int team;
        private float nextAttackTime;

        public int Team => team;

        private void Awake() => health = GetComponent<Health>();

        public void Initialize(int teamId) => team = teamId;

        private void Update()
        {
            if (health == null || health.IsDead || Time.time < nextAttackTime) return;

            Health target = FindTarget();
            if (target == null) return;

            nextAttackTime = Time.time + attackCooldown;
            target.TakeDamage(attackDamage);
            SpawnShotFlash(target.transform.position);
        }

        private Health FindTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, ~0, QueryTriggerInteraction.Ignore);
            Health closestMinion = null;
            Health closestOther = null;
            float minionSqr = float.MaxValue;
            float otherSqr = float.MaxValue;
            HashSet<Health> checkedHealth = new();

            foreach (Collider hit in hits)
            {
                Health candidate = hit.GetComponentInParent<Health>();
                if (candidate == null || candidate == health || candidate.IsDead || !checkedHealth.Add(candidate)) continue;

                LaneTower otherTower = candidate.GetComponent<LaneTower>();
                if (otherTower != null && otherTower.team == team) continue;

                LaneMinion minion = candidate.GetComponent<LaneMinion>();
                if (minion != null)
                {
                    if (minion.Team == team) continue;
                    float sqr = (candidate.transform.position - transform.position).sqrMagnitude;
                    if (sqr < minionSqr) { minionSqr = sqr; closestMinion = candidate; }
                    continue;
                }

                float otherDistance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (otherDistance < otherSqr) { otherSqr = otherDistance; closestOther = candidate; }
            }

            return closestMinion != null ? closestMinion : closestOther;
        }

        private void SpawnShotFlash(Vector3 targetPosition)
        {
            Vector3 origin = transform.position + Vector3.up * 1.7f;
            Vector3 direction = targetPosition - origin;
            float distance = direction.magnitude;
            if (distance < 0.01f) return;

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "AV_TowerShot";
            beam.transform.position = origin + direction * 0.5f;
            beam.transform.rotation = Quaternion.LookRotation(direction);
            beam.transform.localScale = new Vector3(0.09f, 0.09f, distance);
            Collider collider = beam.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = team == 0 ? new Color(0.55f, 0.12f, 0.82f) : new Color(0.08f, 0.72f, 0.78f);
            Destroy(beam, 0.08f);
        }
    }
}
