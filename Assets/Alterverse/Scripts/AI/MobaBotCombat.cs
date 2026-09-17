using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    /// <summary>
    /// Executes close-range PvP attacks. Movement belongs exclusively to MobaBotMover
    /// so combat and navigation never fight over the bot transform.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(MobaBotBrain), typeof(BotTargetSensor))]
    public sealed class MobaBotCombat : MonoBehaviour
    {
        [SerializeField] private float attackRange = 1.65f;
        [SerializeField] private float attackDamage = 9f;
        [SerializeField] private float attackCooldown = 1.05f;
        [SerializeField] private float turnSpeed = 10f;

        private Health health;
        private MobaBotBrain brain;
        private BotTargetSensor sensor;
        private float nextAttackTime;

        private void Awake()
        {
            health = GetComponent<Health>();
            brain = GetComponent<MobaBotBrain>();
            sensor = GetComponent<BotTargetSensor>();
        }

        private void Update()
        {
            if (health == null || health.IsDead || brain == null || sensor == null) return;
            if (brain.State != MobaBotBrain.BotState.Fight) return;

            Health target = sensor.CurrentTarget;
            if (target == null || target.IsDead) return;

            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > attackRange * attackRange) return;

            if (delta.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta.normalized), turnSpeed * Time.deltaTime);

            if (Time.time < nextAttackTime) return;
            nextAttackTime = Time.time + attackCooldown;
            target.TakeDamage(attackDamage);
        }
    }
}
