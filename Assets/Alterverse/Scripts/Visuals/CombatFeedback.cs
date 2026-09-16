using System.Collections;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    [RequireComponent(typeof(Health))]
    public sealed class CombatFeedback : MonoBehaviour
    {
        private Health health;
        private Renderer[] renderers;
        private Color[] baseColors;
        private float lastHealth;
        private Coroutine flashRoutine;

        private void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].material.color;

            lastHealth = health.Current;
            health.Changed += OnHealthChanged;
        }

        private void Start()
        {
            lastHealth = health.Current;
        }

        private void OnDestroy()
        {
            if (health != null) health.Changed -= OnHealthChanged;
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current < lastHealth - 0.01f)
            {
                if (flashRoutine != null) StopCoroutine(flashRoutine);
                flashRoutine = StartCoroutine(FlashRoutine());
                PrototypeVfx.Burst(transform.position, new Color(0.95f, 0.25f, 0.35f), 0.7f, 5);
            }
            lastHealth = current;
        }

        private IEnumerator FlashRoutine()
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].material.color = Color.white;

            yield return new WaitForSeconds(0.07f);

            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].material.color = baseColors[i];

            flashRoutine = null;
        }
    }
}
