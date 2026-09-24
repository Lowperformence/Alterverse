using System.Reflection;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Visual-only pass for the Google Play shard packs.
    /// It deliberately does not create controls or touch the store callbacks/product ids.
    /// The existing MainMenuController keeps owning the price buttons and purchases.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class ShardShopVisualOverlay : MonoBehaviour
    {
        private const float MenuHeight = 720f;
        private static ShardShopVisualOverlay instance;
        private static FieldInfo pageField;
        private static FieldInfo shardShopField;

        private readonly Texture2D[] crystalArt = new Texture2D[4];
        private GUIStyle packNameStyle;
        private GUIStyle amountStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<ShardShopVisualOverlay>() == null)
                new GameObject("DF_ShardShopVisualOverlay").AddComponent<ShardShopVisualOverlay>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < crystalArt.Length; i++)
                if (crystalArt[i] != null) Destroy(crystalArt[i]);
            if (instance == this) instance = null;
        }

        private bool IsShardStoreVisible()
        {
            if (!MainMenuController.IsVisible) return false;
            MainMenuController menu = FindAnyObjectByType<MainMenuController>();
            if (menu == null) return false;

            if (pageField == null)
                pageField = typeof(MainMenuController).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic);
            if (shardShopField == null)
                shardShopField = typeof(MainMenuController).GetField("shardShop", BindingFlags.Instance | BindingFlags.NonPublic);
            if (pageField == null || shardShopField == null) return false;

            object page = pageField.GetValue(menu);
            object shard = shardShopField.GetValue(menu);
            return page != null && page.ToString() == "Shop" && shard is bool && (bool)shard;
        }

        private void OnGUI()
        {
            if (!IsShardStoreVisible()) return;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f) return;

            float scale = Mathf.Min(safe.height / MenuHeight, safe.width / 1200f);
            if (scale <= 0f) return;
            float menuWidth = safe.width / scale;
            float contentWidth = Mathf.Min(1360f, menuWidth - 64f);
            float left = (menuWidth - contentWidth) * .5f;
            float y = Screen.height - safe.yMax + (safe.height - MenuHeight * scale) * .5f;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            try
            {
                // Lower IMGUI depth renders in front of the base menu. No controls are
                // created here, so the existing purchase buttons keep receiving clicks.
                GUI.depth = -2000;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0f), Quaternion.identity, Vector3.one * scale);
                GUI.color = Color.white;
                DrawShardCards(left, contentWidth);
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.depth = oldDepth;
            }
        }

        private void DrawShardCards(float left, float contentWidth)
        {
            ShardPack[] packs = ShardPacks.All;
            if (packs == null || packs.Length == 0) return;
            EnsureStyles();

            float cardWidth = (contentWidth - 48f) / 4f;
            for (int i = 0; i < packs.Length; i++)
            {
                ShardPack pack = packs[i];
                float x = left + i * (cardWidth + 16f);
                int tier = TierFor(pack.Amount, i);

                Rect upper = new Rect(x + 1f, 369f, cardWidth - 2f, 136f);
                Fill(upper, new Color(.018f, .025f, .070f, .985f));

                // Subtle dimensional glow and separators keep the card premium without
                // competing with the localized Google Play price button below it.
                Fill(new Rect(x + 1f, 369f, cardWidth - 2f, 2f), new Color(.22f, .72f, 1f, .85f));
                Fill(new Rect(x + 1f, 503f, cardWidth - 2f, 2f), new Color(.18f, .50f, .95f, .42f));
                Fill(new Rect(x + 1f, 371f, 1f, 132f), new Color(.22f, .55f, 1f, .28f));
                Fill(new Rect(x + cardWidth - 2f, 371f, 1f, 132f), new Color(.22f, .55f, 1f, .28f));

                Color tierGlow = TierGlow(tier);
                Fill(new Rect(x + cardWidth * .18f, 400f, cardWidth * .64f, 1f),
                    new Color(tierGlow.r, tierGlow.g, tierGlow.b, .22f));

                GUI.Label(new Rect(x + 14f, 376f, cardWidth - 28f, 22f), pack.Name, packNameStyle);

                Texture2D art = GetCrystalArt(tier);
                if (art != null)
                {
                    Color tint = GUI.color;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(x + 12f, 397f, cardWidth - 24f, 88f), art, ScaleMode.ScaleToFit, true);
                    GUI.color = tint;
                }

                GUI.Label(new Rect(x + 12f, 476f, cardWidth - 24f, 28f), pack.Amount.ToString("N0") + " SHARDS", amountStyle);
            }
        }

        private void EnsureStyles()
        {
            if (packNameStyle == null)
            {
                packNameStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip
                };
                packNameStyle.normal.textColor = new Color(.68f, .84f, 1f, 1f);
            }
            if (amountStyle == null)
            {
                amountStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 19,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip
                };
                amountStyle.normal.textColor = Color.white;
            }
        }

        private Texture2D GetCrystalArt(int tier)
        {
            tier = Mathf.Clamp(tier, 0, crystalArt.Length - 1);
            if (crystalArt[tier] == null) crystalArt[tier] = BuildCrystalArt(tier);
            return crystalArt[tier];
        }

        private static int TierFor(int amount, int index)
        {
            if (amount <= 500) return 0;
            if (amount <= 1300) return 1;
            if (amount <= 3000) return 2;
            if (amount >= 5000) return 3;
            return Mathf.Clamp(index, 0, 3);
        }

        private static Color TierGlow(int tier)
        {
            switch (tier)
            {
                case 0: return new Color(.20f, .66f, 1f, 1f);
                case 1: return new Color(.27f, .75f, 1f, 1f);
                case 2: return new Color(.36f, .62f, 1f, 1f);
                default: return new Color(.50f, .43f, 1f, 1f);
            }
        }

        private static Texture2D BuildCrystalArt(int tier)
        {
            const int width = 320;
            const int height = 180;
            Color32[] pixels = new Color32[width * height];

            // Blue-violet bloom behind the shards.
            float glowW = 115f + tier * 10f;
            float glowH = 66f + tier * 5f;
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    float dx = (px - 160f) / glowW;
                    float dy = (py - 82f) / glowH;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= 1f) continue;
                    float strength = (1f - d) * (1f - d);
                    byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt((48f + tier * 8f) * strength), 0, 255);
                    Blend(ref pixels[py * width + px], new Color32(58, 92, 255, alpha));
                }
            }

            if (tier >= 3)
            {
                DrawCrystal(pixels, width, height, 76f, 64f, 31f, 68f, -8f, .72f);
                DrawCrystal(pixels, width, height, 247f, 62f, 34f, 72f, 8f, .76f);
            }
            if (tier >= 1)
                DrawCrystal(pixels, width, height, 111f, 70f, 40f, 86f, -7f, .84f);
            if (tier >= 2)
                DrawCrystal(pixels, width, height, 214f, 70f, 43f, 92f, 8f, .88f);

            float mainWidth = 66f + tier * 5f;
            float mainHeight = 126f + tier * 6f;
            DrawCrystal(pixels, width, height, 160f, 85f, mainWidth, mainHeight, tier == 3 ? 5f : 0f, 1f);

            // Deterministic star/spark accents. Higher tiers get a richer cluster.
            Vector2[] sparks =
            {
                new Vector2(54,126),new Vector2(267,124),new Vector2(83,157),new Vector2(239,151),
                new Vector2(36,82),new Vector2(286,80),new Vector2(105,132),new Vector2(221,137)
            };
            int sparkCount = 2 + tier * 2;
            for (int i = 0; i < sparkCount && i < sparks.Length; i++)
                DrawSpark(pixels, width, height, sparks[i], 2 + (i % 2), tier);

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "DF_ShardCrystal_Tier" + tier,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawCrystal(Color32[] pixels, int width, int height, float cx, float cy, float shardWidth, float shardHeight, float lean, float brightness)
        {
            Vector2 top = new Vector2(cx + lean, cy + shardHeight * .50f);
            Vector2 right = new Vector2(cx + shardWidth * .50f, cy + shardHeight * .06f);
            Vector2 bottom = new Vector2(cx - lean * .18f, cy - shardHeight * .50f);
            Vector2 left = new Vector2(cx - shardWidth * .50f, cy + shardHeight * .05f);
            Vector2 center = new Vector2(cx + lean * .16f, cy - shardHeight * .02f);

            Color32 highlight = Scale(new Color32(185, 249, 255, 250), brightness);
            Color32 cyan = Scale(new Color32(72, 220, 255, 248), brightness);
            Color32 blue = Scale(new Color32(28, 145, 241, 248), brightness);
            Color32 violet = Scale(new Color32(73, 81, 210, 245), brightness);
            Color32 edge = Scale(new Color32(171, 245, 255, 220), brightness);

            FillTriangle(pixels, width, height, top, center, left, highlight);
            FillTriangle(pixels, width, height, top, right, center, cyan);
            FillTriangle(pixels, width, height, left, center, bottom, blue);
            FillTriangle(pixels, width, height, center, right, bottom, violet);

            DrawLine(pixels, width, height, top, right, edge);
            DrawLine(pixels, width, height, right, bottom, edge);
            DrawLine(pixels, width, height, bottom, left, edge);
            DrawLine(pixels, width, height, left, top, edge);
            DrawLine(pixels, width, height, top, center, new Color32(220, 255, 255, 170));
            DrawLine(pixels, width, height, center, bottom, new Color32(120, 219, 255, 150));
        }

        private static void DrawSpark(Color32[] pixels, int width, int height, Vector2 center, int radius, int tier)
        {
            Color32 color = tier >= 3 ? new Color32(178, 190, 255, 210) : new Color32(153, 239, 255, 210);
            Vector2 top = center + Vector2.up * (radius + 2);
            Vector2 right = center + Vector2.right * radius;
            Vector2 bottom = center + Vector2.down * (radius + 2);
            Vector2 left = center + Vector2.left * radius;
            FillTriangle(pixels, width, height, top, right, bottom, color);
            FillTriangle(pixels, width, height, top, bottom, left, color);
        }

        private static Color32 Scale(Color32 color, float brightness)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * brightness), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * brightness), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * brightness), 0, 255),
                color.a);
        }

        private static void FillTriangle(Color32[] pixels, int width, int height, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))), 0, height - 1);
            float area = Edge(a, b, c);
            if (Mathf.Abs(area) < .001f) return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x + .5f, y + .5f);
                    float w0 = Edge(b, c, p);
                    float w1 = Edge(c, a, p);
                    float w2 = Edge(a, b, p);
                    bool inside = area > 0f ? w0 >= 0f && w1 >= 0f && w2 >= 0f : w0 <= 0f && w1 <= 0f && w2 <= 0f;
                    if (inside) Blend(ref pixels[y * width + x], color);
                }
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p)
        {
            return (p.x - a.x) * (b.y - a.y) - (p.y - a.y) * (b.x - a.x);
        }

        private static void DrawLine(Color32[] pixels, int width, int height, Vector2 from, Vector2 to, Color32 color)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int px = x + ox, py = y + oy;
                        if (px < 0 || py < 0 || px >= width || py >= height) continue;
                        byte alpha = (byte)(ox == 0 && oy == 0 ? color.a : color.a / 3);
                        Blend(ref pixels[py * width + px], new Color32(color.r, color.g, color.b, alpha));
                    }
                }
            }
        }

        private static void Blend(ref Color32 destination, Color32 source)
        {
            float sa = source.a / 255f;
            if (sa <= 0f) return;
            float da = destination.a / 255f;
            float oa = sa + da * (1f - sa);
            if (oa <= .0001f) return;
            destination = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((source.r * sa + destination.r * da * (1f - sa)) / oa), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((source.g * sa + destination.g * da * (1f - sa)) / oa), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((source.b * sa + destination.b * da * (1f - sa)) / oa), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(oa * 255f), 0, 255));
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
