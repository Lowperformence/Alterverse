using System.Threading.Tasks;
using Alterverse.Multiplayer;
using Alterverse.UI;
using UnityEngine;

namespace Alterverse.Match
{
    public static class MatchExit
    {
        public static bool Leaving { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Leaving = false;
        public static Task ReturnToMenuAsync(string failureMessage=null) => ExitAsync(false, failureMessage);
        public static Task ReturnToOnlineLobbyAsync(string failureMessage=null) => ExitAsync(true, failureMessage);

        private static async Task ExitAsync(bool keepOnlineParty, string failureMessage)
        {
            if (Leaving) return;
            // Never discard an authoritative result while its local reward transaction is pending.
            // The regular result button/countdown already enforces this; this also covers the
            // server disconnecting during the result screen.
            if (keepOnlineParty && Object.FindAnyObjectByType<DimensionsFront>()?.HasEnded == true &&
                CoopMatchResultSync.Instance?.ResultReady == true && MatchScoreboard.Instance?.ResultCommitted != true)
                return;
            Leaving = true;
            OnlineMatchLoadingScreen.Cancel();
            GameObject world = GameObject.Find("DF_MatchWorld");
            if (world != null) world.SetActive(false);
            try
            {
                if (RankedService.Instance?.Searching == true) await RankedService.Instance.CancelAsync();
                // Wait for in-flight session/search mutations; otherwise Leave methods can ignore
                // busy calls and leave stale Unity/Edgegap membership behind.
                var online = OnlinePartyService.Instance;
                var coop = CoopLobbyService.Instance;
                var edgegap = EdgegapOnlineFlow.Instance;
                while ((online != null && online.Busy) ||
                       (coop != null && coop.Busy) ||
                       (edgegap != null && edgegap.Busy))
                    await Task.Delay(50);

                // EdgegapOnlineFlow owns matchmaking cleanup. Always route the online exit through
                // it when available so pending assignments, direct connects and group membership
                // are cancelled before the Unity party is left.
                if (keepOnlineParty)
                {
                    if (edgegap != null) await edgegap.CompleteMatchAsync();
                    if (online != null && online.InParty) await online.ResetLocalReadyAsync();
                }
                else if (edgegap != null)
                    await edgegap.LeavePartyAsync();
                else if (online != null && online.InParty)
                    await online.LeavePartyAsync();

                if (coop != null && coop.InLobby) await coop.LeaveLobbyAsync();
            }
            catch (System.Exception error) { Debug.LogWarning("DF: session exit: " + error.Message); }
            finally
            {
                CoopNetworkBootstrap.CancelDedicatedConnection();
                CoopNetworkBootstrap.ShutdownLocalNetwork();
                CoopAuthority.EndMatch();
                MatchClock.Set(PauseReason.Result, false); MatchClock.Set(PauseReason.Settings, false);
                MatchClock.Set(PauseReason.Transition, false);
                if (world != null) Object.Destroy(world);
                // Keep the old battlefield/skybox hidden while the menu poster and video warm up.
                // Gameplay camera visibility is restored by the next arena build, not by menu exit.
                Alterverse.Prototype.PrototypeBootstrap.PrepareTransitionFrame();
                if (keepOnlineParty)
                {
                    MainMenuController.ShowCoopAfterMatch(failureMessage);
                    OnlinePartyOverlay.Show();
                }
                else
                {
                    MainMenuController.ShowHome();
                    if(!string.IsNullOrEmpty(failureMessage))MainMenuController.ShowLaunchFailure(failureMessage);
                }
                Leaving = false;
            }
        }
    }
}
