using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Profile;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Deterministic 5v5 online roster. Friend parties stay on the Edgegap-assigned team,
    /// public players fill the remaining human slots, and every open slot becomes a
    /// server-authoritative bot so a match can always start.
    /// </summary>
    [Serializable]
    public sealed class OnlineMatchPlan : CoopMatchPlan
    {
        [Serializable]
        public sealed class DedicatedMember
        {
            public string PlayerId;
            public string DisplayName;
            public string TeamId;
            public HeroId Hero;
            public BattleItemType BattleItem;
        }

        private static readonly HeroId[] BotPool =
        {
            HeroId.Veyr, HeroId.Kaelon, HeroId.Nyra, HeroId.Elyra, HeroId.Mira, HeroId.Nerok, HeroId.Rook
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

        /// <summary>
        /// Dedicated-server roster path. Edgegap team ids are the authority for team placement;
        /// the server sorts them deterministically, resolves same-team hero conflicts, and fills
        /// every unoccupied 5v5 slot with a server-owned bot.
        /// </summary>
        public static OnlineMatchPlan BuildDedicated(
            string matchId,
            ArenaVariant arena,
            BotDifficulty difficulty,
            IReadOnlyList<DedicatedMember> members)
        {
            if (members == null || members.Count < 1 || members.Count > MatchSession.MaxPlayers)
                throw new InvalidOperationException("Dedicated online match requires 1-10 human players.");

            var teamKeys = new List<string>(2);
            var playersByTeam = new Dictionary<string, List<DedicatedMember>>(StringComparer.Ordinal);
            var playerIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < members.Count; i++)
            {
                DedicatedMember member = members[i];
                if (member == null || string.IsNullOrWhiteSpace(member.PlayerId) || string.IsNullOrWhiteSpace(member.TeamId))
                    throw new InvalidOperationException("Dedicated online member is missing player or team identity.");
                if (!playerIds.Add(member.PlayerId))
                    throw new InvalidOperationException("Dedicated online roster contains a duplicate player id.");

                string teamKey = member.TeamId.Trim();
                if (!playersByTeam.TryGetValue(teamKey, out List<DedicatedMember> teamPlayers))
                {
                    if (teamKeys.Count >= 2)
                        throw new InvalidOperationException("Dedicated online roster contains more than two Edgegap teams.");
                    teamKeys.Add(teamKey);
                    teamPlayers = new List<DedicatedMember>(MatchSession.TeamSize);
                    playersByTeam.Add(teamKey, teamPlayers);
                }
                if (teamPlayers.Count >= MatchSession.TeamSize)
                    throw new InvalidOperationException("Edgegap assigned more than five humans to one team.");
                teamPlayers.Add(member);
            }

            teamKeys.Sort(StringComparer.Ordinal);
            MatchSession session = new(matchId, SessionMode.OnlineCasual);
            int humanCount = 0;

            for (int team = 0; team < teamKeys.Count; team++)
            {
                List<DedicatedMember> teamPlayers = playersByTeam[teamKeys[team]];
                teamPlayers.Sort((a, b) => string.CompareOrdinal(a.PlayerId, b.PlayerId));
                var usedHeroes = new HashSet<HeroId>();
                int replacementCursor = 0;

                for (int row = 0; row < teamPlayers.Count; row++)
                {
                    DedicatedMember member = teamPlayers[row];
                    HeroId hero = member.Hero;
                    if (!usedHeroes.Add(hero))
                    {
                        hero = PickBotHero(usedHeroes, ref replacementCursor);
                        usedHeroes.Add(hero);
                    }

                    int slot = team * MatchSession.TeamSize + row;
                    MatchPlayerSlot player = new()
                    {
                        PlayerId = member.PlayerId,
                        DisplayName = string.IsNullOrWhiteSpace(member.DisplayName) ? "TRAVELER" : member.DisplayName,
                        Team = team,
                        Hero = hero,
                        BattleItem = member.BattleItem,
                        IsLocal = false,
                        IsBot = false,
                        Ready = true,
                        Connected = true,
                        IsHost = false
                    };
                    if (!session.TryAssign(slot, player))
                        throw new InvalidOperationException($"Could not assign dedicated human slot {slot}.");
                    humanCount++;
                }
            }

            FillTeamWithBots(session, 0);
            FillTeamWithBots(session, 1);
            if (!session.IsFull)
                throw new InvalidOperationException("Dedicated online roster could not fill all ten match slots.");

            return new OnlineMatchPlan
            {
                MatchId = string.IsNullOrWhiteSpace(matchId) ? Guid.NewGuid().ToString("N") : matchId,
                Arena = arena,
                Difficulty = difficulty,
                Session = session,
                HumanCount = humanCount
            };
        }

        /// <summary>
        /// Rebuilds the exact authoritative ten-slot plan received from the dedicated server and
        /// marks only this client's player id as local. IsLocal is intentionally excluded from the
        /// plan fingerprint, so all peers verify the same authoritative roster.
        /// </summary>
        public static OnlineMatchPlan FromAuthoritativeSlots(
            string matchId,
            ArenaVariant arena,
            BotDifficulty difficulty,
            IReadOnlyList<MatchPlayerSlot> slots,
            string localPlayerId)
        {
            if (slots == null || slots.Count != MatchSession.MaxPlayers)
                throw new InvalidOperationException("Authoritative online roster must contain exactly ten slots.");

            MatchSession session = new(matchId, SessionMode.OnlineCasual);
            int humans = 0;
            bool localFound = false;

            for (int i = 0; i < MatchSession.MaxPlayers; i++)
            {
                MatchPlayerSlot source = slots[i];
                if (source == null)
                    throw new InvalidOperationException($"Authoritative online roster slot {i} is empty.");

                bool isLocal = !source.IsBot && !string.IsNullOrEmpty(localPlayerId) && source.PlayerId == localPlayerId;
                MatchPlayerSlot copy = new()
                {
                    PlayerId = source.PlayerId,
                    DisplayName = source.DisplayName,
                    Team = source.Team,
                    Hero = source.Hero,
                    BattleItem = source.BattleItem,
                    IsLocal = isLocal,
                    IsBot = source.IsBot,
                    Ready = source.Ready,
                    Connected = source.Connected,
                    IsHost = source.IsHost
                };
                if (!session.TryAssign(i, copy))
                    throw new InvalidOperationException($"Could not rebuild authoritative online slot {i}.");
                if (!copy.IsBot) humans++;
                if (isLocal) localFound = true;
            }

            if (!localFound)
                throw new InvalidOperationException("Local player is missing from the authoritative online roster.");

            return new OnlineMatchPlan
            {
                MatchId = string.IsNullOrWhiteSpace(matchId) ? session.SessionId : matchId,
                Arena = arena,
                Difficulty = difficulty,
                Session = session,
                HumanCount = humans
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
            BattleItem = member.BattleItem,
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
            BattleItem = BattleItemType.None,
            IsLocal = false,
            IsBot = true,
            Ready = true,
            Connected = true,
            IsHost = false
        };
    }
}
