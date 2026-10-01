using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed class TargetIndicatorHUD : MonoBehaviour
    {
        private SoftTargeting targeting;
        private Health playerHealth;
        private Camera cachedCamera;
        private GUIStyle label;
        private float nextResolve;
        private float lastScale = -1f;

        private void Start()
        {
            cachedCamera = Camera.main;
            ResolveTargeting();
        }

        private void Update()
        {
            if (targeting == null && Time.unscaledTime >= nextResolve)
                ResolveTargeting();

            if (cachedCamera == null)
                cachedCamera = Camera.main;
        }

        private void ResolveTargeting()
        {
            nextResolve = Time.unscaledTime + .5f;
            PlayerHeroAbilities player = FindAnyObjectByType<PlayerHeroAbilities>();
            if (player != null)
            {
                targeting = player.GetComponent<SoftTargeting>();
                playerHealth = player.GetComponent<Health>();
            }
        }

        private void OnGUI()
        {
            Health target = targeting != null ? targeting.Current : null;
            if (target == null || playerHealth == null || playerHealth.IsDead || MainMenuController.IsVisible ||
                cachedCamera == null)
                return;

            Vector3 screen = cachedCamera.WorldToScreenPoint(target.transform.position + Vector3.up * 1.25f);
            float scale = Mathf.Clamp(Screen.height / 900f, .72f, 1.25f);
            float margin = 80f * scale;
            if (screen.z <= 0f || screen.x < -margin || screen.x > Screen.width + margin ||
                screen.y < -margin || screen.y > Screen.height + margin)
                return;

            screen.y = Screen.height - screen.y;
            bool objective = target.GetComponent<DimensionPlanet>() != null;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.6f) * .018f;
            float radius = (objective ? 31f : 24f) * scale * pulse;
            Color tint = objective
                ? new Color(StylizedArtDirection.WarmAccent.r, StylizedArtDirection.WarmAccent.g, StylizedArtDirection.WarmAccent.b, .88f)
                : new Color(1f, .50f, .56f, .88f);

            Vector2 center = new Vector2(screen.x, screen.y);
            // Vector-only marker: no runtime texture/quad, so there can never be an opaque red square.
            CombatGlyphs.Ring(center, radius, Mathf.Max(1.4f, 1.8f * scale), tint, 32);
            Color soft = new Color(tint.r, tint.g, tint.b, .28f);
            CombatGlyphs.Ring(center, radius + 3.5f * scale, Mathf.Max(.8f, 1.0f * scale), soft, 32);

            float tick = 7f * scale;
            float gap = 3f * scale;
            float width = Mathf.Max(1.4f, 1.8f * scale);
            CombatGlyphs.Line(new Vector2(center.x - radius - tick, center.y), new Vector2(center.x - radius - gap, center.y), width, tint);
            CombatGlyphs.Line(new Vector2(center.x + radius + gap, center.y), new Vector2(center.x + radius + tick, center.y), width, tint);
            CombatGlyphs.Line(new Vector2(center.x, center.y - radius - tick), new Vector2(center.x, center.y - radius - gap), width, tint);
            CombatGlyphs.Line(new Vector2(center.x, center.y + radius + gap), new Vector2(center.x, center.y + radius + tick), width, tint);

            // Ordinary soft targets need only the reticle. Keep text for special dimension objectives.
            if (!objective) return;
            EnsureStyle(scale);
            GUI.Label(new Rect(screen.x - 88f * scale, screen.y - radius - 24f * scale,
                176f * scale, 19f * scale), "DIMENSION OBJECTIVE", label);
        }

        private void EnsureStyle(float scale)
        {
            if (label != null && Mathf.Abs(lastScale - scale) < .01f)
                return;
            lastScale = scale;
            label = new GUIStyle(GUI.skin.label)
            {
                font = FrontEndTypography.Display,
                fontSize = Mathf.Max(9, Mathf.RoundToInt(10f * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            label.normal.textColor = new Color(.94f, .97f, 1f, .94f);
        }
    }
}

