using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Routes non-server player combat intentions to the authoritative NGO server. The server
    /// performs the existing BasicAttack / PlayerHeroAbilities logic against its authoritative world.
    /// Clients never directly mutate authoritative combat state.
    /// </summary>
    public sealed class CoopGameplayCommands : MonoBehaviour
    {
        private const string BasicMessage="DF_COOP_BASIC_V1";
        private const string AbilityMessage="DF_COOP_ABILITY_V1";

        public static CoopGameplayCommands Instance { get; private set; }

        private CustomMessagingManager messaging;
        private bool attached;
        private readonly Dictionary<ulong,string> senderOwners=new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if(Instance==null)new GameObject("DF_CoopGameplayCommands").AddComponent<CoopGameplayCommands>();
        }

        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening||!CoopAuthority.Active){Detach();return;}
            Attach(manager.CustomMessagingManager);
        }

        public static bool ShouldRouteClientCommand(GameObject actor)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||actor==null)return false;
            CoopHeroSlot slot=actor.GetComponent<CoopHeroSlot>();
            return slot!=null&&slot.IsHuman&&slot.IsLocalHuman;
        }

        public static bool TrySendBasic(GameObject actor)
        {
            CoopGameplayCommands service=Instance;
            return service!=null&&service.SendCommand(actor,-1);
        }

        public static bool TrySendAbility(GameObject actor,int abilitySlot)
        {
            if(abilitySlot<0||abilitySlot>3)return false;
            CoopGameplayCommands service=Instance;
            return service!=null&&service.SendCommand(actor,abilitySlot);
        }

        private bool SendCommand(GameObject actor,int abilitySlot)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||manager.IsServer||!manager.IsConnectedClient||messaging==null)return false;
            CoopHeroSlot heroSlot=actor!=null?actor.GetComponent<CoopHeroSlot>():null;
            Health health=actor!=null?actor.GetComponent<Health>():null;
            if(heroSlot==null||!heroSlot.IsLocalHuman||health==null||health.IsDead)return false;

            FixedString128Bytes id=new(heroSlot.PlayerId??"");
            using FastBufferWriter writer=new(160,Allocator.Temp);
            writer.WriteValueSafe(heroSlot.Slot);
            writer.WriteValueSafe(id);

            string message=abilitySlot<0?BasicMessage:AbilityMessage;
            if(abilitySlot>=0)
            {
                writer.WriteValueSafe(abilitySlot);
                HeroMotor motor=actor.GetComponent<HeroMotor>();
                Vector3 direction=motor!=null&&motor.CurrentMoveDirection.sqrMagnitude>.0001f
                    ?motor.CurrentMoveDirection.normalized:actor.transform.forward;
                writer.WriteValueSafe(direction.x);writer.WriteValueSafe(direction.y);writer.WriteValueSafe(direction.z);
            }
            messaging.SendNamedMessage(message,NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableSequenced);
            return true;
        }

        private void Attach(CustomMessagingManager value)
        {
            if(value==null||attached&&messaging==value)return;
            Detach();
            messaging=value;
            messaging.RegisterNamedMessageHandler(BasicMessage,OnBasic);
            messaging.RegisterNamedMessageHandler(AbilityMessage,OnAbility);
            attached=true;
        }

        private void Detach()
        {
            if(!attached||messaging==null){messaging=null;attached=false;senderOwners.Clear();return;}
            messaging.UnregisterNamedMessageHandler(BasicMessage);
            messaging.UnregisterNamedMessageHandler(AbilityMessage);
            messaging=null;attached=false;senderOwners.Clear();
        }

        private void OnBasic(ulong senderClientId,FastBufferReader reader)
        {
            if(!TryReadAuthorizedActor(senderClientId,reader,out GameObject actor))return;
            actor.GetComponent<BasicAttack>()?.TryAttack();
        }

        private void OnAbility(ulong senderClientId,FastBufferReader reader)
        {
            if(!TryReadAuthorizedActor(senderClientId,reader,out GameObject actor))return;
            reader.ReadValueSafe(out int slot);
            reader.ReadValueSafe(out float dx);reader.ReadValueSafe(out float dy);reader.ReadValueSafe(out float dz);
            if(slot<0||slot>3)return;
            Vector3 requestedDirection=new(dx,dy,dz);requestedDirection.y=0f;
            if(requestedDirection.sqrMagnitude>.0001f)requestedDirection.Normalize();
            actor.GetComponent<PlayerHeroAbilities>()?.CastNetworkAuthoritative(slot,requestedDirection);
        }

        private bool TryReadAuthorizedActor(ulong senderClientId,FastBufferReader reader,out GameObject actor)
        {
            actor=null;
            NetworkManager manager=NetworkManager.Singleton;
            if(!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer)return false;

            reader.ReadValueSafe(out int rosterSlot);
            reader.ReadValueSafe(out FixedString128Bytes fixedId);
            string playerId=fixedId.ToString();

            CoopMatchPlan plan=CoopAuthority.ActivePlan;
            if(plan?.Session?.Slots==null||rosterSlot<0||rosterSlot>=MatchSession.TeamSize)return false;
            MatchPlayerSlot planned=plan.Session.Slots[rosterSlot];
            if(planned==null||planned.IsBot||planned.IsHost||planned.PlayerId!=playerId)return false;

            if(!CoopPlanHandshake.IsAuthorizedClient(senderClientId,playerId))return false;
            senderOwners[senderClientId]=playerId;

            CoopHeroSlot[] heroes=FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
            for(int i=0;i<heroes.Length;i++)
            {
                CoopHeroSlot h=heroes[i];
                if(h!=null&&h.Slot==rosterSlot&&h.PlayerId==playerId)
                {
                    actor=h.gameObject;
                    return true;
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            Detach();
            if(Instance==this)Instance=null;
        }
    }
}
