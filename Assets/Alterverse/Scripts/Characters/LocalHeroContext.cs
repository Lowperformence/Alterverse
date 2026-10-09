using Alterverse.Combat;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.Characters
{
    /// <summary>Local presentation ownership is independent of server-owned respawn.</summary>
    public static class LocalHeroContext
    {
        public static bool IsLocal(GameObject actor)
        {
            if(actor==null)return false;
            CoopHeroSlot slot=actor.GetComponent<CoopHeroSlot>();
            return slot!=null?slot.IsLocalHuman:actor.GetComponent<BasicAttack>()!=null;
        }

        public static GameObject Find()
        {
            if(CoopAuthority.Active)
            {
                foreach(CoopHeroSlot slot in Object.FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                    if(slot.IsLocalHuman)return slot.gameObject;
                return null; // A server-owned remote hero is never the local presentation owner.
            }
            HeroRespawn player=Object.FindAnyObjectByType<HeroRespawn>();
            return player!=null?player.gameObject:null;
        }

        public static TeamMember Member=>Find()?.GetComponent<TeamMember>();
    }
}
