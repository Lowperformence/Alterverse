using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;

namespace Alterverse.Multiplayer
{
    [Serializable]
    public sealed class CoopMatchPlan
    {
        public string MatchId;
        public ArenaVariant Arena;
        public BotDifficulty Difficulty;
        public MatchSession Session;

        public MatchPlayerSlot Host=>Session?.Slots[0];
        public MatchPlayerSlot Friend=>Session?.Slots[1];

        public static CoopMatchPlan Build(
            string matchId,
            ArenaVariant arena,
            BotDifficulty difficulty,
            IReadOnlyList<CoopLobbyService.MemberView> members)
        {
            if(members==null||members.Count!=2)throw new InvalidOperationException("Co-op match plan requires exactly two human players.");
            if(members[0].Hero==members[1].Hero)throw new InvalidOperationException("Co-op human players must use different heroes.");

            MatchSession session=new(matchId,SessionMode.Private);

            CoopLobbyService.MemberView host=members[0].IsHost?members[0]:members[1];
            CoopLobbyService.MemberView friend=members[0].IsHost?members[1]:members[0];

            if(!session.TryAssign(0,HumanSlot(host,true)))throw new InvalidOperationException("Could not assign co-op host.");
            if(!session.TryAssign(1,HumanSlot(friend,false)))throw new InvalidOperationException("Could not assign co-op friend.");

            HeroId[] pool={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Rook};
            int allySlot=2;
            for(int i=0;i<pool.Length&&allySlot<5;i++)
            {
                HeroId hero=pool[i];
                if(hero==host.Hero||hero==friend.Hero)continue;
                if(!session.TryAssign(allySlot,BotSlot($"ally-{allySlot}",hero,0)))
                    throw new InvalidOperationException("Could not assign allied co-op bot.");
                allySlot++;
            }
            if(allySlot!=5)throw new InvalidOperationException("Co-op roster could not fill three allied bot slots.");

            HeroId[] enemyHeroes={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Rook};
            for(int i=0;i<enemyHeroes.Length;i++)
                if(!session.TryAssign(5+i,BotSlot($"enemy-{i}",enemyHeroes[i],1)))
                    throw new InvalidOperationException("Could not assign enemy co-op bot.");

            if(!session.IsFull)throw new InvalidOperationException("Co-op match plan must fill all ten match slots.");

            return new CoopMatchPlan
            {
                MatchId=matchId,
                Arena=arena,
                Difficulty=difficulty,
                Session=session
            };
        }

        private static MatchPlayerSlot HumanSlot(CoopLobbyService.MemberView member,bool host)=>new()
        {
            PlayerId=member.PlayerId,
            DisplayName=member.DisplayName,
            Team=0,
            Hero=member.Hero,
            IsLocal=member.IsLocal,
            IsBot=false,
            Ready=member.Ready,
            Connected=true,
            IsHost=host
        };

        private static MatchPlayerSlot BotSlot(string id,HeroId hero,int team)=>new()
        {
            PlayerId=id,
            DisplayName=id,
            Team=team,
            Hero=hero,
            IsLocal=false,
            IsBot=true,
            Ready=true,
            Connected=true,
            IsHost=false
        };
    }
}
