using System.Collections.Generic;
using Alterverse.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Match
{
    public sealed partial class HalloweenPumpkinEvent
    {
        const string SpawnMessage="DF_HALLOWEEN_SPAWN_V1",ClaimMessage="DF_HALLOWEEN_CLAIM_V1";
        const string RequestMessage="DF_HALLOWEEN_SYNC_REQUEST_V1",BeginMessage="DF_HALLOWEEN_SYNC_BEGIN_V1";
        const string ItemMessage="DF_HALLOWEEN_SYNC_ITEM_V1",EndMessage="DF_HALLOWEEN_SYNC_END_V1";
        CustomMessagingManager messaging;
        readonly Dictionary<ulong,float> requestTimes=new();
        readonly HashSet<int> snapshotSeen=new();
        bool attached,snapshotOpen;
        uint sentSnapshot,currentSnapshot;
        float nextRequest;

        void UpdateTransport()
        {
            if (!networkMatch) return;
            NetworkManager manager=NetworkManager.Singleton;
            if (!CoopAuthority.Active || manager==null || !manager.IsListening)
            { DetachTransport(); return; }
            if (!attached || messaging!=manager.CustomMessagingManager)
            {
                DetachTransport();
                messaging=manager.CustomMessagingManager;
                if (messaging==null) return;
                messaging.RegisterNamedMessageHandler(SpawnMessage,OnSpawn);
                messaging.RegisterNamedMessageHandler(ClaimMessage,OnClaim);
                messaging.RegisterNamedMessageHandler(RequestMessage,OnSnapshotRequest);
                messaging.RegisterNamedMessageHandler(BeginMessage,OnSnapshotBegin);
                messaging.RegisterNamedMessageHandler(ItemMessage,OnSnapshotItem);
                messaging.RegisterNamedMessageHandler(EndMessage,OnSnapshotEnd);
                attached=true;nextRequest=0;
            }
            if (Authority || !manager.IsConnectedClient || Time.realtimeSinceStartup<nextRequest) return;
            nextRequest=Time.realtimeSinceStartup+5f;
            using FastBufferWriter writer=new(16,Allocator.Temp);
            writer.WriteValueSafe(round);
            messaging.SendNamedMessage(RequestMessage,NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableSequenced);
        }

        void DetachTransport()
        {
            if (attached && messaging!=null)
            {
                messaging.UnregisterNamedMessageHandler(SpawnMessage);
                messaging.UnregisterNamedMessageHandler(ClaimMessage);
                messaging.UnregisterNamedMessageHandler(RequestMessage);
                messaging.UnregisterNamedMessageHandler(BeginMessage);
                messaging.UnregisterNamedMessageHandler(ItemMessage);
                messaging.UnregisterNamedMessageHandler(EndMessage);
            }
            attached=false;messaging=null;snapshotOpen=false;snapshotSeen.Clear();requestTimes.Clear();
        }

        bool ValidServer(ulong sender,ref FastBufferReader reader,int bytes)
        {
            NetworkManager manager=NetworkManager.Singleton;
            return networkMatch && !Authority && manager!=null && manager.IsListening && manager.IsClient &&
                sender==NetworkManager.ServerClientId && MatchNetworkProtocol.ReadRound(ref reader) && reader.TryBeginRead(bytes);
        }

        void SendAll(string name,FastBufferWriter writer)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if (!networkMatch || !Authority || messaging==null || manager?.ConnectedClientsIds==null) return;
            foreach (ulong client in manager.ConnectedClientsIds)
                if (client!=NetworkManager.ServerClientId)
                    messaging.SendNamedMessage(name,client,writer,NetworkDelivery.ReliableSequenced);
        }

        void BroadcastSpawn(int id,Vector3 ground)
        {
            if (!networkMatch || messaging==null) return;
            using FastBufferWriter writer=new(32,Allocator.Temp);
            writer.WriteValueSafe(round);writer.WriteValueSafe(id);
            writer.WriteValueSafe(ground.x);writer.WriteValueSafe(ground.y);writer.WriteValueSafe(ground.z);
            SendAll(SpawnMessage,writer);
        }
        void BroadcastClaim(int id,int slot,int total)
        {
            if (!networkMatch || messaging==null) return;
            using FastBufferWriter writer=new(32,Allocator.Temp);
            writer.WriteValueSafe(round);writer.WriteValueSafe(id);writer.WriteValueSafe(slot);writer.WriteValueSafe(total);
            SendAll(ClaimMessage,writer);
        }
        static bool ReadPickup(ref FastBufferReader reader,out int id,out Vector3 p)
        {
            reader.ReadValueSafe(out id);reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            p=new Vector3(x,y,z);
            return id>0 && MatchNetworkProtocol.Finite(x) && MatchNetworkProtocol.Finite(y) && MatchNetworkProtocol.Finite(z) &&
                Mathf.Abs(x)<=Alterverse.Combat.ArenaLayout.HalfLength && Mathf.Abs(z)<=Alterverse.Combat.ArenaLayout.HalfBreadth;
        }

        void OnSpawn(ulong sender,FastBufferReader reader)
        {
            if (!ValidServer(sender,ref reader,16) || !ReadPickup(ref reader,out int id,out Vector3 p)) return;
            ShowPickup(id,p);
            if (snapshotOpen) snapshotSeen.Add(id);
        }
        void OnClaim(ulong sender,FastBufferReader reader)
        {
            if (!ValidServer(sender,ref reader,12)) return;
            reader.ReadValueSafe(out int id);reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out int total);
            if (id<=0 || slot<0 || slot>=MatchSession.MaxPlayers || total<=0) return;
            RemovePickup(id);snapshotSeen.Remove(id);
            ConfirmReward(slot,total);
        }

        void OnSnapshotRequest(ulong sender,FastBufferReader reader)
        {
            if (!Authority || messaging==null || !MatchNetworkProtocol.ReadRound(ref reader)) return;
            MatchPlayerSlot[] roster=CoopAuthority.ActivePlan?.Session?.Slots;
            if (roster==null) return;
            int slot=-1;
            for (int i=0;i<roster.Length;i++)
                if (roster[i]!=null && !roster[i].IsBot && roster[i].Connected &&
                    MatchNetworkProtocol.Authorized(sender,roster[i].PlayerId)) { slot=i;break; }
            if (slot<0) return;
            if (requestTimes.TryGetValue(sender,out float previous) && Time.realtimeSinceStartup-previous<2f) return;
            requestTimes[sender]=Time.realtimeSinceStartup;
            uint sequence=++sentSnapshot;
            using (FastBufferWriter begin=new(32,Allocator.Temp))
            {
                begin.WriteValueSafe(round);begin.WriteValueSafe(sequence);begin.WriteValueSafe(slot);begin.WriteValueSafe(rules.CollectedBy(slot));
                messaging.SendNamedMessage(BeginMessage,sender,begin,NetworkDelivery.ReliableSequenced);
            }
            // Small reliable chunks avoid an ever-growing snapshot/fragmentation limit in long matches.
            foreach (KeyValuePair<int,Pickup> entry in pickups)
            {
                using FastBufferWriter item=new(32,Allocator.Temp);
                item.WriteValueSafe(round);item.WriteValueSafe(sequence);item.WriteValueSafe(entry.Key);
                item.WriteValueSafe(entry.Value.Ground.x);item.WriteValueSafe(entry.Value.Ground.y);item.WriteValueSafe(entry.Value.Ground.z);
                messaging.SendNamedMessage(ItemMessage,sender,item,NetworkDelivery.ReliableSequenced);
            }
            using FastBufferWriter end=new(16,Allocator.Temp);
            end.WriteValueSafe(round);end.WriteValueSafe(sequence);
            messaging.SendNamedMessage(EndMessage,sender,end,NetworkDelivery.ReliableSequenced);
        }
        void OnSnapshotBegin(ulong sender,FastBufferReader reader)
        {
            if (!ValidServer(sender,ref reader,12)) return;
            reader.ReadValueSafe(out uint sequence);reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out int total);
            if (slot!=localSlot || total<0) return;
            currentSnapshot=sequence;snapshotOpen=true;snapshotSeen.Clear();
            ConfirmReward(slot,total);
        }
        void OnSnapshotItem(ulong sender,FastBufferReader reader)
        {
            if (!ValidServer(sender,ref reader,20)) return;
            reader.ReadValueSafe(out uint sequence);
            if (!snapshotOpen || sequence!=currentSnapshot || !ReadPickup(ref reader,out int id,out Vector3 p)) return;
            ShowPickup(id,p);snapshotSeen.Add(id);
        }
        void OnSnapshotEnd(ulong sender,FastBufferReader reader)
        {
            if (!ValidServer(sender,ref reader,4)) return;
            reader.ReadValueSafe(out uint sequence);
            if (!snapshotOpen || sequence!=currentSnapshot) return;
            scratch.Clear();
            foreach (int id in pickups.Keys) if (!snapshotSeen.Contains(id)) scratch.Add(id);
            foreach (int id in scratch) RemovePickup(id);
            snapshotOpen=false;snapshotSeen.Clear();
        }
    }
}
