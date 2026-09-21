using System.Collections.Generic;
using Alterverse.Combat;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Lightweight custom-message synchronization for the ten deterministic hero slots.
    /// Human owners send only their movement pose to the host. The host validates it, runs
    /// all authoritative gameplay, and broadcasts hero pose/health snapshots to clients.
    /// </summary>
    public sealed class CoopHeroStateSync : MonoBehaviour
    {
        private const string OwnerPoseMessage="DF_COOP_OWNER_POSE_V1";
        private const string HeroSnapshotMessage="DF_COOP_HERO_SNAPSHOT_V1";
        private const float OwnerSendInterval=1f/20f;
        private const float HostSnapshotInterval=1f/12f;
        private const float MaxOwnerStep=6.5f;

        private CustomMessagingManager messaging;
        private readonly CoopHeroSlot[] slots=new CoopHeroSlot[MatchSession.MaxPlayers];
        private readonly Dictionary<ulong,string> senderOwners=new();
        private float nextOwnerSend,nextHostSnapshot,nextRefresh;
        private int localSlot=-1;
        private string localPlayerId="";
        private bool attached;

        private void OnEnable(){RefreshSlots(true);}
        private void OnDisable(){Detach();}

        private void Update()
        {
            if(!CoopAuthority.Active)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return;

            Attach(manager.CustomMessagingManager);

            if(Time.unscaledTime>=nextRefresh)RefreshSlots(false);

            if(CoopAuthority.LocalIsHost)
            {
                if(Time.unscaledTime>=nextHostSnapshot)
                {
                    nextHostSnapshot=Time.unscaledTime+HostSnapshotInterval;
                    BroadcastHostSnapshot(manager);
                }
                return;
            }

            if(localSlot>=0&&Time.unscaledTime>=nextOwnerSend)
            {
                nextOwnerSend=Time.unscaledTime+OwnerSendInterval;
                SendOwnerPose(manager);
            }
        }

        private void RefreshSlots(bool immediate)
        {
            if(!CoopAuthority.Active)return;
            nextRefresh=Time.unscaledTime+(immediate?.05f:.5f);
            for(int i=0;i<slots.Length;i++)slots[i]=null;

            CoopHeroSlot[] found=FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
            localSlot=-1;localPlayerId="";
            for(int i=0;i<found.Length;i++)
            {
                CoopHeroSlot slot=found[i];
                if(slot==null||slot.Slot<0||slot.Slot>=slots.Length)continue;
                slots[slot.Slot]=slot;
                if(slot.IsLocalHuman)
                {
                    localSlot=slot.Slot;
                    localPlayerId=slot.PlayerId??"";
                }

                if(slot.GetComponent<CoopHeroReplicaPresentation>()==null)
                {
                    CoopHeroReplicaPresentation presentation=slot.gameObject.AddComponent<CoopHeroReplicaPresentation>();
                    presentation.Refresh();
                }
            }
        }

        private void Attach(CustomMessagingManager value)
        {
            if(value==null||messaging==value&&attached)return;
            Detach();
            messaging=value;
            messaging.RegisterNamedMessageHandler(OwnerPoseMessage,OnOwnerPose);
            messaging.RegisterNamedMessageHandler(HeroSnapshotMessage,OnHeroSnapshot);
            attached=true;
        }

        private void Detach()
        {
            if(!attached||messaging==null){messaging=null;attached=false;return;}
            messaging.UnregisterNamedMessageHandler(OwnerPoseMessage);
            messaging.UnregisterNamedMessageHandler(HeroSnapshotMessage);
            messaging=null;attached=false;
            senderOwners.Clear();
        }

        private void SendOwnerPose(NetworkManager manager)
        {
            if(manager==null||manager.IsHost||messaging==null||localSlot<0)return;
            CoopHeroSlot slot=slots[localSlot];
            Health health=slot!=null?slot.GetComponent<Health>():null;
            if(slot==null||health==null||health.IsDead)return;

            Vector3 p=slot.transform.position;
            float yaw=slot.transform.eulerAngles.y;
            FixedString128Bytes id=new(localPlayerId??"");

            using FastBufferWriter writer=new(192,Allocator.Temp);
            writer.WriteValueSafe(localSlot);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(p.x);writer.WriteValueSafe(p.y);writer.WriteValueSafe(p.z);
            writer.WriteValueSafe(yaw);
            messaging.SendNamedMessage(OwnerPoseMessage,NetworkManager.ServerClientId,writer,NetworkDelivery.UnreliableSequenced);
        }

        private void OnOwnerPose(ulong senderClientId,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||!CoopAuthority.LocalIsHost)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsHost)return;

            reader.ReadValueSafe(out int rosterSlot);
            reader.ReadValueSafe(out FixedString128Bytes fixedPlayerId);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float yaw);

            string playerId=fixedPlayerId.ToString();
            if(!ValidateOwner(senderClientId,rosterSlot,playerId))return;

            CoopHeroSlot actor=rosterSlot>=0&&rosterSlot<slots.Length?slots[rosterSlot]:null;
            Health health=actor!=null?actor.GetComponent<Health>():null;
            if(actor==null||health==null||health.IsDead)return;

            Vector3 desired=GameplayCollision.ClampToRoundedArena(new Vector3(x,y,z),40f,33f);
            desired.y=actor.transform.position.y;
            Vector3 delta=desired-actor.transform.position;delta.y=0;
            if(delta.magnitude>MaxOwnerStep)desired=actor.transform.position+delta.normalized*MaxOwnerStep;
            actor.transform.SetPositionAndRotation(desired,Quaternion.Euler(0,Mathf.Repeat(yaw,360f),0));
        }

        private bool ValidateOwner(ulong senderClientId,int rosterSlot,string playerId)
        {
            CoopMatchPlan plan=CoopAuthority.ActivePlan;
            if(plan?.Session?.Slots==null||rosterSlot<0||rosterSlot>=MatchSession.TeamSize)return false;
            MatchPlayerSlot slot=plan.Session.Slots[rosterSlot];
            if(slot==null||slot.IsBot||slot.IsHost||slot.PlayerId!=playerId)return false;

            if(senderOwners.TryGetValue(senderClientId,out string known))
                return known==playerId;

            foreach(KeyValuePair<ulong,string> pair in senderOwners)
                if(pair.Value==playerId&&pair.Key!=senderClientId)return false;

            senderOwners[senderClientId]=playerId;
            return true;
        }

        private void BroadcastHostSnapshot(NetworkManager manager)
        {
            if(manager==null||!manager.IsHost||messaging==null||manager.ConnectedClientsIds==null)return;

            int count=0;
            for(int i=0;i<slots.Length;i++)if(slots[i]!=null)count++;
            if(count==0)return;

            using FastBufferWriter writer=new(1024,Allocator.Temp);
            writer.WriteValueSafe(count);
            for(int i=0;i<slots.Length;i++)
            {
                CoopHeroSlot slot=slots[i];if(slot==null)continue;
                Health health=slot.GetComponent<Health>();if(health==null)continue;
                Vector3 p=slot.transform.position;
                writer.WriteValueSafe(i);
                writer.WriteValueSafe(p.x);writer.WriteValueSafe(p.y);writer.WriteValueSafe(p.z);
                writer.WriteValueSafe(slot.transform.eulerAngles.y);
                writer.WriteValueSafe(health.Current);
                writer.WriteValueSafe(health.Shield);
                writer.WriteValueSafe(health.IsDead);
            }

            for(int i=0;i<manager.ConnectedClientsIds.Count;i++)
            {
                ulong clientId=manager.ConnectedClientsIds[i];
                if(clientId==NetworkManager.ServerClientId)continue;
                messaging.SendNamedMessage(HeroSnapshotMessage,clientId,writer,NetworkDelivery.UnreliableSequenced);
            }
        }

        private void OnHeroSnapshot(ulong senderClientId,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalIsHost||senderClientId!=NetworkManager.ServerClientId)return;

            reader.ReadValueSafe(out int count);
            count=Mathf.Clamp(count,0,MatchSession.MaxPlayers);
            for(int n=0;n<count;n++)
            {
                reader.ReadValueSafe(out int rosterSlot);
                reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
                reader.ReadValueSafe(out float yaw);
                reader.ReadValueSafe(out float current);
                reader.ReadValueSafe(out float shield);
                reader.ReadValueSafe(out bool dead);

                if(rosterSlot<0||rosterSlot>=slots.Length)continue;
                CoopHeroSlot actor=slots[rosterSlot];if(actor==null)continue;
                Health health=actor.GetComponent<Health>();if(health==null)continue;

                Vector3 hostPosition=new(x,y,z);
                bool localOwner=actor.IsLocalHuman;
                if(!localOwner||dead||(actor.transform.position-hostPosition).sqrMagnitude>36f)
                    actor.transform.SetPositionAndRotation(hostPosition,Quaternion.Euler(0,yaw,0));
                else if(!localOwner)
                    actor.transform.rotation=Quaternion.Euler(0,yaw,0);

                health.ApplyNetworkSnapshot(current,shield,dead);
                actor.GetComponent<CoopHeroReplicaPresentation>()?.Apply(dead);
            }
        }
    }
}
