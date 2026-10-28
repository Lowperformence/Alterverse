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
            IReadOnlyList<CoopLobbyService.MemberView> members,
            string botNameSeed = null, bool allowHeroConflicts = false)
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
                if(!humanHeroes.Add(ordered[i].Hero) && !allowHeroConflicts)
                    throw new InvalidOperationException("Co-op human players must use different heroes.");

            MatchSession session=new(matchId,SessionMode.Private);
            for(int i=0;i<ordered.Count;i++)
                if(!session.TryAssign(i,HumanSlot(ordered[i],i==0)))
                    throw new InvalidOperationException($"Could not assign co-op human slot {i}.");

            HeroId[] pool={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Nerok,HeroId.Rook};
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
                throw new InvalidOperationException($"Co-op roster could not fill Team Zero to {MatchSession.TeamSize} members.");

            HeroId[] enemyHeroes={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,
                !string.IsNullOrEmpty(matchId)&&(matchId[0]&1)==0?HeroId.Nerok:HeroId.Rook};
            for(int i=0;i<enemyHeroes.Length;i++)
                if(!session.TryAssign(MatchSession.TeamSize+i,BotSlot($"enemy-{i}",enemyHeroes[i],1)))
                    throw new InvalidOperationException("Could not assign enemy co-op bot.");

            if(!session.IsFull)throw new InvalidOperationException($"Co-op match plan must fill all {MatchSession.MaxPlayers} match slots.");

            NameBots(session, botNameSeed ?? matchId, (botNameSeed ?? matchId) + "/opponents");
            return new CoopMatchPlan
            {
                MatchId=matchId,
                Arena=arena,
                Difficulty=difficulty,
                Session=session,
                HumanCount=ordered.Count
            };
        }

        // Preserve teams/heroes while assigning a fresh receipt namespace to each round.
        public static CoopMatchPlan NextRound(CoopMatchPlan previous, string id, int humanMask)
        {
            if (previous?.Session == null || !previous.Session.IsFull || string.IsNullOrEmpty(id) || id == previous.MatchId)
                throw new InvalidOperationException("Invalid next-round plan.");
            CoopMatchPlan next = previous is OnlineMatchPlan ? new OnlineMatchPlan() : new CoopMatchPlan();
            next.MatchId = id; next.Arena = previous.Arena;
            next.Difficulty = previous is OnlineMatchPlan ? BotDifficultySettings.OnlineDifficulty : previous.Difficulty;
            next.Session = new MatchSession(id, previous.Session.Mode);
            for (int i = 0; i < MatchSession.MaxPlayers; i++)
            {
                MatchPlayerSlot old = previous.Session.Slots[i];
                bool human = !old.IsBot && (humanMask & (1 << i)) != 0;
                var slot = new MatchPlayerSlot {
                    PlayerId = human || old.IsBot ? old.PlayerId : "round-bot-" + i,
                    DisplayName = human || old.IsBot ? old.DisplayName : null,
                    AvatarId = old.AvatarId,
                    Team = old.Team, Hero = old.Hero, BattleItem = BattleItemType.None, IsLocal = human && old.IsLocal,
                    IsBot = !human, Ready = true, Connected = true, IsHost = human && old.IsHost
                };
                if (!next.Session.TryAssign(i, slot)) throw new InvalidOperationException("Invalid next-round roster.");
                if (human) next.HumanCount++;
            }
            NameBots(next.Session, id, id + "/opponents");
            if (next.HumanCount == 0) throw new InvalidOperationException("No players remain.");
            return next;
        }

        internal static void NameBots(MatchSession session, string teamZeroSeed, string teamOneSeed)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var slot in session.Slots)
                if (slot != null && !string.IsNullOrEmpty(slot.DisplayName)) used.Add(slot.DisplayName);
            for (int i = 0; i < session.Slots.Length; i++)
            {
                var slot = session.Slots[i];
                if (slot == null || !slot.IsBot || !string.IsNullOrEmpty(slot.DisplayName)) continue;
                for (int attempt = 0; attempt < SafeIdentityCatalog.BotNameCount; attempt++)
                {
                    string name = SafeIdentityCatalog.BotName(slot.Team == 0 ? teamZeroSeed : teamOneSeed,
                        i % MatchSession.TeamSize, attempt);
                    if (used.Add(name)) { slot.DisplayName = name; slot.AvatarId = name[0] % 6; break; }
                }
                if (string.IsNullOrEmpty(slot.DisplayName)) throw new InvalidOperationException("Bot alias pool exhausted.");
            }
        }

        private static MatchPlayerSlot HumanSlot(CoopLobbyService.MemberView member,bool host)=>new()
        {
            PlayerId=member.PlayerId,
            DisplayName=member.DisplayName,
            AvatarId=member.AvatarId,
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
            DisplayName=null,
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

