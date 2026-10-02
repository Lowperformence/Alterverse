using Alterverse.Characters;
using Alterverse.Match;
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
            if (localSlot < 0 || plan.Session.Slots[localSlot] == null)
            {
                Debug.LogError("DIMENSIONS FRONT ONLINE: local human slot missing from verified match plan.");
                CoopAuthority.EndMatch();
                return false;
            }

            PlayerHeroSelection.Select(plan.Session.Slots[localSlot].Hero);

            Alterverse.Prototype.PrototypeBootstrap bootstrap =
                Object.FindAnyObjectByType<Alterverse.Prototype.PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("DIMENSIONS FRONT ONLINE: match bootstrap unavailable.");
                CoopAuthority.EndMatch();
                return false;
            }

            bool restarting = GameObject.Find("DF_MatchWorld") != null;
            Alterverse.Prototype.PrototypeBootstrap.PrepareTransitionFrame();
            MatchClock.Set(PauseReason.Result, false);
            MatchClock.Set(PauseReason.Menu, false);

            GameObject menu = GameObject.Find("DF_MainMenu");
            if (menu != null) menu.SetActive(false);

            if (restarting) bootstrap.StartCoroutine(bootstrap.RestartPrototype());
            else bootstrap.BuildPrototype();
            return true;
        }
    }
}