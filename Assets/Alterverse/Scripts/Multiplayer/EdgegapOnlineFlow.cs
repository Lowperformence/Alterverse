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
    /// Relay is used only while friends assemble and to distribute the Edgegap group id.
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

        public static EdgegapOnlineFlow Instance { get; private set; }

        private CustomMessagingManager messaging;
        private bool attached;
        private bool busy;
        private bool searching;
        private bool polling;
        private bool joiningGroup;
        private bool searchOwner;
        private bool cleanupRunning;
        private int searchGeneration;
        private int expectedPartyPlayers;
        private float nextGroupBroadcast;
        private string activeGroupId = "";
        private string status = "EDGEGAP ONLINE READY";
        private string lastError = "";
        private readonly HashSet<ulong> joinedGuestClients = new();

        public bool Busy => busy;
        public bool IsSearching => searching;
        public string Status => status;
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
            if (searching && !CoopAuthority.Active)
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
                    online.PlayerCount != expectedPartyPlayers)
                {
                    _ = AbortSearchAfterPartyChangeAsync("FRIEND LEFT • MATCHMAKING CANCELLED", true);
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
            if (busy || searching) return false;
            OnlinePartyService online = OnlinePartyService.Instance;
            if (online == null) return Fail("ONLINE PARTY SERVICE UNAVAILABLE");

            if (!online.InParty && !await online.CreatePrivatePartyAsync())
                return Fail(online.LastError.Length > 0 ? online.LastError : "COULD NOT CREATE SOLO PARTY");

            if (!online.LocalReady && !await online.SetLocalReadyAsync(true))
                return Fail(online.LastError.Length > 0 ? online.LastError : "COULD NOT READY SOLO PLAYER");

            return await BeginSearchAsync();
        }

        public async Task<bool> BeginSearchAsync()
        {
            if (busy || searching) return false;
            OnlinePartyService online = OnlinePartyService.Instance;
            EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
            if (online == null || !online.InParty || !online.IsHost)
                return Fail("ONLY THE PARTY LEADER CAN START MATCHMAKING");
            if (online.PlayerCount < 1 || online.PlayerCount > OnlinePartyService.MaxPartyPlayers)
                return Fail("ONLINE PARTIES REQUIRE 1-4 PLAYERS");
            if (edgegap == null || !edgegap.IsConfigured)
                return Fail("EDGEGAP MATCHMAKER TOKEN NOT CONFIGURED");
            if (online.HasHeroConflict)
                return Fail("FRIENDS ON THE SAME TEAM NEED DIFFERENT HEROES");
            if (!online.AllPresentPlayersReady)
                return Fail("ALL FRIENDS MUST BE READY BEFORE SEARCHING");

            int generation = ++searchGeneration;
            searchOwner = true;
            expectedPartyPlayers = online.PlayerCount;

            try
            {
                busy = true;
                lastError = "";
                joinedGuestClients.Clear();
                SetStatus("PREPARING EDGEGAP GROUP");

                if (!await WaitForFriendRelayAsync(expectedPartyPlayers, 12f, generation))
                {
                    ResetSearchState();
                    return Fail("FRIEND PARTY RELAY IS NOT READY");
                }

                // Important: owner stays NOT READY while friends join. Edgegap locks a group once
                // matchmaking starts, so setting the owner ready during creation can strand friends.
                if (!await edgegap.CreateGroupAsync(false))
                {
                    ResetSearchState();
                    return Fail(edgegap.LastError.Length > 0 ? edgegap.LastError : "COULD NOT CREATE EDGEGAP GROUP");
                }

                if (generation != searchGeneration || online == null || !online.InParty ||
                    online.PlayerCount != expectedPartyPlayers)
                {
                    await edgegap.CancelGroupAsync();
                    ResetSearchState();
                    return Fail("FRIEND PARTY CHANGED DURING MATCHMAKING");
                }

                activeGroupId = edgegap.GroupId;
                if (string.IsNullOrWhiteSpace(activeGroupId))
                {
                    ResetSearchState();
                    return Fail("EDGEGAP DID NOT RETURN A GROUP ID");
                }

                searching = true;
                nextGroupBroadcast = 0f;
                SetStatus("FRIENDS JOINING EDGEGAP GROUP");

                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsHost)
                    BroadcastGroup(manager);

                int expectedGuests = Mathf.Max(0, expectedPartyPlayers - 1);
                if (!await WaitForGuestJoinAcksAsync(expectedGuests, FriendJoinTimeoutSeconds, generation))
                {
                    await edgegap.CancelGroupAsync();
                    ResetSearchState();
                    return Fail("NOT ALL FRIENDS JOINED EDGEGAP MATCHMAKING");
                }

                if (generation != searchGeneration || online == null || !online.InParty ||
                    online.PlayerCount != expectedPartyPlayers)
                {
                    await edgegap.CancelGroupAsync();
                    ResetSearchState();
                    return Fail("FRIEND PARTY CHANGED DURING MATCHMAKING");
                }

                // Guests joined already-ready. The owner becomes ready last, which atomically opens
                // the complete friend group to public matchmaking.
                SetStatus("STARTING EDGEGAP MATCHMAKING");
                if (!await edgegap.SetReadyAsync(true))
                {
                    await edgegap.CancelGroupAsync();
                    ResetSearchState();
                    return Fail(edgegap.LastError.Length > 0 ? edgegap.LastError : "COULD NOT START EDGEGAP MATCHMAKING");
                }

                if (generation != searchGeneration)
                {
                    await edgegap.CancelGroupAsync();
                    return false;
                }

                SetStatus("EDGEGAP SEARCH ACTIVE • PARTY STAYS TOGETHER");
                _ = PollAssignmentAndConnectAsync(generation);
                return true;
            }
            catch (Exception ex)
            {
                ResetSearchState();
                return Fail("EDGEGAP SEARCH FAILED: " + ex.Message);
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }
        }

        public async Task<bool> CancelSearchAsync()
        {
            if (busy || !searching) return false;
            OnlinePartyService online = OnlinePartyService.Instance;
            if (online == null || !online.InParty || !online.IsHost)
                return Fail("ONLY THE PARTY LEADER CAN CANCEL MATCHMAKING");

            try
            {
                busy = true;
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsHost)
                    BroadcastCancel(manager);

                bool owner = searchOwner;
                ResetSearchState(); // invalidate poll/connect before awaiting network cleanup

                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                if (edgegap != null)
                {
                    bool cleaned = owner ? await edgegap.CancelGroupAsync() : await edgegap.LeaveGroupAsync();
                    if (!cleaned)
                        Debug.LogWarning("DIMENSIONS FRONT ONLINE: Edgegap search cleanup failed: " + edgegap.LastError);
                }

                SetStatus("PLAYER SEARCH STOPPED • PARTY REMAINS PRIVATE");
                return true;
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }
        }

        public async Task LeavePartyAsync()
        {
            if (busy) return;
            OnlinePartyService online = OnlinePartyService.Instance;

            if (searching)
            {
                bool owner = searchOwner || (online != null && online.IsHost);
                NetworkManager manager = NetworkManager.Singleton;
                if (owner && manager != null && manager.IsHost)
                    BroadcastCancel(manager);

                ResetSearchState(); // kills stale assignment/direct-connect before API cleanup
                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                if (edgegap != null && edgegap.HasMembership)
                {
                    bool cleaned = owner ? await edgegap.CancelGroupAsync() : await edgegap.LeaveGroupAsync();
                    if (!cleaned)
                        Debug.LogWarning("DIMENSIONS FRONT ONLINE: Edgegap leave cleanup failed: " + edgegap.LastError);
                }
            }

            if (online != null && online.InParty)
                await online.LeavePartyAsync();
        }

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
            flow.activeGroupId = "";
            flow.joinedGuestClients.Clear();
            flow.SetStatus("DEDICATED MATCH CONNECTED");
        }

        private async Task<bool> WaitForFriendRelayAsync(int expectedPlayers, float timeoutSeconds, int generation)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(2f, timeoutSeconds);
            expectedPlayers = Mathf.Clamp(expectedPlayers, 1, OnlinePartyService.MaxPartyPlayers);
            while (Time.realtimeSinceStartup < deadline)
            {
                if (generation != searchGeneration) return false;
                OnlinePartyService online = OnlinePartyService.Instance;
                if (online == null || !online.InParty || online.PlayerCount != expectedPlayers) return false;

                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsListening && manager.IsHost &&
                    CoopNetworkBootstrap.IsLinkReadyForPlayers(expectedPlayers))
                    return true;
                await Task.Delay(100);
            }
            return false;
        }

        private async Task<bool> WaitForGuestJoinAcksAsync(int expectedGuests, float timeoutSeconds, int generation)
        {
            if (expectedGuests <= 0) return true;
            float deadline = Time.realtimeSinceStartup + Mathf.Max(3f, timeoutSeconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                if (generation != searchGeneration) return false;
                OnlinePartyService online = OnlinePartyService.Instance;
                if (online == null || !online.InParty || online.PlayerCount != expectedPartyPlayers) return false;
                if (joinedGuestClients.Count >= expectedGuests) return true;
                NetworkManager manager = NetworkManager.Singleton;
                if (manager == null || !manager.IsHost || !manager.IsListening) return false;
                BroadcastGroup(manager);
                await Task.Delay(200);
            }
            return generation == searchGeneration && joinedGuestClients.Count >= expectedGuests;
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
                online == null || !online.InParty || joiningGroup)
                return;

            reader.ReadValueSafe(out FixedString128Bytes fixedGroupId);
            string groupId = fixedGroupId.ToString();
            if (string.IsNullOrWhiteSpace(groupId)) return;
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
            if (reader.TryBeginRead(sizeof(bool))) reader.ReadValueSafe(out bool _);
            ResetSearchState();
            EdgegapMatchmakerClient.Instance?.ResetLocalState();
            SetStatus("PARTY LEADER CANCELLED SEARCH");
        }

        private async Task JoinFriendGroupAsync(string groupId)
        {
            if (joiningGroup) return;
            int generation = ++searchGeneration;
            searchOwner = false;
            OnlinePartyService online = OnlinePartyService.Instance;
            expectedPartyPlayers = online?.PlayerCount ?? 0;
            joiningGroup = true;
            try
            {
                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                if (edgegap == null || !edgegap.IsConfigured)
                {
                    Fail("EDGEGAP MATCHMAKER TOKEN NOT CONFIGURED");
                    return;
                }

                SetStatus("JOINING FRIENDS IN EDGEGAP");
                // Guests may be ready while joining; the owner remains not-ready until all guest
                // membership acknowledgements have arrived, so the group is still joinable.
                if (!await edgegap.JoinGroupAsync(groupId, true))
                {
                    Fail(edgegap.LastError.Length > 0 ? edgegap.LastError : "COULD NOT JOIN EDGEGAP GROUP");
                    return;
                }

                if (generation != searchGeneration)
                {
                    await edgegap.LeaveGroupAsync();
                    return;
                }

                activeGroupId = groupId;
                searching = true;
                SetStatus("EDGEGAP GROUP JOINED • WAITING FOR LEADER");
                SendJoinAck();
                _ = PollAssignmentAndConnectAsync(generation);
            }
            finally
            {
                if (generation == searchGeneration) joiningGroup = false;
                RaiseChanged();
            }
        }

        private async Task PollAssignmentAndConnectAsync(int generation)
        {
            if (polling && generation == searchGeneration) return;
            polling = true;
            try
            {
                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                bool connected = edgegap != null && await edgegap.WaitForAssignmentAndConnectAsync(180);
                if (generation != searchGeneration) return;

                if (!connected)
                {
                    string error = edgegap?.LastError ?? "EDGEGAP MATCH ASSIGNMENT FAILED";
                    if (searching)
                    {
                        ResetSearchState();
                        Fail(error);
                    }
                    return;
                }
                SetStatus("DEDICATED SERVER CONNECTED • VERIFYING MATCH");
            }
            finally
            {
                if (generation == searchGeneration) polling = false;
                RaiseChanged();
            }
        }

        private async Task AbortSearchAfterPartyChangeAsync(string nextStatus, bool broadcastCancel)
        {
            if (cleanupRunning || !searching) return;
            cleanupRunning = true;
            try
            {
                bool owner = searchOwner;
                NetworkManager manager = NetworkManager.Singleton;
                if (broadcastCancel && owner && manager != null && manager.IsHost)
                    BroadcastCancel(manager);

                ResetSearchState();
                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                if (edgegap != null && edgegap.HasMembership)
                {
                    bool cleaned = owner ? await edgegap.CancelGroupAsync() : await edgegap.LeaveGroupAsync();
                    if (!cleaned)
                        Debug.LogWarning("DIMENSIONS FRONT ONLINE: Edgegap disconnect cleanup failed: " + edgegap.LastError);
                }
                SetStatus(nextStatus);
            }
            finally
            {
                cleanupRunning = false;
                RaiseChanged();
            }
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
            activeGroupId = "";
            joinedGuestClients.Clear();
            EdgegapMatchmakerClient.Instance?.CancelPendingAssignmentWait();
        }

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
