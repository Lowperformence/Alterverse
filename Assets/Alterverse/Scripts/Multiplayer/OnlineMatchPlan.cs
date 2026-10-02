using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Deterministic 5v5 online roster. Up to ten humans are assigned in stable order;
    /// every open slot is filled by a server-authoritative bot so a match can still start
    /// when matchmaking does not find enough players.
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

            List<OnlinePartyService.MemberView> ordered = new(members.Count);
            OnlinePartyService.MemberView host = null;
            for (int i = 0; i < members.Count; i++)
            {
                OnlinePartyService.MemberView member = members[i];
                if (member == null) throw new InvalidOperationException("Online lobby contains an invalid player.");
                if (!member.IsHost) continue;
                if (host != null) throw new InvalidOperationException("Online lobby contains multiple hosts.");
                host = member;
            }

            if (host == null) throw new InvalidOperationException("Online lobby has no host.");
            ordered.Add(host);

            List<OnlinePartyService.MemberView> guests = new(members.Count - 1);
            for (int i = 0; i < members.Count; i++)
                if (members[i] != host) guests.Add(members[i]);
            guests.Sort((a, b) => string.CompareOrdinal(a.PlayerId, b.PlayerId));
            ordered.AddRange(guests);

            MatchSession session = new(matchId, SessionMode.OnlineCasual);
            for (int i = 0; i < ordered.Count; i++)
            {
                int team = i < MatchSession.TeamSize ? 0 : 1;
                if (!session.TryAssign(i, HumanSlot(ordered[i], team, i == 0)))
                    throw new InvalidOperationException($"Could not assign online human slot {i}.");
            }

            FillTeamWithBots(session, 0, ordered.Count);
            FillTeamWithBots(session, 1, ordered.Count);

            if (!session.IsFull)
                throw new InvalidOperationException("Online roster could not fill all ten match slots.");

            return new OnlineMatchPlan
            {
                MatchId = string.IsNullOrEmpty(matchId) ? Guid.NewGuid().ToString("N") : matchId,
                Arena = arena,
                Difficulty = difficulty,
                Session = session,
                HumanCount = ordered.Count
            };
        }

        private static void FillTeamWithBots(MatchSession session, int team, int humanCount)
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

        private static MatchPlayerSlot HumanSlot(OnlinePartyService.MemberView member, int team, bool host) => new()
        {
            PlayerId = member.PlayerId,
            DisplayName = member.DisplayName,
            Team = team,
            Hero = member.Hero,
            IsLocal = member.IsLocal,
            IsBot = false,
            Ready = member.Ready,
            Connected = true,
            IsHost = host
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