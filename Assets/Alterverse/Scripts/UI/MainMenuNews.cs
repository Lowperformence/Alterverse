using Alterverse.Match;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        private static Texture2D pumpkinNewsIcon;
        private GUIStyle newsTitle, newsEyebrow;
        private Rect HomeNewsRect => new(Right - 274f,
            HomeAdvertisingActive ? Mathf.Max(110f, HomeBannerMenuRect.y - 268f) : 460f, 274f, 256f);
        private float HomeHeroCaptionX => Left + ContentWidth * .62f + (HomeAdvertisingActive ? 44f : 24f);
        private float HomeHeroCaptionY => HomeAdvertisingActive ? HomeBannerMenuRect.y - 66f : 616f;
        private float HomeHeroCaptionWidth =>
            HomeAdvertisingActive ? Mathf.Max(100f, HomeNewsRect.x - HomeHeroCaptionX - 12f) :
            Mathf.Max(150f, HomeNewsRect.x - HomeHeroCaptionX - 12f);
        private Rect HomeHeroPreviewRect(float heroX) => HomeAdvertisingActive
            ? new Rect(HomeHeroCaptionX, 152f, HomeHeroCaptionWidth, Mathf.Max(140f, HomeHeroCaptionY - 160f))
            : new Rect(heroX - 64f, 152f, Right - heroX, 450f);

        private void DrawHomeNews()
        {
            Rect panel = HomeNewsRect;
            Color orange = new(1f, .52f, .12f, 1f);
            theme.Panel(panel);
            FrontEndTheme.Fill(new Rect(panel.x + 14, panel.y + 1, panel.width - 28, 2), orange);
            newsEyebrow ??= new GUIStyle(theme.Small) { fontSize = 14, fontStyle = FontStyle.Bold };
            newsTitle ??= new GUIStyle(theme.Heading) { fontSize = 24 };
            newsEyebrow.normal.textColor = orange;

            theme.FitLabel(new Rect(panel.x + 16, panel.y + 12, 66, 22), "NEWS", theme.Small, true);
            theme.FitLabel(new Rect(panel.x + 86, panel.y + 12, 172, 22), "HALLOWEEN EVENT", newsEyebrow);
            theme.FitLabel(new Rect(panel.x + 16, panel.y + 40, 242, 32), "Pumpkins on Map", newsTitle);
            BuildPumpkinNewsIcon();
            GUI.DrawTexture(new Rect(panel.x + 12, panel.y + 82, 84, 84), pumpkinNewsIcon, ScaleMode.ScaleToFit, true);
            theme.FitLabel(new Rect(panel.x + 108, panel.y + 85, 150, 26),
                $"Every {HalloweenPumpkinRules.SpawnInterval / 60f:0} min", theme.Body);
            theme.FitLabel(new Rect(panel.x + 108, panel.y + 118, 150, 30),
                $"+{HalloweenPumpkinRules.ShardsPerPumpkin} SHARD", newsTitle);
            theme.StatusBadge(new Rect(panel.x + 16, panel.y + 177, 242, 28), "ONLINE + OFFLINE", orange);
            theme.FitLabel(new Rect(panel.x + 16, panel.y + 216, 242, 24),
                "Players only. No bot pickups.", theme.Small, true);
        }

        private static void BuildPumpkinNewsIcon()
        {
            if (pumpkinNewsIcon != null) return;
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = (x + .5f - 64f) / 50f;
                float py = (y + .5f - 57f) / 43f;
                float body = px * px + py * py;
                Color color = Color.clear;
                if (body < 1f)
                {
                    float ribs = .80f + .20f * Mathf.Cos(px * 16f);
                    float light = Mathf.Clamp01(1f - body * .38f + py * .15f);
                    color = new Color(1f * ribs * light, .35f * ribs * light, .035f, Mathf.Clamp01((1f - body) * 20f));
                    if (NewsTriangle(x, y, 35, 65, 49, 68, 47, 80) ||
                        NewsTriangle(x, y, 79, 68, 93, 65, 81, 80) ||
                        NewsTriangle(x, y, 57, 53, 71, 53, 64, 64) ||
                        (y > 35 && y < 47 && x > 38 && x < 90 &&
                         !(y > 42 && (x > 49 && x < 56 || x > 72 && x < 79))))
                        color = new Color(1f, .91f, .32f, 1f);
                }
                if (x >= 59 && x <= 70 && y >= 96 && y <= 117)
                    color = new Color(.28f, .39f, .12f, 1f);
                pixels[y * size + x] = color;
            }
            pumpkinNewsIcon = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DF_HalloweenNewsPumpkin",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            pumpkinNewsIcon.SetPixels32(pixels);
            pumpkinNewsIcon.Apply(false, true);
        }

        private static bool NewsTriangle(float x, float y,
            float ax, float ay, float bx, float by, float cx, float cy)
        {
            float a = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float b = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float c = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            return !(a < 0 || b < 0 || c < 0) || !(a > 0 || b > 0 || c > 0);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPumpkinNewsIcon()
        {
            if (pumpkinNewsIcon != null) Object.Destroy(pumpkinNewsIcon);
            pumpkinNewsIcon = null;
        }
    }
}

