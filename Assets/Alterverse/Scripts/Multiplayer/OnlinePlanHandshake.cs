using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alterverse.AI;
using Alterverse.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Synchronizes a deterministic online match plan over the already established Relay/NGO link.
    /// The host launches only after every connected human has rebuilt and acknowledged the same plan.
    /// This legacy Relay-host path remains for compatibility; dedicated online matches authorize
    /// gameplay through DedicatedOnlineMatchHandshake.
    /// </summary>
    public sealed class OnlinePlanHandshake : MonoBehaviour
    {
        private const string PlanMessage = "DF_ONLINE_PLAN_V1";
        private const string AckMessage = "DF_ONLINE_PLAN_ACK_V1";
        private const string LaunchMessage = "DF_ONLINE_LAUNCH_V1";
        private const int ProtocolVersion = 3;

        public static OnlinePlanHandshake Instance { get; private set; }
        public static bool Preparing => Instance != null && Instance.preparing;
        public static string Status => Instance == null ? "ONLINE PLAN OFFLINE" : Instance.status;

        private CustomMessagingManager messaging;
        private bool attached;
        private bool preparing;
        private string status = "ONLINE MATCH READY";
        private uint fingerprint;
        private int expectedHumans;
        private float startedAt;
        private const float PlanAckTimeoutSeconds=20f;
        private readonly HashSet<ulong> approvedClients = new();
        private readonly Dictionary<ulong, string> approvedPlayerIds = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if (Instance == null)
                new GameObject("DF_OnlinePlanHandshake").AddComponent<OnlinePlanHandshake>();
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
            OnlinePartyService online = OnlinePartyService.Instance;
            bool needed = CoopAuthority.ActivePlan is OnlineMatchPlan ||
                          CoopAuthority.PreparedPlan is OnlineMatchPlan ||
                          (online != null && online.InParty);

            if (!needed || manager == null || !manager.IsListening)
            {
                if (!CoopAuthority.Active) Detach();
                return;
            }

            Attach(manager.CustomMessagingManager);

            if (preparing && Time.unscaledTime-startedAt>=PlanAckTimeoutSeconds)
            {
                AbortPrepared("ONLINE PLAN CONFIRMATION TIMED OUT • READY UP TO RETRY");
                return;
            }
            if (manager.IsServer && preparing)
            {
                if (online == null || !online.InParty || online.PlayerCount != expectedHumans ||
                    manager.ConnectedClientsIds == null || manager.ConnectedClientsIds.Count < expectedHumans)
                {
                    AbortPrepared("PLAYER LEFT • ONLINE MATCH PREPARATION CANCELLED");
                    return;
                }
                approvedClients.RemoveWhere(id => !IsConnected(manager, id));
                TryCompleteHostHandshake(manager);
            }
        }

        public static bool IsAuthorizedClient(ulong clientId, string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;

            // Production/public online path: the NGO dedicated server binds network client ids
            // to the player id accepted during the Edgegap handoff handshake.
            if (DedicatedOnlineMatchHandshake.IsAuthorizedClient(clientId, playerId)) return true;

            // Compatibility path for the older Relay-host online flow.
            OnlinePlanHandshake service = Instance;
            return service != null &&
                   service.approvedPlayerIds.TryGetValue(clientId, out string approved) && approved == playerId;
        }

        public static async Task<bool> StartOnlineMatchAsync()
        {
            OnlinePlanHandshake service = Instance;
            return service != null && await service.StartHostMatchAsync();
        }

        private async Task<bool> StartHostMatchAsync()
        {
            OnlinePartyService online = OnlinePartyService.Instance;
            if (online == null || !online.InParty || !online.IsHost || online.Busy || preparing)
                return false;
            if (online.MatchmakingLocked || EdgegapOnlineFlow.Instance?.Busy == true || EdgegapOnlineFlow.Instance?.IsSearching == true)
                return false;
            if (!online.AllPresentPlayersReady || online.HasHeroConflict)
            {
                status = online.HasHeroConflict ? "CHOOSE DIFFERENT HEROES" : "WAITING FOR ALL PRESENT PLAYERS READY";
                return false;
            }

            if (online.IsSearching)
                await online.CancelPlayerSearchAsync();
            if (!online.InParty || !online.IsHost) return false;

            NetworkManager manager = NetworkManager.Singleton;
            int humans = online.PlayerCount;
            if (humans > 1 && (manager == null || !manager.IsHost || !CoopNetworkBootstrap.IsLinkReadyForPlayers(humans)))
            {
                status = "WAITING FOR RELAY PLAYERS";
                return false;
            }

            string matchId = Guid.NewGuid().ToString("N");
            OnlineMatchPlan plan;
            try
            {
                plan = OnlineMatchPlan.Build(matchId, ArenaVariantRuntime.Selected, BotDifficultySettings.OnlineDifficulty, online.Members);
            }
            catch (Exception ex)
            {
                status = "ONLINE ROSTER INVALID";
                Debug.LogError("DIMENSIONS FRONT ONLINE: could not build match plan: " + ex.Message);
                return false;
            }

            CoopAuthority.Prepare(plan);
            fingerprint = CoopPlanHandshake.Fingerprint(plan);
            expectedHumans = plan.HumanCount;
            approvedClients.Clear();
            approvedPlayerIds.Clear();
            preparing = true;
            startedAt = Time.unscaledTime;

            if (expectedHumans <= 1)
            {
                status = "SOLO ONLINE MATCH • BOTS FILL OPEN SLOTS";
                LaunchPrepared(true);
                return true;
            }

            Attach(manager.CustomMessagingManager);
            if (messaging == null)
            {
                preparing = false;
                status = "ONLINE RELAY LINK NOT READY";
                return false;
            }

            using FastBufferWriter writer = new(192, Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(new FixedString128Bytes(matchId));
            writer.WriteValueSafe((int)plan.Arena);
            writer.WriteValueSafe((int)plan.Difficulty);
            writer.WriteValueSafe(fingerprint);

            foreach (ulong clientId in manager.ConnectedClientsIds)
                if (clientId != NetworkManager.ServerClientId)
                    messaging.SendNamedMessage(PlanMessage, clientId, writer, NetworkDelivery.ReliableSequenced);

            status = $"SYNCING ONLINE MATCH PLAN • 1/{expectedHumans}";
            return true;
        }

        private void OnPlan(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            OnlinePartyService online = OnlinePartyService.Instance;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId ||
                online == null || !online.InParty) return;

            reader.ReadValueSafe(out int protocol);
            reader.ReadValueSafe(out FixedString128Bytes fixedMatchId);
            reader.ReadValueSafe(out int arenaRaw);
            reader.ReadValueSafe(out int difficultyRaw);
            reader.ReadValueSafe(out uint hostFingerprint);

            bool valid = protocol == ProtocolVersion && difficultyRaw == (int)BotDifficultySettings.OnlineDifficulty;
            OnlineMatchPlan plan = null;
            if (valid)
            {
                try
                {
                    ArenaVariant arena = (ArenaVariant)arenaRaw;
                    BotDifficulty difficulty = (BotDifficulty)difficultyRaw;
                    plan = OnlineMatchPlan.Build(fixedMatchId.ToString(), arena, difficulty, online.Members);
                    valid = CoopPlanHandshake.Fingerprint(plan) == hostFingerprint;
                }
                catch (Exception ex)
                {
                    valid = false;
                    Debug.LogWarning("DIMENSIONS FRONT ONLINE: client plan build failed: " + ex.Message);
                }
            }

            if (valid)
            {
                CoopAuthority.Prepare(plan);
                fingerprint = hostFingerprint;
                expectedHumans = plan.HumanCount;
                preparing = true;
                startedAt = Time.unscaledTime;
                status = "ONLINE MATCH PLAN VERIFIED";
            }

            string localPlayerId = FindLocalPlayerId(plan);
            using FastBufferWriter writer = new(176, Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(valid);
            writer.WriteValueSafe(hostFingerprint);
            writer.WriteValueSafe(new FixedString128Bytes(localPlayerId ?? ""));
            messaging?.SendNamedMessage(AckMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private void OnAck(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || !preparing || CoopAuthority.PreparedPlan is not OnlineMatchPlan plan)
                return;

            reader.ReadValueSafe(out int protocol);
            reader.ReadValueSafe(out bool accepted);
            reader.ReadValueSafe(out uint clientFingerprint);
            reader.ReadValueSafe(out FixedString128Bytes fixedPlayerId);
            string playerId = fixedPlayerId.ToString();

            bool ownClaim = approvedPlayerIds.TryGetValue(senderClientId, out string existing) && existing == playerId;
            bool claimed = !ownClaim && approvedPlayerIds.ContainsValue(playerId);
            bool valid = protocol == ProtocolVersion && accepted && clientFingerprint == fingerprint &&
                         !claimed && PlayerExists(plan, playerId) && IsConnected(manager, senderClientId);

            if (valid)
            {
                approvedClients.Add(senderClientId);
                approvedPlayerIds[senderClientId] = playerId;
            }
            else
            {
                approvedClients.Remove(senderClientId);
                approvedPlayerIds.Remove(senderClientId);
            }

            status = valid
                ? $"ONLINE PLAN VERIFIED • {Mathf.Min(expectedHumans, approvedClients.Count + 1)}/{expectedHumans}"
                : "ONLINE MATCH PLAN MISMATCH";
            TryCompleteHostHandshake(manager);
        }

        private void TryCompleteHostHandshake(NetworkManager manager)
        {
            if (!preparing || manager == null || !manager.IsServer || CoopAuthority.PreparedPlan is not OnlineMatchPlan)
                return;

            int expectedClients = Mathf.Max(0, expectedHumans - 1);
            if (approvedClients.Count < expectedClients) return;
            if (manager.ConnectedClientsIds == null || manager.ConnectedClientsIds.Count < expectedHumans) return;

            status = $"ONLINE MATCH PLAN SYNCED • {expectedHumans}/{expectedHumans}";
            using FastBufferWriter writer = new(12, Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(fingerprint);
            foreach (ulong clientId in manager.ConnectedClientsIds)
                if (clientId != NetworkManager.ServerClientId)
                    messaging?.SendNamedMessage(LaunchMessage, clientId, writer, NetworkDelivery.ReliableSequenced);

            LaunchPrepared(true);
        }

        private void OnLaunch(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId ||
                CoopAuthority.PreparedPlan is not OnlineMatchPlan) return;

            reader.ReadValueSafe(out int protocol);
            reader.ReadValueSafe(out uint hostFingerprint);
            if (protocol != ProtocolVersion || hostFingerprint != fingerprint) return;
            LaunchPrepared(false);
        }

        private void AbortPrepared(string reason)
        {
            preparing=false;
            approvedClients.Clear();approvedPlayerIds.Clear();
            CoopAuthority.ClearPrepared();
            status=reason;
        }

        private void LaunchPrepared(bool localIsHost)
        {
            if (CoopAuthority.PreparedPlan is not OnlineMatchPlan plan) return;
            preparing = false;
            status = "LAUNCHING ONLINE MATCH";
            if (!CoopAuthority.BeginPreparedMatch(localIsHost)) return;
            OnlineMatchRuntime.Enter(plan);
        }

        private void Attach(CustomMessagingManager value)
        {
            if (value == null || attached && messaging == value) return;
            Detach();
            messaging = value;
            messaging.RegisterNamedMessageHandler(PlanMessage, OnPlan);
            messaging.RegisterNamedMessageHandler(AckMessage, OnAck);
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
            messaging.UnregisterNamedMessageHandler(PlanMessage);
            messaging.UnregisterNamedMessageHandler(AckMessage);
            messaging.UnregisterNamedMessageHandler(LaunchMessage);
            messaging = null;
            attached = false;
            if (!CoopAuthority.Active)
            {
                preparing = false;
                approvedClients.Clear();
                approvedPlayerIds.Clear();
            }
        }

        private static string FindLocalPlayerId(CoopMatchPlan plan)
        {
            int slot = CoopRosterBuilder.FindLocalHumanSlot(plan);
            return slot >= 0 && plan?.Session?.Slots != null ? plan.Session.Slots[slot]?.PlayerId ?? "" : "";
        }

        private static bool PlayerExists(CoopMatchPlan plan, string playerId)
        {
            if (plan?.Session?.Slots == null || string.IsNullOrEmpty(playerId)) return false;
            for (int i = 0; i < plan.Session.Slots.Length; i++)
            {
                MatchPlayerSlot slot = plan.Session.Slots[i];
                if (slot != null && !slot.IsBot && !slot.IsHost && slot.PlayerId == playerId) return true;
            }
            return false;
        }

        private static bool IsConnected(NetworkManager manager, ulong clientId)
        {
            if (manager?.ConnectedClientsIds == null) return false;
            for (int i = 0; i < manager.ConnectedClientsIds.Count; i++)
                if (manager.ConnectedClientsIds[i] == clientId) return true;
            return false;
        }

        private void OnDestroy()
        {
            Detach();
            if (Instance == this) Instance = null;
        }
    }
}


