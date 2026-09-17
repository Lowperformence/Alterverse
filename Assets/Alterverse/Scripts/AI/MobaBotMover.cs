using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    /// <summary>
    /// Lightweight movement executor for offline MOBA bots.
    /// Reads the brain state and lane plan without changing combat systems.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(MobaBotBrain), typeof(BotLanePlan))]
    public sealed class MobaBotMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.35f;
        [SerializeField] private float stopDistance = 0.45f;
        [SerializeField] private float turnSpeed = 9f;

        private const float ArenaHalfSize = 29f;
        private Health health;
        private MobaBotBrain brain;
        private BotLanePlan lanePlan;

        private void Awake()
        {
            health = GetComponent<Health>();
            brain = GetComponent<MobaBotBrain>();
            lanePlan = GetComponent<BotLanePlan>();
        }

        private void Update()
        {
            if (health == null || health.IsDead || brain == null || lanePlan == null) return;

            Vector3 destination = lanePlan.GetDestination(brain.State);
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
