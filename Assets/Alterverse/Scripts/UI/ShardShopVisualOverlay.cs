using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Shard artwork drawn by the real shop UI. These methods create no GUI controls
    /// and never handle input; MainMenuController retains the Google Play purchase action.
    /// </summary>
    public static class ShardShopVisualOverlay
    {
        private static Texture2D crystal;
        private static Texture2D glow;
        private static GUIStyle amountStyle;
        private static GUIStyle packStyle;

        public static void DrawPackCard(Rect card,ShardPack pack,int tier)
        {
            if(Event.current.type!=EventType.Repaint)return;
            EnsureArtwork();tier=Mathf.Clamp(tier,0,3);
            Color accent=tier==3?new Color(.72f,.48f,1f):new Color(.20f,.88f,1f);
            Fill(new Rect(card.x+2,card.y+2,card.width-4,3),accent);
            Fill(new Rect(card.x+2,card.y+5,card.width-4,106),new Color(.015f,.035f,.09f,.82f));
            GUI.Label(new Rect(card.x+10,card.y+9,card.width-20,22),pack.Name,packStyle);
            float artWidth=Mathf.Min(124,card.width-48);
            DrawCluster(new Rect(card.center.x-artWidth*.5f,card.y+26,artWidth,80),tier,accent);
            amountStyle.fontSize=Mathf.Min(20,Mathf.FloorToInt(card.width*.105f));
            GUI.Label(new Rect(card.x+8,card.y+108,card.width-16,30),pack.Amount.ToString("N0")+" SHARDS",amountStyle);
        }

        public static void DrawSubscriptionArtwork(Rect card)
        {
            if(Event.current.type!=EventType.Repaint)return;
            EnsureArtwork();
            Fill(new Rect(card.x+2,card.y+2,card.width-4,3),new Color(.43f,1f,.88f));
            DrawCluster(new Rect(card.x+16,card.y+8,86,94),2,new Color(.40f,1f,.90f));
        }

        static void DrawCluster(Rect art,int tier,Color accent)
        {
            Color previous=GUI.color;
            float pulse=.90f+.10f*Mathf.Sin(Time.unscaledTime*1.6f+tier);
            GUI.color=new Color(accent.r,accent.g,accent.b,.80f*pulse);
            GUI.DrawTexture(new Rect(art.x-22,art.y-15,art.width+44,art.height+30),glow,ScaleMode.StretchToFill,true);
            GUI.color=Color.white;
            if(tier>=1)GUI.DrawTexture(new Rect(art.x-12,art.y+24,art.width*.60f,art.height*.72f),crystal,ScaleMode.ScaleToFit,true);
            if(tier>=2)GUI.DrawTexture(new Rect(art.x+art.width*.52f,art.y+28,art.width*.57f,art.height*.68f),crystal,ScaleMode.ScaleToFit,true);
            if(tier>=3)
            {
                GUI.DrawTexture(new Rect(art.x-26,art.y+44,art.width*.45f,art.height*.50f),crystal,ScaleMode.ScaleToFit,true);
                GUI.DrawTexture(new Rect(art.x+art.width*.80f,art.y+46,art.width*.42f,art.height*.48f),crystal,ScaleMode.ScaleToFit,true);
            }
            GUI.DrawTexture(art,crystal,ScaleMode.ScaleToFit,true);
            // Small white glints remain inside the artwork; no per-frame textures.
            float cx=art.center.x+art.width*.22f,cy=art.y+22;
            Fill(new Rect(cx-5,cy,11,1.5f),new Color(1,1,1,.85f*pulse));
            Fill(new Rect(cx,cy-5,1.5f,11),new Color(1,1,1,.85f*pulse));
            GUI.color=previous;
        }

        private static void EnsureArtwork()
        {
            if(crystal==null)crystal=BuildCrystalTexture();
            if(glow==null)glow=BuildGlowTexture();
            if(packStyle==null)
            {
                packStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=12,
                    fontStyle=FontStyle.Bold,clipping=TextClipping.Clip};
                packStyle.normal.textColor=new Color(.64f,.86f,1f,1f);
            }
            if(amountStyle==null)
            {
                amountStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=20,
                    fontStyle=FontStyle.Bold,clipping=TextClipping.Clip};
                amountStyle.normal.textColor=Color.white;
            }
        }

        private static Texture2D BuildCrystalTexture()
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "DF_ShardCrystal",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color32[] p = new Color32[size * size];
            Vector2 a = new Vector2(64, 119);
            Vector2 b = new Vector2(104, 72);
            Vector2 c = new Vector2(79, 9);
            Vector2 d = new Vector2(32, 60);
            Vector2 m = new Vector2(65, 58);

            Triangle(p, size, a, b, m, new Color32(130, 250, 255, 255));
            Triangle(p, size, b, c, m, new Color32(32, 190, 255, 255));
            Triangle(p, size, c, d, m, new Color32(115, 95, 255, 255));
            Triangle(p, size, d, a, m, new Color32(222, 255, 255, 255));
            Line(p, size, a, b, new Color32(224, 255, 255, 255));
            Line(p, size, b, c, new Color32(184, 244, 255, 255));
            Line(p, size, c, d, new Color32(155, 172, 255, 255));
            Line(p, size, d, a, new Color32(219, 255, 255, 255));
            Line(p, size, a, m, new Color32(220, 255, 255, 190));
            Line(p, size, c, m, new Color32(206, 225, 255, 180));

            tex.SetPixels32(p);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildGlowTexture()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "DF_ShardGlow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color32[] p = new Color32[size * size];
            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / 31.5f;
                if (d >= 1f) continue;
                float a = Mathf.Pow(1f - d, 2.2f);
                p[y * size + x] = new Color32(255, 255, 255, (byte)(a * 225f));
            }
            tex.SetPixels32(p);
            tex.Apply(false, true);
            return tex;
        }

        private static void Triangle(Color32[] pixels, int size, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0, size - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))), 0, size - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0, size - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))), 0, size - 1);
            float area = Edge(a, b, c);
            if (Mathf.Abs(area) < .001f) return;

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 q = new Vector2(x + .5f, y + .5f);
                float w0 = Edge(b, c, q), w1 = Edge(c, a, q), w2 = Edge(a, b, q);
                bool inside = area > 0 ? w0 >= 0 && w1 >= 0 && w2 >= 0 : w0 <= 0 && w1 <= 0 && w2 <= 0;
                if (inside) pixels[y * size + x] = color;
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p)
        {
            return (p.x - a.x) * (b.y - a.y) - (p.y - a.y) * (b.x - a.x);
        }

        private static void Line(Color32[] pixels, int size, Vector2 a, Vector2 b, Color32 color)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
                if (x >= 0 && y >= 0 && x < size && y < size) pixels[y * size + x] = color;
            }
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

