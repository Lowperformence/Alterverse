using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;

namespace Alterverse.Multiplayer
{
    [Serializable]
    public class CoopMatchPlan
    {
        public string MatchId;
        public ArenaVariant Arena;
        public BotDifficulty Difficulty;
        public MatchSession Session;

        public MatchPlayerSlot Host=>Session?.Slots[0];
        public MatchPlayerSlot Friend=>Session?.Slots[1];
        public int HumanCount { get; protected set; }

        public static CoopMatchPlan Build(
            string matchId,
            ArenaVariant arena,
            BotDifficulty difficulty,
            IReadOnlyList<CoopLobbyService.MemberView> members)
        {
            if(members==null||members.Count<CoopLobbyService.MinHumanPlayers||members.Count>CoopLobbyService.MaxHumanPlayers)
                throw new InvalidOperationException($"Co-op match plan requires {CoopLobbyService.MinHumanPlayers}-{CoopLobbyService.MaxHumanPlayers} human players.");

            List<CoopLobbyService.MemberView> ordered=new(members.Count);
            CoopLobbyService.MemberView host=null;
            for(int i=0;i<members.Count;i++)
            {
                CoopLobbyService.MemberView member=members[i];
                if(member==null)throw new InvalidOperationException("Co-op lobby contains an invalid player.");
                if(member.IsHost)
                {
                    if(host!=null)throw new InvalidOperationException("Co-op lobby contains multiple hosts.");
                    host=member;
                }
            }
            if(host==null)throw new InvalidOperationException("Co-op lobby has no host.");
            ordered.Add(host);

            List<CoopLobbyService.MemberView> guests=new(members.Count-1);
            for(int i=0;i<members.Count;i++)if(members[i]!=host)guests.Add(members[i]);
            guests.Sort((a,b)=>string.CompareOrdinal(a.PlayerId,b.PlayerId));
            ordered.AddRange(guests);

            HashSet<HeroId> humanHeroes=new();
            for(int i=0;i<ordered.Count;i++)
                if(!humanHeroes.Add(ordered[i].Hero))
                    throw new InvalidOperationException("Co-op human players must use different heroes.");

            MatchSession session=new(matchId,SessionMode.Private);
            for(int i=0;i<ordered.Count;i++)
                if(!session.TryAssign(i,HumanSlot(ordered[i],i==0)))
                    throw new InvalidOperationException($"Could not assign co-op human slot {i}.");

            HeroId[] pool={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Rook};
            int allySlot=ordered.Count;
            for(int i=0;i<pool.Length&&allySlot<MatchSession.TeamSize;i++)
            {
                HeroId hero=pool[i];
                if(humanHeroes.Contains(hero))continue;
                if(!session.TryAssign(allySlot,BotSlot($"ally-{allySlot}",hero,0)))
                    throw new InvalidOperationException("Could not assign allied co-op bot.");
                allySlot++;
            }
            if(allySlot!=MatchSession.TeamSize)
                throw new InvalidOperationException("Co-op roster could not fill Team Zero to five members.");

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
                Session=session,
                HumanCount=ordered.Count
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