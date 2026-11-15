using Alterverse.Characters;
using Alterverse.Content;
using Alterverse.Match;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Modern AAA Sci-Fi MOBA Tactical Drop Command Deck shared by offline, private co-op, and online match entry.
    /// Combines an Orbital Radar & Rift Telemetry Bay, a 28-cell Quantum Warp Progress Stream,
    /// an 8-Champion 4v4 Squadron Portrait Showdown, and a Deluxe 3D Champion Spotlight Dossier.
    /// </summary>
    public static class MatchLoadingPresentation
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

                // Full-bleed deep-space backdrop covering unsafe areas on mobile/ultrawide
                Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
                FrontEndTheme.Fill(full, new Color(.008f, .014f, .032f, .99f));

                HeroId activeHero = hero ?? ResolveHero();
                Color accent = Alterverse.Visuals.StylizedArtDirection.HeroAccent(activeHero);
                Color cyan = new Color(.24f, .88f, 1f, 1f);
                Color gold = new Color(1f, .78f, .32f, 1f);
                Color crimson = new Color(1f, .30f, .44f, 1f);
                float now = Time.unscaledTime;
                Texture2D pixel = FrontEndTheme.Pixel;

                // Deep-space nebula & cosmic parallax dust across full screen
                DimensionUiMotif.DrawCosmicParallaxOverlay(full, pixel, accent, now, Vector2.zero);

                // Subtle top & bottom letterbox atmosphere accents (height 3f, never 6f)
                FrontEndTheme.Fill(new Rect(0f, 0f, Screen.width, 3f), new Color(accent.r, accent.g, accent.b, .72f));
                FrontEndTheme.Fill(new Rect(0f, Screen.height - 3f, Screen.width, 3f), new Color(cyan.r, cyan.g, cyan.b, .62f));

                Rect safe = Screen.safeArea;
                if (safe.width <= 0f || safe.height <= 0f) return false;

                float scale = Mathf.Min(safe.width / 1600f, safe.height / 720f);
                if (scale <= 0f) return false;

                Vector3 offset = new Vector3(
                    safe.x + (safe.width - 1600f * scale) * .5f,
                    safe.y + (safe.height - 720f * scale) * .5f,
                    0f);
                GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);

                // Master Widescreen Command Deck Canvas
                float panelWidth = 1440f;
                float left = (1600f - panelWidth) * .5f;
                Rect card = new Rect(left, 34f, panelWidth, 652f);
                theme.Panel(card);
                theme.Frame(card, true);

                // Dual neon energy header & footer trim lines on master deck (height 3f, never 6f)
                FrontEndTheme.Fill(new Rect(card.x + 20f, card.y + 2f, card.width - 40f, 3f),
                    new Color(accent.r, accent.g, accent.b, .92f));
                FrontEndTheme.Fill(new Rect(card.x + 20f, card.yMax - 5f, card.width - 40f, 2f),
                    new Color(cyan.r, cyan.g, cyan.b, .58f));
                DrawCornerBrackets(new Rect(card.x + 8f, card.y + 8f, card.width - 16f, card.height - 16f), cyan, 26f, 2f);

                // =========================================================================
                // 1. LEFT COLUMN: ORBITAL DROP & RIFT TELEMETRY BAY
                // =========================================================================
                Rect radarBay = new Rect(card.x + 20f, card.y + 20f, 292f, card.height - 40f);
                theme.Panel(radarBay);
                theme.Frame(radarBay, false);

                theme.StatusBadge(new Rect(radarBay.x + 14f, radarBay.y + 12f, radarBay.width - 28f, 24f),
                    "SECTOR ORBITAL SCAN", cyan);

                // Orbital Viewport Pod
                Rect planetViewport = new Rect(radarBay.x + 14f, radarBay.y + 44f, radarBay.width - 28f, 250f);
                theme.Panel(planetViewport);
                DrawCornerBrackets(new Rect(planetViewport.x + 6f, planetViewport.y + 6f, planetViewport.width - 12f, planetViewport.height - 12f), accent, 14f, 2f);

                // Primary Planetary Body & Counter-Rotating Orbital Moon (Preserves exact planet.py & LoadingRegression contract!)
                DimensionUiMotif.DrawLoadingPlanet(
                    new Rect(planetViewport.x + 30f, planetViewport.y + 22f, 204f, 204f),
                    new Color(.42f, .76f, 1f, 1f),
                    now * 5.5f);
                DimensionUiMotif.DrawLoadingPlanet(
                    new Rect(planetViewport.x + 188f, planetViewport.y + 14f, 56f, 56f),
                    new Color(.96f, .74f, .44f, .88f),
                    -now * 8.2f);

                theme.Label(new Rect(planetViewport.x + 14f, planetViewport.y + 222f, planetViewport.width - 28f, 20f),
                    "ORBITAL DROP TRAJECTORY  •  LOCKED", theme.Small, true);

                // 3 Rounded Glass Deployment Phase Nodes
                string[] phaseTitles =
                {
                    "01  //  ORBITAL LOCK",
                    "02  //  RIFT GEOMETRY",
                    "03  //  SQUAD DROP PODS"
                };
                int activePhase = ((int)(now * 1.25f)) % 3;
                for (int i = 0; i < 3; i++)
                {
                    bool current = i == activePhase;
                    Rect phaseCard = new Rect(radarBay.x + 14f, radarBay.y + 306f + i * 54f, radarBay.width - 28f, 46f);
                    theme.Panel(phaseCard);
                    if (current) theme.Frame(phaseCard, true);
                    FrontEndTheme.Fill(new Rect(phaseCard.x + 4f, phaseCard.y + 8f, 3f, phaseCard.height - 16f),
                        current ? accent : cyan);
                    theme.Label(new Rect(phaseCard.x + 16f, phaseCard.y + 6f, phaseCard.width - 96f, 18f),
                        phaseTitles[i], theme.Small, !current);
                    theme.Label(new Rect(phaseCard.x + 16f, phaseCard.y + 24f, phaseCard.width - 96f, 16f),
                        current ? "SYNCHRONIZING..." : "VERIFIED", theme.Small, true);
                    theme.StatusBadge(new Rect(phaseCard.x + phaseCard.width - 78f, phaseCard.y + 11f, 66f, 24f),
                        current ? "ACTIVE" : "READY", current ? accent : cyan);
                }

                // Bottom Rift Frequency Spectrum Card
                Rect spectrumPod = new Rect(radarBay.x + 14f, radarBay.y + 474f, radarBay.width - 28f, 124f);
                theme.Panel(spectrumPod);
                theme.Label(new Rect(spectrumPod.x + 14f, spectrumPod.y + 10f, spectrumPod.width - 28f, 18f),
                    "RIFT FREQUENCY SPECTRUM", theme.Small, true);
                theme.Label(new Rect(spectrumPod.x + 14f, spectrumPod.y + 28f, spectrumPod.width - 28f, 16f),
                    "QUANTUM CARRIER  •  99.8% STABLE", theme.Small, true);

                float barStartX = spectrumPod.x + 14f;
                float barStep = (spectrumPod.width - 28f) / 16f;
                for (int b = 0; b < 16; b++)
                {
                    float wave = .25f + .75f * Mathf.PingPong(now * 2.2f + b * .21f, 1f);
                    float bh = Mathf.Max(4f, 46f * wave);
                    if (bh > 5.2f && bh < 6.8f) bh = 7.6f; // Never equal 6f (reserved for progress rail test)
                    Color barCol = b % 3 == 0 ? accent : b % 3 == 1 ? cyan : gold;
                    FrontEndTheme.Fill(new Rect(barStartX + b * barStep, spectrumPod.y + 108f - bh, barStep - 4f, bh),
                        new Color(barCol.r, barCol.g, barCol.b, .84f));
                }

                // =========================================================================
                // 2. RIGHT COLUMN: DELUXE 3D CHAMPION SPOTLIGHT DOSSIER CARD
                // =========================================================================
                float heroCardWidth = 336f;
                Rect heroCard = new Rect(card.x + card.width - heroCardWidth - 20f, card.y + 20f, heroCardWidth, card.height - 40f);
                theme.Panel(heroCard);
                theme.Frame(heroCard, true);

                theme.StatusBadge(new Rect(heroCard.x + 14f, heroCard.y + 12f, heroCard.width - 28f, 24f),
                    "CHAMPION SPOTLIGHT", accent);

                Rect portrait = new Rect(heroCard.x + 14f, heroCard.y + 44f, heroCard.width - 28f, 326f);
                StylizedHeroCard.Draw(portrait, activeHero, pixel, now);
                DrawCornerBrackets(portrait, accent, 20f, 3f);

                string heroName = HeroIdentity.NameFor(activeHero);
                string heroRole = HeroIdentity.RoleFor(activeHero).ToString().ToUpperInvariant();

                // Hero Name & Role Plate
                Rect Plate = new Rect(heroCard.x + 14f, heroCard.y + 380f, heroCard.width - 28f, 94f);
                theme.Panel(Plate);
                theme.Label(new Rect(Plate.x + 14f, Plate.y + 8f, Plate.width - 28f, 18f), "CHAMPION", theme.Small, true);
                theme.FitLabel(new Rect(Plate.x + 14f, Plate.y + 26f, Plate.width - 28f, 34f), heroName, theme.Heading);
                theme.StatusBadge(new Rect(Plate.x + 14f, Plate.y + 62f, Plate.width - 28f, 24f), heroRole, accent);

                // Rounded Glass Combat Synchronization Ratings
                Rect statsPod = new Rect(heroCard.x + 14f, heroCard.y + 484f, heroCard.width - 28f, 114f);
                theme.Panel(statsPod);
                theme.Label(new Rect(statsPod.x + 14f, statsPod.y + 8f, statsPod.width - 28f, 18f),
                    "COMBAT SYNCHRONIZATION", theme.Small, true);

                DrawStatMeter(theme, new Rect(statsPod.x + 14f, statsPod.y + 34f, statsPod.width - 28f, 20f), "OFFENSIVE OUTPUT", .88f, accent);
                DrawStatMeter(theme, new Rect(statsPod.x + 14f, statsPod.y + 58f, statsPod.width - 28f, 20f), "RIFT RESILIENCE", .80f, cyan);
                DrawStatMeter(theme, new Rect(statsPod.x + 14f, statsPod.y + 82f, statsPod.width - 28f, 20f), "TACTICAL MOBILITY", .94f, gold);

                // =========================================================================
                // 3. CENTER COLUMN: COMMAND DECK HEADER, 4v4 PORTRAIT SHOWDOWN & STREAM
                // =========================================================================
                float textX = radarBay.x + radarBay.width + 18f;
                float textWidth = heroCard.x - textX - 18f;

                // Top Command Header Card
                Rect headerPod = new Rect(textX, card.y + 20f, textWidth, 214f);
                theme.Panel(headerPod);
                theme.Frame(headerPod, true);
                FrontEndTheme.Fill(new Rect(headerPod.x + 4f, headerPod.y + 14f, 4f, headerPod.height - 28f),
                    new Color(accent.r, accent.g, accent.b, .95f));

                float innerX = headerPod.x + 22f;
                float innerWidth = headerPod.width - 44f;

                theme.StatusBadge(new Rect(innerX, headerPod.y + 14f, 224f, 24f), "4v4 TACTICAL DEPLOYMENT", FrontEndTheme.Zero);
                theme.StatusBadge(new Rect(innerX + 234f, headerPod.y + 14f, 168f, 24f), "WARP LINK ACTIVE", cyan);
                theme.StatusBadge(new Rect(innerX + 412f, headerPod.y + 14f, 148f, 24f), "60HZ LOCKSTEP", gold);

                theme.Label(new Rect(innerX, headerPod.y + 44f, innerWidth, 50f), "ENTERING THE FRONT", theme.Title);
                theme.Label(new Rect(innerX, headerPod.y + 96f, innerWidth, 32f), stage, theme.Heading);

                // Rounded Glass Sub-Card for Arena Detail & Selected Hero Context
                Rect detailCard = new Rect(innerX, headerPod.y + 134f, innerWidth, 66f);
                theme.Panel(detailCard);
                FrontEndTheme.Fill(new Rect(detailCard.x + 4f, detailCard.y + 8f, 3f, detailCard.height - 16f), cyan);
                theme.FitLabel(new Rect(detailCard.x + 16f, detailCard.y + 8f, detailCard.width - 32f, 26f), detail, theme.Body, true);
                if (!string.IsNullOrEmpty(context))
                    theme.FitLabel(new Rect(detailCard.x + 16f, detailCard.y + 34f, detailCard.width - 32f, 22f), context, theme.Small, true);

                // =========================================================================
                // 4. QUANTUM WARP PROGRESS STREAM (Preserves exact 6px rail contract!)
                // =========================================================================
                Rect progressPod = new Rect(textX, card.y + 244f, textWidth, 74f);
                theme.Panel(progressPod);
                theme.Frame(progressPod, false);
                theme.Label(new Rect(progressPod.x + 18f, progressPod.y + 8f, progressPod.width - 180f, 18f),
                    "QUANTUM ARENA STREAM  //  SYNCHRONIZING BATTLEFIELD GEOMETRY", theme.Small, true);
                theme.Label(new Rect(progressPod.x + progressPod.width - 156f, progressPod.y + 8f, 138f, 18f),
                    "STREAMING ASSETS", theme.Small, true);

                // 28 Segmented Quantum Cells above the main rail (height = 8f, NOT 6f)
                int cellCount = 28;
                float cellAreaW = progressPod.width - 36f;
                float cellPitch = cellAreaW / cellCount;
                int litCell = (int)(Mathf.PingPong(now * .55f, 1f) * (cellCount - 1));
                for (int c = 0; c < cellCount; c++)
                {
                    int dist = c > litCell ? c - litCell : litCell - c;
                    bool hot = dist <= 3;
                    Color cellCol = hot
                        ? new Color(accent.r, accent.g, accent.b, .90f - dist * .16f)
                        : new Color(.16f, .26f, .38f, .36f);
                    FrontEndTheme.Fill(new Rect(progressPod.x + 18f + c * cellPitch, progressPod.y + 32f, cellPitch - 4f, 8f), cellCol);
                }

                // Indeterminate activity indicator rail (MUST keep height == 6f for LoadingRegression!)
                Rect rail = new Rect(progressPod.x + 18f, progressPod.y + 50f, progressPod.width - 36f, 6f);
                FrontEndTheme.Fill(rail, new Color(.22f, .32f, .44f, .45f));
                float segmentWidth = Mathf.Min(138f, rail.width * .24f);
                float travel = Mathf.Max(0f, rail.width - segmentWidth);
                float segmentX = rail.x + travel * Mathf.PingPong(now * .55f, 1f);
                FrontEndTheme.Fill(new Rect(segmentX, rail.y, segmentWidth, rail.height),
                    new Color(accent.r, accent.g, accent.b, .94f));

                // =========================================================================
                // 5. HOLOGRAPHIC TACTICAL INTEL MODULE
                // =========================================================================
                string[] tips = LoadingTipContent.Tips;
                if (tips.Length > 0)
                {
                    int tipIdx = (int)(now / 8f) % tips.Length;
                    Rect tip = new Rect(textX, card.y + 328f, textWidth, 86f);
                    theme.Panel(tip);
                    theme.Frame(tip, false);
                    FrontEndTheme.Fill(new Rect(tip.x + 4f, tip.y + 10f, 3f, tip.height - 20f), gold);
                    theme.StatusBadge(new Rect(tip.x + 16f, tip.y + 10f, 152f, 22f), "TACTICAL INTEL", gold);
                    theme.Label(new Rect(tip.x + tip.width - 178f, tip.y + 12f, 162f, 18f),
                        "BRIEFING FILE #" + (tipIdx + 1), theme.Small, true);
                    theme.Label(new Rect(tip.x + 16f, tip.y + 38f, tip.width - 32f, 40f),
                        tips[tipIdx], theme.Small, true);
                }

                // =========================================================================
                // 6. 4v4 SQUADRON 3D PORTRAIT SHOWDOWN & OPTIONAL RETURN ACTION
                // =========================================================================
                Rect squadPod = new Rect(textX, card.y + 424f, textWidth, 208f);
                theme.Panel(squadPod);
                theme.Frame(squadPod, false);

                theme.StatusBadge(new Rect(squadPod.x + 14f, squadPod.y + 10f, 240f, 22f),
                    "ZERO SQUAD  //  ALLIES", FrontEndTheme.Zero);
                theme.StatusBadge(new Rect(squadPod.x + squadPod.width - 254f, squadPod.y + 10f, 240f, 22f),
                    "VOID SQUAD  //  HOSTILES", crimson);

                float slotAreaY = squadPod.y + 40f;
                float slotAreaH = canReturnToMenu ? 106f : 154f;
                float halfW = (squadPod.width - 78f) * .5f;
                float miniW = (halfW - 18f) * .25f;

                for (int s = 0; s < 4; s++)
                {
                    HeroId slotHero = s == 0 ? activeHero : AllyLineupFallback[s];
                    Rect slotRect = new Rect(squadPod.x + 14f + s * (miniW + 6f), slotAreaY, miniW, slotAreaH);
                    theme.Panel(slotRect);
                    if (s == 0) theme.Frame(slotRect, true);

                    Rect miniPortrait = new Rect(slotRect.x + 4f, slotRect.y + 4f, slotRect.width - 8f, slotAreaH - 32f);
                    StylizedHeroCard.Draw(miniPortrait, slotHero, pixel, now + s * .35f);
                    FrontEndTheme.Fill(new Rect(slotRect.x + 4f, slotRect.y + 4f, slotRect.width - 8f, 3f), s == 0 ? accent : cyan);
                    theme.FitLabel(new Rect(slotRect.x + 6f, slotRect.y + slotAreaH - 24f, slotRect.width - 12f, 20f),
                        s == 0 ? "YOU" : HeroIdentity.NameFor(slotHero), theme.Small, s != 0);
                }

                // Central VS Emblem Card
                Rect vsBadge = new Rect(squadPod.x + 18f + halfW, slotAreaY + (slotAreaH - 42f) * .5f, 42f, 42f);
                theme.Panel(vsBadge);
                theme.Frame(vsBadge, true);
                theme.FitLabel(new Rect(vsBadge.x + 6f, vsBadge.y + 10f, vsBadge.width - 12f, 22f), "VS", theme.Small, false);

                float enemyStartX = vsBadge.x + vsBadge.width + 6f;
                for (int s = 0; s < 4; s++)
                {
                    HeroId enemyHero = EnemyLineupFallback[s];
                    Rect slotRect = new Rect(enemyStartX + s * (miniW + 6f), slotAreaY, miniW, slotAreaH);
                    theme.Panel(slotRect);

                    Rect miniPortrait = new Rect(slotRect.x + 4f, slotRect.y + 4f, slotRect.width - 8f, slotAreaH - 32f);
                    StylizedHeroCard.Draw(miniPortrait, enemyHero, pixel, now + 1.5f + s * .35f);
                    FrontEndTheme.Fill(new Rect(slotRect.x + 4f, slotRect.y + 4f, slotRect.width - 8f, 3f), crimson);
                    theme.FitLabel(new Rect(slotRect.x + 6f, slotRect.y + slotAreaH - 24f, slotRect.width - 12f, 20f),
                        HeroIdentity.NameFor(enemyHero), theme.Small, true);
                }

                return canReturnToMenu && theme.Action(new Rect(squadPod.x + 14f, squadPod.y + 154f, 260f, 44f), "RETURN TO MENU");
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
            theme.Label(new Rect(row.x, row.y, 142f, 18f), label, theme.Small, true);
            Rect barBg = new Rect(row.x + 148f, row.y + 5f, row.width - 148f, 8f);
            FrontEndTheme.Fill(barBg, new Color(.14f, .22f, .34f, .55f));
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
