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
        // Headwear-only capability upgrade. V1 peers receive only the IDs they already know.
        const string HeadwearRequestMessage="DF_HEADWEAR_REQUEST_V1",HeadwearStateMessage="DF_HEADWEAR_STATE_V1";
        const string HeadwearRequestV2Message="DF_HEADWEAR_REQUEST_V2",HeadwearStateV2Message="DF_HEADWEAR_STATE_V2";
        readonly HashSet<ulong> headwearPeers=new(),modernHeadwearPeers=new();
        readonly UniversalHeroCosmetic[] roundHeadwear=new UniversalHeroCosmetic[MatchSession.MaxPlayers];
        uint headwearRound;
        bool headwearRoundReady,headwearServerReady,headwearModernServerReady;
        int headwearProbes;
        float nextHeadwear;
        void RegisterHeadwear()
        {
            messaging.RegisterNamedMessageHandler(HeadwearRequestMessage,OnHeadwearRequest);
            messaging.RegisterNamedMessageHandler(HeadwearStateMessage,OnHeadwearState);
            messaging.RegisterNamedMessageHandler(HeadwearRequestV2Message,OnHeadwearRequestV2);
            messaging.RegisterNamedMessageHandler(HeadwearStateV2Message,OnHeadwearStateV2);
            ResetHeadwear();
        }
        void UnregisterHeadwear()
        {
            messaging.UnregisterNamedMessageHandler(HeadwearRequestMessage);
            messaging.UnregisterNamedMessageHandler(HeadwearStateMessage);
            messaging.UnregisterNamedMessageHandler(HeadwearRequestV2Message);
            messaging.UnregisterNamedMessageHandler(HeadwearStateV2Message);
            ResetHeadwear();
        }
        void ResetHeadwear()
        {
            headwearPeers.Clear();modernHeadwearPeers.Clear();Array.Clear(roundHeadwear,0,roundHeadwear.Length);
            headwearRoundReady=false;headwearServerReady=false;headwearModernServerReady=false;headwearProbes=0;nextHeadwear=0;
        }
        uint EnsureHeadwearRound()
        {
            uint round=CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan);
            if(!headwearRoundReady||round!=headwearRound)
            {ResetHeadwear();headwearRound=round;headwearRoundReady=true;}
            return round;
        }
        static bool LegacyHeadwear(UniversalHeroCosmetic value)=>
            value==UniversalHeroCosmetic.Original||value==UniversalHeroCosmetic.BetaCap||value==UniversalHeroCosmetic.UfoTestHat;
        static UniversalHeroCosmetic LegacyHeadwearValue(UniversalHeroCosmetic value)=>
            LegacyHeadwear(value)?value:UniversalHeroCosmetic.Original;
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
            if(localSlot<0||slots[localSlot]==null||!headwearServerReady&&headwearProbes>=3)return;
            var identity=slots[localSlot].GetComponent<Alterverse.Characters.HeroIdentity>();
            if(identity==null)return;
            var selected=HeadwearSelection.EquippedFor(identity.Hero);
            if(!headwearModernServerReady)SendHeadwearRequest(round,selected,false);
            // Probe the new cosmetic capability three times at most on legacy hosts.
            if(headwearModernServerReady||headwearProbes<3)SendHeadwearRequest(round,selected,true);
            if(headwearProbes<3)headwearProbes++;
        }
        void SendHeadwearRequest(uint round,UniversalHeroCosmetic selected,bool modern)
        {
            using FastBufferWriter writer=new(160,Allocator.Temp);
            writer.WriteValueSafe(round);writer.WriteValueSafe(localSlot);
            writer.WriteValueSafe(new FixedString128Bytes(localPlayerId));
            writer.WriteValueSafe((int)(modern?selected:LegacyHeadwearValue(selected)));
            messaging.SendNamedMessage(modern?HeadwearRequestV2Message:HeadwearRequestMessage,
                NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableSequenced);
        }
        void OnHeadwearRequest(ulong sender,FastBufferReader reader)=>ReadHeadwearRequest(sender,reader,false);
        void OnHeadwearRequestV2(ulong sender,FastBufferReader reader)=>ReadHeadwearRequest(sender,reader,true);
        void ReadHeadwearRequest(ulong sender,FastBufferReader reader,bool modern)
        {
            var manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsServer||!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority)return;
            try
            {
                if(!MatchNetworkProtocol.ReadRound(ref reader))return;
                EnsureHeadwearRound();
                reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out FixedString128Bytes player);
                reader.ReadValueSafe(out int raw);
                var selected=(UniversalHeroCosmetic)raw;
                if(slot<0||slot>=slots.Length||!(modern?HeadwearSelection.Supported(selected):LegacyHeadwear(selected)))return;
                if(slots[slot]==null)RefreshSlots(true);
                if(!ValidateOwner(sender,slot,player.ToString()))return;
                // A delayed V1 probe must not overwrite a negotiated V2 hat with its fallback.
                if(!modern&&modernHeadwearPeers.Contains(sender))return;
                headwearPeers.Add(sender);if(modern)modernHeadwearPeers.Add(sender);
                roundHeadwear[slot]=selected;
                ApplyHeadwear(slot,selected);SendHeadwearState(sender);
            }
            catch(OverflowException){}catch(ArgumentException){}catch(InvalidOperationException){}
        }
        void SendHeadwearState(ulong peer)
        {
            bool modern=modernHeadwearPeers.Contains(peer);
            using FastBufferWriter writer=new(8+MatchSession.MaxPlayers*8,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(slots.Length);
            for(int i=0;i<slots.Length;i++)
            {writer.WriteValueSafe(i);writer.WriteValueSafe((int)(modern?roundHeadwear[i]:LegacyHeadwearValue(roundHeadwear[i])));}
            messaging.SendNamedMessage(modern?HeadwearStateV2Message:HeadwearStateMessage,peer,writer,NetworkDelivery.ReliableSequenced);
        }
        void OnHeadwearState(ulong sender,FastBufferReader reader)=>ReadHeadwearState(sender,reader,false);
        void OnHeadwearStateV2(ulong sender,FastBufferReader reader)=>ReadHeadwearState(sender,reader,true);
        void ReadHeadwearState(ulong sender,FastBufferReader reader,bool modern)
        {
            if(sender!=NetworkManager.ServerClientId||CoopAuthority.LocalHasServerAuthority||!CoopAuthority.Active)return;
            try
            {
                if(!MatchNetworkProtocol.ReadRound(ref reader))return;
                EnsureHeadwearRound();
                if(!modern&&headwearModernServerReady)return;
                reader.ReadValueSafe(out int count);
                if(count!=slots.Length)return;
                var values=new UniversalHeroCosmetic[count];var seen=new bool[count];
                for(int i=0;i<count;i++)
                {
                    reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out int raw);
                    var value=(UniversalHeroCosmetic)raw;
                    if(slot<0||slot>=count||seen[slot]||!(modern?HeadwearSelection.Supported(value):LegacyHeadwear(value)))return;
                    values[slot]=value;seen[slot]=true;
                }
                headwearServerReady=true;if(modern)headwearModernServerReady=true;
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

