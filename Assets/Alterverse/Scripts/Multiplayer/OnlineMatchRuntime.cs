using Alterverse.Characters;
using Alterverse.Match;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Enters the existing finished battlefield from a verified online match plan.</summary>
    public static class OnlineMatchRuntime
    {
        public static bool Enter(OnlineMatchPlan plan)
        {
            if (plan?.Session == null || !plan.Session.IsFull || !CoopAuthority.Active)
                return false;

            int localSlot = CoopRosterBuilder.FindLocalHumanSlot(plan);
            NetworkManager manager = NetworkManager.Singleton;
            bool serverOnly = localSlot < 0 && manager != null && manager.IsListening && manager.IsServer &&
                              CoopAuthority.LocalHasServerAuthority;

            if (localSlot < 0 && !serverOnly)
            {
                Debug.LogError("DIMENSIONS FRONT ONLINE: local human slot missing from verified match plan.");
                CoopAuthority.EndMatch();
                return false;
            }

            if (!serverOnly)
            {
                MatchPlayerSlot local = plan.Session.Slots[localSlot];
                if (local == null)
                {
                    Debug.LogError("DIMENSIONS FRONT ONLINE: local human slot is invalid.");
                    CoopAuthority.EndMatch();
                    return false;
                }
                PlayerHeroSelection.Select(local.Hero);
            }

            Alterverse.Prototype.PrototypeBootstrap bootstrap =
                Object.FindAnyObjectByType<Alterverse.Prototype.PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("DIMENSIONS FRONT ONLINE: match bootstrap unavailable.");
                CoopAuthority.EndMatch();
                return false;
            }

            bool restarting = GameObject.Find("DF_MatchWorld") != null;
            if (!serverOnly) Alterverse.Prototype.PrototypeBootstrap.PrepareTransitionFrame();
            MatchClock.Set(PauseReason.Result, false);
            MatchClock.Set(PauseReason.Menu, false);

            GameObject menu = GameObject.Find("DF_MainMenu");
            if (menu != null) menu.SetActive(false);

            if (!serverOnly)
            {
                Alterverse.UI.MatchEntryCurtain.Begin();
                Alterverse.UI.OnlineMatchLoadingScreen.Begin(plan);
                bootstrap.StartCoroutine(BuildClientAfterLoadingFrame(bootstrap, plan, restarting));
                return true; // As with restart, the coroutine owns build failure recovery.
            }

            if (restarting)
            {
                bootstrap.StartCoroutine(bootstrap.RestartPrototype());
                return true; // The coroutine owns recovery if the next-round build fails.
            }
            return bootstrap.BuildPrototype();
        }

        private static System.Collections.IEnumerator BuildClientAfterLoadingFrame(
            Alterverse.Prototype.PrototypeBootstrap bootstrap, OnlineMatchPlan plan, bool restarting)
        {
            // Let the loading surface paint before synchronous arena construction blocks a frame.
            // The dedicated authority continues immediately; only client presentation waits.
            yield return null;
            NetworkManager manager = NetworkManager.Singleton;
            if (Alterverse.Match.MatchExit.Leaving || !CoopAuthority.Active ||
                !ReferenceEquals(CoopAuthority.ActivePlan, plan))
            {
                Alterverse.UI.OnlineMatchLoadingScreen.Cancel();
                yield break;
            }
            if (manager == null || !manager.IsConnectedClient)
            {
                Alterverse.UI.OnlineMatchLoadingScreen.Cancel();
                _ = Alterverse.Match.MatchExit.ReturnToMenuAsync("Game server disconnected while loading. Please try again.");
                yield break;
            }
            if (restarting) yield return bootstrap.RestartPrototype();
            else if (!bootstrap.BuildPrototype()) Alterverse.UI.OnlineMatchLoadingScreen.Cancel();
            // The bootstrap completes the loading surface when the initialized arena camera reveals.
        }
    }
}

