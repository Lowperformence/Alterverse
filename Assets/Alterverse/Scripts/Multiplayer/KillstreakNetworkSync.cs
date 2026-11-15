using Alterverse.Combat;
using Alterverse.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Server-owned killstreak rewards and authenticated placement requests.</summary>
    public sealed class KillstreakNetworkSync:MonoBehaviour
    {
        private const string PlaceMessage="DF_COOP_TURRET_PLACE_V1";
        private const string RewardMessage="DF_COOP_TURRET_REWARDS_V1";
        public static KillstreakNetworkSync Instance {get;private set;}
        private CustomMessagingManager messaging;
        private bool attached;
        private CoopMatchPlan round;
        private uint nextSequence;
        private readonly uint[] processed=new uint[MatchSession.MaxPlayers];
        private float nextRewards;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure(){if(Instance==null)new GameObject("DF_KillstreakNetworkSync").AddComponent<KillstreakNetworkSync>();}
        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);
        }
        private void OnDisable(){Detach();}
        private void OnDestroy(){Detach();if(Instance==this)Instance=null;}
        private void EnsureRound()
        {
            if(ReferenceEquals(round,CoopAuthority.ActivePlan))return;
            round=CoopAuthority.ActivePlan;nextSequence=0;nextRewards=0;
            System.Array.Clear(processed,0,processed.Length);
        }
        private void Update()
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(!CoopAuthority.Active||manager==null||!manager.IsListening){Detach();return;}
            Attach(manager.CustomMessagingManager);EnsureRound();
            if(manager.IsServer&&CoopAuthority.LocalHasServerAuthority&&Time.unscaledTime>=nextRewards)
            {nextRewards=Time.unscaledTime+.5f;SendRewards(manager);}
        }
        private void Attach(CustomMessagingManager value)
        {
            if(value==null||attached&&messaging==value)return;
            Detach();messaging=value;
            messaging.RegisterNamedMessageHandler(PlaceMessage,OnPlacement);
            messaging.RegisterNamedMessageHandler(RewardMessage,OnRewards);
            attached=true;
        }
        private void Detach()
        {
            if(attached&&messaging!=null)
            {messaging.UnregisterNamedMessageHandler(PlaceMessage);messaging.UnregisterNamedMessageHandler(RewardMessage);}
            messaging=null;attached=false;
        }

        public static bool TrySendPlacement(GameObject actor,Vector3 position)
        {
            var self=Instance;NetworkManager manager=NetworkManager.Singleton;
            if(self==null||!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||manager==null||
                manager.IsServer||!manager.IsConnectedClient||self.messaging==null||!AimGeometry.Finite(position))return false;
            CoopHeroSlot slot=actor!=null?actor.GetComponent<CoopHeroSlot>():null;
            Health hp=actor!=null?actor.GetComponent<Health>():null;
            if(slot==null||!slot.IsLocalHuman||hp==null||hp.IsDead)return false;
            self.EnsureRound();
            uint sequence=unchecked(self.nextSequence+1);if(sequence==0)sequence=1;
            using FastBufferWriter writer=new(192,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(slot.Slot);writer.WriteValueSafe(new FixedString128Bytes(slot.PlayerId??""));
            writer.WriteValueSafe(sequence);
            writer.WriteValueSafe(position.x);writer.WriteValueSafe(position.y);writer.WriteValueSafe(position.z);
            self.messaging.SendNamedMessage(PlaceMessage,NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableSequenced);
            self.nextSequence=sequence;return true;
        }
        private void OnPlacement(ulong sender,FastBufferReader reader)
        {
            var commands=CoopGameplayCommands.Instance;
            if(commands==null||!commands.TryAuthorizeTurretPlacement(sender,ref reader,out GameObject actor)||
                !reader.TryBeginRead(sizeof(uint)+3*sizeof(float)))return;
            reader.ReadValueSafe(out uint sequence);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            EnsureRound();
            int slot=actor.GetComponent<CoopHeroSlot>().Slot;
            if(slot<0||slot>=processed.Length)return;
            uint previous=processed[slot];
            if(sequence==0||(previous!=0&&unchecked((int)(sequence-previous))<=0))return;
            processed[slot]=sequence;
            CombatFeedbackHUD.DeployNetworkTurret(actor,new Vector3(x,y,z));
        }
        private void SendRewards(NetworkManager manager)
        {
            if(messaging==null||manager.ConnectedClientsIds==null)return;
            using FastBufferWriter writer=new(256,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            for(int i=0;i<MatchSession.MaxPlayers;i++)
            {
                CombatFeedbackHUD.GetNetworkReward(i,out int normal,out int mobile,out bool eligible,out int streak);
                writer.WriteValueSafe(normal);writer.WriteValueSafe(mobile);writer.WriteValueSafe(eligible);writer.WriteValueSafe(streak);
            }
            foreach(ulong id in manager.ConnectedClientsIds)
                if(id!=NetworkManager.ServerClientId)messaging.SendNamedMessage(RewardMessage,id,writer,NetworkDelivery.ReliableSequenced);
        }
        private void OnRewards(ulong sender,FastBufferReader reader)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsClient||
                sender!=NetworkManager.ServerClientId||!MatchNetworkProtocol.ReadRound(ref reader)||
                !reader.TryBeginRead(MatchSession.MaxPlayers*(3*sizeof(int)+sizeof(bool))))return;
            for(int slot=0;slot<MatchSession.MaxPlayers;slot++)
            {
                reader.ReadValueSafe(out int normal);reader.ReadValueSafe(out int mobile);
                reader.ReadValueSafe(out bool eligible);reader.ReadValueSafe(out int streak);
                if(normal<0||mobile<0||streak<0)continue;
                CombatFeedbackHUD.ApplyNetworkReward(slot,normal,mobile,eligible,streak);
            }
        }
    }
}
