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

        public static void DrawPackCard(float x,float width,ShardPack pack,int tier)
        {
            if(Event.current.type!=EventType.Repaint)return;
            EnsureArtwork();
            tier=Mathf.Clamp(tier,0,3);

            Fill(new Rect(x+2f,370f,width-4f,136f),new Color(.008f,.016f,.055f,.99f));
            Fill(new Rect(x+2f,370f,width-4f,3f),new Color(.10f,.76f,1f,.95f));
            Fill(new Rect(x+2f,503f,width-4f,2f),new Color(.32f,.28f,1f,.72f));
            GUI.Label(new Rect(x+12f,376f,width-24f,21f),pack.Name,packStyle);

            float artScale=.76f+tier*.10f;
            float artW=Mathf.Min(width-24f,126f*artScale);
            float artH=92f*artScale;
            Rect art=new Rect(x+(width-artW)*.5f,393f-tier*2f,artW,artH);
            Rect halo=new Rect(art.center.x-art.width*.72f,art.center.y-art.height*.68f,
                art.width*1.44f,art.height*1.36f);
            Color previous=GUI.color;
            GUI.color=tier>=3?new Color(.62f,.50f,1f,.88f):new Color(.20f,.74f,1f,.82f);
            GUI.DrawTexture(halo,glow,ScaleMode.StretchToFill,true);
            GUI.color=Color.white;
            if(tier>=1)GUI.DrawTexture(new Rect(art.x-22f,art.y+18f,art.width*.62f,art.height*.72f),crystal,ScaleMode.ScaleToFit,true);
            if(tier>=2)GUI.DrawTexture(new Rect(art.xMax-art.width*.47f,art.y+22f,art.width*.58f,art.height*.68f),crystal,ScaleMode.ScaleToFit,true);
            if(tier>=3)
            {
                GUI.DrawTexture(new Rect(art.x-34f,art.y+36f,art.width*.50f,art.height*.56f),crystal,ScaleMode.ScaleToFit,true);
                GUI.DrawTexture(new Rect(art.xMax-18f,art.y+38f,art.width*.46f,art.height*.52f),crystal,ScaleMode.ScaleToFit,true);
            }
            GUI.DrawTexture(art,crystal,ScaleMode.ScaleToFit,true);
            GUI.color=previous;
            GUI.Label(new Rect(x+10f,474f,width-20f,29f),pack.Amount.ToString("N0")+" SHARDS",amountStyle);
        }

        public static void DrawPurchaseIcon(float x,float width,int tier)
        {
            if(Event.current.type!=EventType.Repaint)return;
            EnsureArtwork();
            Color previous=GUI.color;
            float iconX=x+28f,iconY=519f;
            GUI.color=tier>=2?new Color(.58f,.43f,1f,.70f):new Color(.20f,.76f,1f,.68f);
            GUI.DrawTexture(new Rect(iconX-9f,iconY-9f,52f,52f),glow,ScaleMode.StretchToFill,true);
            GUI.color=Color.white;
            GUI.DrawTexture(new Rect(iconX,iconY,34f,34f),crystal,ScaleMode.ScaleToFit,true);
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

            Triangle(p, size, a, b, m, new Color32(92, 235, 255, 255));
            Triangle(p, size, b, c, m, new Color32(34, 151, 255, 255));
            Triangle(p, size, c, d, m, new Color32(91, 82, 236, 255));
            Triangle(p, size, d, a, m, new Color32(171, 247, 255, 255));
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
                p[y * size + x] = new Color32(90, 90, 255, (byte)(a * 150f));
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
