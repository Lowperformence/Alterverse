using Alterverse.Content;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>One loading surface for online and local match entry; callers retain their own lifecycle.</summary>
    internal static class MatchLoadingPresentation
    {
        public static bool Draw(FrontEndTheme theme, string stage, string detail,
            string context = null, bool canReturnToMenu = false)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            int oldDepth = GUI.depth;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                GUI.color = Color.white;
                GUI.enabled = true;
                GUI.depth = -9000;
                // Cover the complete screen, including the area outside the mobile safe area.
                FrontEndTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.008f, .014f, .025f, 1f));
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) return false;
                float scale = Mathf.Min(safe.height / 720f, safe.width / 1500f);
                float width = safe.width / scale;
                float y = Screen.height - safe.yMax + (safe.height - 720f * scale) * .5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0), Quaternion.identity, Vector3.one * scale);
                float left = (width - 1040f) * .5f;
                theme.Label(new Rect(left, 180, 1040, 64), "ENTERING THE FRONT", theme.Title);
                theme.Label(new Rect(left, 270, 1040, 42), stage, theme.Heading);
                theme.FitLabel(new Rect(left, 322, 1040, 32), detail, theme.Body, true);
                if (!string.IsNullOrEmpty(context))
                    theme.FitLabel(new Rect(left, 360, 1040, 24), context, theme.Small, true);
                // Activity only: elapsed presentation time is not battlefield loading progress.
                FrontEndTheme.Fill(new Rect(left, 390, 1040, 6), FrontEndTheme.Raised);
                float x = Mathf.PingPong(Time.unscaledTime * 240f, 860f);
                FrontEndTheme.Fill(new Rect(left + x, 390, 180, 6), FrontEndTheme.Accent);
                string[] tips = LoadingTipContent.Tips;
                if (tips.Length > 0)
                    theme.Label(new Rect(left, 430, 1040, 72), tips[(int)(Time.unscaledTime / 8f) % tips.Length], theme.Small, true);
                return canReturnToMenu && theme.Action(new Rect(left, 554, 260, 46), "RETURN TO MENU");
            }
            finally
            {
                GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; GUI.depth = oldDepth;
            }
        }
    }
}
