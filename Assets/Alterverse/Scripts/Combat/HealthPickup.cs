using System.Collections;
using UnityEngine;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(Collider))]
    public sealed class HealthPickup : MonoBehaviour
    {
        [SerializeField, Range(0.01f, 1f)] private float healFraction = 0.25f;
        [SerializeField, Min(1f)] private float respawnSeconds = 20f;

        private Collider trigger;
        private Renderer[] visuals;
        private bool available = true;

        private void Awake()
        {
            trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
            visuals = GetComponentsInChildren<Renderer>(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!available) return;

            Health health = other.GetComponentInParent<Health>();
            if (health == null || health.IsDead || health.Current >= health.Max) return;

            health.Heal(health.Max * healFraction);
            StartCoroutine(RespawnRoutine());
        }

        public void Configure(float fraction, float respawnDelay)
        {
            healFraction = Mathf.Clamp(fraction, 0.01f, 1f);
            respawnSeconds = Mathf.Max(1f, respawnDelay);
        }

        private IEnumerator RespawnRoutine()
        {
            available = false;
            trigger.enabled = false;
            SetVisuals(false);

            yield return new WaitForSeconds(respawnSeconds);

            SetVisuals(true);
            trigger.enabled = true;
            available = true;
        }

        private void SetVisuals(bool visible)
        {
            foreach (Renderer visual in visuals)
            {
                if (visual != null) visual.enabled = visible;
            }
        }
    }
}
