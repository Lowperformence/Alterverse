using System;
using System.IO;
using System.Threading;
using Alterverse.Characters;
using UnityEngine;
namespace Alterverse.UI
{
    internal sealed partial class HeroOriginBackdrop
    {
        private const int Width=1024,Height=576;
        public static void Main(string[] args)
        {
            Directory.CreateDirectory(args[0]);
            foreach(HeroId hero in Enum.GetValues<HeroId>())
            {
                var pixels=BuildPixels(hero,CancellationToken.None);
                using var file=new BinaryWriter(File.Create(Path.Combine(args[0],hero+".rgba")));
                foreach(var p in pixels){file.Write(p.r);file.Write(p.g);file.Write(p.b);file.Write(p.a);}
                Console.WriteLine("Baked "+hero);
            }
        }
        private static Color32[] BuildPixels(HeroId hero,CancellationToken cancellation)
        {
            WorldStyle style = StyleFor(hero);
            var pixels = new Color32[Width * Height];

            // Broad coloured nebulae survive the downscale to a phone-sized hero card.
            // Keep the centre calmer through reduced detail rather than crushing it to black.
            for (int y = 0; y < Height; y++)
            {
                cancellation.ThrowIfCancellationRequested();
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
                    c = c * 1.65f + style.SecondaryHaze * .075f;
                    c += style.Haze * cloud * (.30f + n3 * .34f);
                    c += style.SecondaryHaze * filament * .34f;
                    c += style.Rim * Mathf.Clamp01(n4 - .66f) * cloud * .10f;
                    float edgeShade = Mathf.SmoothStep(0f,1f,Mathf.Min(Mathf.Min(u,1f-u),Mathf.Min(v,1f-v))*3.0f);
                    c *= .88f + edgeShade * .12f;

                    float heroClear = Mathf.Exp(-((u - .50f) * (u - .50f) * 13f + (v - .53f) * (v - .53f) * 8f));
                    c *= 1f - heroClear * .08f;
                    c.a = 1f;
                    pixels[y * Width + x] = Clamp(c);
                }
            }

            // Deterministic stars. System.Random keeps this cosmetic generator isolated from Unity gameplay RNG.
            var random = new System.Random(5107 + style.Seed * 97);
            int starCount = 420 + (style.Seed % 71);
            for (int i = 0; i < starCount; i++)
            {
                int x = random.Next(5, Width - 5);
                int y = random.Next(5, Height - 5);
                float brightness = .50f + (float)random.NextDouble() * .45f;
                Color star = Color.Lerp(new Color(.66f, .78f, 1f), style.Rim, .18f) * brightness;
                star.a = 1f;
                pixels[y * Width + x] = Clamp(star);
                if (i % 5 == 0)
                {
                    for(int oy=-4;oy<=4;oy++)
                    for(int ox=-4;ox<=4;ox++)
                    {
                        if(ox==0&&oy==0)continue;
                        float falloff=1f/(1f+ox*ox+oy*oy);
                        int at=(y+oy)*Width+x+ox;
                        Color halo=Color.Lerp(pixels[at],star,falloff*.70f);
                        halo.a=1f;
                        pixels[at]=Clamp(halo);
                    }
                }
            }

            // Hero-specific origin planet. It is intentionally offset so the character remains the focal point.
            float aspect = Width / (float)Height;
            for (int y = 0; y < Height; y++)
            {
                cancellation.ThrowIfCancellationRequested();
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
                            float ringBands = .72f + .28f * Mathf.Sin(ringDistance * 240f + grain * 3f);
                            c += style.Rim * ringStrength * ringBands * (.22f + grain * .40f);
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
                        float mix = Mathf.Clamp01(.10f + bands * style.Bands * .32f + plate * .65f);
                        Color surface = Color.Lerp(style.PlanetB, style.PlanetA, mix);
                        surface *= (.48f + light * .78f) * (1f + (ridges - .5f) * .40f + (detail - .5f) * .15f);
                        float fissure = Mathf.Pow(Mathf.Clamp01(1f-Mathf.Abs(ridges-.53f)*11f),3f)
                            * Mathf.Clamp01(terrain-.49f);
                        surface += style.Haze * fissure * style.Storm * .52f;
                        surface += style.Rim * Mathf.Clamp01(detail-.70f) * .09f;
                        float stormVeil = Mathf.Clamp01((terrain - ridges * .38f - .33f) * 3f) * style.Storm;
                        surface = Color.Lerp(surface, style.Rim, stormVeil * .12f);
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
                        c += style.Rim * atmosphere * atmosphere * .11f;
                    }

                    // Keep the central character area a little calmer even when a large planet crosses it.
                    float heroClear = Mathf.Exp(-((u - .50f) * (u - .50f) * 16f + (v - .52f) * (v - .52f) * 10f));
                    c *= 1f - heroClear * .05f;
                    c.a = 1f;
                    Color32 display = Clamp(c);
                    display.r = DisplayCurve[display.r];
                    display.g = DisplayCurve[display.g];
                    display.b = DisplayCurve[display.b];
                    pixels[p] = display;
                }
            }

            return pixels;
        }

    }
}
