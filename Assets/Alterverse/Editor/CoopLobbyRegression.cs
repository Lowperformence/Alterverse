using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
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

            HeroId[] humanHeroes={HeroId.Veyr,HeroId.Mira,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra};
            for(int humanCount=CoopLobbyService.MinHumanPlayers;humanCount<=CoopLobbyService.MaxHumanPlayers;humanCount++)
            {
                var members=new List<CoopLobbyService.MemberView>();
                for(int i=0;i<humanCount;i++)
                    members.Add(new CoopLobbyService.MemberView
                    {
                        PlayerId=i==0?"host":$"friend-{i}",
                        DisplayName=i==0?"Host":$"Friend {i}",
                        Hero=humanHeroes[i],
                        Ready=true,
                        IsHost=i==0,
                        IsLocal=i==0
                    });

                CoopMatchPlan plan=CoopMatchPlan.Build($"regression-{humanCount}",ArenaVariant.Kryon,BotDifficulty.Hard,members);
                Assert(plan!=null&&plan.Session!=null,$"{humanCount}-player co-op match plan was not created.");
                Assert(plan.HumanCount==humanCount,$"{humanCount}-player plan reports wrong human count.");
                Assert(plan.Arena==ArenaVariant.Kryon&&plan.Difficulty==BotDifficulty.Hard,"Host match settings were not preserved.");
                Assert(plan.Session.IsFull,$"{humanCount}-player co-op plan must always fill ten match slots.");

                for(int i=0;i<humanCount;i++)Assert(!plan.Session.Slots[i].IsBot,$"Human slot {i} became AI.");
                for(int i=humanCount;i<5;i++)Assert(plan.Session.Slots[i].IsBot,$"Missing Team Zero bot in slot {i}.");
                for(int i=5;i<10;i++)Assert(plan.Session.Slots[i].IsBot,$"Lumen slot {i} must always be AI.");
                for(int i=0;i<5;i++)Assert(plan.Session.Slots[i].Team==0,$"Zero slot {i} has wrong team.");
                for(int i=5;i<10;i++)Assert(plan.Session.Slots[i].Team==1,$"Lumen slot {i} has wrong team.");
                Assert(plan.Session.Slots[0].IsHost,"First Team Zero slot must be the host.");
            }

            var fiveA=new List<CoopLobbyService.MemberView>
            {
                new(){PlayerId="host",DisplayName="Host",Hero=HeroId.Veyr,Ready=true,IsHost=true,IsLocal=true},
                new(){PlayerId="d",DisplayName="D",Hero=HeroId.Elyra,Ready=true},
                new(){PlayerId="b",DisplayName="B",Hero=HeroId.Mira,Ready=true},
                new(){PlayerId="c",DisplayName="C",Hero=HeroId.Nyra,Ready=true},
                new(){PlayerId="a",DisplayName="A",Hero=HeroId.Kaelon,Ready=true}
            };
            var fiveB=new List<CoopLobbyService.MemberView>
            {
                new(){PlayerId="c",DisplayName="C",Hero=HeroId.Nyra,Ready=true,IsLocal=true},
                new(){PlayerId="a",DisplayName="A",Hero=HeroId.Kaelon,Ready=true},
                new(){PlayerId="host",DisplayName="Host",Hero=HeroId.Veyr,Ready=true,IsHost=true},
                new(){PlayerId="b",DisplayName="B",Hero=HeroId.Mira,Ready=true},
                new(){PlayerId="d",DisplayName="D",Hero=HeroId.Elyra,Ready=true}
            };
            CoopMatchPlan orderedA=CoopMatchPlan.Build("stable",ArenaVariant.Moon,BotDifficulty.Normal,fiveA);
            CoopMatchPlan orderedB=CoopMatchPlan.Build("stable",ArenaVariant.Moon,BotDifficulty.Normal,fiveB);
            Assert(CoopPlanHandshake.Fingerprint(orderedA)==CoopPlanHandshake.Fingerprint(orderedB),
                "Five-player match plan fingerprint must not depend on local member-list ordering.");

            bool duplicateRejected=false;
            try
            {
                CoopMatchPlan.Build("bad",ArenaVariant.Moon,BotDifficulty.Normal,new List<CoopLobbyService.MemberView>
                {
                    new(){PlayerId="a",DisplayName="A",Hero=HeroId.Rook,Ready=true,IsHost=true},
                    new(){PlayerId="b",DisplayName="B",Hero=HeroId.Rook,Ready=true,IsHost=false}
                });
            }
            catch(System.InvalidOperationException){duplicateRejected=true;}
            Assert(duplicateRejected,"Duplicate human heroes must invalidate the co-op plan.");

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

            Assert(session.IsFull,"A complete co-op roster must fill all 10 match slots.");
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
