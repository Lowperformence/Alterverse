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
    /// Host snapshots for non-hero Health owners: cores, base guards, spawned planets and
    /// Hot Zone rewards. Missing host entities are sent as tombstones and removed on clients.
    /// </summary>
    public sealed class CoopWorldStateSync : MonoBehaviour
    {
        private const string StateMessage="DF_COOP_WORLD_STATE_V1";
        private const float SendInterval=.125f;

        private readonly Dictionary<string,CoopWorldEntityKey> entities=new();
        private readonly HashSet<string> previousHostKeys=new();
        private readonly HashSet<string> currentHostKeys=new();
        private readonly List<string> tombstones=new();
        private CustomMessagingManager messaging;
        private bool attached;
        private float nextSend,nextRefresh;

        private void OnDisable(){Detach();}

        private void Update()
        {
            if(!CoopAuthority.Active)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return;
            Attach(manager.CustomMessagingManager);

            if(Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+.35f;
                RefreshEntities();
            }

            if(!CoopAuthority.LocalIsHost||Time.unscaledTime<nextSend)return;
            nextSend=Time.unscaledTime+SendInterval;
            SendState(manager);
        }

        private void RefreshEntities()
        {
            entities.Clear();
            IReadOnlyList<Health> active=Health.Active;
            for(int i=0;i<active.Count;i++)
            {
                Health health=active[i];
                if(health==null||health.GetComponent<HeroIdentity>()!=null)continue;
                CoopWorldEntityKey key=health.GetComponent<CoopWorldEntityKey>()??health.gameObject.AddComponent<CoopWorldEntityKey>();
                key.Configure();
                if(!string.IsNullOrEmpty(key.Key)&&!entities.ContainsKey(key.Key))entities.Add(key.Key,key);
            }
        }

        private void SendState(NetworkManager manager)
        {
            if(messaging==null||manager?.ConnectedClientsIds==null)return;
            RefreshEntities();
            currentHostKeys.Clear();
            foreach(string key in entities.Keys)currentHostKeys.Add(key);

            tombstones.Clear();
            foreach(string old in previousHostKeys)if(!currentHostKeys.Contains(old))tombstones.Add(old);

            int count=Mathf.Min(entities.Count,96);
            int tombCount=Mathf.Min(tombstones.Count,64);
            using FastBufferWriter writer=new(16384,Allocator.Temp);
            writer.WriteValueSafe(count);
            int written=0;
            foreach(KeyValuePair<string,CoopWorldEntityKey> pair in entities)
            {
                if(written++>=count)break;
                CoopWorldEntityKey entity=pair.Value;
                Health health=entity!=null?entity.GetComponent<Health>():null;
                if(entity==null||health==null)continue;
                FixedString128Bytes id=new(pair.Key);
                Vector3 p=entity.transform.position;
                writer.WriteValueSafe(id);
                writer.WriteValueSafe(p.x);writer.WriteValueSafe(p.y);writer.WriteValueSafe(p.z);
                writer.WriteValueSafe(entity.transform.eulerAngles.y);
                writer.WriteValueSafe(health.Current);
                writer.WriteValueSafe(health.Shield);
                writer.WriteValueSafe(health.IsDead);
            }
            writer.WriteValueSafe(tombCount);
            for(int i=0;i<tombCount;i++)writer.WriteValueSafe(new FixedString128Bytes(tombstones[i]));

            previousHostKeys.Clear();
            foreach(string key in currentHostKeys)previousHostKeys.Add(key);

            for(int i=0;i<manager.ConnectedClientsIds.Count;i++)
            {
                ulong id=manager.ConnectedClientsIds[i];
                if(id==NetworkManager.ServerClientId)continue;
                messaging.SendNamedMessage(StateMessage,id,writer,NetworkDelivery.UnreliableSequenced);
            }
        }

        private void OnState(ulong sender,FastBufferReader reader)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(!CoopAuthority.Active||CoopAuthority.LocalIsHost||manager==null||sender!=NetworkManager.ServerClientId)return;
            RefreshEntities();

            reader.ReadValueSafe(out int count);
            count=Mathf.Clamp(count,0,96);
            for(int i=0;i<count;i++)
            {
                reader.ReadValueSafe(out FixedString128Bytes fixedId);
                reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
                reader.ReadValueSafe(out float yaw);
                reader.ReadValueSafe(out float current);
                reader.ReadValueSafe(out float shield);
                reader.ReadValueSafe(out bool dead);

                string id=fixedId.ToString();
                if(!entities.TryGetValue(id,out CoopWorldEntityKey entity)||entity==null)continue;
                Health health=entity.GetComponent<Health>();if(health==null)continue;
                entity.ApplyTarget(new Vector3(x,y,z),yaw,false);
                health.ApplyNetworkSnapshot(current,shield,dead);
                DimensionPlanet planet=entity.GetComponent<DimensionPlanet>();
                if(planet!=null)planet.ApplyNetworkAvailability(!dead);
                entity.ApplyAlivePresentation(!dead);
            }

            reader.ReadValueSafe(out int tombCount);
            tombCount=Mathf.Clamp(tombCount,0,64);
            for(int i=0;i<tombCount;i++)
            {
                reader.ReadValueSafe(out FixedString128Bytes fixedId);
                string id=fixedId.ToString();
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
            if(!attached||messaging==null){messaging=null;attached=false;return;}
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging=null;attached=false;
        }
    }
}
