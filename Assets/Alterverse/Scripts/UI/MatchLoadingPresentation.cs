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
                // Cover the complete screen, including the area outside the mobile safe area.
                FrontEndTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.006f, .011f, .020f, alpha));
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) return false;
                float scale = Mathf.Min(safe.height / 720f, safe.width / 1500f);
                float width = safe.width / scale;
                float y = Screen.height - safe.yMax + (safe.height - 720f * scale) * .5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0), Quaternion.identity, Vector3.one * scale);

                Texture2D pixel = FrontEndTheme.Pixel;
                FrontEndTheme.Fill(new Rect(0, 0, width * .5f, 3f), new Color(FrontEndTheme.Zero.r, FrontEndTheme.Zero.g, FrontEndTheme.Zero.b, alpha));
                FrontEndTheme.Fill(new Rect(width * .5f, 0, width * .5f, 3f), new Color(FrontEndTheme.Positive.r, FrontEndTheme.Positive.g, FrontEndTheme.Positive.b, alpha));

                float panelWidth = Mathf.Min(1080f, width - 100f);
                float left = (width - panelWidth) * .5f;
                Rect shell = new Rect(left, 118f, panelWidth, 496f);
                theme.Panel(shell);
                HeroId activeHero = PlayerHeroSelection.Selected;
                Color heroAccent = Alterverse.Visuals.StylizedArtDirection.HeroAccent(activeHero);
                DimensionUiMotif.DrawSplitPanel(shell, pixel,
                    new Color(.014f, .022f, .038f, .96f * alpha),
                    new Color(.018f, .028f, .048f, .96f * alpha),
                    FrontEndTheme.Zero, FrontEndTheme.Positive, .28f * alpha);
                DimensionUiMotif.DrawCornerCuts(shell, pixel, FrontEndTheme.Zero, FrontEndTheme.Positive);

                theme.StatusBadge(new Rect(left + 36f, 146f, 224f, 28f),
                    "4v4 TACTICAL DEPLOYMENT", FrontEndTheme.Zero);
                theme.Label(new Rect(left + 36f, 182f, panelWidth - 250f, 56f), "ENTERING THE FRONT", theme.Title);
                DimensionUiMotif.DrawHeaderLine(new Rect(left + 36f, 246f, panelWidth - 72f, 2f),
                    pixel, FrontEndTheme.Zero, FrontEndTheme.Positive);

                // Right-side Champion Loadout Badge
                Rect heroCard = new Rect(left + panelWidth - 208f, 142f, 172f, 92f);
                FrontEndTheme.Fill(heroCard, new Color(.008f, .014f, .025f, .92f * alpha));
                FrontEndTheme.Fill(new Rect(heroCard.x, heroCard.y, 4f, heroCard.height), heroAccent);
                StylizedHeroCard.Draw(new Rect(heroCard.x + 10f, heroCard.y + 10f, 56f, 72f), activeHero, pixel, Time.unscaledTime);
                theme.Label(new Rect(heroCard.x + 74f, heroCard.y + 14f, 90f, 18f), "CHAMPION", theme.Small, true);
                theme.FitLabel(new Rect(heroCard.x + 74f, heroCard.y + 34f, 90f, 24f),
                    HeroIdentity.NameFor(activeHero), theme.Body);
                theme.FitLabel(new Rect(heroCard.x + 74f, heroCard.y + 58f, 90f, 18f),
                    HeroIdentity.RoleFor(activeHero).ToString().ToUpperInvariant(), theme.Small, true);

                theme.Label(new Rect(left + 38f, 268f, panelWidth - 76f, 38f), stage, theme.Heading);
                theme.FitLabel(new Rect(left + 38f, 312f, panelWidth - 76f, 28f), detail, theme.Body, true);
                if (!string.IsNullOrEmpty(context))
                    theme.FitLabel(new Rect(left + 38f, 346f, panelWidth - 76f, 22f), context, theme.Small, true);

                // Activity only: elapsed presentation time is not battlefield loading progress.
                float barW = panelWidth - 76f;
                Rect track = new Rect(left + 38f, 384f, barW, 8f);
                FrontEndTheme.Fill(new Rect(track.x - 1f, track.y - 1f, track.width + 2f, track.height + 2f),
                    new Color(FrontEndTheme.Zero.r, FrontEndTheme.Zero.g, FrontEndTheme.Zero.b, .35f * alpha));
                FrontEndTheme.Fill(track, FrontEndTheme.Raised);
                float segmentW = 210f;
                float x = Mathf.PingPong(Time.unscaledTime * 280f, Mathf.Max(1f, barW - segmentW));
                FrontEndTheme.Fill(new Rect(track.x + x, track.y, segmentW, track.height), FrontEndTheme.Accent);
                FrontEndTheme.Fill(new Rect(track.x + x, track.y, segmentW, 2f), new Color(1f, 1f, 1f, .55f * alpha));

                string[] tips = LoadingTipContent.Tips;
                if (tips.Length > 0)
                {
                    Rect tipBox = new Rect(left + 38f, 412f, panelWidth - 76f, 68f);
                    FrontEndTheme.Fill(tipBox, new Color(.008f, .014f, .025f, .88f * alpha));
                    FrontEndTheme.Fill(new Rect(tipBox.x, tipBox.y, 4f, tipBox.height), FrontEndTheme.Positive);
                    theme.Label(new Rect(tipBox.x + 16f, tipBox.y + 8f, 220f, 18f), "TACTICAL INTEL", theme.Small, true);
                    theme.Label(new Rect(tipBox.x + 16f, tipBox.y + 28f, tipBox.width - 32f, 34f),
                        tips[(int)(Time.unscaledTime / 8f) % tips.Length], theme.Small, true);
                }

                return canReturnToMenu && theme.Action(new Rect(left + 38f, 542f, 260f, 48f), "RETURN TO MENU");
            }
            finally
            {
                GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; GUI.depth = oldDepth;
            }
        }
    }
}


