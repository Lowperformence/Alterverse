using Alterverse.Content;
using Alterverse.Match;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Shows the real online handoff stages, independently of the party/menu visibility.</summary>
    public sealed class OnlineMatchLoadingScreen : MonoBehaviour
    {
        private static OnlineMatchLoadingScreen instance;
        private static OnlineMatchPlan buildingPlan;
        private FrontEndTheme theme;
        public static bool IsVisible => !MatchExit.Leaving && (buildingPlan != null ||
            (EdgegapOnlineFlow.Instance?.IsSearching == true &&
             string.IsNullOrEmpty(EdgegapOnlineFlow.Instance.LastError) &&
             EdgegapMatchmakerClient.Instance?.HasAssignment == true));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { instance = null; buildingPlan = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if (instance == null) new GameObject("DF_OnlineMatchLoading").AddComponent<OnlineMatchLoadingScreen>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static void Begin(OnlineMatchPlan plan) { Ensure(); buildingPlan = plan; }
        public static void Complete() { buildingPlan = null; }
        public static void Cancel() { buildingPlan = null; }

        private void Update()
        {
            if (buildingPlan != null && (MatchExit.Leaving ||
                !ReferenceEquals(CoopAuthority.ActivePlan, buildingPlan))) Cancel();
        }

        private void OnGUI()
        {
            if (!IsVisible) return;
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
                if (safe.width <= 0 || safe.height <= 0) return;
                float scale = Mathf.Min(safe.height / 720f, safe.width / 1500f);
                float width = safe.width / scale;
                float y = Screen.height - safe.yMax + (safe.height - 720f * scale) * .5f;
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, y, 0), Quaternion.identity, Vector3.one * scale);
                theme ??= new FrontEndTheme();
                float left = (width - 1040f) * .5f;
                theme.Label(new Rect(left, 180, 1040, 64), "ENTERING THE FRONT", theme.Title);
                theme.Label(new Rect(left, 270, 1040, 42), buildingPlan != null
                    ? "PREPARING BATTLEFIELD" : "MATCH FOUND", theme.Heading);
                string detail = buildingPlan != null ? "Building your arena and synchronizing the match"
                    : EdgegapOnlineFlow.Instance?.Status ?? "Connecting to game server";
                theme.FitLabel(new Rect(left, 322, 1040, 32), detail, theme.Body, true);
                // Moving activity segment: no invented percentage for server work.
                FrontEndTheme.Fill(new Rect(left, 390, 1040, 6), FrontEndTheme.Raised);
                float x = Mathf.PingPong(Time.unscaledTime * 240f, 860f);
                FrontEndTheme.Fill(new Rect(left + x, 390, 180, 6), FrontEndTheme.Accent);
                string[] tips = LoadingTipContent.Tips;
                if (tips.Length > 0)
                    theme.Label(new Rect(left, 430, 1040, 72), tips[(int)(Time.unscaledTime / 8f) % tips.Length], theme.Small, true);
                if (buildingPlan == null && theme.Action(new Rect(left, 554, 260, 46), "RETURN TO MENU"))
                    _ = MatchExit.ReturnToMenuAsync();
            }
            finally
            {
                GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; GUI.depth = oldDepth;
            }
        }

        private void OnDestroy() { if (instance == this) { instance = null; buildingPlan = null; } }
    }
}
