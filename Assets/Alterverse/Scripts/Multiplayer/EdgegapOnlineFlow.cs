using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Bridges the existing private Unity friend party into Edgegap Group Up matchmaking.
    /// Session properties distribute the group id and join receipts even after the match
    /// has replaced the party Relay link with a dedicated connection.
    /// The owner creates the Edgegap group NOT READY, waits until every friend has joined,
    /// then becomes ready last so matchmaking cannot lock the group too early.
    /// </summary>
    [DefaultExecutionOrder(-25000)]
    public sealed class EdgegapOnlineFlow : MonoBehaviour
    {
        private const string GroupMessage = "DF_EDGEGAP_GROUP_V1";
        private const string JoinAckMessage = "DF_EDGEGAP_JOIN_ACK_V1";
        private const string CancelMessage = "DF_EDGEGAP_CANCEL_V1";
        private const float GroupBroadcastInterval = 1.0f;
        private const float FriendJoinTimeoutSeconds = 20f;
        // Server allows 60s for assigned humans and 20s for plan acknowledgements.
        private const float MatchStartTimeoutSeconds = 95f;

        public static EdgegapOnlineFlow Instance { get; private set; }

        private CustomMessagingManager messaging;
        private bool attached;
        private bool busy;
        private bool searching;
        private bool polling;
        private bool joiningGroup;
        private bool searchOwner;
        private bool cleanupRunning;
        private Task<bool> preparation;
        private Task<bool> stopTask;
        private int searchGeneration;
        private int expectedPartyPlayers;
        private string expectedRoster = "";
        private float nextGroupBroadcast;
        private string activeGroupId = "";
        private string status = "EDGEGAP ONLINE READY";
        private string lastError = "";
        private readonly HashSet<ulong> joinedGuestClients = new();

        public bool Busy => busy || joiningGroup || cleanupRunning;
        public bool CanCancelSearch => !cleanupRunning && (searching || busy || joiningGroup || EdgegapMatchmakerClient.Instance?.HasMembership == true);
        public bool IsSearching => searching;
        public string Status
        {
            get
            {
                if (!searching || busy || joiningGroup || !string.IsNullOrEmpty(lastError)) return status;
                EdgegapMatchmakerClient client = EdgegapMatchmakerClient.Instance;
                if (client == null) return status;
                if (client.HasAssignment)
                    return client.MatchmakerStatus.StartsWith("DEDICATED SERVER CONNECTED", StringComparison.Ordinal)
                        ? status : "MATCH FOUND • CONNECTING TO GAME SERVER";
                switch (client.MatchmakerStatus)
                {
                    case "SEARCHING": return "SEARCHING FOR OPPONENTS";
                    case "TEAM_FOUND": return "TEAM FOUND • WAITING FOR OPPONENTS";
                    case "MATCH_FOUND": return "MATCH FOUND • STARTING GAME SERVER";
                    default: return status;
                }
            }
        }
        public string LastError => lastError;
        public string ActiveGroupId => activeGroupId;

        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if (Instance == null)
                new GameObject("DF_EdgegapOnlineFlow").AddComponent<EdgegapOnlineFlow>();
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
            if (manager != null && manager.IsListening)
                Attach(manager.CustomMessagingManager);
            else if (!CoopAuthority.Active)
                Detach();

            OnlinePartyService online = OnlinePartyService.Instance;
            if (!CoopAuthority.Active && !Alterverse.Match.MatchExit.Leaving && online != null && online.InParty && !online.IsHost)
            {
                string sharedGroup = online.MatchmakingGroupId;
                if (!Busy && !online.Busy && !searching && online.MatchmakingLocked && !string.IsNullOrEmpty(sharedGroup) &&
                    sharedGroup != online.LocalJoinedGroupId)
                    _ = JoinFriendGroupAsync(sharedGroup);
                else if (searching && !cleanupRunning && (!online.MatchmakingLocked ||
                         !string.IsNullOrEmpty(sharedGroup) && sharedGroup != activeGroupId))
                    _ = StopSearchAsync("PARTY LEADER CANCELLED SEARCH", false);
            }
            if (searching && !CoopAuthority.Active && EdgegapMatchmakerClient.Instance?.HasAssignment != true)
            {
                if (!cleanupRunning && (online == null || !online.InParty))
                {
                    _ = AbortSearchAfterPartyChangeAsync("ONLINE PARTY LOST • MATCHMAKING CANCELLED", false);
                    return;
                }

                // The Edgegap group is created from an exact friend-party snapshot. If somebody
                // disappears afterwards, cancel the whole group so their ready membership cannot
                // survive as a ghost ticket and stall the dedicated server.
                if (!cleanupRunning && searchOwner && expectedPartyPlayers > 0 &&
                    !PartyUnchanged(online))
                {
                    _ = AbortSearchAfterPartyChangeAsync("PARTY CHANGED • MATCHMAKING CANCELLED", true);
                    return;
                }
            }

            if (!searching || string.IsNullOrWhiteSpace(activeGroupId) || online == null || !online.IsHost)
                return;
            if (manager == null || !manager.IsListening || !manager.IsHost || Time.unscaledTime < nextGroupBroadcast)
                return;

            nextGroupBroadcast = Time.unscaledTime + GroupBroadcastInterval;
            BroadcastGroup(manager);
        }

        public async Task<bool> QuickSearchAsync()
        {
            if (Busy || searching) return false;
            OnlinePartyService online = OnlinePartyService.Instance;
            if (online == null) return Fail("ONLINE PARTY SERVICE UNAVAILABLE");

            if (!online.InParty && !await online.CreatePrivatePartyAsync())
                return Fail(online.LastError.Length > 0 ? online.LastError : "COULD NOT CREATE SOLO PARTY");

            if (!online.LocalReady && !await online.SetLocalReadyAsync(true))
                return Fail(online.LastError.Length > 0 ? online.LastError : "COULD NOT READY SOLO PLAYER");

            return await BeginSearchAsync();
        }

        public Task<bool> BeginSearchAsync()
        {
            if (Busy || searching) return Task.FromResult(false);
            preparation = BeginSearchCoreAsync();
            return preparation;
        }

        private async Task<bool> BeginSearchCoreAsync()
        {
            if (CoopAuthority.Active || Alterverse.Match.MatchExit.Leaving) return Fail("RETURN TO PARTY BEFORE SEARCHING AGAIN");
            OnlinePartyService online = OnlinePartyService.Instance;
            EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
            if (online == null || !online.InParty || !online.IsHost)
                return Fail("ONLY THE PARTY LEADER CAN START MATCHMAKING");
            if (online.PlayerCount < 1 || online.PlayerCount > OnlinePartyService.MaxPartyPlayers)
                return Fail("ONLINE PARTIES REQUIRE 1-4 PLAYERS");
            if (edgegap == null || !edgegap.IsConfigured) return Fail("EDGEGAP MATCHMAKER TOKEN NOT CONFIGURED");
            if (online.HasHeroConflict) return Fail("FRIENDS ON THE SAME TEAM NEED DIFFERENT HEROES");
            if (!online.AllPresentPlayersReady) return Fail("ALL FRIENDS MUST BE READY BEFORE SEARCHING");

            int generation = ++searchGeneration;
            searchOwner = true; expectedPartyPlayers = online.PlayerCount; expectedRoster = online.ReadyRosterKey;
            busy = true; searching = true;
            bool started = false;
            try
            {
                lastError = ""; joinedGuestClients.Clear();
                SetStatus("PREPARING EDGEGAP GROUP");
                // A failed earlier deletion must be retried before replacing its membership.
                if (edgegap.HasMembership && !await edgegap.CancelGroupAsync())
                    return Fail("COULD NOT CLEAN UP PREVIOUS SEARCH • TRY AGAIN");
                if (generation != searchGeneration) return false;
                if (!await online.SetMatchmakingLockedAsync(true)) return Fail("COULD NOT LOCK FRIEND PARTY");
                if (generation != searchGeneration) return false;
                if (!await edgegap.CreateGroupAsync(false))
                    return generation == searchGeneration && Fail(edgegap.LastError);
                if (generation != searchGeneration) return false;
                if (!PartyUnchanged(online))
                    return Fail("FRIEND PARTY CHANGED DURING MATCHMAKING");
                activeGroupId = edgegap.GroupId;
                if (string.IsNullOrWhiteSpace(activeGroupId)) return Fail("EDGEGAP DID NOT RETURN A GROUP ID");
                if (!await online.PublishMatchmakingGroupAsync(activeGroupId)) return Fail("COULD NOT SHARE MATCHMAKING GROUP");
                if (generation != searchGeneration) return false;
                nextGroupBroadcast = 0f;
                SetStatus("FRIENDS JOINING EDGEGAP GROUP");
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsHost) BroadcastGroup(manager);
                if (!await WaitForGuestJoinAcksAsync(Mathf.Max(0, expectedPartyPlayers - 1), FriendJoinTimeoutSeconds, generation))
                    return generation == searchGeneration && Fail("NOT ALL FRIENDS JOINED EDGEGAP MATCHMAKING");
                if (generation != searchGeneration) return false;
                if (!PartyUnchanged(online))
                    return Fail("FRIEND PARTY CHANGED DURING MATCHMAKING");
                SetStatus("STARTING EDGEGAP MATCHMAKING");
                if (!await edgegap.SetReadyAsync(true)) return generation == searchGeneration && Fail(edgegap.LastError);
                if (generation != searchGeneration) return false;
                started = true;
                SetStatus("EDGEGAP SEARCH ACTIVE • PARTY STAYS TOGETHER");
                _ = PollAssignmentAndConnectAsync(generation);
                return true;
            }
            catch (Exception ex) { return generation == searchGeneration && Fail("EDGEGAP SEARCH FAILED: " + ex.Message); }
            finally
            {
                if (generation == searchGeneration && !started)
                {
                    cleanupRunning = true;
                    ResetSearchState();
                    try { await CleanupMembershipAsync(true); }
                    finally { cleanupRunning = false; }
                }
                busy = false; RaiseChanged();
            }
        }

        public Task<bool> CancelSearchAsync()
        {
            if (OnlinePartyService.Instance?.IsHost != true)
                return Task.FromResult(Fail("ONLY THE PARTY LEADER CAN CANCEL MATCHMAKING"));
            return StopSearchAsync("PLAYER SEARCH STOPPED • PARTY REMAINS PRIVATE", true);
        }

        private Task<bool> StopSearchAsync(string nextStatus, bool broadcast)
        {
            if (cleanupRunning) return stopTask ?? Task.FromResult(false);
            stopTask = StopSearchCoreAsync(nextStatus, broadcast);
            return stopTask;
        }

        private async Task<bool> StopSearchCoreAsync(string nextStatus, bool broadcast)
        {
            cleanupRunning = true;
            bool owner = searchOwner || OnlinePartyService.Instance?.IsHost == true;
            Task<bool> pending = preparation;
            NetworkManager manager = NetworkManager.Singleton;
            if (broadcast && owner && manager != null && manager.IsHost) BroadcastCancel(manager);
            ResetSearchState();
            try
            {
                // Creation/join may return after cancellation. Wait for its response before deleting
                // that exact membership; a new search cannot start while cleanupRunning is true.
                if (pending != null) try { await pending; } catch (Exception ex) { Debug.LogWarning(ex.Message); }
                bool cleaned = await CleanupMembershipAsync(owner);
                if (cleaned) SetStatus(nextStatus);
                else Fail("SEARCH CLEANUP FAILED • RETRY CANCEL OR LEAVE");
                return cleaned;
            }
            finally { cleanupRunning = false; RaiseChanged(); }
        }

        private async Task<bool> CleanupMembershipAsync(bool owner)
        {
            bool cleaned = true;
            try
            {
                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                if (edgegap != null && (!string.IsNullOrEmpty(edgegap.GroupId) || edgegap.HasMembership))
                    cleaned = owner ? await edgegap.CancelGroupAsync() : await edgegap.LeaveGroupAsync();
            }
            catch (Exception ex) { cleaned = false; Debug.LogWarning("DIMENSIONS FRONT ONLINE: " + ex.Message); }
            OnlinePartyService online = OnlinePartyService.Instance;
            if (online != null && online.InParty && online.IsHost)
                cleaned = await online.SetMatchmakingLockedAsync(false) && cleaned;
            return cleaned;
        }

        public async Task LeavePartyAsync()
        {
            if (searching || Busy || EdgegapMatchmakerClient.Instance?.HasMembership == true)
                await StopSearchAsync("MATCHMAKING STOPPED", true);
            OnlinePartyService online = OnlinePartyService.Instance;
            if (online != null && online.InParty) await online.LeavePartyAsync();
        }

        public Task<bool> CompleteMatchAsync() => StopSearchAsync("MATCH COMPLETE • READY FOR ANOTHER SEARCH", false);

        public static void MarkMatchStarted()
        {
            EdgegapOnlineFlow flow = Instance;
            if (flow == null) return;
            // Do not cancel the direct link here: this is the successful transition into gameplay.
            flow.searchGeneration++;
            flow.searching = false;
            flow.polling = false;
            flow.joiningGroup = false;
            flow.searchOwner = false;
            flow.expectedPartyPlayers = 0;
            flow.expectedRoster = "";
            flow.activeGroupId = "";
            flow.joinedGuestClients.Clear();
            _ = OnlinePartyService.Instance?.ResetLocalReadyAsync();
            flow.SetStatus("DEDICATED MATCH CONNECTED");
        }

        private async Task<bool> WaitForGuestJoinAcksAsync(int expectedGuests, float timeoutSeconds, int generation)
        {
            if (expectedGuests <= 0) return true;
            float deadline = Time.realtimeSinceStartup + Mathf.Max(3f, timeoutSeconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                if (generation != searchGeneration) return false;
                OnlinePartyService online = OnlinePartyService.Instance;
                if (!PartyUnchanged(online)) return false;
                if (online.AllFriendsJoinedGroup(activeGroupId)) return true;
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsHost && manager.IsListening) BroadcastGroup(manager);
                await Task.Delay(200);
            }
            return generation == searchGeneration && OnlinePartyService.Instance?.AllFriendsJoinedGroup(activeGroupId) == true;
        }

        private void BroadcastGroup(NetworkManager manager)
        {
            if (!searching || string.IsNullOrWhiteSpace(activeGroupId) || manager == null || !manager.IsHost || messaging == null)
                return;

            using FastBufferWriter writer = new(160, Allocator.Temp);
            writer.WriteValueSafe(new FixedString128Bytes(activeGroupId));
            if (manager.ConnectedClientsIds == null) return;
            for (int i = 0; i < manager.ConnectedClientsIds.Count; i++)
            {
                ulong clientId = manager.ConnectedClientsIds[i];
                if (clientId == NetworkManager.ServerClientId) continue;
                messaging.SendNamedMessage(GroupMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        private void SendJoinAck()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || messaging == null || !manager.IsConnectedClient) return;
            using FastBufferWriter writer = new(160, Allocator.Temp);
            writer.WriteValueSafe(new FixedString128Bytes(activeGroupId ?? ""));
            messaging.SendNamedMessage(JoinAckMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private void OnJoinAck(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsHost || messaging == null || string.IsNullOrWhiteSpace(activeGroupId)) return;
            reader.ReadValueSafe(out FixedString128Bytes fixedGroupId);
            if (!string.Equals(fixedGroupId.ToString(), activeGroupId, StringComparison.Ordinal)) return;
            if (senderClientId == NetworkManager.ServerClientId) return;
            bool connected = false;
            if (manager.ConnectedClientsIds != null)
                for (int i = 0; i < manager.ConnectedClientsIds.Count; i++)
                    if (manager.ConnectedClientsIds[i] == senderClientId) { connected = true; break; }
            if (!connected) return;
            joinedGuestClients.Add(senderClientId);
            SetStatus($"FRIENDS JOINED EDGEGAP • {joinedGuestClients.Count + 1}/{Mathf.Max(1, expectedPartyPlayers)}");
        }

        private void BroadcastCancel(NetworkManager manager)
        {
            if (manager == null || !manager.IsHost || messaging == null || manager.ConnectedClientsIds == null)
                return;
            using FastBufferWriter writer = new(1, Allocator.Temp);
            writer.WriteValueSafe(true);
            for (int i = 0; i < manager.ConnectedClientsIds.Count; i++)
            {
                ulong clientId = manager.ConnectedClientsIds[i];
                if (clientId == NetworkManager.ServerClientId) continue;
                messaging.SendNamedMessage(CancelMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        private void OnGroupMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            OnlinePartyService online = OnlinePartyService.Instance;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId ||
                online == null || !online.InParty || online.IsHost || joiningGroup || CoopAuthority.Active || Alterverse.Match.MatchExit.Leaving)
                return;

            reader.ReadValueSafe(out FixedString128Bytes fixedGroupId);
            string groupId = fixedGroupId.ToString();
            if (string.IsNullOrWhiteSpace(groupId) || !online.MatchmakingLocked || online.MatchmakingGroupId != groupId) return;
            if (!searching && online.LocalJoinedGroupId == groupId) return;
            if (searching && string.Equals(activeGroupId, groupId, StringComparison.Ordinal))
            {
                if (EdgegapMatchmakerClient.Instance?.HasMembership == true) SendJoinAck();
                return;
            }
            _ = JoinFriendGroupAsync(groupId);
        }

        private void OnCancelMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId) return;
            // Session state is authoritative across searches; an old Relay cancellation
            // has no group id and must not cancel a newer locked group.
            if (OnlinePartyService.Instance?.MatchmakingLocked == true) return;
            if (reader.TryBeginRead(sizeof(bool))) reader.ReadValueSafe(out bool _);
            if (searching || joiningGroup || busy)
                _ = StopSearchAsync("PARTY LEADER CANCELLED SEARCH", false);
        }

        private Task<bool> JoinFriendGroupAsync(string groupId)
        {
            if (Busy || searching) return Task.FromResult(false);
            preparation = JoinFriendGroupCoreAsync(groupId);
            return preparation;
        }

        private async Task<bool> JoinFriendGroupCoreAsync(string groupId)
        {
            int generation = ++searchGeneration;
            searchOwner = false;
            expectedPartyPlayers = OnlinePartyService.Instance?.PlayerCount ?? 0;
            expectedRoster = OnlinePartyService.Instance?.ReadyRosterKey ?? "";
            joiningGroup = true; activeGroupId = groupId;
            bool started = false;
            try
            {
                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                if (edgegap == null || !edgegap.IsConfigured) return Fail("EDGEGAP MATCHMAKER TOKEN NOT CONFIGURED");
                SetStatus("JOINING FRIENDS IN EDGEGAP");
                if (!await edgegap.JoinGroupAsync(groupId, true)) return generation == searchGeneration && Fail(edgegap.LastError);
                if (generation != searchGeneration) return false;
                if (!await OnlinePartyService.Instance.AcknowledgeMatchmakingGroupAsync(groupId))
                    return Fail("COULD NOT CONFIRM FRIEND GROUP JOIN");
                if (generation != searchGeneration) return false;
                searching = true; started = true;
                SetStatus("EDGEGAP GROUP JOINED • WAITING FOR LEADER");
                SendJoinAck();
                _ = PollAssignmentAndConnectAsync(generation);
                return true;
            }
            catch (Exception ex) { return generation == searchGeneration && Fail("COULD NOT JOIN EDGEGAP GROUP: " + ex.Message); }
            finally
            {
                if (generation == searchGeneration && !started)
                {
                    cleanupRunning = true; ResetSearchState();
                    try { await CleanupMembershipAsync(false); }
                    finally { cleanupRunning = false; }
                }
                joiningGroup = false; RaiseChanged();
            }
        }

        private async Task PollAssignmentAndConnectAsync(int generation)
        {
            if (polling && generation == searchGeneration) return;
            polling = true;
            // Finish publishing the preparation task before a synchronous assignment/failure can clean it up.
            await Task.Yield();
            if (generation != searchGeneration) return;
            try
            {
                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                bool connected = edgegap != null && await edgegap.WaitForAssignmentAndConnectAsync(180);
                if (generation != searchGeneration) return;

                if (!connected)
                {
                    string error = !string.IsNullOrEmpty(edgegap?.LastError)
                        ? edgegap.LastError : "EDGEGAP MATCH ASSIGNMENT FAILED";
                    if (searching)
                    {
                        await StopSearchAsync(error, true);
                        Fail(error);
                    }
                    return;
                }
                SetStatus("GAME SERVER CONNECTED • WAITING FOR MATCH START");
                if (!await WaitForMatchStartAsync(generation) && generation == searchGeneration && searching)
                {
                    string error = lastError;
                    await StopSearchAsync(error, true);
                    Fail(error);
                }
            }
            catch (Exception ex)
            {
                if (generation != searchGeneration || !searching) return;
                string error = "ONLINE MATCH START FAILED: " + ex.Message;
                await StopSearchAsync(error, true);
                Fail(error);
            }
            finally
            {
                if (generation == searchGeneration) polling = false;
                RaiseChanged();
            }
        }

        private async Task<bool> WaitForMatchStartAsync(int generation)
        {
            float deadline = Time.realtimeSinceStartup + MatchStartTimeoutSeconds;
            while (generation == searchGeneration && searching)
            {
                if (CoopAuthority.Active) return true;
                NetworkManager manager = CoopNetworkBootstrap.Manager;
                if (manager == null || !manager.IsConnectedClient || manager.IsServer)
                    return Fail("GAME SERVER DISCONNECTED BEFORE MATCH START • TRY AGAIN");
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning("DIMENSIONS FRONT ONLINE: match start timed out at " + DedicatedOnlineMatchHandshake.Status);
                    return Fail("MATCH START TIMED OUT • TRY AGAIN");
                }
                await Task.Delay(250);
            }
            return false;
        }

        private async Task AbortSearchAfterPartyChangeAsync(string nextStatus, bool broadcastCancel)
        {
            if (!cleanupRunning && searching) await StopSearchAsync(nextStatus, broadcastCancel);
        }

        private void Attach(CustomMessagingManager value)
        {
            if (value == null || attached && messaging == value) return;
            Detach();
            messaging = value;
            messaging.RegisterNamedMessageHandler(GroupMessage, OnGroupMessage);
            messaging.RegisterNamedMessageHandler(JoinAckMessage, OnJoinAck);
            messaging.RegisterNamedMessageHandler(CancelMessage, OnCancelMessage);
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
            messaging.UnregisterNamedMessageHandler(GroupMessage);
            messaging.UnregisterNamedMessageHandler(JoinAckMessage);
            messaging.UnregisterNamedMessageHandler(CancelMessage);
            messaging = null;
            attached = false;
        }

        private void ResetSearchState()
        {
            searchGeneration++;
            searching = false;
            polling = false;
            joiningGroup = false;
            searchOwner = false;
            expectedPartyPlayers = 0;
            expectedRoster = "";
            activeGroupId = "";
            joinedGuestClients.Clear();
            EdgegapMatchmakerClient.Instance?.CancelPendingAssignmentWait();
        }

        private bool PartyUnchanged(OnlinePartyService online) => online != null && online.InParty &&
            online.PlayerCount == expectedPartyPlayers && online.ReadyRosterKey == expectedRoster;

        private bool Fail(string message)
        {
            lastError = message ?? "EDGEGAP ONLINE ERROR";
            status = lastError;
            Debug.LogWarning("DIMENSIONS FRONT ONLINE: " + lastError);
            RaiseChanged();
            return false;
        }

        private void SetStatus(string value)
        {
            status = string.IsNullOrWhiteSpace(value) ? "EDGEGAP ONLINE READY" : value;
            lastError = "";
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            try { Changed?.Invoke(); } catch { }
        }

        private void OnDestroy()
        {
            Detach();
            if (searching) EdgegapMatchmakerClient.Instance?.CancelPendingAssignmentWait();
            if (Instance == this) Instance = null;
        }
    }
}

