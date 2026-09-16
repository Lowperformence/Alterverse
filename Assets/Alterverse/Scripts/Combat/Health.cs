using System;
using UnityEngine;

namespace Alterverse.Combat
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnDeath;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsDead { get; private set; }
        public float Normalized => maxHealth <= 0f ? 0f : Current / maxHealth;

        public event Action<float, float> Changed;
        public event Action Died;

        private void Awake()
        {
            Current = maxHealth;
        }

        public void Configure(float newMaxHealth, bool shouldDestroyOnDeath = false)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            destroyOnDeath = shouldDestroyOnDeath;
            IsDead = false;
            Current = maxHealth;
            Changed?.Invoke(Current, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, maxHealth);
            Debug.Log($"{name} took {amount:0.#} damage. HP: {Current:0.#}/{maxHealth:0.#}");

            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
                if (destroyOnDeath) Destroy(gameObject);
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }

        public void RestoreFull()
        {
            IsDead = false;
            Current = maxHealth;
            Changed?.Invoke(Current, maxHealth);
        }
    }
}
