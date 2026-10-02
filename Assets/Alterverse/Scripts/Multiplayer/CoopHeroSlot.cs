using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Stable 0-9 roster identity shared by every peer in a network match.</summary>
    public sealed class CoopHeroSlot : MonoBehaviour
    {
        [SerializeField]private int slot=-1;
        [SerializeField]private string playerId="";
        [SerializeField]private bool human;
        [SerializeField]private bool localHuman;

        public int Slot=>slot;
        public string PlayerId=>playerId;
        public bool IsHuman=>human;
        public bool IsLocalHuman=>localHuman;

        public void Configure(int rosterSlot,MatchPlayerSlot data)
        {
            Configure(rosterSlot,data,data!=null&&data.IsLocal);
        }

        public void Configure(int rosterSlot,MatchPlayerSlot data,bool isLocalOnThisPeer)
        {
            slot=Mathf.Clamp(rosterSlot,0,MatchSession.MaxPlayers-1);
            playerId=data?.PlayerId??"";
            human=data!=null&&!data.IsBot;
            localHuman=human&&isLocalOnThisPeer;
        }
    }
}
