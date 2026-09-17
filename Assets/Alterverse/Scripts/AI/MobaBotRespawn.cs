using System.Collections;
using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health))]
    public sealed class MobaBotRespawn : MonoBehaviour
    {
        [SerializeField] private float firstRespawnDelay = 4f;
        [SerializeField] private float addedDelayPerDeath = 1f;
        [SerializeField] private float maximumRespawnDelay = 10f;

        private Health health;
        private Vector3 spawnPoint;
        private bool respawning;
        private int deathCount;

        public int DeathCount => deathCount;
        public float CurrentRespawnDelay => Mathf.Min(maximumRespawnDelay, firstRespawnDelay + Mathf.Max(0, deathCount - 1) * addedDelayPerDeath);

        private void Awake()
        {
            health = GetComponent<Health>();
            spawnPoint = transform.position;
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (respawning) return;
            deathCount++;
            StartCoroutine(RespawnRoutine(CurrentRespawnDelay));
        }

        private IEnumerator RespawnRoutine(float delay)
        {
            respawning = true;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Renderer r in renderers) r.enabled = false;
            foreach (Collider c in colliders) c.enabled = false;

            yield return new WaitForSeconds(delay);

            transform.position = spawnPoint;
            health.RestoreFull();
            foreach (Renderer r in renderers) if (r != null) r.enabled = true;
            foreach (Collider c in colliders) if (c != null) c.enabled = true;
            respawning = false;
        }
    }
}
