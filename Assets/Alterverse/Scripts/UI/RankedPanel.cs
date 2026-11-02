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
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x + (safe.width - 960 * scale) / 2, Screen.height - safe.yMax + (safe.height - 640 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
                FrontEndTheme.Fill(new Rect(0, 0, 960, 640), FrontEndTheme.Background);
                FrontEndTheme.Fill(new Rect(0, 0, 480, 3), FrontEndTheme.Zero);
                FrontEndTheme.Fill(new Rect(480, 0, 480, 3), FrontEndTheme.Positive);

                var service = RankedService.Instance;
                bool searching = service?.Searching == true;
                FrontEndTheme.Fill(new Rect(28, 22, 4, 44), FrontEndTheme.Accent);
                theme.Label(new Rect(42, 18, 520, 36), "RANKED • SOLO 4v4", theme.Title);
                theme.Label(new Rect(44, 52, 520, 20), "COMPETITIVE LADDER  •  DEDICATED SERVER", theme.Small, true);
                theme.StatusBadge(new Rect(572, 24, 184, 36), searching ? "SEARCHING QUEUE" : "SOLO QUEUE READY",
                    searching ? FrontEndTheme.Positive : FrontEndTheme.Zero);
                if (theme.Action(new Rect(772, 20, 160, 48), "CLOSE", enabled: !searching && service?.Busy != true)) Destroy(gameObject);

                var current = service?.Current;
                Rect leftPanel = new Rect(28, 86, 352, 434);
                theme.Panel(leftPanel);
                FrontEndTheme.Fill(new Rect(28, 86, 352, 3), FrontEndTheme.Accent);
                theme.StatusBadge(new Rect(48, 104, 164, 24), "CURRENT DIVISION", FrontEndTheme.Accent);
                theme.Label(new Rect(48, 134, 312, 40), current?.rank ?? "UNRANKED", theme.Heading);
                theme.Label(new Rect(48, 176, 312, 28), current == null ? "Loading rank…" : current.season + " • " + current.points + " RP", theme.Body);
                FrontEndTheme.Fill(new Rect(48, 210, 312, 1), FrontEndTheme.Raised);
                theme.Label(new Rect(48, 222, 312, 54), current?.placementsRemaining > 0
                    ? current.placementsRemaining + " placement matches remaining"
                    : "Win as a team to climb the ranks.", theme.Body, true);

                Rect rulesBox = new Rect(44, 282, 320, 118);
                FrontEndTheme.Fill(rulesBox, FrontEndTheme.Surface);
                FrontEndTheme.Fill(new Rect(44, 282, 3, 118), FrontEndTheme.Zero);
                theme.Label(new Rect(58, 290, 296, 104), "8 real players • no bot fill\nBronze → Dimensions Legend\nRank saved to your linked account.\nLeaving: point penalty + cooldown", theme.Small, true);

                if (current?.lastResult != null)
                {
                    string deltaText = "LAST MATCH  " + (current.lastResult.delta >= 0 ? "+" : "") + current.lastResult.delta + " RP";
                    theme.StatusBadge(new Rect(48, 412, 312, 30), deltaText,
                        current.lastResult.delta >= 0 ? FrontEndTheme.Positive : FrontEndTheme.Warning);
                }
                theme.Label(new Rect(48, 456, 312, 36), current == null ? "" : current.wins + " WINS / " + current.games + " MATCHES", theme.Small, true);

                Rect rightPanel = new Rect(400, 86, 532, 434);
                theme.Panel(rightPanel);
                FrontEndTheme.Fill(new Rect(400, 86, 532, 3), FrontEndTheme.Positive);
                theme.Label(new Rect(420, 102, 320, 32), "SEASON TOP 50", theme.Heading);
                theme.StatusBadge(new Rect(748, 104, 164, 26), "GLOBAL STANDINGS", FrontEndTheme.Positive);
                FrontEndTheme.Fill(new Rect(420, 138, 492, 1), FrontEndTheme.Raised);

                var leaders = service?.Leaderboard;
                if (leaders == null || leaders.Length == 0)
                    theme.Label(new Rect(420, 160, 480, 80), "Complete your placements to join the season leaderboard.", theme.Body, true);
                else
                {
                    scroll = GUI.BeginScrollView(new Rect(420, 148, 492, 354), scroll, new Rect(0, 0, 470, leaders.Length * 42));
                    for (int i = 0; i < leaders.Length; i++)
                    {
                        Rect row = new Rect(4, i * 42, 462, 36);
                        FrontEndTheme.Fill(row, FrontEndTheme.Surface);
                        if (i < 3)
                            FrontEndTheme.Fill(new Rect(row.x, row.y, 4, row.height), i == 0 ? FrontEndTheme.Accent : FrontEndTheme.Positive);
                        theme.Label(new Rect(18, i * 42 + 2, 440, 32),
                            (i + 1) + ". " + Alterverse.Profile.SafeIdentityCatalog.DisplayPlayerName(leaders[i].displayName) + "  •  " + leaders[i].points + " RP",
                            theme.Body);
                    }
                    GUI.EndScrollView();
                }

                theme.Label(new Rect(28, 528, 900, 34), service?.Status ?? "Ranked is initializing…", theme.Small, true);
                long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                bool cooldown = current != null && current.cooldownUntil > now;
                string action = searching ? "CANCEL SEARCH" : cooldown ? "COOLDOWN " + ((current.cooldownUntil - now + 59) / 60) + " MIN" : "FIND RANKED MATCH";
                if (theme.Action(new Rect(28, 572, 580, 50), action, true, enabled: service != null && !service.Busy && !cooldown))
                {
                    if (searching) _ = service.CancelAsync();
                    else _ = service.StartSearchAsync();
                }
                if (theme.Action(new Rect(628, 572, 304, 50), "REFRESH RANK", enabled: service != null && !service.Busy && !searching))
                    _ = service.RefreshAsync();
            }
            finally { GUI.matrix = old; GUI.depth = depth; }
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
