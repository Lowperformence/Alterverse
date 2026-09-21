using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Host-side fallback for human clients: after the normal authored respawn delay,
    /// remote humans re-enter automatically. The host's own player keeps the existing
    /// manual respawn button. This prevents a disconnected/remote input path from
    /// permanently removing a friend from the match.
    /// </summary>
    [RequireComponent(typeof(HeroRespawn))]
    public sealed class CoopRemoteAutoRespawn : MonoBehaviour
    {
        private HeroRespawn respawn;
        private void Awake(){respawn=GetComponent<HeroRespawn>();}
        private void Update()
        {
            if(!CoopAuthority.Active||!CoopAuthority.LocalIsHost||respawn==null)return;
            CoopHeroSlot slot=GetComponent<CoopHeroSlot>();
            if(slot==null||slot.IsLocalHuman||!slot.IsHuman)return;
            if(respawn.RespawnReady)respawn.ConfirmRespawn();
        }
    }
}
