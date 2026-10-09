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

            if (restarting)
            {
                bootstrap.StartCoroutine(bootstrap.RestartPrototype());
                return true; // The coroutine owns recovery if the next-round build fails.
            }
            return bootstrap.BuildPrototype();
        }
    }
}
