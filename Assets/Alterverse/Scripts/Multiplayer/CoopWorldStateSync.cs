using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Server snapshots for non-hero Health owners: cores, base guards, spawned planets and
    /// Hot Zone rewards. Missing server entities are sent as tombstones and removed on clients.
    /// </summary>
    public sealed class CoopWorldStateSync : MonoBehaviour
    {
        private const string StateMessage="DF_COOP_WORLD_STATE_V2";
        private const float SendInterval=.125f;
        private const float MovingSendInterval=1f/20f;

        private readonly Dictionary<string,CoopWorldEntityKey> entities=new();
        private readonly HashSet<string> removedKeys=new();
        private CoopMatchPlan replicaPlan;
        private readonly HashSet<string> previousHostKeys=new();
        private readonly HashSet<string> currentHostKeys=new();
        private readonly List<string> tombstones=new();
        private readonly List<CoopWorldEntityKey> live=new(128);
        private readonly WorldSnapshotHistory snapshotHistory=new();
        private CoopMatchPlan snapshotPlan;
        private CustomMessagingManager messaging;
        private bool attached;
        private float nextSend,nextMovingSend,nextRefresh;
        private int lastRefreshFrame=-1,lastHealthCount=-1;

        private void OnDisable(){Detach();}

        private void Update()
        {
            if(!CoopAuthority.Active)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return;
            Attach(manager.CustomMessagingManager);

            // The host refreshes immediately before each snapshot; a second timed scan
            // in the same frame would only repeat the same work.
            if(!CoopAuthority.LocalHasServerAuthority&&Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+.35f;
                RefreshEntities();
            }

            if(!CoopAuthority.LocalHasServerAuthority)return;
            float now=Time.unscaledTime;
            // Full-world discovery shares the movement clock. It must not reset that clock,
            // otherwise 8 Hz discovery repeatedly changes the spacing of 20 Hz creature poses.
            if(!SnapshotCadence.IsDue(now,MovingSendInterval,ref nextMovingSend))return;
            bool full=SnapshotCadence.IsDue(now,SendInterval,ref nextSend);
            SendState(manager,!full);
        }

        private void RefreshEntities()
        {
            IReadOnlyList<Health> active=Health.Active;
            // A world snapshot arrives in several packets. Rebuilding the complete entity
            // lookup for every chunk repeats the same component scan in one rendered frame.
            // Still discover entities spawned by another event in that frame immediately.
            if(lastRefreshFrame==Time.frameCount&&lastHealthCount==active.Count)return;
            lastRefreshFrame=Time.frameCount;lastHealthCount=active.Count;
            entities.Clear();
            for(int i=0;i<active.Count;i++)
            {
                Health health=active[i];
                if(health==null||health.GetComponent<HeroIdentity>()!=null)continue;
                CoopWorldEntityKey key=health.GetComponent<CoopWorldEntityKey>()??health.gameObject.AddComponent<CoopWorldEntityKey>();
                key.Configure();
                if(!string.IsNullOrEmpty(key.Key)&&!entities.ContainsKey(key.Key))entities.Add(key.Key,key);
            }
        }

        private void SendState(NetworkManager manager,bool movingOnly=false)
        {
            if(manager==null||!manager.IsServer||messaging==null||manager.ConnectedClientsIds==null)return;
            if(!object.ReferenceEquals(snapshotPlan,CoopAuthority.ActivePlan))
            {
                snapshotHistory.Clear();snapshotPlan=CoopAuthority.ActivePlan;
            }
            if(!movingOnly)RefreshEntities();
            // Partial movement packets must not change the complete-world lifecycle set.
            tombstones.Clear();
            if(!movingOnly)
            {
                currentHostKeys.Clear();
                foreach(string key in entities.Keys)currentHostKeys.Add(key);
                foreach(string old in previousHostKeys)
                    if(!currentHostKeys.Contains(old)){tombstones.Add(old);snapshotHistory.Remove(old);}
            }

            // Keep each unfragmented state message below the transport MTU, even with
            // maximum-length keys. Each chunk is an independent partial snapshot.
            const int chunkSize=6;
            live.Clear();
            if(movingOnly)
            {
                // Use moving-unit registries; do not rescan the entire world at 20 Hz.
                var creatures=HotZoneTank.Active;
                for(int i=0;i<creatures.Count;i++)
                {
                    HotZoneTank creature=creatures[i];
                    if(creature==null||creature.TankHealth==null)continue;
                    CoopWorldEntityKey entity=creature.GetComponent<CoopWorldEntityKey>();
                    if(entity!=null&&!string.IsNullOrEmpty(entity.Key))live.Add(entity);
                }
                var minions=LaneMinion.Active;
                for(int i=0;i<minions.Count;i++)
                {
                    LaneMinion minion=minions[i];
                    if(minion==null)continue;
                    CoopWorldEntityKey entity=minion.GetComponent<CoopWorldEntityKey>();
                    // A newly spawned minion joins after the next full snapshot assigns its key.
                    if(entity!=null&&!string.IsNullOrEmpty(entity.Key))live.Add(entity);
                }
            }
            else
            {
                foreach(CoopWorldEntityKey entity in entities.Values)
                {
                    if(entity==null)continue;
                    Health health=entity.GetComponent<Health>();
                    if(health==null)continue;
                    bool moving=entity.GetComponent<HotZoneTank>()!=null||entity.GetComponent<LaneMinion>()!=null;
                    if(snapshotHistory.ShouldSend(entity.Key,entity.transform.position,entity.transform.eulerAngles.y,
                        health.Current,health.Shield,health.IsDead,moving,Time.unscaledTime))live.Add(entity);
                }
            }
            uint round=CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan);
            for(int offset=0;offset<live.Count;offset+=chunkSize)
            {
                int count=Mathf.Min(chunkSize,live.Count-offset);
                using FastBufferWriter writer=new(1024,Allocator.Temp);
                writer.WriteValueSafe(round);
                writer.WriteValueSafe(count);
                for(int i=0;i<count;i++)
                {
                    CoopWorldEntityKey entity=live[offset+i];
                    Health health=entity.GetComponent<Health>();
                    Vector3 p=entity.transform.position;
                    writer.WriteValueSafe(new FixedString128Bytes(entity.Key));
                    writer.WriteValueSafe(p.x);writer.WriteValueSafe(p.y);writer.WriteValueSafe(p.z);
                    writer.WriteValueSafe(entity.transform.eulerAngles.y);
                    writer.WriteValueSafe(health.Current);writer.WriteValueSafe(health.Shield);
                    writer.WriteValueSafe(health.IsDead);
                }
                writer.WriteValueSafe(0);
                SendChunk(manager,writer,NetworkDelivery.UnreliableSequenced);
            }
            // A removal is not repeated in later snapshots, so it must be reliable.
            for(int offset=0;offset<tombstones.Count;offset+=chunkSize)
            {
                int count=Mathf.Min(chunkSize,tombstones.Count-offset);
                using FastBufferWriter writer=new(1024,Allocator.Temp);
                writer.WriteValueSafe(round);
                writer.WriteValueSafe(0);writer.WriteValueSafe(count);
                for(int i=0;i<count;i++)writer.WriteValueSafe(new FixedString128Bytes(tombstones[offset+i]));
                SendChunk(manager,writer,NetworkDelivery.ReliableSequenced);
            }
            if(!movingOnly)
            {
                previousHostKeys.Clear();
                foreach(string key in currentHostKeys)previousHostKeys.Add(key);
            }
        }

        private void SendChunk(NetworkManager manager,FastBufferWriter writer,NetworkDelivery delivery)
        {
            for(int i=0;i<manager.ConnectedClientsIds.Count;i++)
            {
                ulong id=manager.ConnectedClientsIds[i];
                if(id!=NetworkManager.ServerClientId)messaging.SendNamedMessage(StateMessage,id,writer,delivery);
            }
        }

        private void OnState(ulong sender,FastBufferReader reader)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsClient||sender!=NetworkManager.ServerClientId)return;
            if (!MatchNetworkProtocol.ReadRound(ref reader)) return;
            reader.ReadValueSafe(out int count);
            if (count < 0 || count > 96) return;
            if(!object.ReferenceEquals(replicaPlan,CoopAuthority.ActivePlan))
            {
                replicaPlan=CoopAuthority.ActivePlan;removedKeys.Clear();
            }
            RefreshEntities();
            for(int i=0;i<count;i++)
            {
                reader.ReadValueSafe(out FixedString128Bytes fixedId);
                reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
                reader.ReadValueSafe(out float yaw);
                reader.ReadValueSafe(out float current);
                reader.ReadValueSafe(out float shield);
                reader.ReadValueSafe(out bool dead);

                string id=fixedId.ToString();
                if(removedKeys.Contains(id))continue;
                if(!entities.TryGetValue(id,out CoopWorldEntityKey entity)||entity==null)
                {
                    if(dead||!MatchNetworkProtocol.Finite(x)||!MatchNetworkProtocol.Finite(y)||
                        !MatchNetworkProtocol.Finite(z)||!MatchNetworkProtocol.Finite(yaw)||
                        !MatchNetworkProtocol.Finite(current)||!MatchNetworkProtocol.Finite(shield))continue;
                    HotZoneTank reward=HotZoneTank.EnsureNetworkReplica(id);
                    entity=reward!=null?reward.GetComponent<CoopWorldEntityKey>():null;
                    if(entity==null)continue;
                    entities[id]=entity;
                }
                Health health=entity.GetComponent<Health>();if(health==null)continue;
                entity.ApplyTarget(new Vector3(x,y,z),yaw,false);
                health.ApplyNetworkSnapshot(current,shield,dead);
                DimensionPlanet planet=entity.GetComponent<DimensionPlanet>();
                if(planet!=null)planet.ApplyNetworkAvailability(!dead);
                entity.ApplyAlivePresentation(!dead);
            }

            reader.ReadValueSafe(out int tombCount);
            if (tombCount < 0 || tombCount > 64) return;
            for(int i=0;i<tombCount;i++)
            {
                reader.ReadValueSafe(out FixedString128Bytes fixedId);
                string id=fixedId.ToString();
                removedKeys.Add(id);
                if(entities.TryGetValue(id,out CoopWorldEntityKey entity)&&entity!=null)
                {
                    entities.Remove(id);
                    Destroy(entity.gameObject);
                }
            }
        }

        private void Attach(CustomMessagingManager value)
        {
            if(value==null||attached&&messaging==value)return;
            Detach();
            messaging=value;
            messaging.RegisterNamedMessageHandler(StateMessage,OnState);
            attached=true;
        }

        private void Detach()
        {
            snapshotHistory.Clear();
            if(!attached||messaging==null){messaging=null;attached=false;return;}
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging=null;attached=false;
        }
    }
}


