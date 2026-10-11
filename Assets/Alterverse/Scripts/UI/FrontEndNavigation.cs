using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Small cached line symbols matching the interstellar navigation.</summary>
    internal static class FrontEndNavigation
    {
        private static readonly Texture2D[] icons = new Texture2D[10];

        public static void DrawIcon(Rect rect, int index, Color color)
        {
            index = Mathf.Clamp(index, 0, icons.Length - 1);
            if (icons[index] == null) icons[index] = Build(index);
            Color before = GUI.color;
            GUI.color *= color;
            FrontEndTheme.DrawTexture(rect, icons[index], ScaleMode.ScaleToFit, true);
            GUI.color = before;
        }

        private static Texture2D Build(int kind)
        {
            const int size = 48;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + .5f) / size, 1f - (y + .5f) / size);
                float distance = Distance(kind, p);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01((.037f - distance) * size) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DF_Navigation_" + kind, hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float Line(Vector2 p, float ax, float ay, float bx, float by)
        {
            Vector2 a = new Vector2(ax, ay), b = new Vector2(bx, by), d = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude);
            return (p - a - d * t).magnitude;
        }
        private static float Circle(Vector2 p, float x, float y, float radius) =>
            Mathf.Abs((p - new Vector2(x, y)).magnitude - radius);
        private static float Min(params float[] distances)
        { float nearest = 1; foreach(float value in distances) nearest = Mathf.Min(nearest, value); return nearest; }

        private static float Distance(int kind, Vector2 p)
        {
            switch(kind)
            {
                case 0: return Min(Line(p,.14f,.43f,.5f,.14f),Line(p,.5f,.14f,.86f,.43f),
                    Line(p,.24f,.40f,.24f,.83f),Line(p,.24f,.83f,.76f,.83f),Line(p,.76f,.83f,.76f,.4f),
                    Line(p,.43f,.83f,.43f,.60f),Line(p,.43f,.6f,.57f,.6f),Line(p,.57f,.6f,.57f,.83f));
                case 1: return Min(Line(p,.28f,.16f,.80f,.50f),Line(p,.80f,.5f,.28f,.84f),Line(p,.28f,.84f,.28f,.16f));
                case 2: return Min(Circle(p,.50f,.27f,.12f),Circle(p,.19f,.36f,.085f),Circle(p,.81f,.36f,.085f),
                    Line(p,.26f,.80f,.30f,.55f),Line(p,.30f,.55f,.70f,.55f),Line(p,.70f,.55f,.74f,.80f),
                    Line(p,.74f,.80f,.26f,.80f),Line(p,.08f,.60f,.08f,.81f),Line(p,.08f,.81f,.18f,.81f),
                    Line(p,.92f,.60f,.92f,.81f),Line(p,.92f,.81f,.82f,.81f));
                case 3: return Min(Line(p,.15f,.28f,.85f,.28f),Line(p,.85f,.28f,.79f,.83f),Line(p,.79f,.83f,.21f,.83f),
                    Line(p,.21f,.83f,.15f,.28f),Line(p,.35f,.28f,.35f,.14f),Line(p,.35f,.14f,.65f,.14f),Line(p,.65f,.14f,.65f,.28f));
                case 4: return Min(Line(p,.50f,.12f,.84f,.31f),Line(p,.84f,.31f,.84f,.70f),Line(p,.84f,.70f,.50f,.9f),
                    Line(p,.50f,.9f,.16f,.7f),Line(p,.16f,.7f,.16f,.31f),Line(p,.16f,.31f,.50f,.12f),
                    Line(p,.16f,.31f,.50f,.5f),Line(p,.84f,.31f,.5f,.5f),Line(p,.5f,.5f,.5f,.9f));
                case 5: return Min(Circle(p,.50f,.28f,.15f),Line(p,.20f,.84f,.25f,.58f),Line(p,.25f,.58f,.75f,.58f),
                    Line(p,.75f,.58f,.80f,.84f),Line(p,.80f,.84f,.20f,.84f));
                case 6: return Min(Line(p,.2f,.15f,.80f,.15f),Line(p,.8f,.15f,.8f,.85f),Line(p,.8f,.85f,.2f,.85f),
                    Line(p,.2f,.85f,.2f,.15f),Line(p,.30f,.52f,.45f,.66f),Line(p,.45f,.66f,.70f,.36f));
                case 7: return Min(Line(p,.2f,.83f,.2f,.56f),Line(p,.5f,.83f,.5f,.18f),Line(p,.8f,.83f,.8f,.40f),
                    Line(p,.10f,.88f,.90f,.88f));
                case 9: return Min(Line(p,.18f,.75f,.12f,.30f),Line(p,.12f,.30f,.34f,.48f),Line(p,.34f,.48f,.50f,.16f),
                    Line(p,.5f,.16f,.66f,.48f),Line(p,.66f,.48f,.88f,.30f),Line(p,.88f,.30f,.82f,.75f),
                    Line(p,.82f,.75f,.18f,.75f),Line(p,.20f,.86f,.80f,.86f));
                default:
                    float d = Min(Circle(p,.50f,.50f,.28f),Circle(p,.50f,.50f,.10f));
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI*.25f;
                        d=Mathf.Min(d,Line(p,.5f+Mathf.Cos(a)*.29f,.5f+Mathf.Sin(a)*.29f,
                            .5f+Mathf.Cos(a)*.40f,.5f+Mathf.Sin(a)*.40f));
                    }
                    return d;
            }
        }
    }
}
