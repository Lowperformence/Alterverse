using System.Collections;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health))]
    public sealed class PrototypeEnemyBot : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.6f;
        [SerializeField] private float attackRange = 1.55f;
        [SerializeField] private float attackDamage = 9f;
        [SerializeField] private float attackCooldown = 1.05f;
        [SerializeField] private float respawnDelay = 3f;

        private const float ArenaHalfSize = 29f;
        private Health health;
        private Health target;
        private Vector3 spawnPoint;
        private float nextAttackTime;
        private bool respawning;

        private void Awake()
        {
            health = GetComponent<Health>();
            spawnPoint = transform.position;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        public void SetTarget(Health newTarget)
        {
            target = newTarget;
        }

        private void Update()
        {
            if (respawning || health.IsDead || target == null || target.IsDead) return;

            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;

            if (distance > attackRange)
            {
                Vector3 nextPosition = transform.position + delta.normalized * moveSpeed * Time.deltaTime;
                nextPosition.x = Mathf.Clamp(nextPosition.x, -ArenaHalfSize, ArenaHalfSize);
                nextPosition.z = Mathf.Clamp(nextPosition.z, -ArenaHalfSize, ArenaHalfSize);
                transform.position = nextPosition;
                if (delta.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta.normalized), 10f * Time.deltaTime);
            }
            else if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                target.TakeDamage(attackDamage);
            }
        }

        private void OnDied()
        {
            if (!respawning) StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            respawning = true;
            Renderer renderer = GetComponent<Renderer>();
            Collider collider = GetComponent<Collider>();
            if (renderer != null) renderer.enabled = false;
            if (collider != null) collider.enabled = false;

            yield return new WaitForSeconds(respawnDelay);

            transform.position = spawnPoint;
            health.RestoreFull();
            if (renderer != null) renderer.enabled = true;
            if (collider != null) collider.enabled = true;
            respawning = false;
        }
    }
}
