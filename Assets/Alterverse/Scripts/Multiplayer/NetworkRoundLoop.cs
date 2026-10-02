using System;
using System.Collections.Generic;
using Alterverse.Match;
using Alterverse.Prototype;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Server-owned post-match receipt barrier, plan acknowledgement and countdown.</summary>
    public sealed class NetworkRoundLoop : MonoBehaviour
    {
        const string ReadyMessage = "DF_ROUND_READY_V1", PlanMessage = "DF_ROUND_PLAN_V1",
            AckMessage = "DF_ROUND_ACK_V1", ClockMessage = "DF_ROUND_CLOCK_V1", LaunchMessage = "DF_ROUND_LAUNCH_V1";
        public static NetworkRoundLoop Instance { get; private set; }
        public static bool IsBetweenRounds => CoopAuthority.Active && FindAnyObjectByType<DimensionsFront>()?.HasEnded == true;
        public double Deadline { get; private set; } = -1;
        public bool Transitioning { get; private set; }
        public string Status { get; private set; } = "WAITING FOR TEAM REWARDS…";
        public int Seconds => Deadline < 0 || NetworkManager.Singleton == null ? -1 :
            Mathf.Max(0, (int)Math.Ceiling(Deadline - NetworkManager.Singleton.ServerTime.Time));
        CustomMessagingManager messaging;
        string round = "";
        CoopMatchPlan proposed;
        int proposedMask;
        uint fingerprint;
        float nextSend;
        bool launching;
        readonly HashSet<string> ready = new(), acknowledged = new();

        void Awake() { Instance = this; }
        void Update()
        {
            if (MatchExit.Leaving || !CoopAuthority.Active) { Reset(); Detach(); return; }
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening) return;
            Attach(manager.CustomMessagingManager);
            if (round != CoopAuthority.ActivePlan.MatchId) { Reset(); round = CoopAuthority.ActivePlan.MatchId; }
            if (Transitioning || launching) return;
            DimensionsFront front = FindAnyObjectByType<DimensionsFront>();
            if (front == null || !front.HasEnded) return;
            bool localReady = MatchScoreboard.Instance != null && MatchScoreboard.Instance.ResultCommitted;
            int localSlot = CoopRosterBuilder.FindLocalHumanSlot(CoopAuthority.ActivePlan);
            bool serverOnly = manager.IsServer && !manager.IsClient;
            if (!localReady && !serverOnly) { Status = "FINALIZING REWARDS…"; return; }
            if (!manager.IsServer)
            {
                if (localSlot >= 0 && Time.unscaledTime >= nextSend)
                {
                    nextSend = Time.unscaledTime + 1;
                    using FastBufferWriter writer = new(288, Allocator.Temp);
                    writer.WriteValueSafe(new FixedString128Bytes(round));
                    writer.WriteValueSafe(new FixedString128Bytes(CoopAuthority.ActivePlan.Session.Slots[localSlot].PlayerId));
                    messaging.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
                }
                return;
            }
            if (CoopMatchResultSync.Instance == null || !CoopMatchResultSync.Instance.ResultReady) return;
            int mask = ConnectedHumanMask(manager);
            if (localSlot >= 0) ready.Add(CoopAuthority.ActivePlan.Session.Slots[localSlot].PlayerId);
            if (mask == 0) { Status = "WAITING FOR PLAYERS…"; return; }
            if (proposed != null && mask != proposedMask) ClearProposal();
            if (!AllInSet(mask, ready)) { Status = "WAITING FOR TEAM REWARDS…"; return; }
            if (proposed == null)
            {
                proposedMask = mask;
                proposed = CoopMatchPlan.NextRound(CoopAuthority.ActivePlan, Guid.NewGuid().ToString("N"), mask);
                fingerprint = CoopPlanHandshake.Fingerprint(proposed);
                if (localSlot >= 0) acknowledged.Add(CoopAuthority.ActivePlan.Session.Slots[localSlot].PlayerId);
                nextSend = 0;
            }
            if (AllInSet(mask, acknowledged) && Deadline < 0)
                Deadline = manager.ServerTime.Time + 12;
            Status = Deadline < 0 ? "SYNCING NEXT ROUND…" : "NEXT ROUND IN " + Seconds;
            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + 1;
                BroadcastPlan(manager);
                if (Deadline >= 0) BroadcastClock(manager);
            }
            if (Deadline >= 0 && manager.ServerTime.Time >= Deadline) LaunchHost();
        }
        bool AllInSet(int mask, HashSet<string> set)
        {
            for (int i = 0; i < MatchSession.MaxPlayers; i++)
                if ((mask & (1 << i)) != 0 && !set.Contains(CoopAuthority.ActivePlan.Session.Slots[i].PlayerId)) return false;
            return true;
        }
        int ConnectedHumanMask(NetworkManager manager)
        {
            int mask = 0;
            var slots = CoopAuthority.ActivePlan.Session.Slots;
            int local = CoopRosterBuilder.FindLocalHumanSlot(CoopAuthority.ActivePlan);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].IsBot) continue;
                if (manager.IsHost && i == local) { mask |= 1 << i; continue; }
                foreach (ulong id in manager.ConnectedClientsIds)
                    if (id != NetworkManager.ServerClientId && Authorized(id, slots[i].PlayerId)) { mask |= 1 << i; break; }
            }
            return mask;
        }
        static bool Authorized(ulong id, string player) =>
            CoopPlanHandshake.IsAuthorizedClient(id, player) || OnlinePlanHandshake.IsAuthorizedClient(id, player);
        bool Current(string id) => !MatchExit.Leaving && CoopAuthority.Active && id == CoopAuthority.ActivePlan.MatchId;
        void OnReady(ulong sender, FastBufferReader reader)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;
            reader.ReadValueSafe(out FixedString128Bytes id); reader.ReadValueSafe(out FixedString128Bytes player);
            if (Current(id.ToString()) && Authorized(sender, player.ToString())) ready.Add(player.ToString());
        }
        void BroadcastPlan(NetworkManager manager)
        {
            using FastBufferWriter writer = new(304, Allocator.Temp);
            writer.WriteValueSafe(new FixedString128Bytes(round)); writer.WriteValueSafe(new FixedString128Bytes(proposed.MatchId));
            writer.WriteValueSafe(proposedMask); writer.WriteValueSafe(fingerprint);
            SendAll(manager, PlanMessage, writer);
        }
        void OnPlan(ulong sender, FastBufferReader reader)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out FixedString128Bytes id); reader.ReadValueSafe(out FixedString128Bytes next);
            reader.ReadValueSafe(out int mask); reader.ReadValueSafe(out uint hash);
            if (!Current(id.ToString()) || MatchScoreboard.Instance == null || !MatchScoreboard.Instance.ResultCommitted) return;
            int local = CoopRosterBuilder.FindLocalHumanSlot(CoopAuthority.ActivePlan);
            if (local < 0 || (mask & (1 << local)) == 0) return;
            CoopMatchPlan plan;
            try { plan = CoopMatchPlan.NextRound(CoopAuthority.ActivePlan, next.ToString(), mask); }
            catch (Exception) { return; }
            if (CoopPlanHandshake.Fingerprint(plan) != hash) return;
            if (proposed == null || proposed.MatchId != plan.MatchId) Deadline = -1;
            proposed = plan; proposedMask = mask; fingerprint = hash;
            using FastBufferWriter writer = new(288, Allocator.Temp);
            writer.WriteValueSafe(new FixedString128Bytes(id.ToString())); writer.WriteValueSafe(hash);
            writer.WriteValueSafe(new FixedString128Bytes(CoopAuthority.ActivePlan.Session.Slots[local].PlayerId));
            messaging.SendNamedMessage(AckMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            Status = "WAITING FOR NEXT ROUND…";
        }
        void OnAck(ulong sender, FastBufferReader reader)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || proposed == null) return;
            reader.ReadValueSafe(out FixedString128Bytes id); reader.ReadValueSafe(out uint hash); reader.ReadValueSafe(out FixedString128Bytes player);
            if (Current(id.ToString()) && hash == fingerprint && Authorized(sender, player.ToString())) acknowledged.Add(player.ToString());
        }
        void BroadcastClock(NetworkManager manager)
        {
            using FastBufferWriter writer = new(160, Allocator.Temp);
            writer.WriteValueSafe(new FixedString128Bytes(round)); writer.WriteValueSafe(fingerprint); writer.WriteValueSafe(Deadline);
            SendAll(manager, ClockMessage, writer);
        }
        void OnClock(ulong sender, FastBufferReader reader)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out FixedString128Bytes id); reader.ReadValueSafe(out uint hash); reader.ReadValueSafe(out double deadline);
            if (Current(id.ToString()) && proposed != null && hash == fingerprint) Deadline = deadline;
        }
        async void LaunchHost()
        {
            launching = true;
            try
            {
                var lobby = CoopLobbyService.Instance;
                if (CoopAuthority.ActivePlan is not OnlineMatchPlan && lobby != null && lobby.InLobby &&
                    !await lobby.ResumeRoundAsync(proposed.MatchId)) { ClearProposal(); return; }
                var manager = NetworkManager.Singleton;
                if (!Current(round) || manager == null || !manager.IsServer || proposed == null) return;
                if (ConnectedHumanMask(manager) != proposedMask) { ClearProposal(); return; }
                using FastBufferWriter writer = new(160, Allocator.Temp);
                writer.WriteValueSafe(new FixedString128Bytes(round)); writer.WriteValueSafe(fingerprint);
                SendAll(manager, LaunchMessage, writer);
                Launch();
            }
            catch (Exception error) { Debug.LogWarning("DF: next round pending: " + error.Message); ClearProposal(); }
            finally { launching = false; }
        }
        void OnLaunch(ulong sender, FastBufferReader reader)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out FixedString128Bytes id); reader.ReadValueSafe(out uint hash);
            if (Current(id.ToString()) && proposed != null && hash == fingerprint && !Transitioning) Launch();
        }
        void Launch()
        {
            var bootstrap = FindAnyObjectByType<PrototypeBootstrap>();
            if (bootstrap == null || proposed == null || MatchExit.Leaving) return;
            Transitioning = true;
            CoopAuthority.Prepare(proposed);
            if (!CoopAuthority.BeginPreparedMatch(NetworkManager.Singleton.IsHost)) { Transitioning = false; return; }
            MatchClock.Set(PauseReason.Result, false); MatchClock.Set(PauseReason.Settings, false);
            bootstrap.StartCoroutine(bootstrap.RestartPrototype());
        }
        void SendAll(NetworkManager manager, string name, FastBufferWriter writer)
        {
            foreach (ulong id in manager.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId) messaging.SendNamedMessage(name, id, writer, NetworkDelivery.ReliableSequenced);
        }
        void ClearProposal() { proposed = null; Deadline = -1; acknowledged.Clear(); nextSend = 0; }
        void Reset() { round = ""; ready.Clear(); ClearProposal(); Transitioning = false; launching = false; }
        void Attach(CustomMessagingManager value)
        {
            if (messaging == value) return;
            Detach(); messaging = value; if (messaging == null) return;
            messaging.RegisterNamedMessageHandler(ReadyMessage, OnReady); messaging.RegisterNamedMessageHandler(PlanMessage, OnPlan);
            messaging.RegisterNamedMessageHandler(AckMessage, OnAck); messaging.RegisterNamedMessageHandler(ClockMessage, OnClock);
            messaging.RegisterNamedMessageHandler(LaunchMessage, OnLaunch);
        }
        void Detach()
        {
            if (messaging == null) return;
            foreach (string name in new[] { ReadyMessage, PlanMessage, AckMessage, ClockMessage, LaunchMessage }) messaging.UnregisterNamedMessageHandler(name);
            messaging = null;
        }
        void OnDestroy() { Detach(); if (Instance == this) Instance = null; }
    }
}
