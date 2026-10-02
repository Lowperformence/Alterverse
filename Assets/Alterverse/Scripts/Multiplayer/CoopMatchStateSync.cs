using Alterverse.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Replicates the server-owned match clock, control score and result to every client.</summary>
    public sealed class CoopMatchStateSync : MonoBehaviour
    {
        private const string StateMessage="DF_COOP_MATCH_STATE_V1";
        private const float SendInterval=.20f;

        private DimensionsFront front;
        private CustomMessagingManager messaging;
        private bool attached;
        private float nextSend;

        private void Awake(){front=FindAnyObjectByType<DimensionsFront>();}
        private void OnEnable(){front??=FindAnyObjectByType<DimensionsFront>();}
        private void OnDisable(){Detach();}

        private void Update()
        {
            if(!CoopAuthority.Active)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return;
            front??=FindAnyObjectByType<DimensionsFront>();
            if(front==null)return;
            Attach(manager.CustomMessagingManager);

            if(!CoopAuthority.LocalHasServerAuthority||Time.unscaledTime<nextSend)return;
            nextSend=Time.unscaledTime+SendInterval;
            SendState(manager);
        }

        private void SendState(NetworkManager manager)
        {
            if(manager==null||!manager.IsServer||messaging==null||manager.ConnectedClientsIds==null)return;
            using FastBufferWriter writer=new(64,Allocator.Temp);
            writer.WriteValueSafe(front.TimeRemaining);
            writer.WriteValueSafe(front.ZeroControl);
            writer.WriteValueSafe(front.LumenControl);
            writer.WriteValueSafe(front.IsOvertime);
            writer.WriteValueSafe(front.IsSuddenDeath);
            writer.WriteValueSafe(front.HasEnded);
            writer.WriteValueSafe(front.Winner);

            for(int i=0;i<manager.ConnectedClientsIds.Count;i++)
            {
                ulong id=manager.ConnectedClientsIds[i];
                if(id==NetworkManager.ServerClientId)continue;
                messaging.SendNamedMessage(StateMessage,id,writer,NetworkDelivery.UnreliableSequenced);
            }
        }

        private void OnState(ulong senderClientId,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||senderClientId!=NetworkManager.ServerClientId)return;
            front??=FindAnyObjectByType<DimensionsFront>();
            if(front==null)return;

            reader.ReadValueSafe(out float remaining);
            reader.ReadValueSafe(out float zero);
            reader.ReadValueSafe(out float lumen);
            reader.ReadValueSafe(out bool overtime);
            reader.ReadValueSafe(out bool sudden);
            reader.ReadValueSafe(out bool ended);
            reader.ReadValueSafe(out int winner);
            front.ApplyNetworkState(remaining,zero,lumen,overtime,sudden,ended,winner);
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
