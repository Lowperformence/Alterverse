using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Profile;

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

            int allySlot=ordered.Count,cursor=0;
            var alliedOrder=BotHeroPool.Shuffled(session.SessionId,0);
            while(allySlot<MatchSession.TeamSize)
            {
                HeroId hero=BotHeroPool.Next(alliedOrder,humanHeroes,ref cursor);humanHeroes.Add(hero);
                if(!session.TryAssign(allySlot,BotSlot($"ally-{allySlot}",hero,0)))throw new InvalidOperationException("Could not assign allied co-op bot.");
                allySlot++;
            }
            var enemies=BotHeroPool.Shuffled(session.SessionId,1);var enemyUsed=new HashSet<HeroId>();cursor=0;
            for(int i=0;i<MatchSession.TeamSize;i++)
            {
                var hero=BotHeroPool.Next(enemies,enemyUsed,ref cursor);enemyUsed.Add(hero);
                if(!session.TryAssign(MatchSession.TeamSize+i,BotSlot($"enemy-{i}",hero,1)))throw new InvalidOperationException("Could not assign enemy co-op bot.");
            }

            if(!session.IsFull)throw new InvalidOperationException($"Co-op match plan must fill all {MatchSession.MaxPlayers} match slots.");

            return new CoopMatchPlan
            {
                MatchId=session.SessionId,
                Arena=arena,
                Difficulty=difficulty,
                Session=session,
                HumanCount=ordered.Count
            };
        }

        // Preserve human teams/heroes; draw fresh full-catalog bots for the new round.
        public static CoopMatchPlan NextRound(CoopMatchPlan previous, string id, int humanMask)
        {
            if (previous?.Session == null || !previous.Session.IsFull || string.IsNullOrEmpty(id) || id == previous.MatchId)
                throw new InvalidOperationException("Invalid next-round plan.");
            CoopMatchPlan next = previous is OnlineMatchPlan ? new OnlineMatchPlan() : new CoopMatchPlan();
            next.MatchId = id; next.Arena = previous.Arena;
            next.Difficulty = previous is OnlineMatchPlan ? BotDifficultySettings.OnlineDifficulty : previous.Difficulty;
            next.Session = new MatchSession(id, previous.Session.Mode);
            var used=new[]{new HashSet<HeroId>(),new HashSet<HeroId>()};
            for(int i=0;i<previous.Session.Slots.Length;i++)
            {
                var humanSlot=previous.Session.Slots[i];
                if(!humanSlot.IsBot&&(humanMask&(1<<i))!=0)used[humanSlot.Team].Add(humanSlot.Hero);
            }
            var botOrder=new[]{BotHeroPool.Shuffled(id,0),BotHeroPool.Shuffled(id,1)};var cursors=new int[2];
            for (int i = 0; i < MatchSession.MaxPlayers; i++)
            {
                MatchPlayerSlot old = previous.Session.Slots[i];
                bool human = !old.IsBot && (humanMask & (1 << i)) != 0;
                HeroId hero=human?old.Hero:BotHeroPool.Next(botOrder[old.Team],used[old.Team],ref cursors[old.Team]);used[old.Team].Add(hero);
                var slot = new MatchPlayerSlot {
                    PlayerId = human || old.IsBot ? old.PlayerId : "round-bot-" + i,
                    DisplayName = human || old.IsBot ? old.DisplayName : hero.ToString(),
                    Team = old.Team, Hero = hero, BattleItem = BattleItemType.None, IsLocal = human && old.IsLocal,
                    IsBot = !human, Ready = true, Connected = true, IsHost = human && old.IsHost
                };
                if (!next.Session.TryAssign(i, slot)) throw new InvalidOperationException("Invalid next-round roster.");
                if (human) next.HumanCount++;
            }
            if (next.HumanCount == 0) throw new InvalidOperationException("No players remain.");
            return next;
        }

        private static MatchPlayerSlot HumanSlot(CoopLobbyService.MemberView member,bool host)=>new()
        {
            PlayerId=member.PlayerId,
            DisplayName=member.DisplayName,
            Team=0,
            Hero=member.Hero,
            BattleItem=member.BattleItem,
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
            BattleItem=BattleItemType.None,
            IsLocal=false,
            IsBot=true,
            Ready=true,
            Connected=true,
            IsHost=false
        };
    }
}
