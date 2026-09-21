using Alterverse.Characters;
using Alterverse.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace Alterverse.Editor
{
    public static class CoopLobbyRegression
    {
        [MenuItem("DIMENSIONS FRONT/Regression/Co-op Lobby Foundation")]
        public static void Run()
        {
            Assert(CoopLobbyService.NormalizeJoinCode(" ab-12 c ")=="AB12C",
                "Join-code normalization must strip separators and uppercase letters.");
            Assert(CoopLobbyService.NormalizeJoinCode("  ")=="",
                "Blank join codes must stay blank.");

            MatchSession session=new("regression",SessionMode.Private);
            Assert(session.IsOnline,"Private sessions must be treated as online sessions.");

            Assert(session.TryAssign(0,Slot("host",HeroId.Veyr,0,false,true)),"Host assignment failed.");
            Assert(session.TryAssign(1,Slot("friend",HeroId.Mira,0,false,false)),"Friend assignment failed.");
            Assert(session.TryAssign(2,Slot("ally-1",HeroId.Kaelon,0,true,false)),"Allied bot 1 failed.");
            Assert(session.TryAssign(3,Slot("ally-2",HeroId.Nyra,0,true,false)),"Allied bot 2 failed.");
            Assert(session.TryAssign(4,Slot("ally-3",HeroId.Elyra,0,true,false)),"Allied bot 3 failed.");

            HeroId[] enemies={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Rook};
            for(int i=0;i<5;i++)
                Assert(session.TryAssign(5+i,Slot("enemy-"+i,enemies[i],1,true,false)),$"Enemy bot {i} failed.");

            Assert(session.IsFull,"2 humans + 3 allied bots + 5 enemy bots must fill a 10-slot match.");
            Assert(!session.TryAssign(9,Slot("overflow",HeroId.Rook,0,true,false)),
                "A sixth Team Zero member must be rejected even when replacing a Team One slot.");

            Debug.Log("DIMENSIONS FRONT: co-op lobby foundation regression assertions passed.");
        }

        private static MatchPlayerSlot Slot(string id,HeroId hero,int team,bool bot,bool host)=>new()
        {
            PlayerId=id,
            DisplayName=id,
            Hero=hero,
            Team=team,
            IsBot=bot,
            IsLocal=host,
            IsHost=host,
            Ready=true,
            Connected=true
        };

        private static void Assert(bool condition,string message)
        {
            if(!condition)throw new System.InvalidOperationException("Co-op lobby regression: "+message);
        }
    }
}
