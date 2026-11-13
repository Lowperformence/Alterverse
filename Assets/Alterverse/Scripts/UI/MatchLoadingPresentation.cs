using Alterverse.Characters;
using Alterverse.Content;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>One loading surface for online and local match entry; callers retain their own lifecycle.</summary>
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
                // Cover the whole screen, including notches and other unsafe areas.
                FrontEndTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.008f, .014f, .032f, 1f));
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) return false;
                float scale = Mathf.Min(safe.height / 720f, safe.width / 1500f);
                float width = safe.width / scale;
                float y = Screen.height - safe.yMax + (safe.height - 720f * scale) * .5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0), Quaternion.identity, Vector3.one * scale);

                HeroId activeHero = PlayerHeroSelection.Selected;
                Color accent = Alterverse.Visuals.StylizedArtDirection.HeroAccent(activeHero);
                Texture2D pixel = FrontEndTheme.Pixel;
                // Smooth, slow-moving starlight uses the same cached artwork as the menu.
                DimensionUiMotif.DrawCosmicParallaxOverlay(new Rect(0, 0, width, 720f), pixel,
                    accent, Time.unscaledTime, Vector2.zero);

                float panelWidth = Mathf.Min(1080f, width - 100f);
                float left = (width - panelWidth) * .5f;
                DimensionUiMotif.DrawLoadingPlanet(new Rect(left - 176f, 174f, 150f, 150f),
                    new Color(.48f, .76f, 1f, 1f), Time.unscaledTime * 10f);
                DimensionUiMotif.DrawLoadingPlanet(new Rect(left + panelWidth + 44f, 430f, 100f, 100f),
                    new Color(1f, .70f, .40f, 1f), -Time.unscaledTime * 13f);
                Rect shell = new Rect(left, 88f, panelWidth, 548f);
                theme.Panel(shell);
                FrontEndTheme.Fill(new Rect(left + 1f, 89f, 4f, 546f),
                    new Color(accent.r, accent.g, accent.b, .85f));

                float textX = left + 42f;
                float textWidth = panelWidth - 400f;
                theme.StatusBadge(new Rect(textX, 122f, 226f, 28f), "4v4 TACTICAL DEPLOYMENT", FrontEndTheme.Zero);
                theme.Label(new Rect(textX, 166f, textWidth, 58f), "ENTERING THE FRONT", theme.Title);
                theme.Label(new Rect(textX, 242f, textWidth, 38f), stage, theme.Heading);
                theme.FitLabel(new Rect(textX, 286f, textWidth, 30f), detail, theme.Body, true);
                if (!string.IsNullOrEmpty(context))
                    theme.FitLabel(new Rect(textX, 320f, textWidth, 24f), context, theme.Small, true);

                // This is an indeterminate activity indicator, not a made-up percentage.
                Rect rail = new Rect(textX, 365f, textWidth, 6f);
                FrontEndTheme.Fill(rail, new Color(.22f, .32f, .44f, .45f));
                float segmentWidth = Mathf.Min(130f, rail.width * .24f);
                float travel = Mathf.Max(0f, rail.width - segmentWidth);
                float segmentX = rail.x + travel * Mathf.PingPong(Time.unscaledTime * .55f, 1f);
                FrontEndTheme.Fill(new Rect(segmentX, rail.y, segmentWidth, rail.height),
                    new Color(accent.r, accent.g, accent.b, .92f));

                string[] tips = LoadingTipContent.Tips;
                if (tips.Length > 0)
                {
                    Rect tip = new Rect(textX, 404f, textWidth, 100f);
                    theme.Panel(tip);
                    theme.Label(new Rect(tip.x + 18f, tip.y + 10f, tip.width - 36f, 20f), "TACTICAL INTEL", theme.Small, true);
                    theme.Label(new Rect(tip.x + 18f, tip.y + 34f, tip.width - 36f, 52f),
                        tips[(int)(Time.unscaledTime / 8f) % tips.Length], theme.Small, true);
                }

                // A large sharp champion card gives the screen a clear visual focus.
                Rect heroCard = new Rect(left + panelWidth - 310f, 120f, 268f, 418f);
                theme.Panel(heroCard);
                theme.Frame(heroCard, true);
                Rect portrait = new Rect(heroCard.x + 14f, heroCard.y + 14f, heroCard.width - 28f, 292f);
                StylizedHeroCard.Draw(portrait, activeHero, pixel, Time.unscaledTime);
                theme.Label(new Rect(heroCard.x + 16f, heroCard.y + 318f, heroCard.width - 32f, 18f), "CHAMPION", theme.Small, true);
                theme.FitLabel(new Rect(heroCard.x + 16f, heroCard.y + 342f, heroCard.width - 32f, 32f),
                    HeroIdentity.NameFor(activeHero), theme.Heading);
                theme.StatusBadge(new Rect(heroCard.x + 16f, heroCard.y + 382f, heroCard.width - 32f, 24f),
                    HeroIdentity.RoleFor(activeHero).ToString().ToUpperInvariant(), accent);

                return canReturnToMenu && theme.Action(new Rect(textX, 554f, 260f, 48f), "RETURN TO MENU");
            }
            finally
            {
                GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; GUI.depth = oldDepth;
            }
        }
    }
}
