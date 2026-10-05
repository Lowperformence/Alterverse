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
        public static async Task ReturnToMenuAsync()
        {
            if (Leaving) return;
            Leaving = true;
            GameObject world = GameObject.Find("DF_MatchWorld");
            if (world != null) world.SetActive(false);
            try
            {
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
                if (edgegap != null)
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
                MainMenuController.ShowHome();
                Leaving = false;
            }
        }
    }
}
