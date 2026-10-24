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
            theme ??= new FrontEndTheme();
            string detail = buildingPlan != null ? "Building your arena and synchronizing the match"
                : EdgegapOnlineFlow.Instance?.Status ?? "Connecting to game server";
            if (MatchLoadingPresentation.Draw(theme, buildingPlan != null
                ? "PREPARING BATTLEFIELD" : "MATCH FOUND", detail, canReturnToMenu: buildingPlan == null))
                _ = MatchExit.ReturnToMenuAsync();
        }

        private void OnDestroy() { if (instance == this) { instance = null; buildingPlan = null; } }
    }
}

