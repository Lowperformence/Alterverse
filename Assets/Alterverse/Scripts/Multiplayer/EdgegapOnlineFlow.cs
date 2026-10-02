using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Bridges the existing private Unity friend party into Edgegap Group Up matchmaking.
    /// Relay is used only while friends assemble and to distribute the Edgegap group id.
    /// Public matchmaking and the actual match connection are owned by Edgegap + the
    /// dedicated NGO server.
    /// </summary>
    [DefaultExecutionOrder(-25000)]
    public sealed class EdgegapOnlineFlow : MonoBehaviour
    {
        private const string GroupMessage = "DF_EDGEGAP_GROUP_V1";
        private const string CancelMessage = "DF_EDGEGAP_CANCEL_V1";
        private const float GroupBroadcastInterval = 1.5f;

        public static EdgegapOnlineFlow Instance { get; private set; }

        private CustomMessagingManager messaging;
        private bool attached;
        private bool busy;
        private bool searching;
        private bool polling;
        private bool joiningGroup;
        private float nextGroupBroadcast;
        private string activeGroupId = "";
        private string status = "EDGEGAP ONLINE READY";
        private string lastError = "";

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
            if (searching && (online == null || !online.InParty) && !CoopAuthority.Active)
            {
                searching = false;
                activeGroupId = "";
                EdgegapMatchmakerClient.Instance?.ResetLocalState();
                SetStatus("EDGEGAP ONLINE READY");
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
            if (edgegap == null || !edgegap.IsConfigured)
                return Fail("EDGEGAP MATCHMAKER TOKEN NOT CONFIGURED");
            if (online.HasHeroConflict)
                return Fail("FRIENDS ON THE SAME TEAM NEED DIFFERENT HEROES");
            if (!online.AllPresentPlayersReady)
                return Fail("ALL FRIENDS MUST BE READY BEFORE SEARCHING");

            try
            {
                busy = true;
                lastError = "";
                SetStatus("PREPARING EDGEGAP GROUP");

                if (!await WaitForFriendRelayAsync(online.PlayerCount, 12f))
                    return Fail("FRIEND PARTY RELAY IS NOT READY");

                if (!await edgegap.CreateGroupAsync(true))
                    return Fail(edgegap.LastError.Length > 0 ? edgegap.LastError : "COULD NOT CREATE EDGEGAP GROUP");

                activeGroupId = edgegap.GroupId;
                if (string.IsNullOrWhiteSpace(activeGroupId))
                    return Fail("EDGEGAP DID NOT RETURN A GROUP ID");

                searching = true;
                nextGroupBroadcast = 0f;
                SetStatus("EDGEGAP SEARCH ACTIVE • PARTY STAYS TOGETHER");

                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsHost)
                    BroadcastGroup(manager);

                _ = PollAssignmentAndConnectAsync();
                return true;
            }
            catch (Exception ex)
            {
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

                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                if (edgegap != null && !await edgegap.CancelGroupAsync())
                    Debug.LogWarning("DIMENSIONS FRONT ONLINE: Edgegap group cleanup failed: " + edgegap.LastError);

                searching = false;
                polling = false;
                joiningGroup = false;
                activeGroupId = "";
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
            if (online == null) return;

            if (searching)
            {
                if (online.IsHost)
                    await CancelSearchAsync();
                else
                {
                    EdgegapMatchmakerClient.Instance?.ResetLocalState();
                    searching = false;
                    activeGroupId = "";
                }
            }
            await online.LeavePartyAsync();
        }

        public static void MarkMatchStarted()
        {
            EdgegapOnlineFlow flow = Instance;
            if (flow == null) return;
            flow.searching = false;
            flow.polling = false;
            flow.joiningGroup = false;
            flow.SetStatus("DEDICATED MATCH CONNECTED");
        }

        private async Task<bool> WaitForFriendRelayAsync(int expectedPlayers, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(2f, timeoutSeconds);
            expectedPlayers = Mathf.Clamp(expectedPlayers, 1, OnlinePartyService.MaxPartyPlayers);
            while (Time.realtimeSinceStartup < deadline)
            {
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsListening && manager.IsHost &&
                    CoopNetworkBootstrap.IsLinkReadyForPlayers(expectedPlayers))
                    return true;
                await Task.Delay(100);
            }
            return false;
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
            if (searching && string.Equals(activeGroupId, groupId, StringComparison.Ordinal)) return;
            _ = JoinFriendGroupAsync(groupId);
        }

        private void OnCancelMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId) return;
            if (reader.TryBeginRead(sizeof(bool))) reader.ReadValueSafe(out bool _);
            searching = false;
            polling = false;
            joiningGroup = false;
            activeGroupId = "";
            EdgegapMatchmakerClient.Instance?.ResetLocalState();
            SetStatus("PARTY LEADER CANCELLED SEARCH");
        }

        private async Task JoinFriendGroupAsync(string groupId)
        {
            if (joiningGroup) return;
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
                if (!await edgegap.JoinGroupAsync(groupId, true))
                {
                    Fail(edgegap.LastError.Length > 0 ? edgegap.LastError : "COULD NOT JOIN EDGEGAP GROUP");
                    return;
                }

                activeGroupId = groupId;
                searching = true;
                SetStatus("EDGEGAP SEARCH ACTIVE • PARTY STAYS TOGETHER");
                _ = PollAssignmentAndConnectAsync();
            }
            finally
            {
                joiningGroup = false;
                RaiseChanged();
            }
        }

        private async Task PollAssignmentAndConnectAsync()
        {
            if (polling) return;
            polling = true;
            try
            {
                EdgegapMatchmakerClient edgegap = EdgegapMatchmakerClient.Instance;
                if (edgegap == null || !await edgegap.WaitForAssignmentAndConnectAsync(180))
                {
                    if (searching)
                        Fail(edgegap?.LastError ?? "EDGEGAP MATCH ASSIGNMENT FAILED");
                    return;
                }
                SetStatus("DEDICATED SERVER ASSIGNED • CONNECTING");
            }
            finally
            {
                polling = false;
                RaiseChanged();
            }
        }

        private void Attach(CustomMessagingManager value)
        {
            if (value == null || attached && messaging == value) return;
            Detach();
            messaging = value;
            messaging.RegisterNamedMessageHandler(GroupMessage, OnGroupMessage);
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
            messaging.UnregisterNamedMessageHandler(CancelMessage);
            messaging = null;
            attached = false;
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
            if (Instance == this) Instance = null;
        }
    }
}
