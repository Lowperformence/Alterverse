using System;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Cached, procedural origin-world art for the frontend hero showcase.
    /// Each hero gets a distinct planet/sky treatment without loading scene content or touching gameplay RNG.
    /// </summary>
    internal sealed class HeroOriginBackdrop : IDisposable
    {
        private const int Width = 2048;
        private const int Height = 1152;
        private const float PixelsPerUnit = 144f;

        private readonly Texture2D[] textures = new Texture2D[HeroIdentity.HeroCount];
        private readonly Sprite[] sprites = new Sprite[HeroIdentity.HeroCount];

        public Sprite SpriteFor(HeroId hero)
        {
            int index = Mathf.Clamp((int)hero, 0, HeroIdentity.HeroCount - 1);
            if (sprites[index] != null) return sprites[index];

            Texture2D texture = Build(hero);
            textures[index] = texture;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(.5f, .5f),
                PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "DF_OriginWorld_" + hero;
            sprites[index] = sprite;
            return sprite;
        }

        private static Texture2D Build(HeroId hero)
        {
            WorldStyle style = StyleFor(hero);
            var pixels = new Color32[Width * Height];

            // Background nebula. Kept deliberately dark in the centre so the hero silhouette stays readable.
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)(Width - 1);
                float v = y / (float)(Height - 1);
                float n1 = Mathf.PerlinNoise(u * 3.2f + style.Seed * .013f, v * 3.8f + style.Seed * .019f);
                float n2 = Mathf.PerlinNoise(u * 7.7f + 17f + style.Seed * .007f, v * 6.1f + 29f);
                float n3 = Mathf.PerlinNoise(u * 15.3f + style.Seed * .17f,
                    v * 13.7f + style.Seed * .11f);
                float n4 = Mathf.PerlinNoise(u * 38f + 53f, v * 34f + style.Seed * .29f);
                float cloud = Mathf.Clamp01((n1 * .62f + n2 * .28f + n3 * .10f - .43f) * 3.2f);
                float filament = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(n2 - .55f) * 9f),3f)
                    * Mathf.Clamp01(n3 * 1.9f - .65f);
                Color c = Color.Lerp(style.Bottom, style.Top, Mathf.SmoothStep(0, 1, v));
                c += style.Haze * cloud * (.16f + n3 * .20f);
                c += style.SecondaryHaze * filament * .20f;
                c += style.Rim * Mathf.Clamp01(n4 - .66f) * cloud * .045f;
                float edgeShade = Mathf.SmoothStep(0f,1f,Mathf.Min(Mathf.Min(u,1f-u),Mathf.Min(v,1f-v))*3.0f);
                c *= .73f + edgeShade * .27f;

                float heroClear = Mathf.Exp(-((u - .50f) * (u - .50f) * 13f + (v - .53f) * (v - .53f) * 8f));
                c *= 1f - heroClear * .22f;
                c.a = 1f;
                pixels[y * Width + x] = Clamp(c);
            }

            // Deterministic stars. System.Random keeps this cosmetic generator isolated from Unity gameplay RNG.
            var random = new System.Random(5107 + style.Seed * 97);
            int starCount = 300 + (style.Seed % 71);
            for (int i = 0; i < starCount; i++)
            {
                int x = random.Next(3, Width - 3);
                int y = random.Next(4, Height - 4);
                float brightness = .34f + (float)random.NextDouble() * .50f;
                Color star = Color.Lerp(new Color(.66f, .78f, 1f), style.Rim, .18f) * brightness;
                star.a = 1f;
                pixels[y * Width + x] = Clamp(star);
                if (i % 9 == 0)
                {
                    for(int oy=-2;oy<=2;oy++)
                    for(int ox=-2;ox<=2;ox++)
                    {
                        if(ox==0&&oy==0)continue;
                        float falloff=1f/(1f+ox*ox+oy*oy);
                        int at=(y+oy)*Width+x+ox;
                        Color halo=Color.Lerp(pixels[at],star,falloff*.45f);
                        halo.a=1f;
                        pixels[at]=Clamp(halo);
                    }
                }
            }

            // Hero-specific origin planet. It is intentionally offset so the character remains the focal point.
            float aspect = Width / (float)Height;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)(Width - 1);
                float v = y / (float)(Height - 1);
                float dx = (u - style.PlanetCenter.x) * aspect;
                float dy = v - style.PlanetCenter.y;
                float radius = style.Radius;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                int p = y * Width + x;
                Color c = pixels[p];

                if (style.Ring)
                {
                    float rx = dx / (radius * 1.72f);
                    float ry = (dy + dx * .18f) / (radius * .28f);
                    float ringDistance = Mathf.Abs(Mathf.Sqrt(rx * rx + ry * ry) - 1f);
                    if (ringDistance < .11f && r > .98f)
                    {
                        float ringStrength = Mathf.Pow(1f - ringDistance / .11f,1.5f);
                        float grain = Mathf.PerlinNoise(u * 93f + style.Seed, v * 81f);
                        c += style.Rim * ringStrength * (.11f + grain * .25f);
                    }
                }

                if (r < 1f)
                {
                    float z = Mathf.Sqrt(Mathf.Max(0, 1f - r * r));
                    float light = Mathf.Clamp01(.20f - dx / radius * .24f + dy / radius * .10f + z * .88f);
                    float bands = .5f + .5f * Mathf.Sin((v * (17f + style.Bands * 17f) +
                        Mathf.PerlinNoise(u * 8f + style.Seed, v * 9f) * 1.4f) * Mathf.PI * 2f);
                    float terrain = Mathf.PerlinNoise(u * 31f + style.Seed * .21f, v * 27f + style.Seed * .11f);
                    float ridges = Mathf.PerlinNoise(u * 64f + style.Seed * .37f, v * 61f + 8f);
                    float detail = Mathf.PerlinNoise(u * 127f + 13f, v * 119f + style.Seed * .43f);
                    float plate = Mathf.Clamp01((terrain * .57f + ridges * .30f + detail * .13f - .36f) * 2.3f);
                    float mix = Mathf.Clamp01(.12f + bands * style.Bands * .34f + plate * .58f);
                    Color surface = Color.Lerp(style.PlanetB, style.PlanetA, mix);
                    surface *= .30f + light * .81f;
                    float fissure = Mathf.Pow(Mathf.Clamp01(1f-Mathf.Abs(ridges-.53f)*11f),3f)
                        * Mathf.Clamp01(terrain-.49f);
                    surface += style.Haze * fissure * style.Storm * .26f;
                    surface += style.Rim * Mathf.Clamp01(detail-.70f) * .035f;
                    surface.a = 1f;
                    c = surface;
                }

                if (r > .91f && r < 1.075f)
                {
                    float rim = Mathf.Exp(-Mathf.Abs(r - 1f) * 52f);
                    c += style.Rim * rim * .50f;
                }

                // A soft atmospheric bloom keeps the planet present without competing with UI text.
                if (r >= 1f && r < 1.22f)
                {
                    float atmosphere = 1f - (r - 1f) / .22f;
                    c += style.Rim * atmosphere * atmosphere * .055f;
                }

                // Keep the central character area a little calmer even when a large planet crosses it.
                float heroClear = Mathf.Exp(-((u - .50f) * (u - .50f) * 16f + (v - .52f) * (v - .52f) * 10f));
                c *= 1f - heroClear * .12f;
                c.a = 1f;
                pixels[p] = Clamp(c);
            }

            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "DF_OriginWorldTexture_" + hero,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static WorldStyle StyleFor(HeroId hero)
        {
            switch (hero)
            {
                case HeroId.Veyr: // Zhaev Null — void / fractured purple world
                    return new WorldStyle(
                        new Color(.030f, .014f, .063f), new Color(.006f, .008f, .018f),
                        new Color(.52f, .08f, .66f), new Color(.16f, .03f, .24f),
                        new Color(.105f, .045f, .16f), new Color(.030f, .018f, .055f),
                        new Color(.98f, .20f, 1f), new Vector2(.80f, .61f), .34f, 11, false, .15f, .72f);
                case HeroId.Kaelon: // Auren Kest — ancient amber / iron world
                    return new WorldStyle(
                        new Color(.055f, .035f, .018f), new Color(.008f, .012f, .020f),
                        new Color(.70f, .32f, .07f), new Color(.28f, .11f, .025f),
                        new Color(.40f, .20f, .065f), new Color(.13f, .055f, .025f),
                        new Color(1f, .68f, .22f), new Vector2(.77f, .59f), .38f, 23, true, .72f, .18f);
                case HeroId.Nyra: // Sivra Quell — cold cyan ocean / ring world
                    return new WorldStyle(
                        new Color(.012f, .055f, .090f), new Color(.004f, .012f, .025f),
                        new Color(.04f, .50f, .72f), new Color(.04f, .16f, .30f),
                        new Color(.10f, .50f, .63f), new Color(.025f, .14f, .24f),
                        new Color(.30f, .94f, 1f), new Vector2(.79f, .62f), .36f, 37, true, .40f, .28f);
                case HeroId.Elyra: // Ione Vaska — arcane violet storm world
                    return new WorldStyle(
                        new Color(.060f, .018f, .092f), new Color(.008f, .007f, .021f),
                        new Color(.56f, .12f, .78f), new Color(.21f, .055f, .34f),
                        new Color(.34f, .075f, .48f), new Color(.095f, .025f, .18f),
                        new Color(.83f, .33f, 1f), new Vector2(.78f, .58f), .35f, 49, false, .32f, .95f);
                case HeroId.Mira: // Tahla Venn — jade world under a warm ascendant sky
                    return new WorldStyle(
                        new Color(.008f, .066f, .070f), new Color(.003f, .014f, .023f),
                        new Color(.55f, .38f, .13f), new Color(.18f, .12f, .10f),
                        new Color(.085f, .43f, .34f), new Color(.018f, .13f, .14f),
                        new Color(1f, .78f, .35f), new Vector2(.80f, .60f), .36f, 61, true, .23f, .42f);
                case HeroId.Rook: // Brakk Orun — volcanic ember world
                    return new WorldStyle(
                        new Color(.075f, .020f, .014f), new Color(.012f, .008f, .012f),
                        new Color(.78f, .16f, .025f), new Color(.30f, .055f, .018f),
                        new Color(.33f, .075f, .025f), new Color(.095f, .025f, .018f),
                        new Color(1f, .38f, .08f), new Vector2(.78f, .61f), .39f, 73, false, .12f, 1f);
                case HeroId.Nerok: // Pelagos-9 — deep ocean and cyan bioluminescence
                    return new WorldStyle(
                        new Color(.008f,.030f,.066f), new Color(.003f,.008f,.017f),
                        new Color(.02f,.28f,.55f),new Color(.015f,.09f,.22f),
                        new Color(.02f,.31f,.39f),new Color(.01f,.065f,.12f),
                        new Color(.13f,.85f,1f),new Vector2(.78f,.60f),.37f,101,false,.5f,.32f);
                default: // Traveler — violet temporal eclipse
                    return new WorldStyle(
                        new Color(.025f, .040f, .071f), new Color(.007f, .008f, .019f),
                        new Color(.45f, .25f, .82f), new Color(.60f, .18f, .72f),
                        new Color(.12f, .15f, .20f), new Color(.025f, .025f, .045f),
                        new Color(.83f, .52f, 1f), new Vector2(.78f, .58f), .35f, 89, true, .08f, .72f);
            }
        }

        private static Color32 Clamp(Color c)
        {
            return new Color(
                Mathf.Clamp01(c.r),
                Mathf.Clamp01(c.g),
                Mathf.Clamp01(c.b),
                Mathf.Clamp01(c.a));
        }

        public void Dispose()
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null) UnityEngine.Object.Destroy(sprites[i]);
                if (textures[i] != null) UnityEngine.Object.Destroy(textures[i]);
                sprites[i] = null;
                textures[i] = null;
            }
        }

        private readonly struct WorldStyle
        {
            public readonly Color Top, Bottom, Haze, SecondaryHaze, PlanetA, PlanetB, Rim;
            public readonly Vector2 PlanetCenter;
            public readonly float Radius, Bands, Storm;
            public readonly int Seed;
            public readonly bool Ring;

            public WorldStyle(Color top, Color bottom, Color haze, Color secondaryHaze,
                Color planetA, Color planetB, Color rim, Vector2 planetCenter, float radius,
                int seed, bool ring, float bands, float storm)
            {
                Top = top;
                Bottom = bottom;
                Haze = haze;
                SecondaryHaze = secondaryHaze;
                PlanetA = planetA;
                PlanetB = planetB;
                Rim = rim;
                PlanetCenter = planetCenter;
                Radius = radius;
                Seed = seed;
                Ring = ring;
                Bands = bands;
                Storm = storm;
            }
        }
    }
}
