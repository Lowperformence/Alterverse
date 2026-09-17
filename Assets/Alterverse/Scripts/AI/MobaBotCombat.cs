using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health), typeof(MobaBotBrain), typeof(BotTargetSensor))]
    public sealed class MobaBotCombat : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float attackRange = 1.65f;
        [SerializeField] private float attackDamage = 9f;
        [SerializeField] private float attackCooldown = 1.05f;
        [SerializeField] private float chaseLimit = 7.5f;

        private const float ArenaHalfSize = 29f;
        private Health health;
        private MobaBotBrain brain;
        private BotTargetSensor sensor;
        private BotLanePlan lanePlan;
        private float nextAttackTime;

        private void Awake()
        {
            health = GetComponent<Health>();
            brain = GetComponent<MobaBotBrain>();
            sensor = GetComponent<BotTargetSensor>();
            lanePlan = GetComponent<BotLanePlan>();
        }

        private void Update()
        {
            if (health == null || health.IsDead || brain == null || sensor == null) return;
            if (brain.State != MobaBotBrain.BotState.Fight) return;

            Health target = sensor.CurrentTarget;
            if (target == null || target.IsDead) return;

            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;

            if (distance > chaseLimit) return;

            if (distance > attackRange)
            {
                MoveTowardTarget(delta);
                return;
            }

            if (Time.time < nextAttackTime) return;
            nextAttackTime = Time.time + attackCooldown;
            target.TakeDamage(attackDamage);
        }

        private void MoveTowardTarget(Vector3 delta)
        {
            if (delta.sqrMagnitude < 0.001f) return;

            Vector3 direction = delta.normalized;
            Vector3 next = transform.position + direction * moveSpeed * Time.deltaTime;

            // Do not let a basic chase drag a bot deep past its lane pressure point.
            if (lanePlan != null)
            {
                float maxPressureX = Mathf.Abs(lanePlan.EnemySide.x);
                next.x = Mathf.Clamp(next.x, -maxPressureX, maxPressureX);
            }

            next.x = Mathf.Clamp(next.x, -ArenaHalfSize, ArenaHalfSize);
            next.z = Mathf.Clamp(next.z, -ArenaHalfSize, ArenaHalfSize);
            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
        }
    }
}
