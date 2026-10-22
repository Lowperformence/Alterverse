using System;
using System.Collections.Generic;
using Alterverse.Shop;
using Alterverse.Visuals;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    public sealed partial class CoopHeroStateSync
    {
        // Separate cosmetic messages: existing match/movement packet formats are untouched.
        const string HeadwearRequestMessage="DF_HEADWEAR_REQUEST_V1",HeadwearStateMessage="DF_HEADWEAR_STATE_V1";
        readonly HashSet<ulong> headwearPeers=new();
        readonly UniversalHeroCosmetic[] roundHeadwear=new UniversalHeroCosmetic[MatchSession.MaxPlayers];
        uint headwearRound;
        bool headwearRoundReady,headwearServerReady;
        int headwearProbes;
        float nextHeadwear;
        void RegisterHeadwear()
        {
            messaging.RegisterNamedMessageHandler(HeadwearRequestMessage,OnHeadwearRequest);
            messaging.RegisterNamedMessageHandler(HeadwearStateMessage,OnHeadwearState);
            ResetHeadwear();
        }
        void UnregisterHeadwear()
        {
            messaging.UnregisterNamedMessageHandler(HeadwearRequestMessage);
            messaging.UnregisterNamedMessageHandler(HeadwearStateMessage);
            ResetHeadwear();
        }
        void ResetHeadwear()
        {
            headwearPeers.Clear();Array.Clear(roundHeadwear,0,roundHeadwear.Length);
            headwearRoundReady=false;headwearServerReady=false;headwearProbes=0;nextHeadwear=0;
        }
        uint EnsureHeadwearRound()
        {
            uint round=CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan);
            if(!headwearRoundReady||round!=headwearRound)
            {ResetHeadwear();headwearRound=round;headwearRoundReady=true;}
            return round;
        }
        void TickHeadwear(NetworkManager manager)
        {
            if(messaging==null||Time.unscaledTime<nextHeadwear)return;
            uint round=EnsureHeadwearRound();
            nextHeadwear=Time.unscaledTime+2f;
            if(manager.IsServer)
            {
                if(localSlot>=0&&slots[localSlot]!=null)
                {
                    var hero=slots[localSlot].GetComponent<Alterverse.Characters.HeroIdentity>();
                    if(hero!=null)roundHeadwear[localSlot]=HeadwearSelection.EquippedFor(hero.Hero);
                    ApplyHeadwear(localSlot,roundHeadwear[localSlot]);
                }
                foreach(ulong peer in headwearPeers)
                    if(IsHeadwearPeerConnected(manager,peer))SendHeadwearState(peer);
                return;
            }
            // An older server is probed only three times, then remains compatible.
            if(localSlot<0||slots[localSlot]==null||!headwearServerReady&&headwearProbes>=3)return;
            var identity=slots[localSlot].GetComponent<Alterverse.Characters.HeroIdentity>();
            if(identity==null)return;
            using FastBufferWriter writer=new(160,Allocator.Temp);
            writer.WriteValueSafe(round);writer.WriteValueSafe(localSlot);
            writer.WriteValueSafe(new FixedString128Bytes(localPlayerId));
            writer.WriteValueSafe((int)HeadwearSelection.EquippedFor(identity.Hero));
            messaging.SendNamedMessage(HeadwearRequestMessage,NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableSequenced);
            headwearProbes++;
        }
        void OnHeadwearRequest(ulong sender,FastBufferReader reader)
        {
            var manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsServer||!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority)return;
            try
            {
                if(!MatchNetworkProtocol.ReadRound(ref reader))return;
                EnsureHeadwearRound();
                reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out FixedString128Bytes player);
                reader.ReadValueSafe(out int raw);
                if(slot<0||slot>=slots.Length||!HeadwearSelection.Supported((UniversalHeroCosmetic)raw))return;
                if(slots[slot]==null)RefreshSlots(true);
                if(!ValidateOwner(sender,slot,player.ToString()))return;
                headwearPeers.Add(sender);roundHeadwear[slot]=(UniversalHeroCosmetic)raw;
                ApplyHeadwear(slot,roundHeadwear[slot]);SendHeadwearState(sender);
            }
            catch(OverflowException){}catch(ArgumentException){}catch(InvalidOperationException){}
        }
        void SendHeadwearState(ulong peer)
        {
            using FastBufferWriter writer=new(8+MatchSession.MaxPlayers*8,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(slots.Length);
            for(int i=0;i<slots.Length;i++){writer.WriteValueSafe(i);writer.WriteValueSafe((int)roundHeadwear[i]);}
            messaging.SendNamedMessage(HeadwearStateMessage,peer,writer,NetworkDelivery.ReliableSequenced);
        }
        void OnHeadwearState(ulong sender,FastBufferReader reader)
        {
            if(sender!=NetworkManager.ServerClientId||CoopAuthority.LocalHasServerAuthority||!CoopAuthority.Active)return;
            try
            {
                if(!MatchNetworkProtocol.ReadRound(ref reader))return;
                EnsureHeadwearRound();
                reader.ReadValueSafe(out int count);
                if(count!=slots.Length)return;
                var values=new UniversalHeroCosmetic[count];var seen=new bool[count];
                for(int i=0;i<count;i++)
                {
                    reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out int raw);
                    if(slot<0||slot>=count||seen[slot]||!HeadwearSelection.Supported((UniversalHeroCosmetic)raw))return;
                    values[slot]=(UniversalHeroCosmetic)raw;seen[slot]=true;
                }
                headwearServerReady=true;
                for(int i=0;i<count;i++)if(i!=localSlot)ApplyHeadwear(i,values[i]);
            }
            catch(OverflowException){}catch(ArgumentException){}catch(InvalidOperationException){}
        }
        void ApplyHeadwear(int slot,UniversalHeroCosmetic value)
        {
            if(slot<0||slot>=slots.Length||slots[slot]==null)return;
            var view=slots[slot].GetComponent<HeadwearPresentation>();
            if(view!=null)view.Apply(value);
        }
        static bool IsHeadwearPeerConnected(NetworkManager manager,ulong peer)
        {
            foreach(ulong connected in manager.ConnectedClientsIds)if(connected==peer)return true;
            return false;
        }
    }
}
