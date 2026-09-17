using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    /// <summary>
    /// Single movement authority for offline hero bots. Bots follow their lane plan,
    /// but switch to a nearby PvP target while fighting. Retreat always overrides chase.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(MobaBotBrain), typeof(BotLanePlan), typeof(BotTargetSensor))]
    public sealed class MobaBotMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.35f;
        [SerializeField] private float laneStopDistance = 0.45f;
        [SerializeField] private float combatStopDistance = 1.5f;
        [SerializeField] private float chaseLimit = 7.5f;
        [SerializeField] private float turnSpeed = 9f;

        private const float ArenaHalfSize = 29f;
        private Health health;
        private MobaBotBrain brain;
        private BotLanePlan lanePlan;
        private BotTargetSensor sensor;

        private void Awake()
        {
            health = GetComponent<Health>();
            brain = GetComponent<MobaBotBrain>();
            lanePlan = GetComponent<BotLanePlan>();
            sensor = GetComponent<BotTargetSensor>();
        }

        private void Update()
        {
            if (health == null || health.IsDead || brain == null || lanePlan == null) return;

            Vector3 destination = lanePlan.GetDestination(brain.State);
            float stopDistance = laneStopDistance;

            if (brain.State == MobaBotBrain.BotState.Fight && sensor != null)
            {
                Health target = sensor.CurrentTarget;
                if (target != null && !target.IsDead)
                {
                    Vector3 targetDelta = target.transform.position - transform.position;
                    targetDelta.y = 0f;
                    if (targetDelta.sqrMagnitude <= chaseLimit * chaseLimit)
                    {
                        destination = target.transform.position;
                        stopDistance = combatStopDistance;
                    }
                }
            }

            MoveToward(destination, stopDistance);
        }

        private void MoveToward(Vector3 destination, float stopDistance)
        {
            Vector3 delta = destination - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= stopDistance * stopDistance) return;

            Vector3 direction = delta.normalized;
            Vector3 next = transform.position + direction * moveSpeed * Time.deltaTime;
            next.x = Mathf.Clamp(next.x, -ArenaHalfSize, ArenaHalfSize);
            next.z = Mathf.Clamp(next.z, -ArenaHalfSize, ArenaHalfSize);
            transform.position = next;

            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }
    }
}
