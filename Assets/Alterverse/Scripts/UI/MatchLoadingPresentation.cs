using Alterverse.Characters;
using Alterverse.Content;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Modern AAA sci-fi MOBA tactical drop command deck for online and local match entry.
    /// Callers retain their own lifecycle.
    /// </summary>
    internal static class MatchLoadingPresentation
    {
        public static bool Draw(FrontEndTheme theme, string stage, string detail,
            string context = null, bool canReturnToMenu = false, float opacity = 1f)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            int oldDepth = GUI.depth;
            try
            {
                float alpha = Mathf.Clamp01(opacity);
                GUI.matrix = Matrix4x4.identity;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.enabled = true;
                GUI.depth = -10000;

                // Cover the whole screen, including notches and unsafe areas.
                FrontEndTheme.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(.006f, .011f, .026f, 1f));

                Rect safe = Screen.safeArea;
                if (safe.width <= 0f || safe.height <= 0f) return false;

                float scale = Mathf.Min(safe.height / 720f, safe.width / 1500f);
                float width = safe.width / scale;
                float y = Screen.height - safe.yMax + (safe.height - 720f * scale) * .5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0f), Quaternion.identity, Vector3.one * scale);

                HeroId activeHero = PlayerHeroSelection.Selected;
                Color accent = Alterverse.Visuals.StylizedArtDirection.HeroAccent(activeHero);
                Color cyan = new Color(.22f, .86f, 1f, 1f);
                Color gold = new Color(1f, .76f, .28f, 1f);
                Color crimson = new Color(.96f, .28f, .38f, 1f);
                Texture2D pixel = FrontEndTheme.Pixel;
                float now = Time.unscaledTime;

                // Smooth starlight and cosmic nebula backdrop.
                DimensionUiMotif.DrawCosmicParallaxOverlay(new Rect(0f, 0f, width, 720f), pixel,
                    accent, now, Vector2.zero);

                // Subtle atmospheric cyber-grid scanlines & top/bottom letterbox framing (no height == 6f!).
                FrontEndTheme.Fill(new Rect(0f, 0f, width, 36f), new Color(.012f, .022f, .048f, .92f));
                FrontEndTheme.Fill(new Rect(0f, 36f, width, 2f), new Color(accent.r, accent.g, accent.b, .55f));
                FrontEndTheme.Fill(new Rect(0f, 684f, width, 36f), new Color(.012f, .022f, .048f, .92f));
                FrontEndTheme.Fill(new Rect(0f, 682f, width, 2f), new Color(cyan.r, cyan.g, cyan.b, .42f));

                float panelWidth = Mathf.Min(1380f, width - 56f);
                float left = (width - panelWidth) * .5f;

                // Top & bottom cinematic telemetry readouts.
                theme.Label(new Rect(left, 8f, 420f, 20f), "DIMENSIONS FRONT  //  ORBITAL INSERTION PROTOCOL", theme.Small, true);
                theme.Label(new Rect(left + panelWidth - 360f, 8f, 360f, 20f), "QUANTUM UPLINK: ACTIVE  •  60 HZ TICK", theme.Small, true);
                theme.Label(new Rect(left, 690f, 520f, 20f), "SECTOR: RIFT CONVERGENCE  •  SQUADRON TRANSIT LOCKED", theme.Small, true);

                // Main Widescreen Command Deck Shell
                Rect shell = new Rect(left, 50f, panelWidth, 616f);
                theme.Panel(shell);
                theme.Frame(shell, false);

                // Glowing top & left accent energy rails on the command deck
                FrontEndTheme.Fill(new Rect(shell.x, shell.y, shell.width, 3f), new Color(accent.r, accent.g, accent.b, .88f));
                FrontEndTheme.Fill(new Rect(shell.x, shell.y, 4f, shell.height), new Color(accent.r, accent.g, accent.b, .92f));
                FrontEndTheme.Fill(new Rect(shell.x + shell.width - 3f, shell.y, 3f, shell.height), new Color(cyan.r, cyan.g, cyan.b, .45f));

                // =========================================================================
                // 1. LEFT COLUMN: ORBITAL PLANETARY RADAR & DROP TELEMETRY BAY
                // =========================================================================
                float radarWidth = 248f;
                Rect radarBay = new Rect(left + 18f, 68f, radarWidth, 580f);
                theme.Panel(radarBay);
                theme.Frame(radarBay, false);
                FrontEndTheme.Fill(new Rect(radarBay.x, radarBay.y, radarBay.width, 28f), new Color(.035f, .065f, .125f, .90f));
                FrontEndTheme.Fill(new Rect(radarBay.x, radarBay.y + 28f, radarBay.width, 2f), new Color(cyan.r, cyan.g, cyan.b, .65f));
                theme.Label(new Rect(radarBay.x + 12f, radarBay.y + 5f, radarBay.width - 24f, 18f), "ORBITAL DROP RADAR", theme.Small, true);

                // Primary planetary hologram well
                Rect planetWell = new Rect(radarBay.x + 14f, radarBay.y + 42f, radarBay.width - 28f, 256f);
                FrontEndTheme.Fill(planetWell, new Color(.008f, .016f, .036f, .88f));
                DrawCornerBrackets(planetWell, cyan, 14f, 2f);

                // Crosshair ticks around the primary planet
                float wellCx = planetWell.x + planetWell.width * .5f;
                float wellCy = planetWell.y + 112f;
                FrontEndTheme.Fill(new Rect(planetWell.x + 10f, wellCy, 18f, 1f), new Color(cyan.r, cyan.g, cyan.b, .45f));
                FrontEndTheme.Fill(new Rect(planetWell.x + planetWell.width - 28f, wellCy, 18f, 1f), new Color(cyan.r, cyan.g, cyan.b, .45f));
                FrontEndTheme.Fill(new Rect(wellCx, planetWell.y + 10f, 1f, 16f), new Color(cyan.r, cyan.g, cyan.b, .45f));

                // Rotating Planet #1 (Primary Drop World) & Planet #2 (Orbital Relay Moon)
                DimensionUiMotif.DrawLoadingPlanet(new Rect(wellCx - 66f, wellCy - 66f, 132f, 132f),
                    new Color(.42f, .82f, 1f, 1f), now * 10f);
                DimensionUiMotif.DrawLoadingPlanet(new Rect(wellCx + 28f, wellCy + 34f, 68f, 68f),
                    new Color(1f, .72f, .36f, 1f), -now * 13f);

                theme.Label(new Rect(planetWell.x + 10f, planetWell.y + 206f, planetWell.width - 20f, 18f), "TARGET: ACTIVE BATTLE RIFT", theme.Small, true);
                theme.Label(new Rect(planetWell.x + 10f, planetWell.y + 226f, planetWell.width - 20f, 18f), "ORBITAL LOCK  •  04.82 AU", theme.Small, true);

                // Live Telemetry & Signal Spectrum inside Radar Bay
                Rect telemetryBox = new Rect(radarBay.x + 14f, radarBay.y + 310f, radarBay.width - 28f, 254f);
                theme.Panel(telemetryBox);
                FrontEndTheme.Fill(new Rect(telemetryBox.x, telemetryBox.y, 3f, telemetryBox.height), new Color(accent.r, accent.g, accent.b, .85f));
                theme.Label(new Rect(telemetryBox.x + 12f, telemetryBox.y + 10f, telemetryBox.width - 24f, 18f), "DEPLOYMENT TELEMETRY", theme.Small, true);

                string[] phaseNames = { "01 // ORBITAL LOCK", "02 // ARENA SYNTHESIS", "03 // SQUADRON LINK", "04 // DROP READY" };
                int activePhase = (int)(now * 1.2f) % 4;
                for (int i = 0; i < 4; i++)
                {
                    float py = telemetryBox.y + 38f + i * 38f;
                    bool current = i == activePhase;
                    Rect phaseRect = new Rect(telemetryBox.x + 10f, py, telemetryBox.width - 20f, 30f);
                    FrontEndTheme.Fill(phaseRect, current
                        ? new Color(accent.r * .28f, accent.g * .28f, accent.b * .28f, .85f)
                        : new Color(.02f, .04f, .08f, .70f));
                    FrontEndTheme.Fill(new Rect(phaseRect.x, phaseRect.y, 3f, phaseRect.height),
                        current ? accent : new Color(cyan.r, cyan.g, cyan.b, .45f));
                    theme.Label(new Rect(phaseRect.x + 10f, phaseRect.y + 5f, phaseRect.width - 16f, 20f),
                        phaseNames[i], theme.Small, !current);
                }

                // Animated Waveform Bars at bottom of Radar Bay
                theme.Label(new Rect(telemetryBox.x + 12f, telemetryBox.y + 196f, telemetryBox.width - 24f, 16f), "RIFT FREQUENCY SPECTRUM", theme.Small, true);
                float barStartX = telemetryBox.x + 12f;
                float barStep = (telemetryBox.width - 24f) / 14f;
                for (int b = 0; b < 14; b++)
                {
                    float wave = .25f + .75f * Mathf.PingPong(now * 2.1f + b * .23f, 1f);
                    float bh = Mathf.Max(4f, 24f * wave);
                    if (bh > 5.2f && bh < 6.8f) bh = 7.5f; // Never equal 6f (reserved for progress rail test)
                    FrontEndTheme.Fill(new Rect(barStartX + b * barStep, telemetryBox.y + 240f - bh, barStep - 3f, bh),
                        b % 2 == 0 ? new Color(cyan.r, cyan.g, cyan.b, .78f) : new Color(accent.r, accent.g, accent.b, .78f));
                }

                // =========================================================================
                // 2. RIGHT COLUMN: DELUXE CHAMPION SPOTLIGHT DOSSIER CARD
                // =========================================================================
                float heroCardWidth = 316f;
                Rect heroCard = new Rect(left + panelWidth - heroCardWidth - 18f, 68f, heroCardWidth, 580f);
                theme.Panel(heroCard);
                theme.Frame(heroCard, true);
                FrontEndTheme.Fill(new Rect(heroCard.x, heroCard.y, heroCard.width, 28f),
                    new Color(accent.r * .32f, accent.g * .32f, accent.b * .32f, .92f));
                FrontEndTheme.Fill(new Rect(heroCard.x, heroCard.y + 28f, heroCard.width, 2f),
                    new Color(accent.r, accent.g, accent.b, .92f));
                theme.Label(new Rect(heroCard.x + 14f, heroCard.y + 5f, heroCard.width - 28f, 18f), "CHAMPION SPOTLIGHT  //  LOCKED", theme.Small, false);

                Rect portrait = new Rect(heroCard.x + 14f, heroCard.y + 40f, heroCard.width - 28f, 312f);
                StylizedHeroCard.Draw(portrait, activeHero, pixel, now);
                DrawCornerBrackets(portrait, accent, 18f, 3f);

                string heroName = HeroIdentity.NameFor(activeHero);
                string heroRole = HeroIdentity.RoleFor(activeHero).ToString().ToUpperInvariant();

                theme.Label(new Rect(heroCard.x + 16f, heroCard.y + 360f, heroCard.width - 32f, 18f), "CHAMPION", theme.Small, true);
                theme.FitLabel(new Rect(heroCard.x + 16f, heroCard.y + 380f, heroCard.width - 32f, 34f), heroName, theme.Heading);
                theme.StatusBadge(new Rect(heroCard.x + 16f, heroCard.y + 420f, heroCard.width - 32f, 26f), heroRole, accent);

                // Tactical Attribute Readiness Bars inside Champion Card
                Rect statsPod = new Rect(heroCard.x + 14f, heroCard.y + 456f, heroCard.width - 28f, 110f);
                FrontEndTheme.Fill(statsPod, new Color(.012f, .022f, .045f, .88f));
                FrontEndTheme.Fill(new Rect(statsPod.x, statsPod.y, 3f, statsPod.height), new Color(gold.r, gold.g, gold.b, .85f));
                theme.Label(new Rect(statsPod.x + 12f, statsPod.y + 8f, statsPod.width - 24f, 16f), "COMBAT SYNCHRONIZATION", theme.Small, true);

                DrawStatMeter(theme, new Rect(statsPod.x + 12f, statsPod.y + 30f, statsPod.width - 24f, 20f), "OFFENSIVE OUTPUT", .86f, accent);
                DrawStatMeter(theme, new Rect(statsPod.x + 12f, statsPod.y + 54f, statsPod.width - 24f, 20f), "RIFT RESILIENCE", .78f, cyan);
                DrawStatMeter(theme, new Rect(statsPod.x + 12f, statsPod.y + 78f, statsPod.width - 24f, 20f), "TACTICAL MOBILITY", .92f, gold);

                // =========================================================================
                // 3. CENTER COLUMN: COMMAND DECK HEADER, QUANTUM STREAM & 4v4 ROSTER
                // =========================================================================
                float textX = radarBay.x + radarBay.width + 20f;
                float textWidth = heroCard.x - textX - 20f;

                // Top Command Header Panel
                Rect headerPod = new Rect(textX, 68f, textWidth, 232f);
                theme.Panel(headerPod);
                FrontEndTheme.Fill(new Rect(headerPod.x, headerPod.y, 5f, headerPod.height), new Color(accent.r, accent.g, accent.b, .95f));
                FrontEndTheme.Fill(new Rect(headerPod.x, headerPod.y, headerPod.width, 2f), new Color(accent.r, accent.g, accent.b, .65f));

                float innerX = headerPod.x + 22f;
                float innerWidth = headerPod.width - 44f;

                theme.StatusBadge(new Rect(innerX, headerPod.y + 16f, 228f, 26f), "4v4 TACTICAL DEPLOYMENT", FrontEndTheme.Zero);
                theme.StatusBadge(new Rect(innerX + 238f, headerPod.y + 16f, 156f, 26f), "WARP LINK ACTIVE", cyan);

                theme.Label(new Rect(innerX, headerPod.y + 50f, innerWidth, 54f), "ENTERING THE FRONT", theme.Title);
                theme.Label(new Rect(innerX, headerPod.y + 108f, innerWidth, 34f), stage, theme.Heading);

                // Structured Telemetry Strip for Detail & Context
                Rect detailStrip = new Rect(innerX, headerPod.y + 148f, innerWidth, 68f);
                FrontEndTheme.Fill(detailStrip, new Color(.014f, .028f, .058f, .88f));
                FrontEndTheme.Fill(new Rect(detailStrip.x, detailStrip.y, 3f, detailStrip.height), new Color(cyan.r, cyan.g, cyan.b, .90f));
                theme.FitLabel(new Rect(detailStrip.x + 14f, detailStrip.y + 8f, detailStrip.width - 28f, 28f), detail, theme.Body, true);
                if (!string.IsNullOrEmpty(context))
                    theme.FitLabel(new Rect(detailStrip.x + 14f, detailStrip.y + 36f, detailStrip.width - 28f, 24f), context, theme.Small, true);

                // =========================================================================
                // 4. QUANTUM WARP PROGRESS STREAM (Preserves exact 6px rail contract!)
                // =========================================================================
                Rect progressPod = new Rect(textX, 312f, textWidth, 78f);
                theme.Panel(progressPod);
                theme.Label(new Rect(progressPod.x + 18f, progressPod.y + 10f, progressPod.width - 36f, 18f),
                    "QUANTUM ARENA STREAM  //  SYNCHRONIZING BATTLEFIELD GEOMETRY", theme.Small, true);

                // Segmented quantum cells above the main rail (height = 8f, NOT 6f)
                int cellCount = 24;
                float cellAreaW = progressPod.width - 36f;
                float cellPitch = cellAreaW / cellCount;
                int litCell = (int)(Mathf.PingPong(now * .55f, 1f) * (cellCount - 1));
                for (int c = 0; c < cellCount; c++)
                {
                    int dist = c > litCell ? c - litCell : litCell - c;
                    bool hot = dist <= 3;
                    Color cellCol = hot
                        ? new Color(accent.r, accent.g, accent.b, .85f - dist * .16f)
                        : new Color(.16f, .26f, .38f, .38f);
                    FrontEndTheme.Fill(new Rect(progressPod.x + 18f + c * cellPitch, progressPod.y + 34f, cellPitch - 4f, 8f), cellCol);
                }

                // Indeterminate activity indicator rail (MUST keep height == 6f for LoadingRegression!)
                Rect rail = new Rect(progressPod.x + 18f, progressPod.y + 52f, progressPod.width - 36f, 6f);
                FrontEndTheme.Fill(rail, new Color(.22f, .32f, .44f, .45f));
                float segmentWidth = Mathf.Min(130f, rail.width * .24f);
                float travel = Mathf.Max(0f, rail.width - segmentWidth);
                float segmentX = rail.x + travel * Mathf.PingPong(now * .55f, 1f);
                FrontEndTheme.Fill(new Rect(segmentX, rail.y, segmentWidth, rail.height),
                    new Color(accent.r, accent.g, accent.b, .92f));

                // =========================================================================
                // 5. HOLOGRAPHIC TACTICAL INTEL MODULE
                // =========================================================================
                string[] tips = LoadingTipContent.Tips;
                if (tips.Length > 0)
                {
                    int tipIdx = (int)(now / 8f) % tips.Length;
                    Rect tip = new Rect(textX, 402f, textWidth, 96f);
                    theme.Panel(tip);
                    theme.Frame(tip, false);
                    FrontEndTheme.Fill(new Rect(tip.x, tip.y, 4f, tip.height), new Color(gold.r, gold.g, gold.b, .92f));
                    FrontEndTheme.Fill(new Rect(tip.x + 4f, tip.y, tip.width - 4f, 26f), new Color(.035f, .055f, .095f, .85f));
                    theme.Label(new Rect(tip.x + 16f, tip.y + 4f, 220f, 18f), "TACTICAL INTEL", theme.Small, true);
                    theme.Label(new Rect(tip.x + tip.width - 190f, tip.y + 4f, 174f, 18f),
                        "BRIEFING FILE #" + (tipIdx + 1), theme.Small, true);
                    theme.Label(new Rect(tip.x + 16f, tip.y + 34f, tip.width - 32f, 52f),
                        tips[tipIdx], theme.Small, true);
                }

                // =========================================================================
                // 6. 4v4 SQUADRON STRIKE TELEMETRY & OPTIONAL RETURN ACTION
                // =========================================================================
                Rect squadPod = new Rect(textX, 510f, textWidth, 138f);
                theme.Panel(squadPod);
                FrontEndTheme.Fill(new Rect(squadPod.x, squadPod.y, squadPod.width, 24f), new Color(.022f, .042f, .082f, .90f));
                theme.Label(new Rect(squadPod.x + 14f, squadPod.y + 3f, 280f, 18f), "ZERO SQUAD  //  ALLIED STRIKE FORCE", theme.Small, true);
                theme.Label(new Rect(squadPod.x + squadPod.width - 240f, squadPod.y + 3f, 226f, 18f), "VOID SQUAD  //  OPPOSING FORCE", theme.Small, true);

                float slotAreaY = squadPod.y + 32f;
                float slotAreaH = canReturnToMenu ? 46f : 94f;
                float halfW = (squadPod.width - 76f) * .5f;
                float allyCardW = (halfW - 18f) * .25f;

                string[] allyTags = { "YOU • " + heroName.ToUpperInvariant(), "ALLY 02 • VANGUARD", "ALLY 03 • SUPPORT", "ALLY 04 • STRIKER" };
                for (int s = 0; s < 4; s++)
                {
                    Rect slotRect = new Rect(squadPod.x + 12f + s * (allyCardW + 6f), slotAreaY, allyCardW, slotAreaH);
                    bool isYou = s == 0;
                    FrontEndTheme.Fill(slotRect, isYou
                        ? new Color(accent.r * .25f, accent.g * .25f, accent.b * .25f, .90f)
                        : new Color(.016f, .034f, .068f, .85f));
                    FrontEndTheme.Fill(new Rect(slotRect.x, slotRect.y, slotRect.width, 3f), isYou ? accent : cyan);
                    theme.FitLabel(new Rect(slotRect.x + 6f, slotRect.y + 6f, slotRect.width - 12f, 18f), allyTags[s], theme.Small, !isYou);
                    if (!canReturnToMenu)
                        theme.FitLabel(new Rect(slotRect.x + 6f, slotRect.y + slotAreaH - 24f, slotRect.width - 12f, 18f),
                            isYou ? "LOCKED" : "SYNCED", theme.Small, true);
                }

                // Central VS Badge
                Rect vsBadge = new Rect(squadPod.x + 16f + halfW, slotAreaY + (slotAreaH - 28f) * .5f, 44f, 28f);
                FrontEndTheme.Fill(vsBadge, new Color(gold.r * .25f, gold.g * .25f, gold.b * .25f, .92f));
                FrontEndTheme.Fill(new Rect(vsBadge.x, vsBadge.y, vsBadge.width, 2f), gold);
                theme.FitLabel(new Rect(vsBadge.x + 6f, vsBadge.y + 4f, vsBadge.width - 12f, 20f), "VS", theme.Small, false);

                float enemyStartX = vsBadge.x + vsBadge.width + 8f;
                string[] enemyTags = { "HOSTILE 01", "HOSTILE 02", "HOSTILE 03", "HOSTILE 04" };
                for (int s = 0; s < 4; s++)
                {
                    Rect slotRect = new Rect(enemyStartX + s * (allyCardW + 6f), slotAreaY, allyCardW, slotAreaH);
                    FrontEndTheme.Fill(slotRect, new Color(.048f, .016f, .028f, .85f));
                    FrontEndTheme.Fill(new Rect(slotRect.x, slotRect.y, slotRect.width, 3f), crimson);
                    theme.FitLabel(new Rect(slotRect.x + 6f, slotRect.y + 6f, slotRect.width - 12f, 18f), enemyTags[s], theme.Small, true);
                    if (!canReturnToMenu)
                        theme.FitLabel(new Rect(slotRect.x + 6f, slotRect.y + slotAreaH - 24f, slotRect.width - 12f, 18f),
                            "DETECTED", theme.Small, true);
                }

                return canReturnToMenu && theme.Action(new Rect(squadPod.x + 12f, squadPod.y + 84f, 260f, 46f), "RETURN TO MENU");
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
            theme.Label(new Rect(row.x, row.y, 138f, 18f), label, theme.Small, true);
            Rect barBg = new Rect(row.x + 144f, row.y + 5f, row.width - 144f, 8f);
            FrontEndTheme.Fill(barBg, new Color(.14f, .22f, .34f, .55f));
            FrontEndTheme.Fill(new Rect(barBg.x, barBg.y, barBg.width * Mathf.Clamp01(ratio), 8f),
                new Color(barColor.r, barColor.g, barColor.b, .92f));
        }
    }
}
