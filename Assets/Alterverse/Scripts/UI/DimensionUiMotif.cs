using UnityEngine;

namespace Alterverse.UI
{
    public static class DimensionUiMotif
    {
        private static Texture2D orbitalRingTex;
        private static Texture2D softGlowTex;
        private static Texture2D loadingPlanetTex;

        // High-resolution procedural FUI (Futuristic User Interface) textures
        private static Texture2D fuiRadarScopeTex;
        private static Texture2D fuiRadarSweepTex;
        private static Texture2D fuiTopoContourTex;
        private static Texture2D fuiTargetReticleTex;
        private static Texture2D fuiWaveTex;
        private static Texture2D fuiCircleGaugeTex;

        public static readonly Color FuiObsidian = new Color(.016f, .031f, .058f, .99f);
        public static readonly Color FuiIceBlue = new Color(.48f, .76f, .95f, 1f);
        public static readonly Color FuiSlateBlue = new Color(.23f, .41f, .56f, 1f);
        public static readonly Color FuiThermalOrange = new Color(.89f, .36f, .22f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMotifTextures()
        {
            if (orbitalRingTex != null) Object.Destroy(orbitalRingTex);
            if (softGlowTex != null) Object.Destroy(softGlowTex);
            if (loadingPlanetTex != null) Object.Destroy(loadingPlanetTex);
            if (fuiRadarScopeTex != null) Object.Destroy(fuiRadarScopeTex);
            if (fuiRadarSweepTex != null) Object.Destroy(fuiRadarSweepTex);
            if (fuiTopoContourTex != null) Object.Destroy(fuiTopoContourTex);
            if (fuiTargetReticleTex != null) Object.Destroy(fuiTargetReticleTex);
            if (fuiWaveTex != null) Object.Destroy(fuiWaveTex);
            if (fuiCircleGaugeTex != null) Object.Destroy(fuiCircleGaugeTex);

            orbitalRingTex = null;
            softGlowTex = null;
            loadingPlanetTex = null;
            fuiRadarScopeTex = null;
            fuiRadarSweepTex = null;
            fuiTopoContourTex = null;
            fuiTargetReticleTex = null;
            fuiWaveTex = null;
            fuiCircleGaugeTex = null;
        }

        private static void EnsureMotifTextures()
        {
            if (softGlowTex == null)
            {
                const int size = 128;
                softGlowTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "DF_MotifSoftGlow",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                float half = (size - 1) * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= 1f) continue;
                    float d = Mathf.Sqrt(d2);
                    float falloff = Mathf.Pow(1f - d, 2.4f);
                    float core = Mathf.Exp(-d2 * 9.5f) * .45f;
                    float a = Mathf.Clamp01(falloff + core);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
                softGlowTex.SetPixels32(pixels);
                softGlowTex.Apply(false, true);
            }

            if (orbitalRingTex == null)
            {
                const int size = 256;
                orbitalRingTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "DF_MotifOrbitalRing",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                float half = (size - 1) * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= .98f) continue;

                    float angle = Mathf.Atan2(dy, dx);
                    if (angle < 0f) angle += Mathf.PI * 2f;

                    float alpha = 0f;

                    // 1. Outer precision ring (r = 0.88) with cardinal arc highlights
                    float outerDist = Mathf.Abs(r - .88f);
                    float outerRing = Mathf.Clamp01(1f - outerDist / .013f);
                    float cardinalArc = Mathf.Abs(Mathf.Cos(angle * 2f)) > .35f ? 1f : .28f;
                    alpha += outerRing * outerRing * cardinalArc * .78f;

                    // 2. Outer degree ticks (72 ticks between r = 0.81 and r = 0.855)
                    if (r >= .81f && r <= .855f)
                    {
                        float tickWave = Mathf.Cos(angle * 72f);
                        if (tickWave > .82f)
                        {
                            float tickStrength = (tickWave - .82f) / .18f;
                            float radialFade = 1f - Mathf.Abs(r - .832f) / .023f;
                            alpha += tickStrength * Mathf.Clamp01(radialFade) * .48f;
                        }
                    }

                    // 3. Mid segmented astrolabe ring (r = 0.68)
                    float midDist = Mathf.Abs(r - .68f);
                    float midRing = Mathf.Clamp01(1f - midDist / .011f);
                    float midGap = Mathf.Sin(angle * 6f) > -.45f ? 1f : .12f;
                    alpha += midRing * midRing * midGap * .65f;

                    // 4. 6 glowing orbital nodes on the mid ring
                    for (int n = 0; n < 6; n++)
                    {
                        float na = n * (Mathf.PI / 3f);
                        float nx = Mathf.Cos(na) * .68f;
                        float ny = Mathf.Sin(na) * .68f;
                        float nd = Mathf.Sqrt((dx - nx) * (dx - nx) + (dy - ny) * (dy - ny));
                        if (nd < .042f)
                        {
                            float nodeGlow = Mathf.Pow(1f - nd / .042f, 2f);
                            alpha += nodeGlow * .85f;
                        }
                    }

                    // 5. Inner delicate resonance ring (r = 0.46)
                    float innerDist = Mathf.Abs(r - .46f);
                    float innerRing = Mathf.Clamp01(1f - innerDist / .009f);
                    float innerMask = Mathf.Cos(angle * 4f) > -.2f ? .52f : .15f;
                    alpha += innerRing * innerMask;

                    // 6. Subtle radial halo falloff near the outer ring
                    float halo = Mathf.Exp(-Mathf.Pow((r - .78f) / .14f, 2f)) * .10f;
                    alpha = Mathf.Clamp01(alpha + halo);

                    if (alpha > .004f)
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
                orbitalRingTex.SetPixels32(pixels);
                orbitalRingTex.Apply(false, true);
            }
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float wx = px - ax, wy = py - ay;
            float c1 = vx * wx + vy * wy;
            if (c1 <= 0f) return Mathf.Sqrt(wx * wx + wy * wy);
            float c2 = vx * vx + vy * vy;
            if (c2 <= c1) return Mathf.Sqrt((px - bx) * (px - bx) + (py - by) * (py - by));
            float b = c1 / c2;
            float pbx = ax + b * vx, pby = ay + b * vy;
            return Mathf.Sqrt((px - pbx) * (px - pbx) + (py - pby) * (py - pby));
        }

