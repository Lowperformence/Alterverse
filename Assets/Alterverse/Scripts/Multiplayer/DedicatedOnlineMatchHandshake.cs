using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Profile;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Establishes the authoritative online roster after Edgegap assigns all clients to a
    /// dedicated server. Clients announce their Edgegap ticket; on an Edgegap deployment the
    /// server verifies that ticket/team/match against injected Matchmaker variables before a
    /// player is authorized. The server builds the only authoritative 4v4 plan, returns it to
    /// every client, waits for plan acknowledgements, then launches the battlefield on all peers.
    /// </summary>
    [DefaultExecutionOrder(-24000)]
    public sealed class DedicatedOnlineMatchHandshake : MonoBehaviour
    {
        private const string HelloMessage = "DF_DEDICATED_HELLO_V1";
        private const string PlanMessage = "DF_DEDICATED_PLAN_V1";
        private const string AckMessage = "DF_DEDICATED_PLAN_ACK_V1";
        private const string LaunchMessage = "DF_DEDICATED_LAUNCH_V1";
        // Includes source-timed snapshots and stable nonhero combat target identities.
        private const int ProtocolVersion = 7;
        private const uint DisplayNameCapability = 0x44464E31; // Optional trailing DFN1 marker; old servers ignore it.
        private const float HumanJoinWindowSeconds = 60f;
        private const float PlanAckTimeoutSeconds = 20f;
        private const float AuthenticationTimeoutSeconds = 10f;

        private sealed class PendingHuman
        {
            public ulong ClientId;
            public string TicketId;
            public string TeamId;
            public string MatchId;
            public string PlayerId;
            public string DisplayName;
            public HeroId Hero;
            public BattleItemType BattleItem;
            public string Admission;
            public bool SupportsDisplayNames;
            public uint ArenaOwnedMask;
        }

        public static DedicatedOnlineMatchHandshake Instance { get; private set; }
        public static string Status => Instance == null ? "DEDICATED HANDSHAKE OFFLINE" : Instance.status;

        private CustomMessagingManager messaging;
        private bool attached;
        private bool helloSent;
        private bool planSent;
        private bool launched;
        private bool registeringRanked;
        private float firstHelloAt = -1f;
        private uint planFingerprint;
        private float planSentAt = -1f;
        private string status = "WAITING FOR EDGEGAP ASSIGNMENT";
        private readonly Dictionary<ulong, PendingHuman> pending = new();
        private readonly Dictionary<ulong, string> authorizedPlayers = new();
        private readonly HashSet<ulong> planAcks = new();
        private readonly Dictionary<ulong, float> unauthenticatedSince = new();

        public static bool IsAuthorizedClient(ulong clientId, string playerId)
        {
            return Instance != null && !string.IsNullOrEmpty(playerId) &&
                   Instance.authorizedPlayers.TryGetValue(clientId, out string approved) && approved == playerId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if (Instance == null)
                new GameObject("DF_DedicatedOnlineMatchHandshake").AddComponent<DedicatedOnlineMatchHandshake>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
            {
                if (!CoopAuthority.Active)
                {
                    Detach(); planSent = false; launched = false; firstHelloAt = -1; planSentAt = -1;
                    pending.Clear(); authorizedPlayers.Clear(); planAcks.Clear(); unauthenticatedSince.Clear();
                }
                if (manager == null || !manager.IsConnectedClient) helloSent = false;
                return;
            }

            Attach(manager.CustomMessagingManager);

            if (manager.IsServer && !manager.IsClient && DedicatedServerNetworkRuntime.Requested)
            {
                UpdateDedicatedServer(manager);
                return;
            }

            if (manager.IsClient && !manager.IsServer && manager.IsConnectedClient && !helloSent &&
                CoopNetworkBootstrap.DirectServerStatus.StartsWith("CLIENT STARTED", StringComparison.Ordinal) &&
                EdgegapMatchmakerClient.Instance != null && EdgegapMatchmakerClient.Instance.HasAssignment)
            {
                SendClientHello();
            }
        }

        private void UpdateDedicatedServer(NetworkManager manager)
        {
            DisconnectUnauthenticatedClients(manager);
            if (launched) return;

            RemoveDisconnectedPending(manager);
            if (pending.Count == 0)
            {
                firstHelloAt = -1f;
                return;
            }

            if (!planSent)
            {
                int edgegapAssigned = EdgegapInjectedMatchContext.IsEdgegapDeployment
                    ? Mathf.Clamp(EdgegapInjectedMatchContext.AssignedTicketCount, 0, MatchSession.MaxPlayers)
                    : 0;
                bool allAssignedConnected = edgegapAssigned > 0 && pending.Count >= edgegapAssigned;
                bool full = pending.Count >= MatchSession.MaxPlayers;
                bool graceElapsed = firstHelloAt >= 0f && Time.unscaledTime - firstHelloAt >= HumanJoinWindowSeconds;

                if (edgegapAssigned > 0 && !allAssignedConnected && !graceElapsed)
                    status = $"WAITING FOR EDGEGAP PLAYERS • {pending.Count}/{edgegapAssigned}";

                if (RankedMatchReporter.RankedDeployment && graceElapsed && !full)
                { AbortRanked(manager); return; }
                if (allAssignedConnected || full || graceElapsed)
                    BuildAndSendAuthoritativePlan(manager);
                return;
            }

            if (planAcks.Count >= pending.Count)
                LaunchAuthoritativeMatch(manager);
            else if (Time.unscaledTime - planSentAt >= PlanAckTimeoutSeconds)
            {
                if (RankedMatchReporter.RankedDeployment)
                { AbortRanked(manager); return; }
                // Evict peers that never accepted the plan, then rebuild for the survivors.
                var stalled = new List<ulong>();
                foreach (ulong id in pending.Keys)
                    if (!planAcks.Contains(id)) stalled.Add(id);
                foreach (ulong id in stalled)
                {
                    pending.Remove(id);
                    authorizedPlayers.Remove(id);
                    unauthenticatedSince.Remove(id);
                    manager.DisconnectClient(id);
                }
                planSent = false; planSentAt = -1f; planAcks.Clear();
                CoopAuthority.ClearPrepared();
                status = "START CONFIRMATION TIMED OUT • REBUILDING MATCH";
                if (pending.Count > 0) BuildAndSendAuthoritativePlan(manager);
            }
        }

        private void DisconnectUnauthenticatedClients(NetworkManager manager)
        {
            if (manager?.ConnectedClientsIds == null) return;

            var staleTimers = new List<ulong>();
            foreach (KeyValuePair<ulong, float> pair in unauthenticatedSince)
            {
                bool stillConnected = false;
                for (int i = 0; i < manager.ConnectedClientsIds.Count; i++)
                {
                    if (manager.ConnectedClientsIds[i] == pair.Key)
                    {
                        stillConnected = true;
                        break;
                    }
                }
                if (!stillConnected || authorizedPlayers.ContainsKey(pair.Key)) staleTimers.Add(pair.Key);
            }
            for (int i = 0; i < staleTimers.Count; i++) unauthenticatedSince.Remove(staleTimers[i]);

            var timedOut = new List<ulong>();
            for (int i = 0; i < manager.ConnectedClientsIds.Count; i++)
            {
                ulong clientId = manager.ConnectedClientsIds[i];
                if (clientId == NetworkManager.ServerClientId || authorizedPlayers.ContainsKey(clientId)) continue;
                if (!unauthenticatedSince.TryGetValue(clientId, out float connectedAt))
                {
                    unauthenticatedSince[clientId] = Time.unscaledTime;
                    continue;
                }
                if (Time.unscaledTime - connectedAt >= AuthenticationTimeoutSeconds)
                    timedOut.Add(clientId);
            }

            for (int i = 0; i < timedOut.Count; i++)
                RejectClient(manager, timedOut[i], "dedicated authentication timeout");
        }

        private void SendClientHello()
        {
            NetworkManager manager = NetworkManager.Singleton;
            EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
            if (manager == null || !manager.IsConnectedClient || manager.IsServer || messaging == null || edgegap == null)
                return;

            ResolveLocalIdentity(out string playerId, out string displayName, out HeroId hero);
            if (string.IsNullOrWhiteSpace(playerId)) playerId = edgegap.TicketId;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = "TRAVELER";
            if (string.IsNullOrWhiteSpace(edgegap.TicketId) || string.IsNullOrWhiteSpace(edgegap.TeamId))
            {
                status = "EDGEGAP TICKET OR TEAM MISSING";
                return;
            }

            using FastBufferWriter writer = new(6144, Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(new FixedString128Bytes(edgegap.TicketId));
            writer.WriteValueSafe(new FixedString128Bytes(edgegap.TeamId));
            writer.WriteValueSafe(new FixedString128Bytes(edgegap.MatchId ?? ""));
            writer.WriteValueSafe(new FixedString128Bytes(playerId));
            writer.WriteValueSafe(new FixedString64Bytes(SanitizeName(displayName)));
            writer.WriteValueSafe((int)hero);
            writer.WriteValueSafe((int)BattleItemInventory.Equipped);
            writer.WriteValueSafe(new FixedString4096Bytes(RankedService.Instance?.Admission ?? ""));
            writer.WriteValueSafe(DisplayNameCapability);
            // Optional trailing field: older servers ignore it; updated servers merge
            // this client's unlocked arenas into the match-wide random pool.
            writer.WriteValueSafe(OnlineArenaPool.LocalOwnedMask());
            messaging.SendNamedMessage(HelloMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            helloSent = true;
            status = "PLAYER IDENTITY SENT TO DEDICATED SERVER";
        }

        private void OnClientHello(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.IsClient || !DedicatedServerNetworkRuntime.Requested)
                return;
            if (launched)
            {
                if (!authorizedPlayers.ContainsKey(senderClientId))
                    RejectClient(manager, senderClientId, "match already launched");
                return;
            }

            // The roster freezes when its plan is sent. Do not enlarge pending while
            // waiting for ACKs from the already-issued roster.
            if (planSent)
            {
                if (!pending.ContainsKey(senderClientId))
                    RejectClient(manager, senderClientId, "roster already frozen");
                return;
            }
            if (!reader.TryBeginRead(sizeof(int)))
            {
                RejectClient(manager, senderClientId, "truncated dedicated hello");
                return;
            }
            reader.ReadValueSafe(out int protocol);
            try
            {
            reader.ReadValueSafe(out FixedString128Bytes fixedTicket);
            reader.ReadValueSafe(out FixedString128Bytes fixedTeam);
            reader.ReadValueSafe(out FixedString128Bytes fixedMatch);
            reader.ReadValueSafe(out FixedString128Bytes fixedPlayer);
            reader.ReadValueSafe(out FixedString64Bytes fixedName);
            reader.ReadValueSafe(out int heroRaw);
            reader.ReadValueSafe(out int battleItemRaw);
            reader.ReadValueSafe(out FixedString4096Bytes fixedAdmission);
            bool supportsDisplayNames = false;
            if (reader.TryBeginRead(sizeof(uint)))
            {
                reader.ReadValueSafe(out uint nameCapability);
                supportsDisplayNames = nameCapability == DisplayNameCapability;
            }
            uint reportedArenas = 0u;
            if (reader.TryBeginRead(sizeof(uint)))
                reader.ReadValueSafe(out reportedArenas);
            string admission = fixedAdmission.ToString();
            if (RankedMatchReporter.RankedDeployment != !string.IsNullOrEmpty(admission))
            { RejectClient(manager, senderClientId, "ranked admission required for ranked deployment only"); return; }

            string ticketId = fixedTicket.ToString();
            string claimedTeamId = fixedTeam.ToString();
            string claimedMatchId = fixedMatch.ToString();
            string claimedPlayerId = fixedPlayer.ToString();
            string displayName = fixedName.ToString();

            if (protocol != ProtocolVersion)
            { RejectClient(manager, senderClientId, "game version mismatch: update the Android app and dedicated server together"); return; }
            if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(claimedTeamId) ||
                !Enum.IsDefined(typeof(HeroId), heroRaw) || !Enum.IsDefined(typeof(BattleItemType), battleItemRaw))
            {
                RejectClient(manager, senderClientId, "invalid dedicated hello");
                return;
            }

            if (!EdgegapInjectedMatchContext.TryValidateTicket(
                    ticketId,
                    claimedTeamId,
                    claimedMatchId,
                    out string teamId,
                    out string matchId,
                    out string validationReason))
            {
                RejectClient(manager, senderClientId, "invalid Edgegap ticket: " + validationReason);
                return;
            }

            // Ticket ID is the authoritative per-match player identity. A client-provided account or
            // lobby id is intentionally not trusted by gameplay authorization on the dedicated server.
            string playerId = ticketId;
            if (!string.IsNullOrWhiteSpace(claimedPlayerId) && claimedPlayerId != ticketId)
                Debug.Log($"DIMENSIONS FRONT SERVER: lobby identity {claimedPlayerId} bound to verified ticket {ticketId}.");

            if (!pending.ContainsKey(senderClientId) && pending.Count >= MatchSession.MaxPlayers)
            {
                RejectClient(manager, senderClientId, "dedicated match is full");
                return;
            }
            int teamMembers = 0;
            foreach (KeyValuePair<ulong, PendingHuman> pair in pending)
            {
                if (pair.Key == senderClientId) continue;
                if (pair.Value.TeamId == teamId) teamMembers++;
                if (pair.Value.TicketId == ticketId || pair.Value.PlayerId == playerId)
                {
                    RejectClient(manager, senderClientId, "duplicate dedicated identity");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(matchId) && !string.IsNullOrWhiteSpace(pair.Value.MatchId) &&
                    !string.Equals(pair.Value.MatchId, matchId, StringComparison.Ordinal))
                {
                    RejectClient(manager, senderClientId, "cross-match dedicated identity");
                    return;
                }
            }

            if (teamMembers >= MatchSession.TeamSize)
            {
                RejectClient(manager, senderClientId, "dedicated team is full");
                return;
            }
            pending[senderClientId] = new PendingHuman
            {
                ClientId = senderClientId,
                TicketId = ticketId,
                TeamId = teamId,
                MatchId = matchId,
                PlayerId = playerId,
                DisplayName = SanitizeName(displayName),
                Hero = (HeroId)heroRaw,
                BattleItem = (BattleItemType)battleItemRaw,
                Admission = admission,
                SupportsDisplayNames = supportsDisplayNames,
                ArenaOwnedMask = OnlineArenaPool.Normalize(reportedArenas)
            };
            authorizedPlayers[senderClientId] = playerId;
            unauthenticatedSince.Remove(senderClientId);
            if (firstHelloAt < 0f) firstHelloAt = Time.unscaledTime;

            int assigned = EdgegapInjectedMatchContext.IsEdgegapDeployment
                ? EdgegapInjectedMatchContext.AssignedTicketCount
                : MatchSession.MaxPlayers;
            status = $"DEDICATED PLAYERS CONNECTING • {pending.Count}/{Mathf.Max(1, assigned)}";
            }
            catch (OverflowException)
            {
                RejectClient(manager, senderClientId, "truncated dedicated hello");
            }
        }

        private void RejectClient(NetworkManager manager, ulong clientId, string reason)
        {
            bool rosterChanged = pending.Remove(clientId);
            authorizedPlayers.Remove(clientId);
            planAcks.Remove(clientId);
            unauthenticatedSince.Remove(clientId);

            if (rosterChanged && planSent && !launched)
            {
                planSent = false;
                planSentAt = -1f;
                planAcks.Clear();
                CoopAuthority.ClearPrepared();
            }

            status = "DEDICATED CLIENT REJECTED";
            Debug.LogWarning($"DIMENSIONS FRONT SERVER: disconnected client {clientId}: {reason}.");
            manager?.DisconnectClient(clientId);
        }

        private void BuildAndSendAuthoritativePlan(NetworkManager manager)
        {
            if (planSent || pending.Count == 0 || messaging == null) return;

            try
            {
                List<PendingHuman> ordered = new(pending.Values);
                ordered.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));
                var members = new List<OnlineMatchPlan.DedicatedMember>(ordered.Count);
                string matchId = EdgegapInjectedMatchContext.InjectedMatchId;
                for (int i = 0; i < ordered.Count; i++)
                {
                    PendingHuman human = ordered[i];
                    if (string.IsNullOrWhiteSpace(matchId) && !string.IsNullOrWhiteSpace(human.MatchId))
                        matchId = human.MatchId;
                    members.Add(new OnlineMatchPlan.DedicatedMember
                    {
                        PlayerId = human.PlayerId,
                        DisplayName = human.DisplayName,
                        TeamId = human.TeamId,
                        Hero = human.Hero,
                        BattleItem = human.BattleItem
                    });
                }
                if (string.IsNullOrWhiteSpace(matchId)) matchId = Guid.NewGuid().ToString("N");

                uint sharedArenaPool = 0u;
                for (int i = 0; i < ordered.Count; i++)
                    sharedArenaPool |= ordered[i].ArenaOwnedMask;
                ArenaVariant drawnArena = OnlineArenaPool.Draw(sharedArenaPool);
                Debug.Log($"DIMENSIONS FRONT SERVER: online arena {drawnArena} chosen from {ordered.Count} player pool(s).");

                OnlineMatchPlan plan = OnlineMatchPlan.BuildDedicated(
                    matchId,
                    drawnArena,
                    BotDifficultySettings.OnlineDifficulty,
                    members,
                    RankedMatchReporter.RankedDeployment ? SessionMode.OnlineRanked : SessionMode.OnlineCasual);
                // An older client sanitizes aliases before hashing its plan. Use
                // canonical-format labels for the whole roster in mixed matches
                // so every peer hashes the same names and can still connect.
                bool allowPlayGamesNames = ordered.TrueForAll(human => human.SupportsDisplayNames);
                foreach(var slot in plan.Session.Slots)
                    slot.DisplayName=SafeIdentityCatalog.DisplayPlayerName(slot.DisplayName,null,allowPlayGamesNames&&!slot.IsBot);
                CoopAuthority.Prepare(plan);
                planFingerprint = CoopPlanHandshake.Fingerprint(plan);
                planAcks.Clear();
                planSent = true;
                planSentAt = Time.unscaledTime;

                for (int i = 0; i < ordered.Count; i++)
                    SendPlanToClient(ordered[i].ClientId, ordered[i].PlayerId, plan, planFingerprint);

                status = $"AUTHORITATIVE PLAN SENT • 0/{pending.Count} ACK";
            }
            catch (Exception ex)
            {
                if (RankedMatchReporter.RankedDeployment)
                {
                    Debug.LogError("DIMENSIONS FRONT SERVER: ranked plan build failed: " + ex);
                    AbortRanked(manager);
                    return;
                }
                planSent = false;
                CoopAuthority.ClearPrepared();
                status = "DEDICATED PLAN BUILD FAILED";
                Debug.LogError("DIMENSIONS FRONT SERVER: dedicated plan build failed: " + ex);
            }
        }

        private void SendPlanToClient(ulong clientId, string localPlayerId, OnlineMatchPlan plan, uint fingerprint)
        {
            using FastBufferWriter writer = new(4096, Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(fingerprint);
            writer.WriteValueSafe(new FixedString128Bytes(plan.MatchId ?? ""));
            writer.WriteValueSafe((int)plan.Arena);
            writer.WriteValueSafe((int)plan.Difficulty);
            writer.WriteValueSafe((int)plan.Session.Mode);
            writer.WriteValueSafe(new FixedString128Bytes(localPlayerId ?? ""));
            writer.WriteValueSafe(MatchSession.MaxPlayers);

            for (int i = 0; i < MatchSession.MaxPlayers; i++)
            {
                MatchPlayerSlot slot = plan.Session.Slots[i];
                writer.WriteValueSafe(new FixedString128Bytes(slot.PlayerId ?? ""));
                writer.WriteValueSafe(new FixedString64Bytes(SanitizeName(slot.DisplayName)));
                writer.WriteValueSafe(slot.Team);
                writer.WriteValueSafe((int)slot.Hero);
                writer.WriteValueSafe((int)slot.BattleItem);
                writer.WriteValueSafe(slot.IsBot);
                writer.WriteValueSafe(slot.Ready);
                writer.WriteValueSafe(slot.Connected);
                writer.WriteValueSafe(slot.IsHost);
            }

            messaging.SendNamedMessage(PlanMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private void OnAuthoritativePlan(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId) return;

            reader.ReadValueSafe(out int protocol);
            reader.ReadValueSafe(out uint serverFingerprint);
            reader.ReadValueSafe(out FixedString128Bytes fixedMatchId);
            reader.ReadValueSafe(out int arenaRaw);
            reader.ReadValueSafe(out int difficultyRaw);
            reader.ReadValueSafe(out int modeRaw);
            reader.ReadValueSafe(out FixedString128Bytes fixedLocalPlayerId);
            reader.ReadValueSafe(out int slotCount);

            bool valid = protocol == ProtocolVersion && slotCount == MatchSession.MaxPlayers &&
                         Enum.IsDefined(typeof(ArenaVariant), arenaRaw) && difficultyRaw == (int)BotDifficultySettings.OnlineDifficulty &&
                         (modeRaw == (int)SessionMode.OnlineCasual || modeRaw == (int)SessionMode.OnlineRanked) &&
                         (modeRaw == (int)SessionMode.OnlineRanked) == !string.IsNullOrEmpty(RankedService.Instance?.Admission);
            var slots = new MatchPlayerSlot[MatchSession.MaxPlayers];

            if (valid)
            {
                for (int i = 0; i < MatchSession.MaxPlayers; i++)
                {
                    reader.ReadValueSafe(out FixedString128Bytes fixedPlayerId);
                    reader.ReadValueSafe(out FixedString64Bytes fixedName);
                    reader.ReadValueSafe(out int team);
                    reader.ReadValueSafe(out int heroRaw);
                    reader.ReadValueSafe(out int battleItemRaw);
                    reader.ReadValueSafe(out bool isBot);
                    reader.ReadValueSafe(out bool ready);
                    reader.ReadValueSafe(out bool connected);
                    reader.ReadValueSafe(out bool isHost);

                    if ((team != 0 && team != 1) || !Enum.IsDefined(typeof(HeroId), heroRaw) || !Enum.IsDefined(typeof(BattleItemType), battleItemRaw)) valid = false;
                    slots[i] = new MatchPlayerSlot
                    {
                        PlayerId = fixedPlayerId.ToString(),
                        DisplayName = SanitizeName(fixedName.ToString()),
                        Team = team,
                        Hero = Enum.IsDefined(typeof(HeroId), heroRaw) ? (HeroId)heroRaw : HeroId.Veyr,
                        BattleItem = Enum.IsDefined(typeof(BattleItemType), battleItemRaw) ? (BattleItemType)battleItemRaw : BattleItemType.None,
                        IsLocal = false,
                        IsBot = isBot,
                        Ready = ready,
                        Connected = connected,
                        IsHost = isHost
                    };
                }
            }

            OnlineMatchPlan plan = null;
            string localPlayerId = fixedLocalPlayerId.ToString();
            if (valid)
            {
                try
                {
                    plan = OnlineMatchPlan.FromAuthoritativeSlots(
                        fixedMatchId.ToString(),
                        (ArenaVariant)arenaRaw,
                        (BotDifficulty)difficultyRaw,
                        slots,
                        localPlayerId,
                        (SessionMode)modeRaw);
                    valid = CoopPlanHandshake.Fingerprint(plan) == serverFingerprint;
                }
                catch (Exception ex)
                {
                    valid = false;
                    Debug.LogWarning("DIMENSIONS FRONT ONLINE: dedicated plan rejected: " + ex.Message);
                }
            }

            if (valid)
            {
                CoopAuthority.Prepare(plan);
                planFingerprint = serverFingerprint;
                status = "DEDICATED PLAN VERIFIED";
            }
            else
            {
                CoopAuthority.ClearPrepared();
                status = "DEDICATED PLAN MISMATCH";
            }

            using FastBufferWriter writer = new(192, Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(valid);
            writer.WriteValueSafe(serverFingerprint);
            writer.WriteValueSafe(new FixedString128Bytes(localPlayerId ?? ""));
            messaging?.SendNamedMessage(AckMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private void OnPlanAck(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.IsClient || !planSent || launched) return;

            if (!reader.TryBeginRead(sizeof(int) + sizeof(bool) + sizeof(uint) + sizeof(ushort)))
            {
                RejectClient(manager, senderClientId, "truncated dedicated plan acknowledgement");
                return;
            }
            try
            {
            reader.ReadValueSafe(out int protocol);
            reader.ReadValueSafe(out bool accepted);
            reader.ReadValueSafe(out uint clientFingerprint);
            reader.ReadValueSafe(out FixedString128Bytes fixedPlayerId);
            string playerId = fixedPlayerId.ToString();

            bool valid = protocol == ProtocolVersion && accepted && clientFingerprint == planFingerprint &&
                         pending.TryGetValue(senderClientId, out PendingHuman human) && human.PlayerId == playerId;
            if (valid)
            {
                planAcks.Add(senderClientId);
                status = $"DEDICATED PLAN VERIFIED • {planAcks.Count}/{pending.Count} ACK";
            }
            else
            {
                RejectClient(manager, senderClientId, "invalid dedicated plan acknowledgement");
            }
            }
            catch (OverflowException)
            {
                RejectClient(manager, senderClientId, "truncated dedicated plan acknowledgement");
            }
        }

        private void LaunchAuthoritativeMatch(NetworkManager manager)
        {
            if (launched || CoopAuthority.PreparedPlan is not OnlineMatchPlan plan || messaging == null) return;
            if (plan.Session.Mode == SessionMode.OnlineRanked && RankedMatchReporter.Instance?.Registered != true)
            {
                if (!registeringRanked) _ = RegisterRankedAsync(manager, plan);
                return;
            }

            // Activate and construct the authoritative world first. Clients receive launch only
            // after the server has successfully entered the match, avoiding a client-only launch.
            if (!CoopAuthority.BeginPreparedMatch(false))
            {
                status = "DEDICATED SERVER COULD NOT ACTIVATE PLAN";
                if (plan.Session.Mode == SessionMode.OnlineRanked) AbortRanked(manager);
                return;
            }

            DedicatedServerNetworkRuntime.ReleaseMatchBootstrap();
            if (!OnlineMatchRuntime.Enter(plan))
            {
                CoopAuthority.EndMatch();
                status = "DEDICATED SERVER MATCH BOOTSTRAP FAILED";
                if (plan.Session.Mode == SessionMode.OnlineRanked) AbortRanked(manager);
                return;
            }

            launched = true;
            using FastBufferWriter writer = new(12, Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(planFingerprint);
            foreach (ulong clientId in pending.Keys)
                messaging.SendNamedMessage(LaunchMessage, clientId, writer, NetworkDelivery.ReliableSequenced);

            status = $"DEDICATED MATCH LIVE • {plan.HumanCount} HUMANS";
            Debug.Log($"DIMENSIONS FRONT SERVER: authoritative online match started with {plan.HumanCount} humans and bot fill.");
        }

        private void AbortRanked(NetworkManager manager)
        {
            if (launched) return;
            launched = true;
            status = "RANKED START CANCELLED • NO POINTS CHANGED";
            string abortedMatch = CoopAuthority.PreparedPlan?.MatchId ?? RankedMatchReporter.Instance?.MatchId ??
                EdgegapInjectedMatchContext.InjectedMatchId;
            if (string.IsNullOrEmpty(abortedMatch))
                foreach (var human in pending.Values)
                    if (!string.IsNullOrEmpty(human.MatchId)) { abortedMatch = human.MatchId; break; }
            if (!string.IsNullOrEmpty(abortedMatch))
                _ = RankedMatchReporter.Ensure().VoidAsync(abortedMatch);
            var ids = new List<ulong>(pending.Keys);
            foreach (ulong id in ids) manager.DisconnectClient(id);
            CoopAuthority.ClearPrepared();
            EdgegapDedicatedServerLifecycle.StopAbortedMatch();
        }

        private async System.Threading.Tasks.Task RegisterRankedAsync(NetworkManager manager, OnlineMatchPlan plan)
        {
            registeringRanked = true;
            try
            {
                if (pending.Count != MatchSession.MaxPlayers || planAcks.Count != MatchSession.MaxPlayers)
                { AbortRanked(manager); return; }
                var roster = new RankedMatchReporter.Player[MatchSession.MaxPlayers];
                for (int i = 0; i < roster.Length; i++)
                {
                    MatchPlayerSlot slot = plan.Session.Slots[i];
                    PendingHuman human = null;
                    foreach (var candidate in pending.Values) if (candidate.TicketId == slot.PlayerId) { human = candidate; break; }
                    if (human == null || slot.IsBot) { AbortRanked(manager); return; }
                    roster[i] = new RankedMatchReporter.Player { ticket = human.TicketId, team = slot.Team, admission = human.Admission };
                }
                if (!await RankedMatchReporter.Ensure().RegisterAsync(plan.MatchId, roster))
                {
                    status = "RANKED REGISTRATION FAILED • RETRYING";
                    if (Time.unscaledTime - planSentAt > 60) AbortRanked(manager);
                    else await System.Threading.Tasks.Task.Delay(3000);
                    return;
                }
                if (launched || manager == null || !manager.IsListening || CoopAuthority.PreparedPlan != plan ||
                    pending.Count != MatchSession.MaxPlayers || planAcks.Count != MatchSession.MaxPlayers)
                { AbortRanked(manager); return; }
                LaunchAuthoritativeMatch(manager);
            }
            finally { registeringRanked = false; }
        }

        private void OnLaunch(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId ||
                CoopAuthority.PreparedPlan is not OnlineMatchPlan plan)
                return;

            if (!reader.TryBeginRead(sizeof(int) + sizeof(uint))) return;
            reader.ReadValueSafe(out int protocol);
            reader.ReadValueSafe(out uint serverFingerprint);
            if (protocol != ProtocolVersion || serverFingerprint != planFingerprint ||
                CoopPlanHandshake.Fingerprint(plan) != serverFingerprint)
                return;

            if (!CoopAuthority.BeginPreparedMatch(false)) return;
            launched = true;
            EdgegapOnlineFlow.MarkMatchStarted();
            if (plan.Session.Mode == SessionMode.OnlineRanked) RankedService.Instance?.MatchStarted();
            if (!OnlineMatchRuntime.Enter(plan))
            {
                launched = false;
                CoopAuthority.EndMatch();
                _ = MatchExit.ReturnToMenuAsync();
                status = "CLIENT MATCH BOOTSTRAP FAILED";
                return;
            }
            status = "DEDICATED MATCH LIVE";
        }

        private void RemoveDisconnectedPending(NetworkManager manager)
        {
            if (manager?.ConnectedClientsIds == null || pending.Count == 0) return;
            var remove = new List<ulong>();
            foreach (ulong clientId in pending.Keys)
            {
                bool connected = false;
                for (int i = 0; i < manager.ConnectedClientsIds.Count; i++)
                {
                    if (manager.ConnectedClientsIds[i] == clientId)
                    {
                        connected = true;
                        break;
                    }
                }
                if (!connected) remove.Add(clientId);
            }

            if (remove.Count > 0 && planSent && !launched)
            {
                // Ranked cannot rebuild a smaller roster. Keep the prepared match id
                // until the abort has issued its tombstone, including in-flight register.
                if (RankedMatchReporter.RankedDeployment)
                { AbortRanked(manager); return; }
                planSent = false; planSentAt = -1f; planAcks.Clear(); CoopAuthority.ClearPrepared();
            }
            for (int i = 0; i < remove.Count; i++)
            {
                ulong id = remove[i];
                pending.Remove(id);
                authorizedPlayers.Remove(id);
                planAcks.Remove(id);
                unauthenticatedSince.Remove(id);
            }
        }

        private static void ResolveLocalIdentity(out string playerId, out string displayName, out HeroId hero)
        {
            playerId = "";
            displayName = "TRAVELER";
            hero = PlayerHeroSelection.Selected;
            OnlinePartyService online = OnlinePartyService.Instance;
            if (online == null) return;

            hero = online.LocalHero;
            IReadOnlyList<OnlinePartyService.MemberView> members = online.Members;
            for (int i = 0; i < members.Count; i++)
            {
                OnlinePartyService.MemberView member = members[i];
                if (member == null || !member.IsLocal) continue;
                playerId = member.PlayerId ?? "";
                displayName = member.DisplayName ?? "TRAVELER";
                hero = member.Hero;
                return;
            }
        }

        private static string SanitizeName(string value) => Alterverse.Profile.SafeIdentityCatalog.DisplayPlayerName(value);

        private void Attach(CustomMessagingManager value)
        {
            if (value == null || attached && messaging == value) return;
            Detach();
            messaging = value;
            messaging.RegisterNamedMessageHandler(HelloMessage, OnClientHello);
            messaging.RegisterNamedMessageHandler(PlanMessage, OnAuthoritativePlan);
            messaging.RegisterNamedMessageHandler(AckMessage, OnPlanAck);
            messaging.RegisterNamedMessageHandler(LaunchMessage, OnLaunch);
            attached = true;
        }

        private void Detach()
        {
            if (!attached || messaging == null)
            {
                messaging = null;
                attached = false;
                return;
            }
            messaging.UnregisterNamedMessageHandler(HelloMessage);
            messaging.UnregisterNamedMessageHandler(PlanMessage);
            messaging.UnregisterNamedMessageHandler(AckMessage);
            messaging.UnregisterNamedMessageHandler(LaunchMessage);
            messaging = null;
            attached = false;
        }

        private void OnDestroy()
        {
            Detach();
            if (Instance == this) Instance = null;
        }
    }
}


