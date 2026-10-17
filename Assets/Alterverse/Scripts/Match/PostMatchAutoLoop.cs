using System.Collections;
using Alterverse.Multiplayer;
using Alterverse.Prototype;
using Alterverse.UI;
using UnityEngine;

namespace Alterverse.Match
{
    [DefaultExecutionOrder(1200)]
    public sealed class PostMatchAutoLoop : MonoBehaviour
    {
        DimensionsFront trackedFront;
        float restartAt = -1;
        bool restarting;
        FrontEndTheme theme;
        GUIStyle centered;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (FindAnyObjectByType<PostMatchAutoLoop>() == null)
                new GameObject("DF_PostMatchAutoLoop").AddComponent<PostMatchAutoLoop>();
        }
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (NetworkRoundLoop.Instance == null) gameObject.AddComponent<NetworkRoundLoop>();
        }
        void Update()
        {
            if (DedicatedServerNetworkRuntime.Requested) return;
            if (MatchExit.Leaving || MainMenuController.IsVisible) { trackedFront = null; restartAt = -1; return; }
            if (restarting) return;
            if (CoopAuthority.Active && !NetworkRoundLoop.OnlineReturnsToLobby) return;
            // Dedicated servers have no local reward receipt or party to return to.
            if (CoopAuthority.Active && CoopRosterBuilder.FindLocalHumanSlot(CoopAuthority.ActivePlan) < 0) return;
            var current = FindAnyObjectByType<DimensionsFront>();
            if (current != trackedFront) { trackedFront = current; restartAt = -1; }
            if (trackedFront == null || !trackedFront.HasEnded || MatchScoreboard.Instance == null || !MatchScoreboard.Instance.ResultCommitted) return;
            if (restartAt < 0) restartAt = Time.unscaledTime + 12;
            if (Time.unscaledTime >= restartAt)
            {
                if (CoopAuthority.Active) _ = MatchExit.ReturnToOnlineLobbyAsync();
                else StartCoroutine(RestartLocal());
            }
        }
        IEnumerator RestartLocal()
        {
            restarting = true;
            try
            {
                if (MatchExit.Leaving || MainMenuController.IsVisible || CoopAuthority.Active) yield break;
                var bootstrap = FindAnyObjectByType<PrototypeBootstrap>();
                if (bootstrap == null) { MainMenuController.ShowHome(); yield break; }
                MatchClock.Set(PauseReason.Result, false); MatchClock.Set(PauseReason.Settings, false);
                yield return bootstrap.RestartPrototype();
            }
            finally { restarting = false; trackedFront = null; restartAt = -1; }
        }
        void OnGUI()
        {
            if (MainMenuController.IsVisible || MatchExit.Leaving) return;
            var front = FindAnyObjectByType<DimensionsFront>();
            if (front == null || !front.HasEnded) return;
            string text;
            if (CoopAuthority.Active)
            {
                var loop = NetworkRoundLoop.Instance;
                text = NetworkRoundLoop.OnlineReturnsToLobby
                    ? restartAt < 0 ? "FINALIZING REWARDS…" : "BACK TO PARTY IN " + Mathf.Max(0, Mathf.CeilToInt(restartAt - Time.unscaledTime))
                    : loop != null && loop.Seconds >= 0 ? "NEXT ROUND IN " + loop.Seconds : loop?.Status ?? "WAITING FOR TEAM…";
            }
            else text = restartAt < 0 ? "FINALIZING REWARDS…" : "NEXT ROUND IN " + Mathf.Max(0, Mathf.CeilToInt(restartAt - Time.unscaledTime));
            theme ??= new FrontEndTheme(); centered ??= new GUIStyle(theme.Small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var layout = new CombatHudLayout(Screen.safeArea);
            Matrix4x4 old = GUI.matrix; int depth = GUI.depth;
            try
            {
                GUI.depth = -6000; GUI.matrix = layout.Matrix;
                Rect box = new Rect(layout.Width - 300, 12, 284, 34);
                FrontEndTheme.Fill(box, FrontEndTheme.Surface); theme.Label(box, text, centered, true);
            }
            finally { GUI.matrix = old; GUI.depth = depth; }
        }
    }
}
