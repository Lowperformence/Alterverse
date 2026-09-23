using Alterverse.Multiplayer;
using Alterverse.Prototype;
using Alterverse.UI;
using UnityEngine;

namespace Alterverse.Match
{
    /// <summary>
    /// Keeps local play in a match loop: after the result reward is safely committed, the result
    /// screen remains visible briefly and the next round starts automatically unless the player
    /// explicitly returns to the main menu. Co-op keeps its existing lobby/readiness flow.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    public sealed class PostMatchAutoLoop : MonoBehaviour
    {
        private const float ResultHoldSeconds = 12f;

        private DimensionsFront trackedFront;
        private int rewardSequenceAtMatchStart;
        private float restartAt = -1f;
        private bool restarting;
        private FrontEndTheme theme;
        private GUIStyle smallCentered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if (FindAnyObjectByType<PostMatchAutoLoop>() == null)
                new GameObject("DF_PostMatchAutoLoop").AddComponent<PostMatchAutoLoop>();
        }

        private void Update()
        {
            if (restarting) return;

            if (MainMenuController.IsVisible || CoopAuthority.Active)
            {
                ForgetMatch();
                return;
            }

            DimensionsFront current = FindAnyObjectByType<DimensionsFront>();
            if (current != trackedFront)
                Track(current);

            if (trackedFront == null || !trackedFront.HasEnded)
                return;

            // Never tear down a result screen while its receipt/reward is still being finalized.
            if (restartAt < 0f && MatchRewardService.GrantSequence > rewardSequenceAtMatchStart)
                restartAt = Time.unscaledTime + ResultHoldSeconds;

            if (restartAt >= 0f && Time.unscaledTime >= restartAt)
                RestartLocalMatch();
        }

        private void Track(DimensionsFront front)
        {
            trackedFront = front;
            rewardSequenceAtMatchStart = MatchRewardService.GrantSequence;
            restartAt = -1f;
            restarting = false;
        }

        private void ForgetMatch()
        {
            trackedFront = null;
            restartAt = -1f;
            restarting = false;
        }

        private void RestartLocalMatch()
        {
            if (CoopAuthority.Active || MainMenuController.IsVisible)
            {
                ForgetMatch();
                return;
            }

            PrototypeBootstrap bootstrap = FindAnyObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                MatchClock.Set(PauseReason.Result, false);
                MainMenuController.ShowHome();
                ForgetMatch();
                return;
            }

            restarting = true;
            MatchClock.Set(PauseReason.Result, false);
            MatchClock.Set(PauseReason.Settings, false);
            bootstrap.StartCoroutine(bootstrap.RestartPrototype());
            trackedFront = null;
            restartAt = -1f;
        }

        private void BackToMenu()
        {
            restartAt = -1f;
            trackedFront = null;
            MatchClock.Set(PauseReason.Result, false);
            MatchClock.Set(PauseReason.Settings, false);
            MainMenuController.ShowHome();
        }

        private void OnGUI()
        {
            if (CoopAuthority.Active || MainMenuController.IsVisible || trackedFront == null || !trackedFront.HasEnded)
                return;

            CombatHudLayout layout = new CombatHudLayout(Screen.safeArea);
            if (layout.Safe.width <= 0 || layout.Safe.height <= 0) return;

            theme ??= new FrontEndTheme();
            smallCentered ??= new GUIStyle(theme.Small)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };

            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            int depth = GUI.depth;
            try
            {
                GUI.depth = -6000;
                GUI.matrix = layout.Matrix;

                float width = 220f;
                float x = layout.Width - width - 16f;
                Rect info = new Rect(x, 14f, width, 34f);

                bool rewardReady = MatchRewardService.GrantSequence > rewardSequenceAtMatchStart;
                if (!rewardReady)
                {
                    FrontEndTheme.Fill(info, new Color(FrontEndTheme.Surface.r, FrontEndTheme.Surface.g, FrontEndTheme.Surface.b, .94f));
                    theme.Label(info, "FINALIZING REWARD…", smallCentered, true);
                    return;
                }

                int seconds = Mathf.Max(0, Mathf.CeilToInt(restartAt - Time.unscaledTime));
                FrontEndTheme.Fill(info, new Color(FrontEndTheme.Surface.r, FrontEndTheme.Surface.g, FrontEndTheme.Surface.b, .94f));
                theme.Label(info, $"NEXT MATCH IN {seconds}", smallCentered, true);

                if (theme.Action(new Rect(x, 54f, width, 42f), "BACK TO MENU"))
                    BackToMenu();
            }
            finally
            {
                GUI.depth = depth;
                GUI.matrix = matrix;
                GUI.color = color;
            }
        }
    }
}
