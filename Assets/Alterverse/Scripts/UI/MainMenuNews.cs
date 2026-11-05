using Alterverse.Match;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        private static Texture2D snowmanNewsIcon;
        private GUIStyle newsTitle, newsEyebrow;
        private Rect HomeNewsRect => new(Right - 274f,
            HomeAdvertisingActive ? Mathf.Max(110f, HomeBannerMenuRect.y - 268f) : 460f, 274f, 256f);
        private float HomeHeroColumnX => Left + ContentWidth * .62f + 44f;
        private float HomeHeroColumnWidth => Mathf.Max(100f, HomeNewsRect.x - HomeHeroColumnX - 12f);
        private float HomeHeroPreviewWidth => Mathf.Min(320f, HomeHeroColumnWidth * 1.6f);
        private float HomeHeroCaptionX => HomeAdvertisingActive
            ? HomeNewsRect.x - 12f - (HomeHeroPreviewWidth + HomeHeroColumnWidth) * .5f
            : Left + ContentWidth * .62f + 24f;
        private float HomeHeroCaptionY => HomeAdvertisingActive ? HomeBannerMenuRect.y - 66f : 616f;
        private float HomeHeroCaptionWidth =>
            HomeAdvertisingActive ? HomeHeroColumnWidth :
            Mathf.Max(150f, HomeNewsRect.x - HomeHeroCaptionX - 12f);
        private Rect HomeHeroPreviewRect(float heroX) => HomeAdvertisingActive
            // Grow into the free space on the left; retain the full portrait and pedestal.
            ? new Rect(HomeNewsRect.x - 12f - HomeHeroPreviewWidth, 124f, HomeHeroPreviewWidth,
                Mathf.Max(140f, HomeHeroCaptionY - 132f))
            : new Rect(heroX - 64f, 152f, Right - heroX, 450f);

        private void DrawHomeNews()
        {
            Rect panel = HomeNewsRect;
            Color iceBlue = new(.55f, .88f, 1f, 1f);
            theme.Panel(panel);
            FrontEndTheme.Fill(new Rect(panel.x + 14, panel.y + 1, panel.width - 28, 2), iceBlue);
            newsEyebrow ??= new GUIStyle(theme.Small) { fontSize = 14, fontStyle = FontStyle.Bold };
            newsTitle ??= new GUIStyle(theme.Heading) { fontSize = 24 };
            newsEyebrow.normal.textColor = iceBlue;

            theme.FitLabel(new Rect(panel.x + 16, panel.y + 12, 66, 22), "NEWS", theme.Small, true);
            theme.FitLabel(new Rect(panel.x + 86, panel.y + 12, 172, 22), "CHRISTMAS EVENT", newsEyebrow);
            theme.FitLabel(new Rect(panel.x + 16, panel.y + 40, 242, 32), "Snowmen on Map", newsTitle);
            BuildSnowmanNewsIcon();
            GUI.DrawTexture(new Rect(panel.x + 12, panel.y + 82, 84, 84), snowmanNewsIcon, ScaleMode.ScaleToFit, true);
            theme.FitLabel(new Rect(panel.x + 108, panel.y + 85, 150, 26),
                $"Every {ChristmasSnowmanRules.SpawnInterval / 60f:0} min", theme.Body);
            theme.FitLabel(new Rect(panel.x + 108, panel.y + 118, 150, 30),
                $"+{ChristmasSnowmanRules.ShardsPerSnowman} SHARD", newsTitle);
            theme.StatusBadge(new Rect(panel.x + 16, panel.y + 177, 242, 28), "ONLINE + OFFLINE", iceBlue);
            theme.FitLabel(new Rect(panel.x + 16, panel.y + 216, 242, 24),
                "Players only. No bot pickups.", theme.Small, true);
        }

        private static void BuildSnowmanNewsIcon()
        {
            if (snowmanNewsIcon != null) return;
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color color = Color.clear;
                // Twig arms sit behind the three snowballs.
                if (x >= 14 && x <= 46 && Mathf.Abs(y - (82f - x * .35f)) < 1.5f ||
                    x >= 82 && x <= 114 && Mathf.Abs(y - (37f + x * .35f)) < 1.5f)
                    color = new Color(.32f, .17f, .08f);
                float body = NewsDisc(x, y, 64, 31, 26);
                float middle = NewsDisc(x, y, 64, 65, 21);
                float head = NewsDisc(x, y, 64, 94, 17);
                float surface = Mathf.Min(body, Mathf.Min(middle, head));
                if (surface < 1f)
                {
                    float light = 1f - surface * .12f;
                    color = new Color(.88f * light, .96f * light, light, Mathf.Clamp01((1f - surface) * 16f));
                }
                if (NewsDisc(x, y, 58, 97, 2.4f) < 1 || NewsDisc(x, y, 70, 97, 2.4f) < 1 ||
                    NewsDisc(x, y, 64, 65, 2.7f) < 1 || NewsDisc(x, y, 64, 54, 2.7f) < 1)
                    color = new Color(.025f, .035f, .05f);
                for (int i = 0; i < 5; i++)
                    if (NewsDisc(x, y, 64 + (i - 2) * 3.4f, 86 + (i - 2) * (i - 2) * .7f, 1.2f) < 1)
                        color = new Color(.025f, .035f, .05f);
                if (NewsTriangle(x, y, 63, 94, 78, 90, 63, 89))
                    color = new Color(1f, .35f, .035f);
                if (x >= 45 && x <= 83 && y >= 78 && y <= 83 ||
                    x >= 75 && x <= 81 && y >= 60 && y <= 82 ||
                    NewsTriangle(x, y, 48, 106, 80, 106, 70, 123))
                    color = new Color(.88f, .035f, .075f);
                if (x >= 45 && x <= 83 && y >= 104 && y <= 109 || NewsDisc(x, y, 70, 123, 4) < 1)
                    color = new Color(.95f, .99f, 1f);
                pixels[y * size + x] = color;
            }
            snowmanNewsIcon = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DF_ChristmasNewsSnowman",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            snowmanNewsIcon.SetPixels32(pixels);
            snowmanNewsIcon.Apply(false, true);
        }

        private static float NewsDisc(float x, float y, float cx, float cy, float radius)
        {
            float dx = (x + .5f - cx) / radius, dy = (y + .5f - cy) / radius;
            return dx * dx + dy * dy;
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
        private static void ResetSnowmanNewsIcon()
        {
            if (snowmanNewsIcon != null) Object.Destroy(snowmanNewsIcon);
            snowmanNewsIcon = null;
        }
    }
}


