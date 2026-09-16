using System.Collections;
using UnityEngine;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class CombatFeedback : MonoBehaviour
    {
        private Health health;
        private Renderer bodyRenderer;
        private Color baseColor;
        private float previousHealth;

        private void Awake()
        {
            health = GetComponent<Health>();
            bodyRenderer = GetComponent<Renderer>();
            if (bodyRenderer != null) baseColor = bodyRenderer.material.color;
            previousHealth = health.Current;
            health.Changed += OnHealthChanged;
        }

        private void Start() => previousHealth = health.Current;

        private void OnDestroy()
        {
            if (health != null) health.Changed -= OnHealthChanged;
        }

        private void OnHealthChanged(float current, float max)
        {
            float delta = previousHealth - current;
            previousHealth = current;
            if (delta <= 0.01f) return;

            if (bodyRenderer != null) StartCoroutine(Flash());
            SpawnImpact(transform.position + Vector3.up * 1.1f, delta);
        }

        private IEnumerator Flash()
        {
            bodyRenderer.material.color = Color.white;
            transform.localScale = new Vector3(1.08f, 0.94f, 1.08f);
            yield return new WaitForSeconds(0.07f);
            if (bodyRenderer != null) bodyRenderer.material.color = baseColor;
            transform.localScale = Vector3.one;
        }

        private static void SpawnImpact(Vector3 position, float damage)
        {
            GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impact.name = "AV_HitImpact";
            impact.transform.position = position;
            impact.transform.localScale = Vector3.one * Mathf.Clamp(0.25f + damage * 0.008f, 0.3f, 0.65f);
            Collider collider = impact.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Renderer renderer = impact.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.82f, 0.25f, 1f);
            impact.AddComponent<TransientVfx>().Initialize(0.22f, 2.4f);
        }
    }

    public sealed class TransientVfx : MonoBehaviour
    {
        private float lifetime;
        private float growSpeed;
        private float age;

        public void Initialize(float duration, float growth)
        {
            lifetime = duration;
            growSpeed = growth;
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.localScale += Vector3.one * growSpeed * Time.deltaTime;
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
