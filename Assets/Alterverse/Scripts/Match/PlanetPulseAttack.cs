using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Match
{
    [RequireComponent(typeof(DimensionPlanet), typeof(Health))]
    public sealed class PlanetPulseAttack : MonoBehaviour
    {
        [SerializeField] private float interval = 5.5f, telegraphDuration = 1.15f, radius = 5.2f, damage = 7f;
        private const int Segments = 64;
        private DimensionPlanet planet;
        private Health health;
        private LineRenderer boundary, charge;
        private Material ringMaterial;
        private float nextPulse, telegraphStart, flashUntil;
        private bool telegraphing;
        public bool IsTelegraphing => isActiveAndEnabled && telegraphing && planet != null && planet.Available && health != null && !health.IsDead;
        public float BlastRadius => radius;
        private readonly Color warning = new Color(1f, .48f, .12f);

        private void Awake()
        {
            planet = GetComponent<DimensionPlanet>();
            health = GetComponent<Health>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                ringMaterial = new Material(shader);
                ringMaterial.color = warning;
                boundary = CreateRing("DF_PulseBoundary", .09f);
                charge = CreateRing("DF_PulseCharge", .14f);
            }
            nextPulse = Time.time + 2.5f;
        }

        private LineRenderer CreateRing(string label, float width)
        {
            GameObject ring = new GameObject(label);
            ring.transform.SetParent(transform, false);
            LineRenderer line = ring.AddComponent<LineRenderer>();
            line.sharedMaterial = ringMaterial;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.widthMultiplier = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private void DrawRing(LineRenderer line, float worldRadius)
        {
            if (line == null) return;
            Vector3 center = transform.position;
            center.y = .16f;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * worldRadius);
            }
        }

        private void Update()
        {
            if (planet == null || !planet.Available || health == null || health.IsDead)
            {
                HideRings();
                return;
            }
            if (Time.timeScale <= 0f) return;
            if (!telegraphing)
            {
                if (Time.time < flashUntil) return;
                HideRings();
                if (Time.time < nextPulse) return;
                telegraphing = true;
                telegraphStart = Time.time;
                if (boundary != null) { boundary.enabled = true; boundary.widthMultiplier = .09f; }
                if (charge != null) charge.enabled = true;
                if (ringMaterial != null) ringMaterial.color = warning;
            }

            float progress = Mathf.Clamp01((Time.time - telegraphStart) / Mathf.Max(.1f, telegraphDuration));
            // The outer boundary always matches the damage radius; the inner ring shows time remaining.
            DrawRing(boundary, radius);
            DrawRing(charge, radius * progress);
            if (progress < 1f) return;
            Explode();
            telegraphing = false;
            flashUntil = Time.time + .18f;
            nextPulse = Time.time + Mathf.Max(.5f, interval);
            if (charge != null) charge.enabled = false;
            if (boundary != null) boundary.widthMultiplier = .24f;
            if (ringMaterial != null) ringMaterial.color = Color.white;
        }

        private void Explode()
        {
            foreach (Health victim in FindObjectsByType<Health>())
            {
                if (victim == null || victim.IsDead || victim == health) continue;
                if (victim.GetComponent<TeamMember>() == null && victim.GetComponent<LaneMinion>() == null) continue;
                Vector3 delta = victim.transform.position - transform.position;
                delta.y = 0;
                if (delta.sqrMagnitude <= radius * radius) victim.TakeDamage(damage, gameObject);
            }
            Alterverse.Visuals.PrototypeVfx.Burst(transform.position, warning, radius * .55f, 10);
        }

        private void HideRings()
        {
            telegraphing = false;
            if (boundary != null) boundary.enabled = false;
            if (charge != null) charge.enabled = false;
        }

        private void OnDisable() => HideRings();
        private void OnDestroy() { if (ringMaterial != null) Destroy(ringMaterial); }
    }
}

