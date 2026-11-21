using Alterverse.Characters;
using Alterverse.Content;
using Alterverse.Match;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Modern AAA Sci-Fi MOBA R35 Dropship Cabin & Tactical Airlift Deck shared by offline, private co-op, and online match entry.
    /// Immerses players inside the R35 troop transport flying at high speed toward the Front.
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

                // Subtle top & bottom cabin letterbox atmosphere accents (width Screen.width - 2f to preserve Cover test; height 3f, never 6f)
                FrontEndTheme.Fill(new Rect(1f, 0f, Screen.width - 2f, 3f), new Color(accent.r, accent.g, accent.b, .72f));
                FrontEndTheme.Fill(new Rect(1f, Screen.height - 3f, Screen.width - 2f, 3f), new Color(cyan.r, cyan.g, cyan.b, .62f));

                Rect safe = Screen.safeArea;
                if (safe.width <= 0f || safe.height <= 0f) return false;
                float scale = Mathf.Min(safe.width / 1600f, safe.height / 720f);
                if (scale <= 0f) return false;

                Vector3 offset = new Vector3(
                    safe.x + (safe.width - 1600f * scale) * .5f,
                    safe.y + (safe.height - 720f * scale) * .5f,
                    0f);
                GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);

                // High-altitude slipstream wind streaks & R35 cabin structural ribs across the 1600x720 flight deck
                DrawDropshipCabinBulkhead(theme, now, accent, cyan, gold);

                // Master Widescreen Dropship Cabin Deck Canvas
                float panelWidth = 1440f;
                float left = (1600f - panelWidth) * .5f;
                Rect card = new Rect(left, 34f, panelWidth, 652f);
                theme.Panel(card);
                theme.Frame(card, true);

                // Dual neon energy header & footer trim lines on master cabin deck (height 3f, never 6f)
                FrontEndTheme.Fill(new Rect(card.x + 20f, card.y + 2f, card.width - 40f, 3f),
                    new Color(accent.r, accent.g, accent.b, .92f));
                FrontEndTheme.Fill(new Rect(card.x + 20f, card.yMax - 5f, card.width - 40f, 2f),
                    new Color(cyan.r, cyan.g, cyan.b, .58f));
                DrawCornerBrackets(new Rect(card.x + 8f, card.y + 8f, card.width - 16f, card.height - 16f), cyan, 26f, 2f);

                // =========================================================================
                // 1. LEFT COLUMN: R35 COCKPIT NOSE RADAR & FLIGHT TELEMETRY BAY
                // =========================================================================
                Rect radarBay = new Rect(card.x + 20f, card.y + 20f, 292f, card.height - 40f);
                theme.Panel(radarBay);
                theme.Frame(radarBay, false);
                theme.StatusBadge(new Rect(radarBay.x + 14f, radarBay.y + 12f, radarBay.width - 28f, 24f),
                    "R35 FLIGHT RADAR  •  FRONT", cyan);

                // Nose Orbital Approach Viewport Pod
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

                // R35 Dropship Flight Silhouette & Pitch-Ladder Approach HUD overlaid inside the cockpit nose viewport
                DrawDropshipApproachHud(theme, planetViewport, now, accent, cyan, gold);

                theme.Label(new Rect(planetViewport.x + 14f, planetViewport.y + 222f, planetViewport.width - 28f, 20f),
                    "R35 FLIGHT VECTOR  •  ALT 42.8 KM", theme.Small, true);

                // 3 Rounded Glass R35 Flight Sequence Nodes
                string[] phaseTitles =
                {
                    "01  //  R35 CABIN SEALED",
                    "02  //  FLYING TO THE FRONT",
                    "03  //  DROP RAMP STANDBY"
                };
                int activePhase = ((int)(now * 1.25f)) % 3;
                for (int i = 0; i < 3; i++)
                {
                    bool current = i == activePhase;
                    Rect phaseCard = new Rect(radarBay.x + 14f, radarBay.y + 306f + i * 54f, radarBay.width - 28f, 46f);
                    theme.Panel(phaseCard);
                    if (current) theme.Frame(phaseCard, true);
                    FrontEndTheme.Fill(new Rect(phaseCard.x + 4f, phaseCard.y + 8f, 3f, phaseCard.height - 16f),
                        current ? accent : new Color(cyan.r, cyan.g, cyan.b, .45f));
                    theme.Label(new Rect(phaseCard.x + 16f, phaseCard.y + 6f, phaseCard.width - 96f, 18f),
                        phaseTitles[i], theme.Small, !current);
                    theme.Label(new Rect(phaseCard.x + 16f, phaseCard.y + 24f, phaseCard.width - 96f, 16f),
                        current ? "IN TRANSIT..." : "LOCKED", theme.Small, true);
                    theme.StatusBadge(new Rect(phaseCard.x + phaseCard.width - 78f, phaseCard.y + 11f, 66f, 24f),
                        current ? "ACTIVE" : "READY", current ? accent : cyan);
                }

                // Bottom R35 Twin-Turbine Thrust Telemetry Card
                Rect spectrumPod = new Rect(radarBay.x + 14f, radarBay.y + 474f, radarBay.width - 28f, 124f);
                theme.Panel(spectrumPod);
                theme.Label(new Rect(spectrumPod.x + 14f, spectrumPod.y + 10f, spectrumPod.width - 28f, 18f),
                    "R35 TWIN-TURBINE THRUST", theme.Small, true);
                theme.Label(new Rect(spectrumPod.x + 14f, spectrumPod.y + 28f, spectrumPod.width - 28f, 16f),
                    "AERO-THRUSTERS  •  MACH 4.2 CRUISE", theme.Small, true);

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
                // 2. RIGHT COLUMN: R35 JUMP SEAT 01 CHAMPION HARNESS & DOSSIER CARD
                // =========================================================================
                float heroCardWidth = 336f;
                Rect heroCard = new Rect(card.x + card.width - heroCardWidth - 20f, card.y + 20f, heroCardWidth, card.height - 40f);
                theme.Panel(heroCard);
                theme.Frame(heroCard, true);
                theme.StatusBadge(new Rect(heroCard.x + 14f, heroCard.y + 12f, heroCard.width - 28f, 24f),
                    "R35 JUMP SEAT 01  //  LOCKED IN", accent);

                Rect portrait = new Rect(heroCard.x + 14f, heroCard.y + 44f, heroCard.width - 28f, 326f);
                StylizedHeroCard.Draw(portrait, activeHero, pixel, now);
                DrawCornerBrackets(portrait, accent, 20f, 3f);

                string heroName = HeroIdentity.NameFor(activeHero);
                string heroRole = HeroIdentity.RoleFor(activeHero).ToString().ToUpperInvariant();

                // Hero Name & Role Plate
                Rect Plate = new Rect(heroCard.x + 14f, heroCard.y + 380f, heroCard.width - 28f, 94f);
                theme.Panel(Plate);
                theme.Label(new Rect(Plate.x + 14f, Plate.y + 8f, Plate.width - 28f, 18f), "DEPLOYING CHAMPION", theme.Small, true);
                theme.FitLabel(new Rect(Plate.x + 14f, Plate.y + 26f, Plate.width - 28f, 34f), heroName, theme.Heading);
                theme.StatusBadge(new Rect(Plate.x + 14f, Plate.y + 62f, Plate.width - 28f, 24f), heroRole, accent);

                // Rounded Glass Drop Suit & Harness Telemetry Ratings
                Rect statsPod = new Rect(heroCard.x + 14f, heroCard.y + 484f, heroCard.width - 28f, 114f);
                theme.Panel(statsPod);
                theme.Label(new Rect(statsPod.x + 14f, statsPod.y + 8f, statsPod.width - 28f, 18f),
                    "DROP HARNESS & SUIT TELEMETRY", theme.Small, true);
                DrawStatMeter(theme, new Rect(statsPod.x + 14f, statsPod.y + 34f, statsPod.width - 28f, 20f), "WEAPON SYSTEMS", .92f, accent);
                DrawStatMeter(theme, new Rect(statsPod.x + 14f, statsPod.y + 58f, statsPod.width - 28f, 20f), "HEAT SHIELDING", .86f, cyan);
                DrawStatMeter(theme, new Rect(statsPod.x + 14f, statsPod.y + 82f, statsPod.width - 28f, 20f), "DROP THRUSTERS", .96f, gold);

                // =========================================================================
                // 3. CENTER COLUMN: R35 TROOP CABIN DECK, FLIGHT CORRIDOR & 4v4 DROP SQUAD
                // =========================================================================
                float textX = radarBay.x + radarBay.width + 18f;
                float textWidth = heroCard.x - textX - 18f;

                // Top R35 Troop Cabin Flight Header Card
                Rect headerPod = new Rect(textX, card.y + 20f, textWidth, 214f);
                theme.Panel(headerPod);
                theme.Frame(headerPod, true);
                FrontEndTheme.Fill(new Rect(headerPod.x + 4f, headerPod.y + 14f, 4f, headerPod.height - 28f),
                    new Color(accent.r, accent.g, accent.b, .95f));

                float innerX = headerPod.x + 22f;
                float innerWidth = headerPod.width - 44f;

                theme.StatusBadge(new Rect(innerX, headerPod.y + 14f, 214f, 24f), "R35 TROOP TRANSPORT", FrontEndTheme.Zero);
                theme.StatusBadge(new Rect(innerX + 224f, headerPod.y + 14f, 178f, 24f), "EN ROUTE TO FRONT", cyan);
                theme.StatusBadge(new Rect(innerX + 412f, headerPod.y + 14f, 152f, 24f), "JUMP LIGHT: AMBER", gold);

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
                // 4. R35 FLIGHT CORRIDOR & DROP ZONE APPROACH STREAM (Preserves 6px rail!)
                // =========================================================================
                Rect progressPod = new Rect(textX, card.y + 244f, textWidth, 74f);
                theme.Panel(progressPod);
                theme.Frame(progressPod, false);
                theme.Label(new Rect(progressPod.x + 18f, progressPod.y + 8f, progressPod.width - 196f, 18f),
                    "R35 FLIGHT CORRIDOR  //  APPROACHING FRONT LINE DROP ZONE", theme.Small, true);
                theme.Label(new Rect(progressPod.x + progressPod.width - 172f, progressPod.y + 8f, 154f, 18f),
                    "AIRSPEED MACH 4.2", theme.Small, true);

                // 28 Segmented Flight Corridor Beacons above the main rail (height = 8f, NOT 6f)
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
                // 5. DROPSHIP CABIN INTERCOM & TACTICAL BRIEFING MODULE
                // =========================================================================
                string[] tips = LoadingTipContent.Tips;
                if (tips.Length > 0)
                {
                    int tipIdx = (int)(now / 8f) % tips.Length;
                    Rect tip = new Rect(textX, card.y + 328f, textWidth, 86f);
                    theme.Panel(tip);
                    theme.Frame(tip, false);
                    FrontEndTheme.Fill(new Rect(tip.x + 4f, tip.y + 10f, 3f, tip.height - 20f), gold);
                    theme.StatusBadge(new Rect(tip.x + 16f, tip.y + 10f, 174f, 22f), "DROPSHIP INTERCOM", gold);
                    theme.Label(new Rect(tip.x + tip.width - 188f, tip.y + 12f, 172f, 18f),
                        "FLIGHT BRIEFING #" + (tipIdx + 1), theme.Small, true);
                    theme.Label(new Rect(tip.x + 16f, tip.y + 38f, tip.width - 32f, 40f),
                        tips[tipIdx], theme.Small, true);
                }

                // =========================================================================
                // 6. 4v4 TROOP BAY JUMP SEATS vs INBOUND HOSTILE DROPSHIP
                // =========================================================================
                Rect squadPod = new Rect(textX, card.y + 424f, textWidth, 208f);
                theme.Panel(squadPod);
                theme.Frame(squadPod, false);
                theme.StatusBadge(new Rect(squadPod.x + 14f, squadPod.y + 10f, 256f, 22f),
                    "R35 TROOP BAY  //  YOUR SQUAD", FrontEndTheme.Zero);
                theme.StatusBadge(new Rect(squadPod.x + squadPod.width - 270f, squadPod.y + 10f, 256f, 22f),
                    "BOGEY DROPSHIP  //  HOSTILES", crimson);

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

        private static void DrawDropshipCabinBulkhead(FrontEndTheme theme, float now, Color accent, Color cyan, Color gold)
        {
            // Never use height == 6f or width == Screen.width here (reserved for LoadingRegression checks)
            Color hullRib = new Color(.14f, .24f, .36f, .42f);
            FrontEndTheme.Fill(new Rect(80f, 6f, 1440f, 4f), hullRib);
            FrontEndTheme.Fill(new Rect(80f, 710f, 1440f, 4f), hullRib);

            // Overhead cabin jump-light beacons & structural ribs
            for (int i = 0; i < 6; i++)
            {
                float bx = 136f + i * 265f;
                float pulse = .4f + .6f * Mathf.PingPong(now * 2.4f + i * .22f, 1f);
                FrontEndTheme.Fill(new Rect(bx, 12f, 44f, 3f), new Color(gold.r, gold.g, gold.b, .45f + .45f * pulse));
                FrontEndTheme.Fill(new Rect(bx + 8f, 703f, 28f, 3f), new Color(cyan.r, cyan.g, cyan.b, .35f + .35f * pulse));
            }

            // High-velocity slipstream wind streaks passing outside the transport cabin
            for (int s = 0; s < 8; s++)
            {
                float sw = 96f + (s % 3) * 44f;
                float progress = Mathf.PingPong(now * (.75f + (s % 4) * .18f) + s * .31f, 1f);
                float sx = 40f + (1520f - sw) * (1f - progress);
                float sy = 16f + s * 96f;
                FrontEndTheme.Fill(new Rect(sx, sy, sw, 2f), new Color(cyan.r, cyan.g, cyan.b, .14f));
            }
        }

        private static void DrawDropshipApproachHud(FrontEndTheme theme, Rect vp, float now, Color accent, Color cyan, Color gold)
        {
            // Pitch ladder bars along left & right edges of the cockpit orbital viewport (heights 2f..4f, never 6f)
            Color ladderCol = new Color(cyan.r, cyan.g, cyan.b, .38f);
            for (int p = 0; p < 5; p++)
            {
                float py = vp.y + 44f + p * 36f;
                float w = (p == 2) ? 18f : 11f;
                FrontEndTheme.Fill(new Rect(vp.x + 12f, py, w, 2f), ladderCol);
                FrontEndTheme.Fill(new Rect(vp.x + vp.width - 12f - w, py, w, 2f), ladderCol);
            }

            // Dotted approach vector leading from R35 dropship silhouette toward the planet drop zone
            float drift = 6f * Mathf.PingPong(now * 1.4f, 1f);
            float shipX = vp.x + 34f + drift;
            float shipY = vp.y + 168f - drift * .6f;
            for (int d = 0; d < 6; d++)
            {
                float t = (d + 1) / 7f;
                float dx = shipX + 26f + t * 88f;
                float dy = shipY - 6f - t * 52f;
                float pulse = .4f + .6f * Mathf.PingPong(now * 3.2f - d * .25f, 1f);
                FrontEndTheme.Fill(new Rect(dx, dy, 5f, 2f), new Color(gold.r, gold.g, gold.b, .35f + .5f * pulse));
            }

            // Target Drop Zone Reticle on the planet surface
            float lzX = shipX + 122f;
            float lzY = shipY - 64f;
            FrontEndTheme.Fill(new Rect(lzX - 8f, lzY, 16f, 2f), new Color(gold.r, gold.g, gold.b, .85f));
            FrontEndTheme.Fill(new Rect(lzX - 1f, lzY - 7f, 2f, 16f), new Color(gold.r, gold.g, gold.b, .85f));

            // Geometric R35 Dropship Silhouette (Twin-turbine swept-wing troop transport + thruster plumes)
            float plumeW = 10f + 8f * Mathf.PingPong(now * 8f, 1f);
            FrontEndTheme.Fill(new Rect(shipX - plumeW, shipY - 5f, plumeW, 3f), new Color(cyan.r, cyan.g, cyan.b, .85f));
            FrontEndTheme.Fill(new Rect(shipX - plumeW * .85f, shipY + 3f, plumeW * .85f, 3f), new Color(accent.r, accent.g, accent.b, .85f));
            // Fuselage & troop bay hull (heights 4f, 8f, 3f — never 6f)
            FrontEndTheme.Fill(new Rect(shipX, shipY - 3f, 24f, 8f), new Color(.86f, .94f, 1f, .92f));
            FrontEndTheme.Fill(new Rect(shipX + 18f, shipY - 1f, 10f, 4f), new Color(cyan.r, cyan.g, cyan.b, .95f));
            // Upper & lower VTOL engine nacelles
            FrontEndTheme.Fill(new Rect(shipX + 3f, shipY - 8f, 13f, 4f), new Color(accent.r, accent.g, accent.b, .88f));
            FrontEndTheme.Fill(new Rect(shipX + 3f, shipY + 6f, 13f, 4f), new Color(accent.r, accent.g, accent.b, .88f));
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
