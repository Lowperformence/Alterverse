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
            public readonly int QuestKills,HotZoneCaptures,SoldierKills;
            public readonly bool HasQuestStats;
            public readonly float DamageDealt,DamageTaken,Healing,FrontContribution;
            public SlotResult(int slot,int kills,int deaths,int assists,int objectives,
                float damageDealt,float damageTaken,float healing,float frontContribution,
                int questKills=-1,int hotZoneCaptures=0,int soldierKills=0)
            {
                Slot=slot;Kills=kills;Deaths=deaths;Assists=assists;Objectives=objectives;
                DamageDealt=damageDealt;DamageTaken=damageTaken;Healing=healing;FrontContribution=frontContribution;
                // The optional suffix is absent when connected to an older dedicated server.
                HasQuestStats=questKills>=0;
                QuestKills=Mathf.Max(0,questKills);
                HotZoneCaptures=Mathf.Max(0,hotZoneCaptures);
                SoldierKills=Mathf.Max(0,soldierKills);
            }
        }

        private const string ResultMessage="DF_COOP_MATCH_RESULT_V1";
        private const int ProtocolVersion=1;
        // Append quest counters after the unchanged V1 result payload: old clients ignore
        // the tail, and new clients can still accept results from an older server.
        private const int QuestStatsExtensionVersion=0x44515131;

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
                // Telemetry.Kills includes soldiers/objectives for quests. K/D/A must use
                // the same hero-only authority as the in-match scoreboard.
                int kills=0,deaths=0,assists=0;
                MatchScoreboard.Instance?.GetNetworkScore(health,out kills,out deaths,out assists);
                results[slot.Slot]=new SlotResult(
                    slot.Slot,
                    kills,
                    deaths,
                    assists,
                    stats?.Objectives??0,
                    stats?.DamageDealt??0,
                    stats?.DamageTaken??0,
                    stats?.Healing??0,
                    stats?.FrontContribution??0,
                    stats?.Kills??0,
                    stats?.HotZoneCaptures??0,
                    stats?.SoldierKills??0);
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

            // Quest statistics must come from the host's authoritative telemetry.
            // Sending them as a suffix preserves compatibility with V1 clients.
            writer.WriteValueSafe(QuestStatsExtensionVersion);
            writer.WriteValueSafe(results.Count);
            foreach(KeyValuePair<int,SlotResult> pair in results)
            {
                SlotResult s=pair.Value;
                writer.WriteValueSafe(s.Slot);
                writer.WriteValueSafe(s.QuestKills);
                writer.WriteValueSafe(s.HotZoneCaptures);
                writer.WriteValueSafe(s.SoldierKills);
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

            // The V1 payload ends above. Ignore a missing or incomplete extension
            // rather than rejecting match rewards when the server is still on V1.
            if(reader.TryBeginRead(sizeof(int)*2))
            {
                reader.ReadValueSafe(out int extensionVersion);
                reader.ReadValueSafe(out int questCount);
                if(extensionVersion==QuestStatsExtensionVersion &&
                    questCount>=0 && questCount<=MatchSession.MaxPlayers &&
                    reader.TryBeginRead(questCount*sizeof(int)*4))
                {
                    for(int i=0;i<questCount;i++)
                    {
                        reader.ReadValueSafe(out int slot);
                        reader.ReadValueSafe(out int questKills);
                        reader.ReadValueSafe(out int hotZoneCaptures);
                        reader.ReadValueSafe(out int soldierKills);
                        if(!results.TryGetValue(slot,out SlotResult old))continue;
                        results[slot]=new SlotResult(old.Slot,old.Kills,old.Deaths,old.Assists,old.Objectives,
                            old.DamageDealt,old.DamageTaken,old.Healing,old.FrontContribution,
                            questKills,hotZoneCaptures,soldierKills);
                    }
                }
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
