using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Captures authoritative end-of-match statistics on the server and sends the same immutable
    /// result snapshot to every client. Each device can then grant its own local progression
    /// exactly once using a deterministic receipt id derived from match + player id.
    /// </summary>
    public sealed class CoopMatchResultSync : MonoBehaviour
    {
        public readonly struct SlotResult
        {
            public readonly int Slot,Kills,Deaths,Assists,Objectives;
            public readonly float DamageDealt,DamageTaken,Healing,FrontContribution;
            public SlotResult(int slot,int kills,int deaths,int assists,int objectives,
                float damageDealt,float damageTaken,float healing,float frontContribution)
            {
                Slot=slot;Kills=kills;Deaths=deaths;Assists=assists;Objectives=objectives;
                DamageDealt=damageDealt;DamageTaken=damageTaken;Healing=healing;FrontContribution=frontContribution;
            }
        }

        private const string ResultMessage="DF_COOP_MATCH_RESULT_V1";
        private const int ProtocolVersion=1;

        public static CoopMatchResultSync Instance { get; private set; }

        private readonly Dictionary<int,SlotResult> results=new();
        private DimensionsFront front;
        private CustomMessagingManager messaging;
        private bool attached,resultReady;
        private int winningTeam=-1;
        private string matchId="";

        public bool ResultReady=>resultReady;
        public int WinningTeam=>winningTeam;
        public string MatchId=>matchId;

        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;
        }

        private void Start()
        {
            front=FindAnyObjectByType<DimensionsFront>();
            if(front!=null)front.MatchEnded+=OnMatchEnded;
        }

        private void Update()
        {
            if(!CoopAuthority.Active)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return;
            Attach(manager.CustomMessagingManager);
            if(front==null)
            {
                front=FindAnyObjectByType<DimensionsFront>();
                if(front!=null)front.MatchEnded+=OnMatchEnded;
            }
        }

        public static bool TryGetLocalResult(out SlotResult result,out int winner,out string receiptId)
        {
            result=default;winner=-1;receiptId="";
            CoopMatchResultSync sync=Instance;
            CoopMatchPlan plan=CoopAuthority.ActivePlan;
            if(sync==null||!sync.resultReady||plan?.Session==null)return false;

            int slot=CoopRosterBuilder.FindLocalHumanSlot(plan);
            if(slot<0||!sync.results.TryGetValue(slot,out result))return false;
            MatchPlayerSlot player=plan.Session.Slots[slot];
            if(player==null||string.IsNullOrEmpty(player.PlayerId))return false;

            winner=sync.winningTeam;
            receiptId=$"coop:{sync.matchId}:{player.PlayerId}";
            return true;
        }

        public static bool TryGetSlotResult(int slot,out SlotResult result)
        {
            result=default;
            return Instance!=null&&Instance.resultReady&&Instance.results.TryGetValue(slot,out result);
        }

        private void OnMatchEnded(int winner)
        {
            if(!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||resultReady)return;
            CaptureHostResult(winner);
            if (CoopAuthority.ActivePlan?.Session?.Mode == SessionMode.OnlineRanked)
                _ = RankedMatchReporter.Instance?.SettleAsync(winner);
            BroadcastResult();
            CoopLobbyService lobby=CoopLobbyService.Instance;
            if(lobby!=null)_=lobby.EnterPostMatchAsync();
        }

        private void CaptureHostResult(int winner)
        {
            results.Clear();
            winningTeam=Mathf.Clamp(winner,0,1);
            matchId=CoopAuthority.ActivePlan?.MatchId??System.Guid.NewGuid().ToString("N");

            CoopHeroSlot[] heroes=FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
            for(int i=0;i<heroes.Length;i++)
            {
                CoopHeroSlot slot=heroes[i];
                if(slot==null||slot.Slot<0||slot.Slot>=MatchSession.MaxPlayers)continue;
                Health health=slot.GetComponent<Health>();
                MatchTelemetry.HeroStats stats=health!=null?MatchTelemetry.Instance?.GetStats(health):null;
                results[slot.Slot]=new SlotResult(
                    slot.Slot,
                    stats?.Kills??0,
                    stats?.Deaths??0,
                    stats?.Assists??0,
                    stats?.Objectives??0,
                    stats?.DamageDealt??0,
                    stats?.DamageTaken??0,
                    stats?.Healing??0,
                    stats?.FrontContribution??0);
            }
            resultReady=true;
        }

        private void BroadcastResult()
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsServer||messaging==null||manager.ConnectedClientsIds==null)return;

            using FastBufferWriter writer=new(2048,Allocator.Temp);
            writer.WriteValueSafe(ProtocolVersion);
            writer.WriteValueSafe(winningTeam);
            writer.WriteValueSafe(new FixedString128Bytes(matchId??""));
            writer.WriteValueSafe(results.Count);

            foreach(KeyValuePair<int,SlotResult> pair in results)
            {
                SlotResult s=pair.Value;
                writer.WriteValueSafe(s.Slot);
                writer.WriteValueSafe(s.Kills);writer.WriteValueSafe(s.Deaths);writer.WriteValueSafe(s.Assists);writer.WriteValueSafe(s.Objectives);
                writer.WriteValueSafe(s.DamageDealt);writer.WriteValueSafe(s.DamageTaken);writer.WriteValueSafe(s.Healing);writer.WriteValueSafe(s.FrontContribution);
            }

            for(int i=0;i<manager.ConnectedClientsIds.Count;i++)
            {
                ulong clientId=manager.ConnectedClientsIds[i];
                if(clientId==NetworkManager.ServerClientId)continue;
                messaging.SendNamedMessage(ResultMessage,clientId,writer,NetworkDelivery.ReliableSequenced);
            }
        }

        private void OnResult(ulong senderClientId,FastBufferReader reader)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsClient||senderClientId!=NetworkManager.ServerClientId)return;

            reader.ReadValueSafe(out int protocol);
            if(protocol!=ProtocolVersion)return;
            reader.ReadValueSafe(out int winner);
            reader.ReadValueSafe(out FixedString128Bytes fixedMatchId);
            if (fixedMatchId.ToString() != CoopAuthority.ActivePlan?.MatchId) return;
            reader.ReadValueSafe(out int count);
            count=Mathf.Clamp(count,0,MatchSession.MaxPlayers);

            results.Clear();
            for(int i=0;i<count;i++)
            {
                reader.ReadValueSafe(out int slot);
                reader.ReadValueSafe(out int kills);reader.ReadValueSafe(out int deaths);reader.ReadValueSafe(out int assists);reader.ReadValueSafe(out int objectives);
                reader.ReadValueSafe(out float dealt);reader.ReadValueSafe(out float taken);reader.ReadValueSafe(out float healing);reader.ReadValueSafe(out float frontContribution);
                if(slot<0||slot>=MatchSession.MaxPlayers)continue;
                results[slot]=new SlotResult(slot,kills,deaths,assists,objectives,dealt,taken,healing,frontContribution);
            }

            winningTeam=Mathf.Clamp(winner,0,1);
            matchId=fixedMatchId.ToString();
            resultReady=!string.IsNullOrEmpty(matchId);
            if (resultReady && CoopAuthority.ActivePlan?.Session?.Mode == SessionMode.OnlineRanked)
                _ = RankedService.Instance?.RefreshResultAsync(matchId);
        }

        private void Attach(CustomMessagingManager value)
        {
            if(value==null||attached&&messaging==value)return;
            Detach();
            messaging=value;
            messaging.RegisterNamedMessageHandler(ResultMessage,OnResult);
            attached=true;
        }

        private void Detach()
        {
            if(!attached||messaging==null){messaging=null;attached=false;return;}
            messaging.UnregisterNamedMessageHandler(ResultMessage);
            messaging=null;attached=false;
        }

        private void OnDestroy()
        {
            if(front!=null)front.MatchEnded-=OnMatchEnded;
            Detach();
            if(Instance==this)Instance=null;
        }
    }
}
