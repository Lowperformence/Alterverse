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
        private Camera cachedCamera;
        private Texture2D ringTexture;
        private GUIStyle label;
        private float nextResolve;
        private float lastScale = -1f;

        private void Start()
        {
            ringTexture = BuildRingTexture();
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

        private static Texture2D BuildRingTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "DF_TargetReticle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[size * size];
            const float radius = .405f;
            const float halfWidth = .020f;
            const float feather = .014f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size - .5f;
                float dy = (y + .5f) / size - .5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.Abs(distance - radius);
                float core = 1f - Mathf.SmoothStep(halfWidth - feather, halfWidth + feather, edge);
                float glow = Mathf.Exp(-edge * 150f) * .16f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(core, glow)));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void ResolveTargeting()
        {
            nextResolve = Time.unscaledTime + .5f;
            PlayerHeroAbilities player = FindAnyObjectByType<PlayerHeroAbilities>();
            if (player != null)
                targeting = player.GetComponent<SoftTargeting>();
        }

        private void OnGUI()
        {
            Health target = targeting != null ? targeting.Current : null;
            if (target == null || cachedCamera == null || ringTexture == null)
                return;

            Vector3 screen = cachedCamera.WorldToScreenPoint(target.transform.position + Vector3.up * 1.25f);
            float scale = Mathf.Clamp(Screen.height / 900f, .72f, 1.25f);
            float margin = 80f * scale;
            if (screen.z <= 0f || screen.x < -margin || screen.x > Screen.width + margin ||
                screen.y < -margin || screen.y > Screen.height + margin)
                return;

            screen.y = Screen.height - screen.y;
            bool objective = target.GetComponent<DimensionPlanet>() != null;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.6f) * .025f;
            float diameter = (objective ? 70f : 54f) * scale * pulse;
            Color tint = objective
                ? StylizedArtDirection.WarmAccent
                : new Color(1f, .32f, .42f, .78f);
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(screen.x - diameter * .5f, screen.y - diameter * .5f, diameter, diameter),
                ringTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;

            EnsureStyle(scale);
            GUI.Label(new Rect(screen.x - 76f * scale, screen.y - diameter * .5f - 22f * scale,
                152f * scale, 19f * scale), objective ? "DIMENSION OBJECTIVE" : "SOFT TARGET", label);
        }

        private void EnsureStyle(float scale)
        {
            if (label != null && Mathf.Abs(lastScale - scale) < .01f)
                return;
            lastScale = scale;
            label = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(9, Mathf.RoundToInt(10f * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnDestroy()
        {
            if (ringTexture != null)
                Destroy(ringTexture);
        }
    }
}
