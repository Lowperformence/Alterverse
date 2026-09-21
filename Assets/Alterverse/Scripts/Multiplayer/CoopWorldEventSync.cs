using Alterverse.Combat;
using Alterverse.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Replicates sparse deterministic battlefield events. Damage/scoring remain host-owned;
    /// clients receive the exact Hot Zone position, planet spawn and bomber route seed for visuals.
    /// </summary>
    public sealed class CoopWorldEventSync : MonoBehaviour
    {
        private const string HotOpenMessage="DF_COOP_HOT_OPEN_V1";
        private const string HotResolveMessage="DF_COOP_HOT_RESOLVE_V1";
        private const string PlanetMessage="DF_COOP_PLANET_V1";
        private const string BomberMessage="DF_COOP_BOMBER_V1";

        private CustomMessagingManager messaging;
        private bool attached,eventsBound;

        private void OnEnable(){BindEvents();}
        private void OnDisable(){UnbindEvents();Detach();}

        private void Update()
        {
            if(!CoopAuthority.Active)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return;
            Attach(manager.CustomMessagingManager);
            if(!eventsBound)BindEvents();
        }

        private void BindEvents()
        {
            if(eventsBound)return;
            HotZoneDirector.NetworkZoneOpened+=OnHostHotOpened;
            HotZoneDirector.NetworkZoneResolved+=OnHostHotResolved;
            DimensionPlanetDirector.NetworkPlanetSpawned+=OnHostPlanetSpawned;
            SpaceBomberDirector.NetworkFlyoverSpawned+=OnHostBomberSpawned;
            eventsBound=true;
        }

        private void UnbindEvents()
        {
            if(!eventsBound)return;
            HotZoneDirector.NetworkZoneOpened-=OnHostHotOpened;
            HotZoneDirector.NetworkZoneResolved-=OnHostHotResolved;
            DimensionPlanetDirector.NetworkPlanetSpawned-=OnHostPlanetSpawned;
            SpaceBomberDirector.NetworkFlyoverSpawned-=OnHostBomberSpawned;
            eventsBound=false;
        }

        private void OnHostHotOpened(Vector3 position,int occurrence)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(32,Allocator.Temp);
            writer.WriteValueSafe(position.x);writer.WriteValueSafe(position.y);writer.WriteValueSafe(position.z);
            writer.WriteValueSafe(occurrence);
            SendAll(HotOpenMessage,writer);
        }

        private void OnHostHotResolved(int winner,float zero,float lumen,int occurrence)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(32,Allocator.Temp);
            writer.WriteValueSafe(winner);writer.WriteValueSafe(zero);writer.WriteValueSafe(lumen);writer.WriteValueSafe(occurrence);
            SendAll(HotResolveMessage,writer);
        }

        private void OnHostPlanetSpawned(int variant)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(8,Allocator.Temp);
            writer.WriteValueSafe(variant);
            SendAll(PlanetMessage,writer);
        }

        private void OnHostBomberSpawned(int pass,int seed)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(12,Allocator.Temp);
            writer.WriteValueSafe(pass);writer.WriteValueSafe(seed);
            SendAll(BomberMessage,writer);
        }

        private bool CanBroadcast()
        {
            NetworkManager manager=NetworkManager.Singleton;
            return CoopAuthority.Active&&CoopAuthority.LocalIsHost&&manager!=null&&manager.IsHost&&messaging!=null;
        }

        private void SendAll(string name,FastBufferWriter writer)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(manager?.ConnectedClientsIds==null||messaging==null)return;
            for(int i=0;i<manager.ConnectedClientsIds.Count;i++)
            {
                ulong id=manager.ConnectedClientsIds[i];
                if(id==NetworkManager.ServerClientId)continue;
                messaging.SendNamedMessage(name,id,writer,NetworkDelivery.ReliableSequenced);
            }
        }

        private void OnHotOpen(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender))return;
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out int occurrence);
            FindAnyObjectByType<HotZoneDirector>()?.ApplyNetworkOpen(new Vector3(x,y,z),occurrence);
        }

        private void OnHotResolve(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender))return;
            reader.ReadValueSafe(out int winner);reader.ReadValueSafe(out float zero);reader.ReadValueSafe(out float lumen);
            reader.ReadValueSafe(out int occurrence);
            FindAnyObjectByType<HotZoneDirector>()?.ApplyNetworkResolve(winner,zero,lumen,occurrence);
        }

        private void OnPlanet(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender))return;
            reader.ReadValueSafe(out int variant);
            FindAnyObjectByType<DimensionPlanetDirector>()?.ApplyNetworkSpawn(variant);
        }

        private void OnBomber(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender))return;
            reader.ReadValueSafe(out int pass);reader.ReadValueSafe(out int seed);
            FindAnyObjectByType<SpaceBomberDirector>()?.ApplyNetworkFlyover(pass,seed);
        }

        private static bool ValidServerMessage(ulong sender)
        {
            NetworkManager manager=NetworkManager.Singleton;
            return CoopAuthority.Active&&!CoopAuthority.LocalIsHost&&manager!=null&&manager.IsClient&&sender==NetworkManager.ServerClientId;
        }

        private void Attach(CustomMessagingManager value)
        {
            if(value==null||attached&&messaging==value)return;
            Detach();
            messaging=value;
            messaging.RegisterNamedMessageHandler(HotOpenMessage,OnHotOpen);
            messaging.RegisterNamedMessageHandler(HotResolveMessage,OnHotResolve);
            messaging.RegisterNamedMessageHandler(PlanetMessage,OnPlanet);
            messaging.RegisterNamedMessageHandler(BomberMessage,OnBomber);
            attached=true;
        }

        private void Detach()
        {
            if(!attached||messaging==null){messaging=null;attached=false;return;}
            messaging.UnregisterNamedMessageHandler(HotOpenMessage);
            messaging.UnregisterNamedMessageHandler(HotResolveMessage);
            messaging.UnregisterNamedMessageHandler(PlanetMessage);
            messaging.UnregisterNamedMessageHandler(BomberMessage);
            messaging=null;attached=false;
        }
    }
}
