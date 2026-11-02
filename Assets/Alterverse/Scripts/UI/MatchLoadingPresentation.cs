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
                FrontEndTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.008f, .014f, .026f, alpha));
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) return false;
                float scale = Mathf.Min(safe.height / 720f, safe.width / 1500f);
                float width = safe.width / scale;
                float y = Screen.height - safe.yMax + (safe.height - 720f * scale) * .5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0), Quaternion.identity, Vector3.one * scale);

                Texture2D pixel = FrontEndTheme.Pixel;
                float panelWidth = Mathf.Min(1080f, width - 100f);
                float left = (width - panelWidth) * .5f;
                Rect shell = new Rect(left, 114f, panelWidth, 502f);
                theme.Panel(shell);

                HeroId activeHero = PlayerHeroSelection.Selected;
                Color heroAccent = Alterverse.Visuals.StylizedArtDirection.HeroAccent(activeHero);

                theme.StatusBadge(new Rect(left + 36f, 144f, 224f, 28f),
                    "4v4 TACTICAL DEPLOYMENT", FrontEndTheme.Zero);
                theme.Label(new Rect(left + 36f, 180f, panelWidth - 280f, 56f), "ENTERING THE FRONT", theme.Title);

                // Right-side Champion Loadout Badge with 3D Hero Mini-Portrait
                Rect heroCard = new Rect(left + panelWidth - 246f, 138f, 210f, 112f);
                theme.Panel(heroCard);
                theme.Frame(heroCard, true);
                StylizedHeroCard.Draw(new Rect(heroCard.x + 12f, heroCard.y + 12f, 72f, 88f), activeHero, pixel, Time.unscaledTime);
                theme.Label(new Rect(heroCard.x + 94f, heroCard.y + 18f, 104f, 18f), "CHAMPION", theme.Small, true);
                theme.FitLabel(new Rect(heroCard.x + 94f, heroCard.y + 40f, 104f, 26f),
                    HeroIdentity.NameFor(activeHero), theme.Heading);
                theme.StatusBadge(new Rect(heroCard.x + 94f, heroCard.y + 70f, 104f, 22f),
                    HeroIdentity.RoleFor(activeHero).ToString().ToUpperInvariant(), heroAccent);

                theme.Label(new Rect(left + 38f, 266f, panelWidth - 76f, 38f), stage, theme.Heading);
                theme.FitLabel(new Rect(left + 38f, 310f, panelWidth - 76f, 28f), detail, theme.Body, true);
                if (!string.IsNullOrEmpty(context))
                    theme.FitLabel(new Rect(left + 38f, 344f, panelWidth - 76f, 22f), context, theme.Small, true);

                // Smooth orbital progress bar
                float barW = panelWidth - 76f;
                float pulse = 0.25f + 0.75f * Mathf.PingPong(Time.unscaledTime * 0.9f, 1f);
                theme.Progress(new Rect(left + 38f, 384f, barW, 8f), pulse);

                string[] tips = LoadingTipContent.Tips;
                if (tips.Length > 0)
                {
                    Rect tipBox = new Rect(left + 38f, 412f, panelWidth - 76f, 74f);
                    theme.Panel(tipBox);
                    theme.Label(new Rect(tipBox.x + 18f, tipBox.y + 10f, 220f, 18f), "TACTICAL INTEL", theme.Small, true);
                    theme.Label(new Rect(tipBox.x + 18f, tipBox.y + 32f, tipBox.width - 36f, 34f),
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