        private static void EnsureFuiTextures()
        {
            EnsureMotifTextures();

            // 1. Top-Left FUI Radar Scope with fine grid, range rings, and smooth anti-aliased top-down spacecraft vector
            if (fuiRadarScopeTex == null)
            {
                const int size = 192;
                fuiRadarScopeTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "DF_FuiRadarScope",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                float half = (size - 1) * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half; // +dy is up in texture space
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;

                    // Fine technical background grid (10 divisions)
                    float gx = Mathf.Abs(((dx + 1f) * 5f) - Mathf.Round((dx + 1f) * 5f));
                    float gy = Mathf.Abs(((dy + 1f) * 5f) - Mathf.Round((dy + 1f) * 5f));
                    if (gx < .035f || gy < .035f) a = Mathf.Max(a, .12f);

                    // Primary cardinal crosshairs
                    if (Mathf.Abs(dx) < .011f || Mathf.Abs(dy) < .011f) a = Mathf.Max(a, .32f);

                    // 3 Concentric radar range rings (r = 0.34, 0.62, 0.90)
                    float r1 = Mathf.Clamp01(1f - Mathf.Abs(r - .34f) / .015f) * .45f;
                    float r2 = Mathf.Clamp01(1f - Mathf.Abs(r - .62f) / .015f) * .55f;
                    float r3 = Mathf.Clamp01(1f - Mathf.Abs(r - .90f) / .016f) * .85f;
                    a = Mathf.Max(a, Mathf.Max(r1, Mathf.Max(r2, r3)));

                    // Degree ticks along outer ring (r in 0.84..0.90)
                    if (r >= .83f && r <= .90f)
                    {
                        float ang = Mathf.Atan2(dy, dx);
                        float tick = Mathf.Cos(ang * 36f);
                        if (tick > .84f) a = Mathf.Max(a, ((tick - .84f) / .16f) * .65f);
                    }

                    // Diagonal 45-degree subtle radial spokes
                    if (r >= .22f && r <= .90f)
                    {
                        float dDiag = Mathf.Min(Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)), 1f);
                        if (dDiag < .012f) a = Mathf.Max(a, (1f - dDiag / .012f) * .22f);
                    }

                    // Smooth Anti-Aliased Top-Down Futuristic Dropship / Delta-Jet Silhouette in Center
                    float ax = Mathf.Abs(dx);
                    float jy = dy; // Nose points up (+jy), tail at -jy
                    if (ax < .24f && jy > -.24f && jy < .26f)
                    {
                        // Wireframe / filled vector SDF of a sleek military sci-fi delta dropship
                        float dNose = DistanceToSegment(ax, jy, 0f, .22f, .042f, .05f) - .014f;
                        float dWingLead = DistanceToSegment(ax, jy, .042f, .05f, .195f, -.085f) - .012f;
                        float dWingTip = DistanceToSegment(ax, jy, .195f, -.085f, .175f, -.125f) - .011f;
                        float dWingTrail = DistanceToSegment(ax, jy, .175f, -.125f, .052f, -.095f) - .012f;
                        float dTailFin = DistanceToSegment(ax, jy, .045f, -.08f, .088f, -.185f) - .011f;
                        float dSpine = DistanceToSegment(ax, jy, 0f, .20f, 0f, -.165f) - .018f;

                        // Interior hull fill test (sleek delta polygon)
                        bool insideFuselage = jy <= .20f && jy >= -.16f && ax <= (.048f * (1f - Mathf.Max(0f, jy) * 3.8f));
                        bool insideWing = jy <= .05f && jy >= -.11f && ax <= (.042f + (.05f - jy) * 1.12f) && jy >= (-.125f + ax * .15f);

                        float minEdge = Mathf.Min(dNose, Mathf.Min(dWingLead, Mathf.Min(dWingTip, Mathf.Min(dWingTrail, Mathf.Min(dTailFin, dSpine)))));
                        float edgeAlpha = Mathf.Clamp01(1f - minEdge / .014f);
                        float fillAlpha = (insideFuselage || insideWing) ? .55f : 0f;
                        a = Mathf.Max(a, Mathf.Max(edgeAlpha * .98f, fillAlpha));
                    }

