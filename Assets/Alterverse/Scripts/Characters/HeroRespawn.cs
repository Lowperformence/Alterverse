using System.Collections;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class HeroRespawn : MonoBehaviour
    {
        [SerializeField] private float firstRespawnDelay = 4f;
        [SerializeField] private float addedDelayPerDeath = 1f;
        [SerializeField] private float maximumRespawnDelay = 10f;

        private Health health;
        private CharacterController controller;
        private Vector3 spawnPoint;
        private bool respawning;
        private int deathCount;

        public int DeathCount => deathCount;
        public float CurrentRespawnDelay => Mathf.Min(maximumRespawnDelay, firstRespawnDelay + Mathf.Max(0, deathCount - 1) * addedDelayPerDeath);

        private void Awake()
        {
            health = GetComponent<Health>();
            controller = GetComponent<CharacterController>();
            spawnPoint = transform.position;
            health.Died += OnDied;
        }

        private void OnDestroy()
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
            yield return new WaitForSeconds(delay);
            controller.enabled = false;
            transform.position = spawnPoint;
            controller.enabled = true;
            health.RestoreFull();
            respawning = false;
        }
    }
}
