using Alterverse.Match;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Replicates the server-owned match clock, opening countdown, control score and result to every client.</summary>
    public sealed class CoopMatchStateSync : MonoBehaviour
    {
        private const string StateMessage="DF_COOP_MATCH_STATE_V2";
        private const string ReadyMessage="DF_COOP_ARENA_READY_V1";
        private const float SendInterval=.20f;
        private static CoopMatchStateSync instance;
        private readonly Dictionary<string,ulong> readyPlayers=new();
        private bool localArenaReady,startAborted;
        private float nextReady,waitingSince;

        public static bool OpeningReady=>!CoopAuthority.Active||instance!=null&&instance.AllPlayersReady();
        public static void MarkLocalArenaReady(){if(instance!=null)instance.localArenaReady=true;}

        private DimensionsFront front;
        private MatchOpening opening;
        private CustomMessagingManager messaging;
        private bool attached,openingEverActive;
        private float nextSend;

        private void Awake(){instance=this;waitingSince=Time.unscaledTime;front=FindAnyObjectByType<DimensionsFront>();opening=FindAnyObjectByType<MatchOpening>();}
        private void OnEnable(){instance=this;front??=FindAnyObjectByType<DimensionsFront>();opening??=FindAnyObjectByType<MatchOpening>();}
        private void OnDisable(){if(instance==this)instance=null;Detach();}

        private bool AllPlayersReady()
        {
            var planned=CoopAuthority.ActivePlan?.Session?.Slots;
            var manager=NetworkManager.Singleton;
            if(planned==null||manager==null||startAborted)return false;
            foreach(var player in planned)
            {
                if(player==null||player.IsBot)continue;
                if(!readyPlayers.TryGetValue(player.PlayerId,out ulong id))return false;
                bool connected=false;
                foreach(ulong peer in manager.ConnectedClientsIds)if(peer==id){connected=true;break;}
                if(!connected)return false;
            }
            return true;
        }

        private void TickReady(NetworkManager manager)
        {
            if(!localArenaReady||Time.unscaledTime<nextReady)return;
            nextReady=Time.unscaledTime+.5f;
            foreach(var hero in FindObjectsByType<CoopHeroSlot>(FindObjectsSortMode.None))
            {
                if(!hero.IsLocalHuman)continue;
                if(manager.IsServer){readyPlayers[hero.PlayerId]=manager.LocalClientId;return;}
                using FastBufferWriter writer=new(256,Allocator.Temp);
                writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
                writer.WriteValueSafe(new FixedString128Bytes(hero.PlayerId));
                messaging.SendNamedMessage(ReadyMessage,NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableSequenced);
                return;
            }
        }

        private void OnArenaReady(ulong sender,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||
                !MatchNetworkProtocol.ReadRound(ref reader)||!reader.TryBeginRead(sizeof(ushort)))return;
            FixedString128Bytes value;
            try{reader.ReadValueSafe(out value);}catch(System.OverflowException){return;}
            string playerId=value.ToString();
            if(!MatchNetworkProtocol.Authorized(sender,playerId))return;
            foreach(var player in CoopAuthority.ActivePlan.Session.Slots)
                if(player!=null&&!player.IsBot&&player.PlayerId==playerId)
                {readyPlayers[playerId]=sender;return;}
        }

        private void Update()
        {
            if(!CoopAuthority.Active)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return;
            front??=FindAnyObjectByType<DimensionsFront>();
            opening??=FindAnyObjectByType<MatchOpening>();
            if(front==null)return;

            // Keep a headless-safe fallback for alternate world builders. The standard
            // bootstrap creates the opening pause before it constructs any actors.
            if(opening==null&&CoopAuthority.LocalHasServerAuthority)
                opening=new GameObject("DF_ServerMatchOpening").AddComponent<MatchOpening>();

            Attach(manager.CustomMessagingManager);
            if(messaging==null)return;
            if(opening!=null&&opening.OpeningActive)TickReady(manager);

            if(!CoopAuthority.LocalHasServerAuthority)return;

            if(!startAborted&&opening!=null&&opening.PhaseCode<0&&!AllPlayersReady()&&Time.unscaledTime-waitingSince>90f)
            {
                startAborted=true;
                Debug.LogError("DIMENSIONS FRONT: arena loading timed out; match cancelled before gameplay.");
                if(DedicatedServerNetworkRuntime.Requested)
                {
                    if(RankedMatchReporter.Instance?.Registered==true)
                        _=RankedMatchReporter.Instance.VoidAsync(RankedMatchReporter.Instance.MatchId);
                    var peers=new List<ulong>(manager.ConnectedClientsIds);
                    foreach(ulong peer in peers)if(peer!=NetworkManager.ServerClientId)manager.DisconnectClient(peer);
                    EdgegapDedicatedServerLifecycle.StopAbortedMatch();
                }
                else _=MatchExit.ReturnToMenuAsync("A player could not finish loading. Please try again.");
                return;
            }

            // Never publish an initial "opening=false" before the authoritative countdown has
            // actually entered its pause. Remote clients intentionally remain paused until this happens.
            if(!openingEverActive)
            {
                if(opening==null||!opening.OpeningActive)return;
                openingEverActive=true;
            }

            if(!SnapshotCadence.IsDue(Time.unscaledTime,SendInterval,ref nextSend))return;
            SendState(manager);
        }

        private void SendState(NetworkManager manager)
        {
            if(manager==null||!manager.IsServer||messaging==null||manager.ConnectedClientsIds==null)return;
            using FastBufferWriter writer=new(96,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(front.TimeRemaining);
            writer.WriteValueSafe(front.ZeroControl);
            writer.WriteValueSafe(front.LumenControl);
            writer.WriteValueSafe(front.IsOvertime);
            writer.WriteValueSafe(front.IsSuddenDeath);
            writer.WriteValueSafe(front.HasEnded);
            writer.WriteValueSafe(front.Winner);

            bool openingActive=opening!=null&&opening.OpeningActive;
            writer.WriteValueSafe(openingActive);
            writer.WriteValueSafe(opening!=null?opening.PhaseCode:-1);
            writer.WriteValueSafe(opening!=null?opening.PhaseElapsed:0f);
            writer.WriteValueSafe(opening!=null?opening.PhaseDuration:0f);

            for(int i=0;i<manager.ConnectedClientsIds.Count;i++)
            {
                ulong id=manager.ConnectedClientsIds[i];
                if(id==NetworkManager.ServerClientId)continue;
                messaging.SendNamedMessage(StateMessage,id,writer,NetworkDelivery.UnreliableSequenced);
            }
        }

        private void OnState(ulong senderClientId,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||senderClientId!=NetworkManager.ServerClientId)return;
            front??=FindAnyObjectByType<DimensionsFront>();
            opening??=FindAnyObjectByType<MatchOpening>();
            if(front==null)return;

            reader.ReadValueSafe(out uint round);
            if (round != CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan)) return;
            reader.ReadValueSafe(out float remaining);
            reader.ReadValueSafe(out float zero);
            reader.ReadValueSafe(out float lumen);
            reader.ReadValueSafe(out bool overtime);
            reader.ReadValueSafe(out bool sudden);
            reader.ReadValueSafe(out bool ended);
            reader.ReadValueSafe(out int winner);
            reader.ReadValueSafe(out bool openingActive);
            reader.ReadValueSafe(out int openingPhase);
            reader.ReadValueSafe(out float openingElapsed);
            reader.ReadValueSafe(out float openingDuration);

            front.ApplyNetworkState(remaining,zero,lumen,overtime,sudden,ended,winner);
            opening?.ApplyNetworkOpeningState(openingActive,openingPhase,openingElapsed,openingDuration);
        }

        private void Attach(CustomMessagingManager value)
        {
            if(value==null||attached&&messaging==value)return;
            Detach();
            messaging=value;
            messaging.RegisterNamedMessageHandler(StateMessage,OnState);
            messaging.RegisterNamedMessageHandler(ReadyMessage,OnArenaReady);
            attached=true;
        }

        private void Detach()
        {
            if(!attached||messaging==null){messaging=null;attached=false;return;}
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(ReadyMessage);
            messaging=null;attached=false;
        }
    }
}
