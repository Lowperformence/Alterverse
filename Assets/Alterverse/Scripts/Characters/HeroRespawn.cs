using System.Collections;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class HeroRespawn : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 3f;

        private Health health;
        private CharacterController controller;
        private Vector3 spawnPoint;
        private bool respawning;

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
            if (!respawning) StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            respawning = true;
            yield return new WaitForSeconds(respawnDelay);
            controller.enabled = false;
            transform.position = spawnPoint;
            controller.enabled = true;
            health.RestoreFull();
            respawning = false;
        }
    }
}
