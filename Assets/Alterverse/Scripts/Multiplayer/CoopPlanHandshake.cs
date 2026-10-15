using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Verifies that the host and every connected co-op client produced the same staged
    /// 8-slot match plan before gameplay is allowed to launch. Uses NGO named messages
    /// over the Relay link and scales from one to four human players; bots fill open team slots.
    /// </summary>
    public sealed class CoopPlanHandshake : MonoBehaviour
    {
        private const string ProbeMessage="DF_COOP_PLAN_PROBE_V1";
        private const string AckMessage="DF_COOP_PLAN_ACK_V1";
        private const int ProtocolVersion=2;

        public static CoopPlanHandshake Instance { get; private set; }
        public static bool PlanHandshakeReady=>Instance!=null&&Instance.planHandshakeReady;
        public static string HandshakeStatus=>Instance==null?"PLAN SYNC OFFLINE":Instance.status;

        private CustomMessagingManager registeredMessaging;
        private bool planHandshakeReady;
        private string status="WAITING FOR STAGED PLAN";
        private float nextProbeAt;
        private uint lastFingerprint;
        private int lastExpectedPlayers;
        private readonly HashSet<ulong> approvedClients=new();
        private readonly Dictionary<ulong,string> approvedPlayerIds=new();

        public static bool IsAuthorizedClient(ulong clientId,string playerId)
        {
            return Instance!=null&&!string.IsNullOrEmpty(playerId)&&
                Instance.approvedPlayerIds.TryGetValue(clientId,out string approved)&&approved==playerId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if(Instance==null)new GameObject("DF_CoopPlanHandshake").AddComponent<CoopPlanHandshake>();
        }

        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            NetworkManager manager=NetworkManager.Singleton;
            CoopLobbyService lobby=CoopLobbyService.Instance;

            if(manager==null||!manager.IsListening||lobby==null||!lobby.InLobby)
            {
                Detach();
                ResetHandshake("WAITING FOR RELAY");
                return;
            }

            Attach(manager.CustomMessagingManager);

            if(!lobby.IsStaging)
            {
                // Keep the verified Relay-client -> session-player binding through launch and the
                // active match. It is cleared only after returning to a normal lobby/disconnect.
                if(lobby.IsLaunching||CoopAuthority.Active)return;
                ResetHandshake("WAITING FOR MATCH STAGING");
                return;
            }
            if(CoopAuthority.PreparedPlan==null||
               lobby.PlayerCount<CoopLobbyService.MinHumanPlayers||lobby.PlayerCount>CoopLobbyService.MaxHumanPlayers)
            {
                ResetHandshake("WAITING FOR MATCH PLAN");
                return;
            }

            int expectedPlayers=lobby.PlayerCount;
            uint fingerprint=Fingerprint(CoopAuthority.PreparedPlan);
            if(fingerprint!=lastFingerprint||expectedPlayers!=lastExpectedPlayers)
            {
                lastFingerprint=fingerprint;
                lastExpectedPlayers=expectedPlayers;
                planHandshakeReady=false;
                approvedClients.Clear();
                approvedPlayerIds.Clear();
                nextProbeAt=0f;
            }

            if(manager.IsHost)
            {
                approvedClients.RemoveWhere(id=>!IsConnected(manager,id));
                var stale=new List<ulong>();
                foreach(var pair in approvedPlayerIds)if(!IsConnected(manager,pair.Key))stale.Add(pair.Key);
                for(int i=0;i<stale.Count;i++)approvedPlayerIds.Remove(stale[i]);
                int expectedClients=Mathf.Max(0,expectedPlayers-1);
                planHandshakeReady=manager.ConnectedClientsIds!=null&&
                    manager.ConnectedClientsIds.Count>=expectedPlayers&&
                    approvedClients.Count>=expectedClients;
                status=planHandshakeReady
                    ?expectedPlayers==1?"MATCH PLAN READY • BOTS FILL OPEN SLOTS":$"MATCH PLAN SYNCED • {expectedPlayers}/{expectedPlayers} PLAYERS"
                    :$"HOST WAITING FOR PLAN SYNC • {Mathf.Min(approvedClients.Count+1,expectedPlayers)}/{expectedPlayers}";
                return;
            }

            if(planHandshakeReady)return;
            status="CLIENT VERIFYING MATCH PLAN";
            if(Time.unscaledTime<nextProbeAt)return;
            nextProbeAt=Time.unscaledTime+.75f;
            SendProbe(manager,fingerprint);
        }

        private void Attach(CustomMessagingManager messaging)
        {
            if(messaging==null||registeredMessaging==messaging)return;
            Detach();
            registeredMessaging=messaging;
            registeredMessaging.RegisterNamedMessageHandler(ProbeMessage,OnPlanProbe);
            registeredMessaging.RegisterNamedMessageHandler(AckMessage,OnPlanAck);
        }

        private void Detach()
        {
            if(registeredMessaging==null)return;
            registeredMessaging.UnregisterNamedMessageHandler(ProbeMessage);
            registeredMessaging.UnregisterNamedMessageHandler(AckMessage);
            registeredMessaging=null;
        }

        private void SendProbe(NetworkManager manager,uint fingerprint)
        {
            if(manager==null||manager.IsHost||registeredMessaging==null)return;
            CoopMatchPlan plan=CoopAuthority.PreparedPlan;
            int localSlot=CoopRosterBuilder.FindLocalHumanSlot(plan);
            if(localSlot<0||plan?.Session?.Slots==null)return;
            string playerId=plan.Session.Slots[localSlot]?.PlayerId??"";
            if(string.IsNullOrEmpty(playerId))return;

            using FastBufferWriter writer=new(160,Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(fingerprint);
            writer.WriteValueSafe(new FixedString128Bytes(playerId));
            registeredMessaging.SendNamedMessage(
                ProbeMessage,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        private void OnPlanProbe(ulong senderClientId,FastBufferReader reader)
        {
            NetworkManager manager=NetworkManager.Singleton;
            CoopLobbyService lobby=CoopLobbyService.Instance;
            if(manager==null||!manager.IsHost||lobby==null||!lobby.IsStaging||CoopAuthority.PreparedPlan==null)return;

            reader.ReadValueSafe(out int protocol);
            reader.ReadValueSafe(out uint clientFingerprint);
            reader.ReadValueSafe(out FixedString128Bytes fixedPlayerId);
            string playerId=fixedPlayerId.ToString();

            uint hostFingerprint=Fingerprint(CoopAuthority.PreparedPlan);
            bool playerValid=PlayerExistsAsGuest(CoopAuthority.PreparedPlan,playerId);
            bool alreadyClaimed=false;
            foreach(var pair in approvedPlayerIds)
                if(pair.Key!=senderClientId&&pair.Value==playerId){alreadyClaimed=true;break;}

            bool valid=protocol==ProtocolVersion&&clientFingerprint==hostFingerprint&&playerValid&&!alreadyClaimed&&
                lobby.PlayerCount>=CoopLobbyService.MinHumanPlayers&&
                lobby.PlayerCount<=CoopLobbyService.MaxHumanPlayers;
            if(valid)
            {
                approvedClients.Add(senderClientId);
                approvedPlayerIds[senderClientId]=playerId;
            }
            else
            {
                approvedClients.Remove(senderClientId);
                approvedPlayerIds.Remove(senderClientId);
            }

            int expectedPlayers=lobby.PlayerCount;
            int expectedClients=Mathf.Max(0,expectedPlayers-1);
            planHandshakeReady=valid&&manager.ConnectedClientsIds!=null&&
                manager.ConnectedClientsIds.Count>=expectedPlayers&&
                approvedClients.Count>=expectedClients;
            lastFingerprint=hostFingerprint;
            lastExpectedPlayers=expectedPlayers;
            status=planHandshakeReady
                ?$"MATCH PLAN SYNCED • {expectedPlayers}/{expectedPlayers} PLAYERS"
                :valid
                    ?$"PLAN VERIFIED • {Mathf.Min(approvedClients.Count+1,expectedPlayers)}/{expectedPlayers}"
                    :"MATCH PLAN MISMATCH";

            using FastBufferWriter writer=new(20,Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(valid);
            writer.WriteValueSafe(hostFingerprint);
            registeredMessaging?.SendNamedMessage(
                AckMessage,
                senderClientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        private void OnPlanAck(ulong senderClientId,FastBufferReader reader)
        {
            NetworkManager manager=NetworkManager.Singleton;
            CoopLobbyService lobby=CoopLobbyService.Instance;
            if(manager==null||manager.IsHost||lobby==null||!lobby.IsStaging||CoopAuthority.PreparedPlan==null)return;
            if(senderClientId!=NetworkManager.ServerClientId)return;

            reader.ReadValueSafe(out int protocol);
            reader.ReadValueSafe(out bool accepted);
            reader.ReadValueSafe(out uint hostFingerprint);

            uint localFingerprint=Fingerprint(CoopAuthority.PreparedPlan);
            planHandshakeReady=protocol==ProtocolVersion&&accepted&&hostFingerprint==localFingerprint;
            lastFingerprint=localFingerprint;
            status=planHandshakeReady?"MATCH PLAN SYNCED":"MATCH PLAN MISMATCH";
            if(!planHandshakeReady)nextProbeAt=Time.unscaledTime+.75f;
        }

        private static bool PlayerExistsAsGuest(CoopMatchPlan plan,string playerId)
        {
            if(plan?.Session?.Slots==null||string.IsNullOrEmpty(playerId))return false;
            for(int i=0;i<MatchSession.TeamSize;i++)
            {
                MatchPlayerSlot slot=plan.Session.Slots[i];
                if(slot!=null&&!slot.IsBot&&!slot.IsHost&&slot.PlayerId==playerId)return true;
            }
            return false;
        }

        private static bool IsConnected(NetworkManager manager,ulong clientId)
        {
            if(manager?.ConnectedClientsIds==null)return false;
            for(int i=0;i<manager.ConnectedClientsIds.Count;i++)
                if(manager.ConnectedClientsIds[i]==clientId)return true;
            return false;
        }

        private void ResetHandshake(string nextStatus)
        {
            planHandshakeReady=false;
            lastFingerprint=0;
            lastExpectedPlayers=0;
            approvedClients.Clear();
            approvedPlayerIds.Clear();
            nextProbeAt=0f;
            status=nextStatus;
        }

        public static uint Fingerprint(CoopMatchPlan plan)
        {
            if(plan==null||plan.Session==null)return 0;

            StringBuilder builder=new(256);
            builder.Append(plan.MatchId??"").Append('|')
                .Append((int)plan.Arena).Append('|')
                .Append((int)plan.Difficulty);
            if (plan.Session.Mode == SessionMode.OnlineRanked) builder.Append("|ranked");

            for(int i=0;i<plan.Session.Slots.Length;i++)
            {
                MatchPlayerSlot slot=plan.Session.Slots[i];
                builder.Append('|').Append(i).Append(':');
                if(slot==null){builder.Append("null");continue;}
                builder.Append(slot.PlayerId??"").Append(',')
                    .Append(slot.DisplayName??"").Append(',')
                    .Append(slot.Team).Append(',')
                    .Append((int)slot.Hero).Append(',')
                    .Append((int)slot.BattleItem).Append(',')
                    .Append(slot.IsBot?'1':'0').Append(',')
                    .Append(slot.IsHost?'1':'0');
            }

            // Stable FNV-1a over UTF-16 code units; deterministic across both peers.
            const uint offset=2166136261u;
            const uint prime=16777619u;
            uint hash=offset;
            string text=builder.ToString();
            for(int i=0;i<text.Length;i++)
            {
                char ch=text[i];
                hash^=(byte)(ch&0xFF);hash*=prime;
                hash^=(byte)(ch>>8);hash*=prime;
            }
            return hash==0?1u:hash;
        }

        private void OnDestroy()
        {
            Detach();
            if(Instance==this)Instance=null;
        }
    }
}
