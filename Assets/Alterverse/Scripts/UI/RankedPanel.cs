using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Ranked lobby, rank progress and season leaderboard in the existing menu style.</summary>
    public sealed class RankedPanel : MonoBehaviour
    {
        static RankedPanel instance;
        FrontEndTheme theme;
        Vector2 scroll;
        public static bool Visible => instance != null;
        public static void Show()
        {
            if (!RankedAvailability.Enabled) return;
            if (instance == null) new GameObject("DF_RankedPanel").AddComponent<RankedPanel>();
            if (RankedService.Instance != null) _ = RankedService.Instance.RefreshAsync();
        }
        void Awake() { instance = this; }
        void OnGUI()
        {
            if (!RankedAvailability.Enabled || !MainMenuController.IsVisible || CoopAuthority.Active) { Destroy(gameObject); return; }
            theme ??= new FrontEndTheme();
            Matrix4x4 old = GUI.matrix; int depth = GUI.depth;
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / 960f, safe.height / 640f);
            try
            {
                GUI.depth = -9000;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x + (safe.width-960*scale)/2, Screen.height-safe.yMax+(safe.height-640*scale)/2, 0), Quaternion.identity, Vector3.one * scale);
                FrontEndTheme.Fill(new Rect(0, 0, 960, 640), FrontEndTheme.Background);
                theme.Label(new Rect(28, 20, 640, 48), "RANKED • SOLO 5v5", theme.Title);
                var service = RankedService.Instance;
                bool searching = service?.Searching == true;
                if (theme.Action(new Rect(772, 20, 160, 48), "CLOSE", enabled:!searching && service?.Busy != true)) Destroy(gameObject);
                var current = service?.Current;
                theme.Panel(new Rect(28, 90, 352, 430));
                theme.Label(new Rect(48, 108, 312, 44), current?.rank ?? "UNRANKED", theme.Heading);
                theme.Label(new Rect(48, 160, 312, 32), current == null ? "Loading rank…" : current.season + " • " + current.points + " RP", theme.Body);
                theme.Label(new Rect(48, 204, 312, 74), current?.placementsRemaining > 0
                    ? current.placementsRemaining + " placement matches remaining"
                    : "Win as a team to climb the ranks.", theme.Body, true);
                theme.Label(new Rect(48, 284, 312, 120), "10 real players • no bot fill\nBronze → Dimensions Legend\nRank saved to your linked account.\nLeaving: point penalty + cooldown", theme.Small, true);
                if (current?.lastResult != null)
                    theme.Label(new Rect(48, 418, 312, 34), "LAST MATCH  " + (current.lastResult.delta >= 0 ? "+" : "") + current.lastResult.delta + " RP", theme.Body);
                theme.Label(new Rect(48, 464, 312, 36), current == null ? "" : current.wins + " WINS / " + current.games + " MATCHES", theme.Small, true);
                theme.Panel(new Rect(400, 90, 532, 430));
                theme.Label(new Rect(420, 104, 492, 32), "SEASON TOP 50", theme.Heading);
                var leaders = service?.Leaderboard;
                if (leaders == null || leaders.Length == 0)
                    theme.Label(new Rect(420, 154, 480, 80), "Complete your placements to join the season leaderboard.", theme.Body, true);
                else
                {
                    scroll = GUI.BeginScrollView(new Rect(420, 148, 492, 352), scroll, new Rect(0, 0, 470, leaders.Length * 40));
                    for (int i = 0; i < leaders.Length; i++)
                        theme.Label(new Rect(4, i * 40, 456, 36), (i+1) + ". " + Alterverse.Profile.SafeIdentityCatalog.DisplayPlayerName(leaders[i].displayName) + "  •  " + leaders[i].points + " RP", theme.Body);
                    GUI.EndScrollView();
                }
                theme.Label(new Rect(28, 528, 900, 38), service?.Status ?? "Ranked is initializing…", theme.Small, true);
                long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                bool cooldown = current != null && current.cooldownUntil > now;
                string action = searching ? "CANCEL SEARCH" : cooldown ? "COOLDOWN " + ((current.cooldownUntil-now+59)/60) + " MIN" : "FIND RANKED MATCH";
                if (theme.Action(new Rect(28, 576, 580, 48), action, true, enabled:service != null && !service.Busy && !cooldown))
                {
                    if (searching) _ = service.CancelAsync();
                    else _ = service.StartSearchAsync();
                }
                if (theme.Action(new Rect(628, 576, 304, 48), "REFRESH RANK", enabled:service != null && !service.Busy && !searching))
                    _ = service.RefreshAsync();
            }
            finally { GUI.matrix = old; GUI.depth = depth; }
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
