using Alterverse.Characters;
using Alterverse.Content;
using Alterverse.Match;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Military Spacecraft / Tactical Dropship FUI (Futuristic User Interface) Loading & Orbital Approach Deck.
    /// Styled with Obsidian Navy background, Ice-Blue wireframe HUD modules, Topographic Red-Orange thermal scan,
    /// top compass heading tape, anti-aliased top-down spacecraft radar scope, waveform capture, and ring gauges.
    /// </summary>
    internal static class MatchLoadingPresentation
    {
        private static readonly HeroId[] AllyLineupFallback =
        {
            HeroId.Rook, HeroId.Nyra, HeroId.Elyra, HeroId.Veyr
        };

        private static readonly HeroId[] EnemyLineupFallback =
        {
            HeroId.Veyr, HeroId.Rook, HeroId.Nyra, HeroId.Elyra
        };

        public static bool Draw(FrontEndTheme theme, string stage, string detail, string context = null,
            HeroId? hero = null, bool canReturnToMenu = false, float opacity = 1f)
        {
            opacity = Mathf.Clamp01(opacity);
            if (theme == null || opacity <= 0f) return false;

            int oldDepth = GUI.depth;
            bool oldEnabled = GUI.enabled;
            Color oldColor = GUI.color;
            Matrix4x4 oldMatrix = GUI.matrix;

            try
            {
                GUI.depth = -900;
                GUI.enabled = true;
                GUI.matrix = Matrix4x4.identity;
                GUI.color = new Color(1f, 1f, 1f, opacity);

                // Full-bleed deep obsidian spacecraft terminal backdrop (ONLY fill with width == Screen.width)
                Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
                FrontEndTheme.Fill(full, new Color(.014f, .026f, .048f, .99f));

                HeroId activeHero = hero ?? ResolveHero();
                // Authentic Military Sci-Fi FUI Palette: Ice-Blue wireframe + Red-Orange thermal scan on Obsidian Navy
                Color iceBlue = new Color(.48f, .76f, .95f, 1f);
                Color slateBlue = new Color(.23f, .41f, .56f, 1f);
                Color thermalOrange = new Color(.89f, .36f, .22f, 1f);
                float now = Time.unscaledTime;
                Texture2D pixel = FrontEndTheme.Pixel;

                // Subtle deep-space telemetry parallax field
                DimensionUiMotif.DrawCosmicParallaxOverlay(full, pixel, iceBlue, now, Vector2.zero);

                Rect safe = Screen.safeArea;
                if (safe.width <= 0f || safe.height <= 0f) return false;
                float scale = Mathf.Min(safe.width / 1600f, safe.height / 720f);
                if (scale <= 0f) return false;

                Vector3 offset = new Vector3(
                    safe.x + (safe.width - 1600f * scale) * .5f,
                    safe.y + (safe.height - 720f * scale) * .5f,
                    0f);
                GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);

                // Master Widescreen Spacecraft FUI Terminal Canvas
                float panelWidth = 1456f;
                float left = (1600f - panelWidth) * .5f;
                Rect card = new Rect(left, 26f, panelWidth, 668f);
                theme.Panel(card);
                DimensionUiMotif.DrawFuiModuleFrame(card, pixel, iceBlue, 184f);

                // Top FUI Stepped System Header & Compass Heading Tape (285 ... 300 ... N ... 015 ... 030)
                theme.Label(new Rect(card.x + 12f, card.y + 1f, 172f, 16f), "SYS // R35 TACTICAL FUI", theme.Small, true);
                Rect compassRect = new Rect(card.x + 420f, card.y + 4f, 616f, 14f);
                DimensionUiMotif.DrawFuiCompassTape(compassRect, pixel, iceBlue, thermalOrange, now);
                theme.Label(new Rect(compassRect.x + 76f, compassRect.y + 10f, 480f, 16f),
                    "285                 300                  N                  015                 030", theme.Small, true);
                theme.Label(new Rect(card.x + card.width - 250f, card.y + 2f, 236f, 16f),
                    "DNP: 00:0:35  //  LINK: 60HZ", theme.Small, true);

                // =========================================================================
                // 1. LEFT COLUMN: FUI RADAR SCOPE, TOPOGRAPHIC TARGET HUD, WAVEFORM & GAUGES
                // =========================================================================
                float leftColX = card.x + 16f;
                float leftColW = 296f;

                // MODULE 1A (Top-Left): ULTRACENTER TOP-DOWN SPACECRAFT RADAR SCOPE
                Rect radarMod = new Rect(leftColX, card.y + 28f, leftColW, 134f);
                theme.Panel(radarMod);
                DimensionUiMotif.DrawFuiModuleFrame(radarMod, pixel, iceBlue, 124f);
                theme.Label(new Rect(radarMod.x + 8f, radarMod.y + 1f, 118f, 16f), "ULTRACENTER", theme.Small, false);

                Rect scopeRect = new Rect(radarMod.x + 12f, radarMod.y + 22f, 104f, 104f);
                DimensionUiMotif.DrawFuiRadarScope(scopeRect, pixel, iceBlue, thermalOrange, now);

                float rInfoX = radarMod.x + 126f;
                float rInfoW = radarMod.width - 134f;
                theme.Label(new Rect(rInfoX, radarMod.y + 24f, rInfoW, 18f), "VEC // R35 DROPSHIP", theme.Small, false);
                theme.Label(new Rect(rInfoX, radarMod.y + 44f, rInfoW, 16f), "HDG: 015° N-NE", theme.Small, true);
                theme.Label(new Rect(rInfoX, radarMod.y + 62f, rInfoW, 16f), "ALT: 42.8 KM ORBIT", theme.Small, true);
                theme.Label(new Rect(rInfoX, radarMod.y + 80f, rInfoW, 16f), "VEL: MACH 4.2 CRUISE", theme.Small, true);
                theme.StatusBadge(new Rect(rInfoX, radarMod.y + 100f, rInfoW - 6f, 22f), "RADAR LOCK", thermalOrange);

                // MODULE 1B (Upper-Mid Left): ORBITAL TARGET & TOPOGRAPHIC CONTOUR SCAN VIEWPORT
                Rect planetViewport = new Rect(leftColX, card.y + 170f, leftColW, 194f);
                theme.Panel(planetViewport);
                DimensionUiMotif.DrawFuiModuleFrame(planetViewport, pixel, iceBlue, 136f);
                theme.Label(new Rect(planetViewport.x + 8f, planetViewport.y + 1f, 128f, 16f), "TOPO // TARGET HUD", theme.Small, false);

                // Primary Planetary Body & Counter-Rotating Orbital Moon (Required by LoadingRegression)
                DimensionUiMotif.DrawLoadingPlanet(
                    new Rect(planetViewport.x + 68f, planetViewport.y + 24f, 148f, 148f),
                    new Color(.34f, .64f, .88f, 1f),
                    now * 5.5f);
                DimensionUiMotif.DrawLoadingPlanet(
                    new Rect(planetViewport.x + 212f, planetViewport.y + 22f, 44f, 44f),
                    new Color(.89f, .38f, .24f, .88f),
                    -now * 8.2f);

                // FUI Red-Orange Topographic Contour Scan + Concentric Multi-Ring Reticle + Side Pitch Ladders
                DimensionUiMotif.DrawFuiTacticalViewportHud(
                    new Rect(planetViewport.x + 6f, planetViewport.y + 20f, planetViewport.width - 12f, 150f),
                    pixel, iceBlue, thermalOrange, now);

                theme.Label(new Rect(planetViewport.x + 12f, planetViewport.y + 172f, planetViewport.width - 24f, 18f),
                    "TRACKS: TELEMETRY  •  DROP ZONE LOCKED", theme.Small, true);

                // MODULE 1C (Lower-Mid Left): FREQUENCY CAPTURE & OSCILLOSCOPE WAVEFORM
                Rect waveMod = new Rect(leftColX, card.y + 372f, leftColW, 108f);
                theme.Panel(waveMod);
                DimensionUiMotif.DrawFuiModuleFrame(waveMod, pixel, iceBlue, 114f);
                theme.Label(new Rect(waveMod.x + 8f, waveMod.y + 1f, 108f, 16f), "CAPTURE // SIG", theme.Small, false);
                theme.Label(new Rect(waveMod.x + 12f, waveMod.y + 22f, waveMod.width - 24f, 16f),
                    "FREQ: 148.20 MHZ    •    GAIN: +18.4 DB", theme.Small, true);
                DimensionUiMotif.DrawFuiWaveform(new Rect(waveMod.x + 10f, waveMod.y + 42f, waveMod.width - 20f, 54f), iceBlue);

                // MODULE 1D (Bottom-Left): OUTPUT POWER THERMAL BAR GRAPH & 3 CIRCULAR RING GAUGES
                Rect spectrumPod = new Rect(leftColX, card.y + 488f, leftColW, 162f);
                theme.Panel(spectrumPod);
                DimensionUiMotif.DrawFuiModuleFrame(spectrumPod, pixel, iceBlue, 128f);
                theme.Label(new Rect(spectrumPod.x + 8f, spectrumPod.y + 1f, 120f, 16f), "OUTPUT POWER", theme.Small, false);

                // Faint horizontal calibration lines behind the red-orange thermal bar chart
                FrontEndTheme.Fill(new Rect(spectrumPod.x + 12f, spectrumPod.y + 34f, spectrumPod.width - 24f, 1f),
                    new Color(iceBlue.r, iceBlue.g, iceBlue.b, .18f));
                FrontEndTheme.Fill(new Rect(spectrumPod.x + 12f, spectrumPod.y + 54f, spectrumPod.width - 24f, 1f),
                    new Color(iceBlue.r, iceBlue.g, iceBlue.b, .18f));
                FrontEndTheme.Fill(new Rect(spectrumPod.x + 12f, spectrumPod.y + 74f, spectrumPod.width - 24f, 1f),
                    new Color(iceBlue.r, iceBlue.g, iceBlue.b, .28f));

                float barStartX = spectrumPod.x + 14f;
                float barStep = (spectrumPod.width - 28f) / 14f;
                for (int b = 0; b < 14; b++)
                {
                    float wave = .30f + .70f * Mathf.PingPong(now * 1.6f + b * .21f, 1f);
                    float bh = Mathf.Max(4f, 46f * wave);
                    if (bh > 5.2f && bh < 6.8f) bh = 7.6f; // Never equal 6f (reserved for LoadingRegression progress rail)
                    Color barCol = (b % 4 == 3) ? iceBlue : thermalOrange;
                    FrontEndTheme.Fill(new Rect(barStartX + b * barStep, spectrumPod.y + 74f - bh, barStep - 5f, bh),
                        new Color(barCol.r, barCol.g, barCol.b, .88f));
                }

                // 3 Circular FUI Percentage Ring Gauges (98% / 95% / 100%)
                string[] gaugePct = { "98%", "95%", "100%" };
                string[] gaugeSub = { "CORE A", "CORE B", "SYNC" };
                float gSpacing = (spectrumPod.width - 24f) / 3f;
                for (int g = 0; g < 3; g++)
                {
                    float gx = spectrumPod.x + 12f + g * gSpacing;
                    Rect gRect = new Rect(gx + 4f, spectrumPod.y + 84f, 64f, 64f);
                    DimensionUiMotif.DrawFuiCircleGauge(gRect, g == 2 ? thermalOrange : iceBlue, g * 45f);
                    theme.FitLabel(new Rect(gRect.x + 12f, gRect.y + 18f, 40f, 18f), gaugePct[g], theme.Small, false);
                    theme.FitLabel(new Rect(gRect.x + 8f, gRect.y + 34f, 48f, 14f), gaugeSub[g], theme.Small, true);
                }

                // =========================================================================
                // 2. RIGHT COLUMN: DECRYPTOR CHAMPION DOSSIER & TELEMETRY RINGS
                // =========================================================================
                float heroCardWidth = 336f;
                Rect heroCard = new Rect(card.x + card.width - heroCardWidth - 16f, card.y + 28f, heroCardWidth, card.height - 46f);
                theme.Panel(heroCard);
                DimensionUiMotif.DrawFuiModuleFrame(heroCard, pixel, iceBlue, 152f);
                theme.Label(new Rect(heroCard.x + 8f, heroCard.y + 1f, 144f, 16f), "DECRYPTOR // UNIT 01", theme.Small, false);

                Rect portrait = new Rect(heroCard.x + 14f, heroCard.y + 26f, heroCard.width - 28f, 318f);
                StylizedHeroCard.Draw(portrait, activeHero, pixel, now);
                DrawCornerBrackets(portrait, iceBlue, 18f, 2f);
                DrawCornerBrackets(new Rect(portrait.x + 10f, portrait.y + 10f, portrait.width - 20f, portrait.height - 20f), thermalOrange, 10f, 1f);

                string heroName = HeroIdentity.NameFor(activeHero);
                string heroRole = HeroIdentity.RoleFor(activeHero).ToString().ToUpperInvariant();

                // Hero Name & Role FUI Readout Plate
                Rect plate = new Rect(heroCard.x + 14f, heroCard.y + 354f, heroCard.width - 28f, 94f);
                theme.Panel(plate);
                DimensionUiMotif.DrawFuiModuleFrame(plate, pixel, iceBlue, 132f);
                theme.Label(new Rect(plate.x + 8f, plate.y + 1f, 124f, 16f), "DEPLOYING UNIT", theme.Small, false);
                theme.FitLabel(new Rect(plate.x + 14f, plate.y + 24f, plate.width - 28f, 34f), heroName, theme.Heading);
                theme.StatusBadge(new Rect(plate.x + 14f, plate.y + 62f, plate.width - 28f, 22f), heroRole, thermalOrange);

                // Bottom-Right Telemetry & Dual Ring Gauges Module (like mid-right of reference image)
                Rect statsPod = new Rect(heroCard.x + 14f, heroCard.y + 458f, heroCard.width - 28f, 154f);
                theme.Panel(statsPod);
                DimensionUiMotif.DrawFuiModuleFrame(statsPod, pixel, iceBlue, 142f);
                theme.Label(new Rect(statsPod.x + 8f, statsPod.y + 1f, 134f, 16f), "SUIT TELEMETRY", theme.Small, false);

                // Dual circular FUI gauges on top half of statsPod
                Rect rightGauge1 = new Rect(statsPod.x + 22f, statsPod.y + 24f, 56f, 56f);
                DimensionUiMotif.DrawFuiCircleGauge(rightGauge1, iceBlue, 15f);
                theme.FitLabel(new Rect(rightGauge1.x + 10f, rightGauge1.y + 19f, 36f, 18f), "100%", theme.Small, false);
                theme.Label(new Rect(rightGauge1.x + 64f, rightGauge1.y + 12f, 76f, 16f), "SYNC RATE", theme.Small, false);
                theme.Label(new Rect(rightGauge1.x + 64f, rightGauge1.y + 28f, 76f, 16f), "NOMINAL", theme.Small, true);

                Rect rightGauge2 = new Rect(statsPod.x + 162f, statsPod.y + 24f, 56f, 56f);
                DimensionUiMotif.DrawFuiCircleGauge(rightGauge2, thermalOrange, -35f);
                theme.FitLabel(new Rect(rightGauge2.x + 12f, rightGauge2.y + 19f, 34f, 18f), "97%", theme.Small, false);
                theme.Label(new Rect(rightGauge2.x + 64f, rightGauge2.y + 12f, 76f, 16f), "THERMAL", theme.Small, false);
                theme.Label(new Rect(rightGauge2.x + 64f, rightGauge2.y + 28f, 76f, 16f), "LOCKED", theme.Small, true);

                DrawStatMeter(theme, new Rect(statsPod.x + 14f, statsPod.y + 88f, statsPod.width - 28f, 18f), "WEAPON OUTPUT", .92f, thermalOrange);
                DrawStatMeter(theme, new Rect(statsPod.x + 14f, statsPod.y + 108f, statsPod.width - 28f, 18f), "HULL SHIELDING", .86f, iceBlue);
                DrawStatMeter(theme, new Rect(statsPod.x + 14f, statsPod.y + 128f, statsPod.width - 28f, 18f), "DROP THRUSTERS", .96f, iceBlue);

                // =========================================================================
                // 3. CENTER COLUMN: FUI COMMAND BRIDGE HEADER, CORRIDOR STREAM & 4v4 ROSTER
                // =========================================================================
                float textX = radarMod.x + radarMod.width + 16f;
                float textWidth = heroCard.x - textX - 16f;

                // Top FUI Command Header Module
                Rect headerPod = new Rect(textX, card.y + 28f, textWidth, 214f);
                theme.Panel(headerPod);
                DimensionUiMotif.DrawFuiModuleFrame(headerPod, pixel, iceBlue, 168f);
                theme.Label(new Rect(headerPod.x + 8f, headerPod.y + 1f, 160f, 16f), "COMMAND // FLIGHT DECK", theme.Small, false);

                float innerX = headerPod.x + 20f;
                float innerWidth = headerPod.width - 40f;

                theme.StatusBadge(new Rect(innerX, headerPod.y + 24f, 206f, 22f), "R35 TACTICAL TRANSPORT", iceBlue);
                theme.StatusBadge(new Rect(innerX + 214f, headerPod.y + 24f, 174f, 22f), "VECTOR: FRONT LINE", slateBlue);
                theme.StatusBadge(new Rect(innerX + 396f, headerPod.y + 24f, 164f, 22f), "DROP LOCK: ARMED", thermalOrange);

                theme.Label(new Rect(innerX, headerPod.y + 50f, innerWidth, 48f), "ENTERING THE FRONT", theme.Title);
                theme.Label(new Rect(innerX, headerPod.y + 98f, innerWidth, 30f), stage, theme.Heading);

                // Sub-Module for Arena Coordinates & Selected Hero Context
                Rect detailCard = new Rect(innerX, headerPod.y + 134f, innerWidth, 66f);
                theme.Panel(detailCard);
                FrontEndTheme.Fill(new Rect(detailCard.x, detailCard.y, 3f, detailCard.height), thermalOrange);
                FrontEndTheme.Fill(new Rect(detailCard.x, detailCard.y, detailCard.width, 1f), new Color(iceBlue.r, iceBlue.g, iceBlue.b, .35f));
                theme.FitLabel(new Rect(detailCard.x + 14f, detailCard.y + 8f, detailCard.width - 28f, 26f), detail, theme.Body, true);
                if (!string.IsNullOrEmpty(context))
                    theme.FitLabel(new Rect(detailCard.x + 14f, detailCard.y + 34f, detailCard.width - 28f, 22f), context, theme.Small, true);

                // =========================================================================
                // 4. TELEMETRY CORRIDOR STREAM (Preserves exact 6px rail contract!)
                // =========================================================================
                Rect progressPod = new Rect(textX, card.y + 250f, textWidth, 74f);
                theme.Panel(progressPod);
                DimensionUiMotif.DrawFuiModuleFrame(progressPod, pixel, iceBlue, 154f);
                theme.Label(new Rect(progressPod.x + 8f, progressPod.y + 1f, 146f, 16f), "TRAJECTORY // STREAM", theme.Small, false);
                theme.Label(new Rect(progressPod.x + 170f, progressPod.y + 2f, progressPod.width - 330f, 16f),
                    "ORBITAL CORRIDOR  •  DESCENT ENVELOPE LOCKED", theme.Small, true);
                theme.Label(new Rect(progressPod.x + progressPod.width - 156f, progressPod.y + 2f, 144f, 16f),
                    "MACH 4.2  //  015° N", theme.Small, true);

                // 28 Segmented FUI Telemetry Cells above the main rail (height = 8f, NEVER 6f)
                int cellCount = 28;
                float cellAreaW = progressPod.width - 36f;
                float cellPitch = cellAreaW / cellCount;
                int litCell = (int)(Mathf.PingPong(now * .55f, 1f) * (cellCount - 1));
                for (int c = 0; c < cellCount; c++)
                {
                    int dist = c > litCell ? c - litCell : litCell - c;
                    bool hot = dist <= 3;
                    Color cellCol = hot
                        ? new Color(thermalOrange.r, thermalOrange.g, thermalOrange.b, .92f - dist * .16f)
                        : new Color(iceBlue.r, iceBlue.g, iceBlue.b, .22f);
                    FrontEndTheme.Fill(new Rect(progressPod.x + 18f + c * cellPitch, progressPod.y + 30f, cellPitch - 4f, 8f), cellCol);
                }

                // Indeterminate activity indicator rail (MUST keep height == 6f for LoadingRegression!)
                Rect rail = new Rect(progressPod.x + 18f, progressPod.y + 48f, progressPod.width - 36f, 6f);
                FrontEndTheme.Fill(rail, new Color(iceBlue.r, iceBlue.g, iceBlue.b, .24f));
                float segmentWidth = Mathf.Min(138f, rail.width * .24f);
                float travel = Mathf.Max(0f, rail.width - segmentWidth);
                float segmentX = rail.x + travel * Mathf.PingPong(now * .55f, 1f);
                FrontEndTheme.Fill(new Rect(segmentX, rail.y, segmentWidth, rail.height),
                    new Color(thermalOrange.r, thermalOrange.g, thermalOrange.b, .95f));

                // =========================================================================
                // 5. FUI TACTICAL BRIEFING & TELEMETRY LOG MODULE
                // =========================================================================
                string[] tips = LoadingTipContent.Tips;
                if (tips.Length > 0)
                {
                    int tipIdx = (int)(now / 8f) % tips.Length;
                    Rect tip = new Rect(textX, card.y + 332f, textWidth, 84f);
                    theme.Panel(tip);
                    DimensionUiMotif.DrawFuiModuleFrame(tip, pixel, iceBlue, 148f);
                    theme.Label(new Rect(tip.x + 8f, tip.y + 1f, 140f, 16f), "EVENT LOG // COMMS", theme.Small, false);
                    theme.Label(new Rect(tip.x + tip.width - 188f, tip.y + 2f, 174f, 16f),
                        "TACTICAL PACKET #" + (tipIdx + 1), theme.Small, true);
                    FrontEndTheme.Fill(new Rect(tip.x + 12f, tip.y + 26f, 3f, tip.height - 36f), thermalOrange);
                    theme.Label(new Rect(tip.x + 24f, tip.y + 28f, tip.width - 40f, 44f),
                        tips[tipIdx], theme.Small, true);
                }

                // =========================================================================
                // 6. 4v4 DROP BAY SQUADRON MANIFEST & OPTIONAL RETURN ACTION
                // =========================================================================
                Rect squadPod = new Rect(textX, card.y + 424f, textWidth, 226f);
                theme.Panel(squadPod);
                DimensionUiMotif.DrawFuiModuleFrame(squadPod, pixel, iceBlue, 162f);
                theme.Label(new Rect(squadPod.x + 8f, squadPod.y + 1f, 154f, 16f), "SQUADRON // MANIFEST", theme.Small, false);

                theme.StatusBadge(new Rect(squadPod.x + 14f, squadPod.y + 22f, 244f, 20f),
                    "ALLIED DROP SQUAD // PORT", iceBlue);
                theme.StatusBadge(new Rect(squadPod.x + squadPod.width - 258f, squadPod.y + 22f, 244f, 20f),
                    "HOSTILE BOGEYS // TARGETS", thermalOrange);

                float slotAreaY = squadPod.y + 48f;
                float slotAreaH = canReturnToMenu ? 114f : 164f;
                float halfW = (squadPod.width - 78f) * .5f;
                float miniW = (halfW - 18f) * .25f;

                for (int s = 0; s < 4; s++)
                {
                    HeroId slotHero = s == 0 ? activeHero : AllyLineupFallback[s];
                    Rect slotRect = new Rect(squadPod.x + 14f + s * (miniW + 6f), slotAreaY, miniW, slotAreaH);
                    theme.Panel(slotRect);
                    DrawCornerBrackets(slotRect, s == 0 ? thermalOrange : iceBlue, 10f, 1f);
                    Rect miniPortrait = new Rect(slotRect.x + 4f, slotRect.y + 4f, slotRect.width - 8f, slotAreaH - 30f);
                    StylizedHeroCard.Draw(miniPortrait, slotHero, pixel, now + s * .35f);
                    FrontEndTheme.Fill(new Rect(slotRect.x + 4f, slotRect.y + 4f, slotRect.width - 8f, 2f), s == 0 ? thermalOrange : iceBlue);
                    theme.FitLabel(new Rect(slotRect.x + 6f, slotRect.y + slotAreaH - 23f, slotRect.width - 12f, 19f),
                        s == 0 ? "YOU" : HeroIdentity.NameFor(slotHero), theme.Small, s != 0);
                }

                // Central VS Reticle Node
                Rect vsBadge = new Rect(squadPod.x + 18f + halfW, slotAreaY + (slotAreaH - 42f) * .5f, 42f, 42f);
                theme.Panel(vsBadge);
                DrawCornerBrackets(vsBadge, thermalOrange, 10f, 2f);
                theme.FitLabel(new Rect(vsBadge.x + 6f, vsBadge.y + 10f, vsBadge.width - 12f, 22f), "VS", theme.Small, false);

                float enemyStartX = vsBadge.x + vsBadge.width + 6f;
                for (int s = 0; s < 4; s++)
                {
                    HeroId enemyHero = EnemyLineupFallback[s];
                    Rect slotRect = new Rect(enemyStartX + s * (miniW + 6f), slotAreaY, miniW, slotAreaH);
                    theme.Panel(slotRect);
                    DrawCornerBrackets(slotRect, thermalOrange, 10f, 1f);
                    Rect miniPortrait = new Rect(slotRect.x + 4f, slotRect.y + 4f, slotRect.width - 8f, slotAreaH - 30f);
                    StylizedHeroCard.Draw(miniPortrait, enemyHero, pixel, now + 1.5f + s * .35f);
                    FrontEndTheme.Fill(new Rect(slotRect.x + 4f, slotRect.y + 4f, slotRect.width - 8f, 2f), thermalOrange);
                    theme.FitLabel(new Rect(slotRect.x + 6f, slotRect.y + slotAreaH - 23f, slotRect.width - 12f, 19f),
                        HeroIdentity.NameFor(enemyHero), theme.Small, true);
                }

                return canReturnToMenu && theme.Action(new Rect(squadPod.x + 14f, squadPod.y + 170f, 260f, 44f), "RETURN TO MENU");
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.enabled = oldEnabled;
                GUI.depth = oldDepth;
            }
        }

        private static void DrawCornerBrackets(Rect r, Color c, float len, float thick)
        {
            if (thick > 5.2f && thick < 6.8f) thick = 4f;
            if (len > 5.2f && len < 6.8f) len = 12f;
            Color bracket = new Color(c.r, c.g, c.b, .85f);
            FrontEndTheme.Fill(new Rect(r.x, r.y, len, thick), bracket);
            FrontEndTheme.Fill(new Rect(r.x, r.y, thick, len), bracket);
            FrontEndTheme.Fill(new Rect(r.x + r.width - len, r.y, len, thick), bracket);
            FrontEndTheme.Fill(new Rect(r.x + r.width - thick, r.y, thick, len), bracket);
            FrontEndTheme.Fill(new Rect(r.x, r.y + r.height - thick, len, thick), bracket);
            FrontEndTheme.Fill(new Rect(r.x, r.y + r.height - len, thick, len), bracket);
            FrontEndTheme.Fill(new Rect(r.x + r.width - len, r.y + r.height - thick, len, thick), bracket);
            FrontEndTheme.Fill(new Rect(r.x + r.width - thick, r.y + r.height - len, thick, len), bracket);
        }

        private static void DrawStatMeter(FrontEndTheme theme, Rect row, string label, float ratio, Color barColor)
        {
            theme.Label(new Rect(row.x, row.y, 136f, 16f), label, theme.Small, true);
            Rect barBg = new Rect(row.x + 142f, row.y + 4f, row.width - 142f, 8f);
            FrontEndTheme.Fill(barBg, new Color(.14f, .24f, .36f, .48f));
            FrontEndTheme.Fill(new Rect(barBg.x, barBg.y, barBg.width * Mathf.Clamp01(ratio), 8f),
                new Color(barColor.r, barColor.g, barColor.b, .92f));
        }

        private static HeroId ResolveHero()
        {
            var plan = CoopAuthority.ActivePlan;
            if (plan?.Session != null && plan.Session.IsFull)
            {
                int slot = CoopRosterBuilder.FindLocalHumanSlot(plan);
                if (slot >= 0 && slot < plan.Session.Slots.Length && plan.Session.Slots[slot] != null)
                    return plan.Session.Slots[slot].Hero;
            }
            return PlayerHeroSelection.Selected;
        }
    }
}
