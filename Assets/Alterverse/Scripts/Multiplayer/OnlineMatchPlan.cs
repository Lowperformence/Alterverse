using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Deterministic 5v5 online roster. The private friend party is kept together on team 0,
    /// public players fill the remaining human slots, and every open slot becomes a
    /// server-authoritative bot so a match can always start.
    /// </summary>
    [Serializable]
    public sealed class OnlineMatchPlan : CoopMatchPlan
    {
        private static readonly HeroId[] BotPool =
        {
            HeroId.Veyr, HeroId.Kaelon, HeroId.Nyra, HeroId.Elyra, HeroId.Mira, HeroId.Rook
        };

        public static OnlineMatchPlan Build(
            string matchId,
            ArenaVariant arena,
            BotDifficulty difficulty,
            IReadOnlyList<OnlinePartyService.MemberView> members)
        {
            if (members == null || members.Count < 1 || members.Count > MatchSession.MaxPlayers)
                throw new InvalidOperationException("Online match plan requires 1-10 human players.");

            List<OnlinePartyService.MemberView> party = new(MatchSession.TeamSize);
            List<OnlinePartyService.MemberView> publicPlayers = new(MatchSession.MaxPlayers);
            OnlinePartyService.MemberView host = null;

            for (int i = 0; i < members.Count; i++)
            {
                OnlinePartyService.MemberView member = members[i];
                if (member == null)
                    throw new InvalidOperationException("Online lobby contains an invalid player.");

                if (member.IsHost)
                {
                    if (host != null)
                        throw new InvalidOperationException("Online lobby contains multiple hosts.");
                    host = member;
                }

                if (member.IsPartyMember) party.Add(member);
                else publicPlayers.Add(member);
            }

            if (host == null)
                throw new InvalidOperationException("Online lobby has no host.");
            if (!host.IsPartyMember)
                throw new InvalidOperationException("Online host must belong to the friend party.");
            if (party.Count < 1 || party.Count > MatchSession.TeamSize)
                throw new InvalidOperationException("Friend party must contain 1-5 players.");

            party.Sort((a, b) =>
            {
                if (a.IsHost != b.IsHost) return a.IsHost ? -1 : 1;
                return string.CompareOrdinal(a.PlayerId, b.PlayerId);
            });
            publicPlayers.Sort((a, b) => string.CompareOrdinal(a.PlayerId, b.PlayerId));

            MatchSession session = new(matchId, SessionMode.OnlineCasual);

            int team0Humans = 0;
            for (int i = 0; i < party.Count; i++)
            {
                if (!session.TryAssign(team0Humans, HumanSlot(party[i], 0)))
                    throw new InvalidOperationException($"Could not assign friend-party slot {team0Humans}.");
                team0Humans++;
            }

            int team1Humans = 0;
            for (int i = 0; i < publicPlayers.Count; i++)
            {
                OnlinePartyService.MemberView member = publicPlayers[i];
                if (team0Humans < MatchSession.TeamSize)
                {
                    int slot = team0Humans;
                    if (!session.TryAssign(slot, HumanSlot(member, 0)))
                        throw new InvalidOperationException($"Could not assign online human slot {slot}.");
                    team0Humans++;
                }
                else
                {
                    if (team1Humans >= MatchSession.TeamSize)
                        throw new InvalidOperationException("Online roster contains more than ten human players.");
                    int slot = MatchSession.TeamSize + team1Humans;
                    if (!session.TryAssign(slot, HumanSlot(member, 1)))
                        throw new InvalidOperationException($"Could not assign online human slot {slot}.");
                    team1Humans++;
                }
            }

            FillTeamWithBots(session, 0);
            FillTeamWithBots(session, 1);

            if (!session.IsFull)
                throw new InvalidOperationException("Online roster could not fill all ten match slots.");

            return new OnlineMatchPlan
            {
                MatchId = string.IsNullOrEmpty(matchId) ? Guid.NewGuid().ToString("N") : matchId,
                Arena = arena,
                Difficulty = difficulty,
                Session = session,
                HumanCount = members.Count
            };
        }

        private static void FillTeamWithBots(MatchSession session, int team)
        {
            int start = team * MatchSession.TeamSize;
            int end = start + MatchSession.TeamSize;
            HashSet<HeroId> used = new();

            for (int slot = start; slot < end; slot++)
            {
                MatchPlayerSlot existing = session.Slots[slot];
                if (existing != null) used.Add(existing.Hero);
            }

            int poolCursor = 0;
            for (int slot = start; slot < end; slot++)
            {
                if (session.Slots[slot] != null) continue;

                HeroId hero = PickBotHero(used, ref poolCursor);
                used.Add(hero);
                string id = $"online-bot-t{team}-{slot}";
                if (!session.TryAssign(slot, BotSlot(id, hero, team)))
                    throw new InvalidOperationException($"Could not assign online bot slot {slot}.");
            }
        }

        private static HeroId PickBotHero(HashSet<HeroId> used, ref int cursor)
        {
            for (int attempt = 0; attempt < BotPool.Length; attempt++)
            {
                HeroId candidate = BotPool[cursor % BotPool.Length];
                cursor++;
                if (!used.Contains(candidate)) return candidate;
            }

            HeroId fallback = BotPool[cursor % BotPool.Length];
            cursor++;
            return fallback;
        }

        private static MatchPlayerSlot HumanSlot(OnlinePartyService.MemberView member, int team) => new()
        {
            PlayerId = member.PlayerId,
            DisplayName = member.DisplayName,
            Team = team,
            Hero = member.Hero,
            IsLocal = member.IsLocal,
            IsBot = false,
            Ready = member.Ready,
            Connected = true,
            IsHost = member.IsHost
        };

        private static MatchPlayerSlot BotSlot(string id, HeroId hero, int team) => new()
        {
            PlayerId = id,
            DisplayName = id,
            Team = team,
            Hero = hero,
            IsLocal = false,
            IsBot = true,
            Ready = true,
            Connected = true,
            IsHost = false
        };
    }
}