                    if (a > .004f)
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
                fuiRadarScopeTex.SetPixels32(pixels);
                fuiRadarScopeTex.Apply(false, true);
            }

            // 2. Rotating Radar Sweep Cone Texture
            if (fuiRadarSweepTex == null)
            {
                const int size = 192;
                fuiRadarSweepTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "DF_FuiRadarSweep",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                float half = (size - 1) * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= .89f || r < .04f) continue;

                    float ang = Mathf.Atan2(dy, dx);
                    if (ang < 0f) ang += Mathf.PI * 2f;

                    float a = 0f;
                    // Sweep sector between 0 and 1.05 radians (~60 deg)
                    if (ang <= 1.05f)
                    {
                        float trail = ang / 1.05f;
                        a = trail * trail * .36f * (1f - Mathf.Max(0f, r - .78f) / .12f);
                    }
                    // Leading laser line at ang ~ 1.05
                    float lineDist = Mathf.Abs(ang - 1.05f) * r;
                    if (lineDist < .018f)
                        a = Mathf.Max(a, (1f - lineDist / .018f) * .88f);

                    if (a > .004f)
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
                fuiRadarSweepTex.SetPixels32(pixels);
                fuiRadarSweepTex.Apply(false, true);
            }

            // 3. Topographic Red-Orange Terrain Contour Scan Texture (inspired by center of reference image)
            if (fuiTopoContourTex == null)
            {
                const int w = 256, h = 160;
                fuiTopoContourTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "DF_FuiTopoContour",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float ny = (y / (float)(h - 1)) * 2f - 1f;

                    // Perspective projection warp so contour lines look like a 3D tactical terrain table
                    float persp = 1.15f - ny * .38f;
                    float tx = nx * persp * 2.4f;
                    float ty = (ny + .15f) * 2.1f;

                    // Multi-peak mountain terrain heightfield
                    float p1 = Mathf.Exp(-(tx * tx * .55f + (ty - .15f) * (ty - .15f) * .85f)) * 1.15f;
                    float p2 = Mathf.Exp(-((tx + .95f) * (tx + .95f) * .9f + (ty + .25f) * (ty + .25f) * 1.1f)) * .72f;
                    float p3 = Mathf.Exp(-((tx - 1.05f) * (tx - 1.05f) * .85f + (ty + .18f) * (ty + .18f) * .95f)) * .68f;
                    float ridge = .14f * Mathf.Sin(tx * 2.7f + Mathf.Cos(ty * 2.4f)) + .08f * Mathf.Cos(tx * 5.1f - ty * 4.3f);
                    float elev = p1 + p2 + p3 + ridge;

                    // Topographic contour isolines
                    float phase = elev * 15.5f;
                    float frac = Mathf.Abs(phase - Mathf.Round(phase));
                    float line = Mathf.Clamp01(1f - frac / .13f);
                    float majorLine = Mathf.Abs((elev * 3.875f) - Mathf.Round(elev * 3.875f)) < .04f ? 1.35f : .82f;

                    // Vignette mask so terrain sits cleanly inside the holographic table
                    float vig = Mathf.Clamp01(1.12f - (nx * nx * .78f + ny * ny * .92f));
                    float a = (line * line * .74f * majorLine + elev * .18f) * vig;

                    if (a > .005f)
                        pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
                fuiTopoContourTex.SetPixels32(pixels);
                fuiTopoContourTex.Apply(false, true);
            }

            // 4. Concentric Multi-Ring FUI Targeting Reticle with 4-corner lock brackets & degree ticks
            if (fuiTargetReticleTex == null)
            {
                const int size = 256;
                fuiTargetReticleTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "DF_FuiTargetReticle",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                float half = (size - 1) * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= .98f) continue;

                    float ang = Mathf.Atan2(dy, dx);
                    if (ang < 0f) ang += Mathf.PI * 2f;

                    float a = 0f;

                    // Outer segmented lock ring (r = 0.86)
                    float dOuter = Mathf.Abs(r - .86f);
                    if (dOuter < .016f && Mathf.Cos(ang * 4f) > -.35f)
                        a = Mathf.Max(a, (1f - dOuter / .016f) * .85f);

                    // Primary crisp continuous ring (r = 0.74)
                    float dMain = Mathf.Abs(r - .74f);
                    if (dMain < .014f)
                        a = Mathf.Max(a, (1f - dMain / .014f) * .92f);

                    // Fine degree tick belt (r in 0.65..0.72)
                    if (r >= .65f && r <= .72f)
                    {
                        float t72 = Mathf.Cos(ang * 72f);
                        if (t72 > .80f)
                            a = Mathf.Max(a, ((t72 - .80f) / .20f) * .62f);
                    }

                    // Inner technical ring (r = 0.56)
                    float dInner = Mathf.Abs(r - .56f);
                    if (dInner < .011f && Mathf.Sin(ang * 2f) > -.6f)
                        a = Mathf.Max(a, (1f - dInner / .011f) * .55f);

                    // Inner 4-corner square target lock brackets [ + ]
                    float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                    if (ax >= .14f && ax <= .30f && ay >= .14f && ay <= .30f)
                    {
                        bool hArm = Mathf.Abs(ay - .28f) < .015f && ax >= .17f && ax <= .29f;
                        bool vArm = Mathf.Abs(ax - .28f) < .015f && ay >= .17f && ay <= .29f;
                        bool innerH = Mathf.Abs(ay - .17f) < .013f && ax >= .11f && ax <= .18f;
                        bool innerV = Mathf.Abs(ax - .17f) < .013f && ay >= .11f && ay <= .18f;
                        if (hArm || vArm) a = Mathf.Max(a, .92f);
                        if (innerH || innerV) a = Mathf.Max(a, .68f);
                    }

                    // Center precision pip & micro-crosshair
                    if ((ax < .009f && ay < .055f) || (ay < .009f && ax < .055f))
                        a = Mathf.Max(a, .85f);

                    if (a > .004f)
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
                fuiTargetReticleTex.SetPixels32(pixels);
                fuiTargetReticleTex.Apply(false, true);
            }

            // 5. High-Res Anti-Aliased Oscilloscope Telemetry Waveform Texture
            if (fuiWaveTex == null)
            {
                const int w = 256, h = 56;
                fuiWaveTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "DF_FuiWave",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[w * h];
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    // Burst envelope on left-mid + high-frequency carrier harmonics (like CAPTURE module in reference)
                    float env1 = Mathf.Exp(-Mathf.Pow((nx - .28f) / .16f, 2f)) * .78f;
                    float env2 = Mathf.Exp(-Mathf.Pow((nx - .68f) / .14f, 2f)) * .42f;
                    float wave = (Mathf.Sin(nx * 68f) * .62f + Mathf.Cos(nx * 115f) * .38f) * (env1 + env2 + .10f);

                    for (int y = 0; y < h; y++)
                    {
                        float ny = (y / (float)(h - 1)) * 2f - 1f;
                        float a = 0f;

                        // Subtle horizontal center baseline
                        if (Mathf.Abs(ny) < .035f) a = .22f;

                        // Anti-aliased waveform trace
                        float dist = Mathf.Abs(ny - wave);
                        if (dist < .11f)
                        {
                            float trace = Mathf.Clamp01(1f - dist / .11f);
                            a = Mathf.Max(a, trace * trace * .95f);
                        }

                        if (a > .005f)
                            pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                    }
                }
                fuiWaveTex.SetPixels32(pixels);
                fuiWaveTex.Apply(false, true);
            }

            // 6. Circular Segmented Ring Gauge Texture (98% / 95% / 100% dials)
            if (fuiCircleGaugeTex == null)
            {
                const int size = 96;
                fuiCircleGaugeTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "DF_FuiCircleGauge",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                float half = (size - 1) * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= .96f) continue;

                    float ang = Mathf.Atan2(dy, dx);
                    if (ang < 0f) ang += Mathf.PI * 2f;

                    float a = 0f;
                    // Thin outer precision rim (r = 0.88)
                    float dRim = Mathf.Abs(r - .88f);
                    if (dRim < .024f) a = Mathf.Max(a, (1f - dRim / .024f) * .45f);

                    // Thick segmented gauge track (r in 0.66..0.78)
                    if (r >= .66f && r <= .78f)
                    {
                        float radial = 1f - Mathf.Abs(r - .72f) / .065f;
                        // Leave a small 25-degree gap at bottom-right for technical dial look
                        bool inArc = !(ang > 5.35f && ang < 5.85f);
                        float seg = Mathf.Cos(ang * 24f) > -.65f ? 1f : .35f;
                        if (inArc) a = Mathf.Max(a, Mathf.Clamp01(radial) * seg * .92f);
                    }

                    if (a > .005f)
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
                fuiCircleGaugeTex.SetPixels32(pixels);
                fuiCircleGaugeTex.Apply(false, true);
            }
        }

        /// <summary>
        /// Draws the signature FUI stepped header tab + 1px technical border + corner ticks from the reference cockpit UI.
        /// </summary>
        public static void DrawFuiModuleFrame(Rect r, Texture2D pixel, Color accent, float tabWidth = 118f)
        {
            if (pixel == null || r.width <= 0f || r.height <= 0f) return;
            Color old = GUI.color;

            // 1. Stepped top-left FUI header tab block + horizontal rule across top edge
            float tw = Mathf.Min(tabWidth, r.width * .48f);
            GUI.color = new Color(accent.r, accent.g, accent.b, .26f * old.a);
            GUI.DrawTexture(new Rect(r.x, r.y, tw, 18f), pixel);

            GUI.color = new Color(accent.r, accent.g, accent.b, .72f * old.a);
            GUI.DrawTexture(new Rect(r.x, r.y, tw, 2f), pixel);
            GUI.DrawTexture(new Rect(r.x + tw, r.y + 17f, Mathf.Max(0f, r.width - tw), 1f), pixel);
            GUI.DrawTexture(new Rect(r.x + tw, r.y, 1f, 18f), pixel);

            // 2. Crisp 1px technical outer frame
            GUI.color = new Color(accent.r, accent.g, accent.b, .34f * old.a);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), pixel);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y + 17f, 1f, Mathf.Max(0f, r.height - 17f)), pixel);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), pixel);

            // 3. Precision corner micro-brackets (bottom-left and bottom-right)
            GUI.color = new Color(accent.r, accent.g, accent.b, .85f * old.a);
            float cl = Mathf.Min(12f, r.width * .1f);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, cl, 2f), pixel);
            GUI.DrawTexture(new Rect(r.x, r.yMax - cl, 2f, cl), pixel);
            GUI.DrawTexture(new Rect(r.xMax - cl, r.yMax - 2f, cl, 2f), pixel);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.yMax - cl, 2f, cl), pixel);

            GUI.color = old;
        }

        /// <summary>
        /// Draws the top spacecraft compass heading tape (285 ... 300 ... N ... 015 ... 030) with fine degree tick marks.
        /// </summary>
        public static void DrawFuiCompassTape(Rect r, Texture2D pixel, Color iceBlue, Color thermalOrange, float time)
        {
            if (pixel == null || r.width <= 0f || r.height <= 0f) return;
            Color old = GUI.color;

            // Top baseline rule
            GUI.color = new Color(iceBlue.r, iceBlue.g, iceBlue.b, .42f * old.a);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), pixel);

            int ticks = 37;
            float step = r.width / (ticks - 1);
            float drift = Mathf.Sin(time * .35f) * 2.5f;
            for (int i = 0; i < ticks; i++)
            {
                float tx = r.x + i * step + drift;
                if (tx < r.x || tx > r.xMax) continue;
                bool major = (i % 6) == 0;
                bool mid = (i % 3) == 0;
                float th = major ? 10f : (mid ? 6.5f : 3.5f);
                GUI.color = new Color(iceBlue.r, iceBlue.g, iceBlue.b, (major ? .85f : .42f) * old.a);
                GUI.DrawTexture(new Rect(tx, r.y + 1f, 1f, th), pixel);
            }

            // Center downward heading bug indicator
            float cx = r.x + r.width * .5f;
            GUI.color = new Color(thermalOrange.r, thermalOrange.g, thermalOrange.b, .92f * old.a);
            GUI.DrawTexture(new Rect(cx - 4f, r.y, 8f, 2f), pixel);
            GUI.DrawTexture(new Rect(cx - 2f, r.y + 2f, 4f, 2f), pixel);
            GUI.DrawTexture(new Rect(cx - 1f, r.y + 4f, 2f, 3f), pixel);

            GUI.color = old;
        }

        /// <summary>
        /// Draws the top-left circular FUI radar scope with smooth top-down spacecraft vector, rotating sweep cone, and target blips.
        /// </summary>
        public static void DrawFuiRadarScope(Rect r, Texture2D pixel, Color iceBlue, Color thermalOrange, float time)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            EnsureFuiTextures();
            Color old = GUI.color;
            Matrix4x4 prevMat = GUI.matrix;

            float dim = Mathf.Min(r.width, r.height);
            Rect scopeRect = new Rect(r.x + (r.width - dim) * .5f, r.y + (r.height - dim) * .5f, dim, dim);

            // Base radar grid + smooth anti-aliased top-down dropship silhouette
            GUI.color = new Color(iceBlue.r, iceBlue.g, iceBlue.b, .92f * old.a);
            GUI.DrawTexture(scopeRect, fuiRadarScopeTex);

            // Rotating radar sweep sector
            GUIUtility.RotateAroundPivot(-(time * 48f) % 360f, scopeRect.center);
            GUI.color = new Color(iceBlue.r, iceBlue.g, iceBlue.b, .75f * old.a);
            GUI.DrawTexture(scopeRect, fuiRadarSweepTex);
            GUI.matrix = prevMat;

            // Red-orange heading vector line from center forward + 2 pulsing bogey contacts
            if (pixel != null)
            {
                Vector2 c = scopeRect.center;
                GUI.color = new Color(thermalOrange.r, thermalOrange.g, thermalOrange.b, .88f * old.a);
                GUI.DrawTexture(new Rect(c.x - 1f, scopeRect.y + dim * .08f, 2f, dim * .32f), pixel);

                float pulse = .55f + .45f * Mathf.Sin(time * 4.2f);
                GUI.color = new Color(thermalOrange.r, thermalOrange.g, thermalOrange.b, pulse * old.a);
                GUI.DrawTexture(new Rect(c.x + dim * .22f, c.y - dim * .24f, 4f, 4f), pixel);
                GUI.DrawTexture(new Rect(c.x - dim * .28f, c.y + dim * .16f, 4f, 4f), pixel);
            }

            GUI.color = old;
        }

        /// <summary>
        /// Draws the red-orange topographic terrain contour scan, rotating concentric FUI targeting reticle,
        /// target lock boxes, and side pitch-ladder brackets inside the orbital/tactical viewport.
        /// </summary>
        public static void DrawFuiTacticalViewportHud(Rect viewport, Texture2D pixel, Color iceBlue, Color thermalOrange, float time)
        {
            if (viewport.width <= 0f || viewport.height <= 0f) return;
            EnsureFuiTextures();
            Color old = GUI.color;
            Matrix4x4 prevMat = GUI.matrix;

            // 1. Red-orange 3D topographic contour scan bed across the lower-center of the viewport
            Rect topoRect = new Rect(viewport.x + 6f, viewport.y + viewport.height * .16f, viewport.width - 12f, viewport.height * .76f);
            GUI.color = new Color(thermalOrange.r, thermalOrange.g, thermalOrange.b, .82f * old.a);
            GUI.DrawTexture(topoRect, fuiTopoContourTex);

            // 2. Concentric FUI Targeting Reticle in the center
            float retSize = Mathf.Min(viewport.width, viewport.height) * .72f;
            Rect retRect = new Rect(viewport.center.x - retSize * .5f, viewport.center.y - retSize * .5f, retSize, retSize);

            GUIUtility.RotateAroundPivot((time * 7.5f) % 360f, retRect.center);
            GUI.color = new Color(iceBlue.r, iceBlue.g, iceBlue.b, .94f * old.a);
            GUI.DrawTexture(retRect, fuiTargetReticleTex);
            GUI.matrix = prevMat;

            // 3. Left & Right HUD Pitch-Ladder Brackets & Target Callout Lock Markers
            if (pixel != null)
            {
                // Left and right pitch scale verticals with degree rungs
                float lx = viewport.x + 12f, rx = viewport.xMax - 13f;
                float py = viewport.y + 24f, ph = viewport.height - 48f;
                GUI.color = new Color(iceBlue.r, iceBlue.g, iceBlue.b, .48f * old.a);
                GUI.DrawTexture(new Rect(lx, py, 1f, ph), pixel);
                GUI.DrawTexture(new Rect(rx, py, 1f, ph), pixel);
                for (int i = 0; i <= 6; i++)
                {
                    float ry = py + (ph * i) / 6f;
                    float rw = (i == 3) ? 10f : 5f;
                    GUI.DrawTexture(new Rect(lx, ry, rw, 1f), pixel);
                    GUI.DrawTexture(new Rect(rx - rw, ry, rw, 1f), pixel);
                }

                // 2 Small red-orange target lock markers on the topographic ridges
                GUI.color = new Color(thermalOrange.r, thermalOrange.g, thermalOrange.b, .92f * old.a);
                Rect lock1 = new Rect(viewport.center.x - retSize * .38f, viewport.center.y - retSize * .14f, 14f, 14f);
                Rect lock2 = new Rect(viewport.center.x + retSize * .24f, viewport.center.y - retSize * .28f, 14f, 14f);
                DrawCornerCuts(lock1, pixel, thermalOrange);
                DrawCornerCuts(lock2, pixel, thermalOrange);
            }

            GUI.color = old;
        }

        /// <summary>
        /// Draws the anti-aliased oscilloscope frequency capture waveform and 3 circular percentage ring gauges.
        /// </summary>
        public static void DrawFuiWaveform(Rect waveRect, Color iceBlue)
        {
            if (waveRect.width <= 0f || waveRect.height <= 0f) return;
            EnsureFuiTextures();
            Color old = GUI.color;
            GUI.color = new Color(iceBlue.r, iceBlue.g, iceBlue.b, .92f * old.a);
            GUI.DrawTexture(waveRect, fuiWaveTex);
            GUI.color = old;
        }

        public static void DrawFuiCircleGauge(Rect gaugeRect, Color ringColor, float rotationOffset = 0f)
        {
            if (gaugeRect.width <= 0f || gaugeRect.height <= 0f) return;
            EnsureFuiTextures();
            Color old = GUI.color;
            Matrix4x4 prevMat = GUI.matrix;
            if (Mathf.Abs(rotationOffset) > .01f)
                GUIUtility.RotateAroundPivot(rotationOffset, gaugeRect.center);
            GUI.color = new Color(ringColor.r, ringColor.g, ringColor.b, .92f * old.a);
            GUI.DrawTexture(gaugeRect, fuiCircleGaugeTex);
            GUI.matrix = prevMat;
            GUI.color = old;
        }

        public static void DrawSplitPanel(Rect r,Texture2D pixel,Color zero,Color lumen,float time)
        {
            if(pixel==null||r.width<=0||r.height<=0)return;
            Color old=GUI.color;
            GUI.color=new Color(0,0,0,.72f);
            GUI.DrawTexture(r,pixel);
            float inset=Mathf.Min(3f,Mathf.Min(r.width,r.height)*.08f),innerH=Mathf.Max(0,r.height-inset*2),split=r.x+r.width*.5f+Mathf.Sin(time*.35f)*r.width*.004f;
            GUI.color=new Color(zero.r,zero.g,zero.b,.13f);
            GUI.DrawTexture(new Rect(r.x+inset,r.y+inset,Mathf.Max(0,split-r.x-inset),innerH),pixel);
            GUI.color=new Color(lumen.r,lumen.g,lumen.b,.13f);
            GUI.DrawTexture(new Rect(split,r.y+inset,Mathf.Max(0,r.xMax-split-inset),innerH),pixel);
            if(r.width>=40&&r.height>=14)
            {
                GUI.color=new Color(1,1,1,.08f);
                GUI.DrawTexture(new Rect(r.x+8,r.y+7,r.width*.27f,2),pixel);
                GUI.DrawTexture(new Rect(r.xMax-r.width*.22f-8,r.yMax-9,r.width*.22f,2),pixel);
            }
            for(int i=0;i<5;i++)
            {
                float y=r.y+r.height*(.12f+i*.19f),drift=Mathf.Sin(time*.45f+i*1.7f)*Mathf.Min(1f,r.width*.004f);
                GUI.color=i%2==0?new Color(zero.r,zero.g,zero.b,.24f):new Color(lumen.r,lumen.g,lumen.b,.24f);
                GUI.DrawTexture(new Rect(split-1+drift,y,2,Mathf.Max(1,r.height*.08f)),pixel);
            }
            GUI.color=old;
        }

        public static void DrawSplitPanel(Rect r,Texture2D pixel,Color leftTint,Color rightTint,Color zero,Color lumen,float accentStrength)
        {
            if(pixel==null||r.width<=0||r.height<=0)return;
            Color old=GUI.color;
            float half=r.width*.5f;
            GUI.color=leftTint;
            GUI.DrawTexture(new Rect(r.x,r.y,half,r.height),pixel);
            GUI.color=rightTint;
            GUI.DrawTexture(new Rect(r.x+half,r.y,r.width-half,r.height),pixel);
            DrawSplitPanel(r,pixel,Color.Lerp(zero,Color.white,.1f*accentStrength),Color.Lerp(lumen,Color.white,.1f*accentStrength),Time.unscaledTime);
            GUI.color=old;
        }

        public static void DrawHeaderLine(Rect r,Texture2D pixel,Color zero,Color lumen,float time)
        {
            if(pixel==null)return;
            Color old=GUI.color;
            float split=r.x+r.width*(.48f+Mathf.Sin(time*.35f)*.02f);
            GUI.color=new Color(zero.r,zero.g,zero.b,.75f);
            GUI.DrawTexture(new Rect(r.x,r.y,Mathf.Max(0,split-r.x),2),pixel);
            GUI.color=new Color(lumen.r,lumen.g,lumen.b,.75f);
            GUI.DrawTexture(new Rect(split,r.y,Mathf.Max(0,r.xMax-split),2),pixel);
            GUI.color=old;
        }

        public static void DrawHeaderLine(Rect r,Texture2D pixel,Color zero,Color lumen)=>DrawHeaderLine(r,pixel,zero,lumen,Time.unscaledTime);

        public static void DrawCornerCuts(Rect r,Texture2D pixel,Color accent)
        {
            if(pixel==null||r.width<=0||r.height<=0)return;
            Color old=GUI.color;
            GUI.color=new Color(accent.r,accent.g,accent.b,.48f);
            float w=Mathf.Min(r.width*.45f,Mathf.Max(8,r.width*.08f)),h=Mathf.Min(18,r.height*.18f),t=Mathf.Min(2f,Mathf.Max(1f,r.height*.08f));
            GUI.DrawTexture(new Rect(r.x,r.y,w,t),pixel);
            GUI.DrawTexture(new Rect(r.x,r.y,t,h),pixel);
            GUI.DrawTexture(new Rect(r.xMax-w,r.yMax-t,w,t),pixel);
            GUI.DrawTexture(new Rect(r.xMax-t,r.yMax-h,t,h),pixel);
            GUI.color=old;
        }

        public static void DrawCornerCuts(Rect r,Texture2D pixel,Color topLeftAccent,Color bottomRightAccent)
        {
            if(pixel==null||r.width<=0||r.height<=0)return;
            Color old=GUI.color;
            float w=Mathf.Min(r.width*.45f,Mathf.Max(8,r.width*.08f)),h=Mathf.Min(18,r.height*.18f),t=Mathf.Min(2f,Mathf.Max(1f,r.height*.08f));
            GUI.color=new Color(topLeftAccent.r,topLeftAccent.g,topLeftAccent.b,.56f);
            GUI.DrawTexture(new Rect(r.x,r.y,w,t),pixel);
            GUI.DrawTexture(new Rect(r.x,r.y,t,h),pixel);
            GUI.color=new Color(bottomRightAccent.r,bottomRightAccent.g,bottomRightAccent.b,.56f);
            GUI.DrawTexture(new Rect(r.xMax-w,r.yMax-t,w,t),pixel);
            GUI.DrawTexture(new Rect(r.xMax-t,r.yMax-h,t,h),pixel);
            GUI.color=old;
        }

        /// <summary>Subtle multi-layered cosmic parallax dust and soft nebula horizon glow reactive to pointer/hero rotation.</summary>
        public static void DrawCosmicParallaxOverlay(Rect viewport,Texture2D pixel,Color heroAccent,float time,Vector2 pointerNormalized)
        {
            if(pixel==null||viewport.width<=0f||viewport.height<=0f)return;
            EnsureMotifTextures();
            Color old=GUI.color;
            Color cyan=FuiIceBlue;
            Color thermal=FuiThermalOrange;

            float px=Mathf.Clamp(pointerNormalized.x,-1f,1f);
            float py=Mathf.Clamp(pointerNormalized.y,-1f,1f);

            // Multi-layered deep-space nebula clouds & anamorphic horizon glow (smooth radial textures).
            float bloomX=viewport.x+viewport.width*(.68f-px*.025f);
            float bloomY=viewport.y+viewport.height*(.42f-py*.020f);
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,(.10f+Mathf.Sin(time*.7f)*.02f)*old.a);
            GUI.DrawTexture(new Rect(bloomX-420f,bloomY-310f,840f,620f),softGlowTex);

            float leftBloomX=viewport.x+viewport.width*(.24f+px*.018f);
            float leftBloomY=viewport.y+viewport.height*(.38f+py*.015f);
            GUI.color=new Color(cyan.r,cyan.g,cyan.b,(.09f+Mathf.Sin(time*.55f+1.4f)*.02f)*old.a);
            GUI.DrawTexture(new Rect(leftBloomX-360f,leftBloomY-280f,720f,560f),softGlowTex);

            // Subtle holographic astrolabe ring in deep space behind the command deck
            if(orbitalRingTex!=null)
            {
                Matrix4x4 prevMat=GUI.matrix;
                float ringDim=Mathf.Min(viewport.width,viewport.height)*.82f;
                Vector2 ringCenter=new Vector2(viewport.x+viewport.width*.50f,viewport.y+viewport.height*.50f);
                GUIUtility.RotateAroundPivot((time*3.2f)%360f,ringCenter);
                GUI.color=new Color(cyan.r,cyan.g,cyan.b,.07f*old.a);
                GUI.DrawTexture(new Rect(ringCenter.x-ringDim*.5f,ringCenter.y-ringDim*.5f,ringDim,ringDim),orbitalRingTex);
                GUI.matrix=prevMat;
            }

            // Soft-glow drifting cosmic bokeh motes across 3 depth planes.
            const int moteCount=18;
            for(int i=0;i<moteCount;i++)
            {
                float depth=(i%3+1)*.33f;
                float seedX=((i*73+19)%97)/97f;
                float seedY=((i*41+53)%89)/89f;
                float driftX=Mathf.Sin(time*(.16f+depth*.12f)+i*1.3f)*16f*depth - px*24f*depth;
                float driftY=((seedY*viewport.height - time*(4f+depth*7f) - py*14f*depth)%viewport.height + viewport.height)%viewport.height;
                float x=viewport.x + ((seedX*viewport.width + driftX)%viewport.width + viewport.width)%viewport.width;
                float y=viewport.y + driftY;
                float twinkle=.35f+.65f*Mathf.Sin(time*(1.3f+(i%5)*.4f)+i*2.1f);
                if(twinkle<=.15f)continue;
                Color c=i%3==0?cyan:i%3==1?heroAccent:thermal;
                float size=(i%4==0?14f:9f)*(.65f+depth*.55f);
                GUI.color=new Color(c.r,c.g,c.b,twinkle*(.14f+depth*.14f)*old.a);
                GUI.DrawTexture(new Rect(x-size*.5f,y-size*.5f,size,size),softGlowTex);
            }
            GUI.color=old;
        }

        /// <summary>A softly shaded planet with slowly rotating surface detail for loading screens.</summary>
        public static void DrawLoadingPlanet(Rect rect,Color tint,float rotation)
        {
            if(rect.width<=0f||rect.height<=0f)return;
            EnsureMotifTextures();
            if(loadingPlanetTex==null)
            {
                // Generate once; bilinear sampling and a soft edge keep the planet smooth at mobile sizes.
                const int size=256;
                loadingPlanetTex=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {
                    name="DF_LoadingPlanet",hideFlags=HideFlags.HideAndDontSave,
                    wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear
                };
                var pixels=new Color32[size*size];
                float half=(size-1)*.5f;
                for(int y=0;y<size;y++)
                for(int x=0;x<size;x++)
                {
                    float nx=(x-half)/half,ny=(y-half)/half;
                    float radius2=nx*nx+ny*ny;
                    if(radius2>=1f)continue;
                    float depth=Mathf.Sqrt(1f-radius2);
                    float sun=Mathf.Clamp01(.22f+depth*.62f-nx*.36f+ny*.26f);
                    float lat=ny+nx*.16f+Mathf.Sin(nx*3.1f)*.035f;
                    float band=.5f+.5f*Mathf.Sin(lat*8.5f)*(.70f+.30f*Mathf.Sin(lat*3.8f+1.2f));
                    float swirl=.5f+.5f*Mathf.Sin(nx*4.8f+ny*6.2f+Mathf.Sin(lat*6f)*.65f);
                    float rim=Mathf.Clamp01(1f-depth);
                    float fresnel=rim*rim*Mathf.Clamp01(.42f-nx*.42f+ny*.32f);
                    float color=Mathf.Clamp01(sun*(.72f+band*.19f+swirl*.09f)+fresnel*.48f);
                    float edge=Mathf.Clamp01((1f-Mathf.Sqrt(radius2))*size*.40f);
                    pixels[y*size+x]=new Color32((byte)Mathf.RoundToInt(255f*color),
                        (byte)Mathf.RoundToInt(255f*color),(byte)Mathf.RoundToInt(255f*color),
                        (byte)Mathf.RoundToInt(255f*edge));
                }
                loadingPlanetTex.SetPixels32(pixels);
                loadingPlanetTex.Apply(false,true);
            }

            Color old=GUI.color;Matrix4x4 matrix=GUI.matrix;
            try
            {
                GUI.color=new Color(tint.r,tint.g,tint.b,.30f*old.a);
                GUI.DrawTexture(new Rect(rect.x-rect.width*.28f,rect.y-rect.height*.28f,
                    rect.width*1.56f,rect.height*1.56f),softGlowTex);
                GUIUtility.RotateAroundPivot(rotation,rect.center);
                GUI.color=new Color(tint.r,tint.g,tint.b,.95f*old.a);
                GUI.DrawTexture(rect,loadingPlanetTex);
            }
            finally {GUI.matrix=matrix;GUI.color=old;}
        }

        /// <summary>
        /// High-resolution holographic orbital astrolabe, radial backlight bloom, and perspective pedestal light-well.
        /// Must ALWAYS be drawn BEHIND the 3D hero texture so the hero stays 100% crisp and unobstructed in the foreground.
        /// </summary>
        public static void DrawDimensionalRiftStage(Rect stageRect,Texture2D pixel,Color heroAccent,float time,float switchPulse,float yawOffset)
        {
            if(stageRect.width<=0f||stageRect.height<=0f)return;
            EnsureMotifTextures();
            Color old=GUI.color;
            Color brightAccent=Color.Lerp(heroAccent,Color.white,.55f);
            Color cyan=new Color(.32f,.90f,1f,1f);

            float cx=stageRect.center.x;
            float upperCy=stageRect.y+stageRect.height*.43f;
            float minDim=Mathf.Min(stageRect.width,stageRect.height);
            float breathe=1f+Mathf.Sin(time*1.8f)*.035f+switchPulse*.12f;

            // 1. Smooth radial backlight aura behind the hero silhouette.
            float backlightSize=minDim*(.92f*breathe);
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,.24f+switchPulse*.22f);
            GUI.DrawTexture(new Rect(cx-backlightSize*.5f,upperCy-backlightSize*.5f,backlightSize,backlightSize),softGlowTex);

            float coreGlowSize=minDim*(.54f*breathe);
            GUI.color=new Color(brightAccent.r,brightAccent.g,brightAccent.b,.20f+switchPulse*.25f);
            GUI.DrawTexture(new Rect(cx-coreGlowSize*.5f,upperCy-coreGlowSize*.5f,coreGlowSize,coreGlowSize),softGlowTex);

            // 2. Dual counter-rotating anti-aliased Holographic Orbital Rings behind the hero's shoulders/upper torso.
            float ringSize=minDim*(.78f+switchPulse*.06f);
            Rect ringRect=new Rect(cx-ringSize*.5f,upperCy-ringSize*.5f,ringSize,ringSize);
            Vector2 pivot=new Vector2(cx,upperCy);

            Matrix4x4 prevMatrix=GUI.matrix;
            float angleA=(time*9f + yawOffset*.45f)%360f;
            GUIUtility.RotateAroundPivot(angleA,pivot);
            GUI.color=new Color(brightAccent.r,brightAccent.g,brightAccent.b,.34f+switchPulse*.28f);
            GUI.DrawTexture(ringRect,orbitalRingTex);
            GUI.matrix=prevMatrix;

            float innerRingSize=ringSize*.76f;
            Rect innerRingRect=new Rect(cx-innerRingSize*.5f,upperCy-innerRingSize*.5f,innerRingSize,innerRingSize);
            float angleB=(-time*6.5f - yawOffset*.30f + 30f)%360f;
            GUIUtility.RotateAroundPivot(angleB,pivot);
            GUI.color=new Color(cyan.r,cyan.g,cyan.b,.22f+switchPulse*.18f);
            GUI.DrawTexture(innerRingRect,orbitalRingTex);
            GUI.matrix=prevMatrix;

            // 3. Perspective-projected elliptical holographic light-well under the 3D pedestal.
            float deckY=stageRect.yMax-stageRect.height*.115f;
            float wellW=stageRect.width*(.76f+switchPulse*.08f);
            float wellH=Mathf.Max(22f,stageRect.height*.13f);
            GUI.color=new Color(heroAccent.r,heroAccent.g,heroAccent.b,.32f+switchPulse*.24f);
            GUI.DrawTexture(new Rect(cx-wellW*.5f,deckY-wellH*.5f,wellW,wellH),softGlowTex);

            // Anamorphic horizontal light flare across the pedestal rim.
            float flareW=stageRect.width*(.64f+switchPulse*.12f);
            GUI.color=new Color(brightAccent.r,brightAccent.g,brightAccent.b,.38f+switchPulse*.30f);
            GUI.DrawTexture(new Rect(cx-flareW*.5f,deckY-5f,flareW,10f),softGlowTex);

            // 4. Soft-glow floating energy bokeh rising gently behind the hero.
            const int sparkCount=8;
            for(int i=0;i<sparkCount;i++)
            {
                float progress=((time*(.22f+(i%3)*.08f)+i*.14f)%1f);
                float lane=((i*37%9)-4f)/4f;
                float sx=cx + lane*(minDim*.34f) + Mathf.Sin(time*1.6f+i*1.9f)*6f;
                float sy=deckY - progress*(stageRect.height*.68f);
                float alpha=Mathf.Sin(progress*Mathf.PI)*(.42f+switchPulse*.28f);
                float sSize=(10f+(i%3)*5f)*(1f-progress*.35f);
                Color sc=i%2==0?brightAccent:cyan;
                GUI.color=new Color(sc.r,sc.g,sc.b,alpha);
                GUI.DrawTexture(new Rect(sx-sSize*.5f,sy-sSize*.5f,sSize,sSize),softGlowTex);
            }

            GUI.color=old;
        }

        /// <summary>Draws an animated diagonal shimmer sweep and tactical corner brackets on primary CTA buttons/cards.</summary>
        public static void DrawButtonShimmer(Rect rect,Texture2D pixel,Color accent,float time,bool hovered=false)
        {
            if(pixel==null||rect.width<=0f||rect.height<=0f)return;
            EnsureMotifTextures();
            Color old=GUI.color;
            float pulse=.5f+.5f*Mathf.Sin(time*3.2f);
            Color bracketColor=Color.Lerp(accent,Color.white,hovered?.55f:.25f*pulse);
            DrawCornerCuts(rect,pixel,bracketColor);

            float cycle=(time%2.8f)/1.15f;
            if(cycle>=0f&&cycle<=1f)
            {
                float sweepX=Mathf.Lerp(rect.x+8f,rect.xMax-36f,cycle);
                float edgeFade=Mathf.Sin(cycle*Mathf.PI);
                GUI.color=new Color(1f,1f,1f,edgeFade*(hovered?.20f:.12f));
                GUI.DrawTexture(new Rect(sweepX,rect.y+1f,28f,rect.height-2f),softGlowTex);
            }
            GUI.color=old;
        }

        /// <summary>
        /// Draws the military spacecraft FUI bridge frame, top compass heading tape, and subtle ice-blue telemetry drift for Lobby & Matchmaking views.
        /// </summary>
        public static void DrawDropshipBayAtmosphere(Rect canvas, Texture2D pixel, Color accent, float time, bool inFlight)
        {
            if (pixel == null || canvas.width <= 0f || canvas.height <= 0f) return;
            EnsureFuiTextures();
            Color old = GUI.color;

            // Subtle ice-blue slipstream drift outside the spacecraft viewport
            int streakCount = inFlight ? 8 : 5;
            float speed = inFlight ? 520f : 220f;
            for (int i = 0; i < streakCount; i++)
            {
                float seed = (i * 97 + 29) % 100 / 100f;
                float sw = (inFlight ? 120f : 72f) + (i % 3) * 32f;
                float travel = canvas.width + sw * 2f;
                float sx = canvas.xMax + sw - ((time * (speed + (i % 3) * 90f) + seed * travel) % travel);
                float sy = canvas.y + 64f + ((i * 67 + 19) % 86) / 86f * (canvas.height - 128f);
                float alpha = (inFlight ? .10f : .05f) * (.5f + .5f * Mathf.Sin(time * 1.8f + i));
                GUI.color = new Color(FuiIceBlue.r, FuiIceBlue.g, FuiIceBlue.b, alpha * old.a);
                GUI.DrawTexture(new Rect(sx, sy, sw, 2f), softGlowTex);
            }

            // Top FUI compass heading tape & outer technical frame rails
            Rect compassRect = new Rect(canvas.x + canvas.width * .30f, canvas.y + 8f, canvas.width * .40f, 16f);
            DrawFuiCompassTape(compassRect, pixel, FuiIceBlue, FuiThermalOrange, time);

            Color trimCol = inFlight ? FuiThermalOrange : FuiIceBlue;
            GUI.color = new Color(trimCol.r, trimCol.g, trimCol.b, .34f * old.a);
            GUI.DrawTexture(new Rect(canvas.x + 36f, canvas.y + 4f, canvas.width * .24f, 1f), pixel);
            GUI.DrawTexture(new Rect(canvas.xMax - 36f - canvas.width * .24f, canvas.y + 4f, canvas.width * .24f, 1f), pixel);
            GUI.DrawTexture(new Rect(canvas.x + 36f, canvas.yMax - 6f, canvas.width - 72f, 1f), pixel);

            GUI.color = old;
        }
    }
}
