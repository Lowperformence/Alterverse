using System.Collections;
using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health))]
    public sealed class MobaBotRespawn : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 4f;
        private Health health;
        private Vector3 spawnPoint;
        private bool respawning;

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
            if (!respawning) StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            respawning = true;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Renderer r in renderers) r.enabled = false;
            foreach (Collider c in colliders) c.enabled = false;

            yield return new WaitForSeconds(respawnDelay);

            transform.position = spawnPoint;
            health.RestoreFull();
            foreach (Renderer r in renderers) if (r != null) r.enabled = true;
            foreach (Collider c in colliders) if (c != null) c.enabled = true;
            respawning = false;
        }
    }
}
