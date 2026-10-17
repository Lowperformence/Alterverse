using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Combat;
using Alterverse.Visuals;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Lightweight custom-message synchronization for the eight deterministic hero slots.
    /// Human owners send only their movement pose to the authoritative server. The server validates it,
    /// runs authoritative gameplay, and broadcasts hero pose/health snapshots to clients.
    /// </summary>
    public sealed partial class CoopHeroStateSync : MonoBehaviour
    {
        private static CoopHeroStateSync audioRelay;
        private const string SharkEffectMessage="DF_COOP_SHARK_FX_V2";
        private const string VoidKnightEffectMessage="DF_COOP_VOID_KNIGHT_FX_V1";
        private const string TimmyEffectMessage="DF_COOP_TIMMY_FX_V1";
        private const string OrunOverdriveMessage="DF_COOP_ORUN_OVERDRIVE_FX_V1";
        private const string KillSoundMessage="DF_COOP_KILL_AUDIO_V2";
        private const string CombatSoundMessage="DF_COOP_COMBAT_AUDIO_V2";
        private const string OwnerPoseMessage="DF_COOP_OWNER_POSE_V4";
        private const string HeroSnapshotMessage="DF_COOP_HERO_SNAPSHOT_V5";
        private const float OwnerSendInterval=1f/20f;
        private const float HostSnapshotInterval=1f/12f;
        private const float MinimumMovementSlack=.18f;

        private CustomMessagingManager messaging;
        private readonly CoopHeroSlot[] slots=new CoopHeroSlot[MatchSession.MaxPlayers];
        private readonly Dictionary<ulong,string> senderOwners=new();
        private readonly Dictionary<ulong,float> senderLastPoseAt=new();
        private float nextOwnerSend,nextHostSnapshot,nextRefresh;
        private int localSlot=-1;
        private string localPlayerId="";
        private bool attached;

        private void OnEnable()
        {
            audioRelay=this;RefreshSlots(true);
            NetworkManager manager=NetworkManager.Singleton;
            if(manager!=null&&manager.IsListening)Attach(manager.CustomMessagingManager);
        }
        private void OnDisable(){if(audioRelay==this)audioRelay=null;Detach();}

        private void Update()
        {
            if(!CoopAuthority.Active)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return;

            Attach(manager.CustomMessagingManager);

            if(Time.unscaledTime>=nextRefresh)RefreshSlots(false);

            if(CoopAuthority.LocalHasServerAuthority)
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
            HeroVfxEvents.Emitted+=OnHostHeroVfx;
            messaging.RegisterNamedMessageHandler(HeroVfxMessage,OnHeroVfx);
            messaging.RegisterNamedMessageHandler(OwnerPoseMessage,OnOwnerPose);
            messaging.RegisterNamedMessageHandler(HeroSnapshotMessage,OnHeroSnapshot);
            messaging.RegisterNamedMessageHandler(CombatSoundMessage,OnCombatSound);
            messaging.RegisterNamedMessageHandler(KillSoundMessage,OnKillSound);
            messaging.RegisterNamedMessageHandler(SharkEffectMessage,OnSharkEffect);
            messaging.RegisterNamedMessageHandler(TimmyEffectMessage,OnTimmyEffect);
            messaging.RegisterNamedMessageHandler(VoidKnightEffectMessage,OnVoidKnightEffect);
            messaging.RegisterNamedMessageHandler(OrunOverdriveMessage,OnOrunOverdrive);
            attached=true;
        }

        private void Detach()
        {
            HeroVfxEvents.Emitted-=OnHostHeroVfx;
            if(!attached||messaging==null){messaging=null;attached=false;return;}
            messaging.UnregisterNamedMessageHandler(HeroVfxMessage);
            messaging.UnregisterNamedMessageHandler(OwnerPoseMessage);
            messaging.UnregisterNamedMessageHandler(HeroSnapshotMessage);
            messaging.UnregisterNamedMessageHandler(CombatSoundMessage);
            messaging.UnregisterNamedMessageHandler(KillSoundMessage);
            messaging.UnregisterNamedMessageHandler(SharkEffectMessage);
            messaging.UnregisterNamedMessageHandler(TimmyEffectMessage);
            messaging.UnregisterNamedMessageHandler(VoidKnightEffectMessage);
            messaging.UnregisterNamedMessageHandler(OrunOverdriveMessage);
            messaging=null;attached=false;
            senderOwners.Clear();
            senderLastPoseAt.Clear();
            ResetPoseTransport();
        }

        // Presentation only: confirmed phases originate on the authoritative server. Sending before
        // viewport culling lets a client hear its own hero even when it is outside the server's view.
        public static void BroadcastCombatSound(GameObject actor,int action,int phase,Vector3 position)
        {
            var service=audioRelay;var manager=NetworkManager.Singleton;
            if(service==null||service.messaging==null||!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer)return;
            var slot=actor.GetComponent<CoopHeroSlot>();if(slot==null||slot.Slot<0||slot.Slot>=MatchSession.MaxPlayers)return;
            using FastBufferWriter writer=new(28,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(slot.Slot);writer.WriteValueSafe(action);writer.WriteValueSafe(phase);
            writer.WriteValueSafe(position.x);writer.WriteValueSafe(position.y);writer.WriteValueSafe(position.z);
            for(int i=0;i<manager.ConnectedClientsIds.Count;i++)
            {
                ulong id=manager.ConnectedClientsIds[i];if(id==NetworkManager.ServerClientId)continue;
                service.messaging.SendNamedMessage(CombatSoundMessage,id,writer,NetworkDelivery.UnreliableSequenced);
            }
        }
        private void OnCombatSound(ulong sender,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||sender!=NetworkManager.ServerClientId||!reader.TryBeginRead(28))return;
            if (!MatchNetworkProtocol.ReadRound(ref reader)) return;
            reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out int action);reader.ReadValueSafe(out int phase);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            if(slot<0||slot>=slots.Length||slots[slot]==null||action<0||action>4||phase<0||phase>2)return;
            if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||float.IsNaN(z)||float.IsInfinity(z))return;
            Alterverse.Audio.HeroCombatAudio.Receive(slots[slot].gameObject,action,phase,new Vector3(x,y,z));
        }

        public static void BroadcastKillSound(GameObject killer)
        {
            var service=audioRelay;var manager=NetworkManager.Singleton;
            if(service==null||service.messaging==null||!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer)return;
            var slot=killer.GetComponent<CoopHeroSlot>();if(slot==null||slot.Slot<0||slot.Slot>=MatchSession.MaxPlayers)return;
            using FastBufferWriter writer=new(8,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(slot.Slot);
            foreach(ulong id in manager.ConnectedClientsIds)
                if(id!=NetworkManager.ServerClientId)
                    service.messaging.SendNamedMessage(KillSoundMessage,id,writer,NetworkDelivery.ReliableSequenced);
        }
        private void OnKillSound(ulong sender,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||sender!=NetworkManager.ServerClientId||!reader.TryBeginRead(8))return;
            if (!MatchNetworkProtocol.ReadRound(ref reader)) return;
            reader.ReadValueSafe(out int slot);
            if(slot>=0&&slot<slots.Length&&slots[slot]!=null)
                Alterverse.Audio.HeroCombatAudio.ConfirmedKill(slots[slot].gameObject,false);
        }

        public static void BroadcastOrunOverdrive(GameObject actor,Vector3 center)
        {
            if(HeroVfxEvents.InsideSemanticEffect)return;
            var service=audioRelay;var manager=NetworkManager.Singleton;
            if(actor==null||service==null||service.messaging==null||!CoopAuthority.Active||
                !CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer)return;
            var slot=actor.GetComponent<CoopHeroSlot>();
            if(slot==null||slot.Slot<0||slot.Slot>=MatchSession.MaxPlayers||actor.GetComponent<HeroIdentity>()?.Hero!=HeroId.Rook)return;
            using FastBufferWriter writer=new(20,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(slot.Slot);
            writer.WriteValueSafe(center.x);writer.WriteValueSafe(center.y);writer.WriteValueSafe(center.z);
            foreach(ulong id in manager.ConnectedClientsIds)
                if(id!=NetworkManager.ServerClientId)
                    service.messaging.SendNamedMessage(OrunOverdriveMessage,id,writer,NetworkDelivery.ReliableSequenced);
        }

        private void OnOrunOverdrive(ulong sender,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||
                sender!=NetworkManager.ServerClientId||!reader.TryBeginRead(20))return;
            if(!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int slot);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            if(slot<0||slot>=slots.Length||slots[slot]==null||
                float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||float.IsNaN(z)||float.IsInfinity(z))return;
            GameObject actor=slots[slot].gameObject;
            if(actor.GetComponent<HeroIdentity>()?.Hero!=HeroId.Rook)return;
            HeroSkillVfx.OrunOverdrive(actor,new Vector3(x,y,z));
        }

        public static void BroadcastSharkEffect(GameObject actor,int kind,Vector3 center,float radius,float duration,Health target=null)
        {
            if(HeroVfxEvents.InsideSemanticEffect)return;
            var service=audioRelay;var manager=NetworkManager.Singleton;
            if(service==null||service.messaging==null||!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer)return;
            var slot=actor.GetComponent<CoopHeroSlot>();if(slot==null||slot.Slot<0||slot.Slot>=MatchSession.MaxPlayers)return;
            using FastBufferWriter writer=new(256,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(slot.Slot);writer.WriteValueSafe(kind);
            writer.WriteValueSafe(center.x);writer.WriteValueSafe(center.y);writer.WriteValueSafe(center.z);
            writer.WriteValueSafe(radius);writer.WriteValueSafe(duration);
            if(kind==0||kind==6)
            {
                writer.WriteValueSafe(target!=null?target.GetComponent<CoopHeroSlot>()?.Slot??-1:-1);
                FixedString128Bytes key=new(target!=null?target.GetComponent<CoopWorldEntityKey>()?.Key??"":"");
                writer.WriteValueSafe(key);
            }
            foreach(ulong id in manager.ConnectedClientsIds)
                if(id!=NetworkManager.ServerClientId)
                    service.messaging.SendNamedMessage(SharkEffectMessage,id,writer,NetworkDelivery.ReliableSequenced);
        }
        private void OnSharkEffect(ulong sender,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||sender!=NetworkManager.ServerClientId||!reader.TryBeginRead(32))return;
            if(!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out int kind);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float radius);reader.ReadValueSafe(out float duration);
            if(slot<0||slot>=slots.Length||slots[slot]==null||kind<0||kind>6||
                float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||float.IsNaN(z)||float.IsInfinity(z)||
                float.IsNaN(radius)||float.IsInfinity(radius)||radius<0||radius>4||
                float.IsNaN(duration)||float.IsInfinity(duration)||duration<=0||duration>4)return;
            GameObject actor=slots[slot].gameObject;
            if(actor.GetComponent<HeroIdentity>()?.Hero!=HeroId.Nerok)return;
            Vector3 center=new Vector3(x,y,z);
            if(kind==0||kind==6)
            {
                reader.ReadValueSafe(out int targetSlot);reader.ReadValueSafe(out FixedString128Bytes fixedKey);
                Health target=null;
                if(targetSlot>=0&&targetSlot<slots.Length&&slots[targetSlot]!=null)target=slots[targetSlot].GetComponent<Health>();
                else
                {
                    string key=fixedKey.ToString();
                    if(key.Length>0)foreach(Health candidate in Health.Active)
                        if(candidate!=null&&candidate.GetComponent<CoopWorldEntityKey>()?.Key==key){target=candidate;break;}
                }
                if(kind==0)Alterverse.Visuals.HeroSkillVfx.SharkAnchor(actor,center,duration,target!=null?target.transform:null);
                else if(target!=null)Alterverse.Visuals.HeroSkillVfx.SharkPullChain(actor,target,duration);
            }
            else if(kind==1)Alterverse.Visuals.HeroSkillVfx.SharkVortex(actor,center,radius,duration);
            else Alterverse.Visuals.HeroSkillVfx.Area(actor,center,radius,duration,kind!=5,false,kind==4?2:3);
        }

        public static void BroadcastTimmyEffect(GameObject actor,int kind,Vector3 from,Vector3 to,float radius,float duration)
        {
            if(HeroVfxEvents.InsideSemanticEffect)return;
            var service=audioRelay;var manager=NetworkManager.Singleton;
            if(actor==null||service==null||service.messaging==null||!CoopAuthority.Active||
                !CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer||actor.GetComponent<HeroIdentity>()?.Hero!=HeroId.Timmy)return;
            var slot=actor.GetComponent<CoopHeroSlot>();if(slot==null||slot.Slot<0||slot.Slot>=MatchSession.MaxPlayers)return;
            using FastBufferWriter writer=new(52,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(slot.Slot);writer.WriteValueSafe(kind);
            writer.WriteValueSafe(from.x);writer.WriteValueSafe(from.y);writer.WriteValueSafe(from.z);
            writer.WriteValueSafe(to.x);writer.WriteValueSafe(to.y);writer.WriteValueSafe(to.z);
            writer.WriteValueSafe(radius);writer.WriteValueSafe(duration);writer.WriteValueSafe(manager.ServerTime.Time);
            foreach(ulong id in manager.ConnectedClientsIds)
                if(id!=NetworkManager.ServerClientId)
                    service.messaging.SendNamedMessage(TimmyEffectMessage,id,writer,NetworkDelivery.ReliableSequenced);
        }
        private void OnTimmyEffect(ulong sender,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||sender!=NetworkManager.ServerClientId||!reader.TryBeginRead(52))return;
            if(!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out int kind);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float tx);reader.ReadValueSafe(out float ty);reader.ReadValueSafe(out float tz);
            reader.ReadValueSafe(out float radius);reader.ReadValueSafe(out float duration);reader.ReadValueSafe(out double sent);
            if(slot<0||slot>=slots.Length||slots[slot]==null||kind<0||kind>6||
                !TimmyFxFinite(x)||!TimmyFxFinite(y)||!TimmyFxFinite(z)||!TimmyFxFinite(tx)||!TimmyFxFinite(ty)||!TimmyFxFinite(tz)||
                !TimmyFxFinite(radius)||radius<0||radius>6||!TimmyFxFinite(duration)||duration<0||duration>2.1f||
                double.IsNaN(sent)||double.IsInfinity(sent))return;
            GameObject actor=slots[slot].gameObject;if(actor.GetComponent<HeroIdentity>()?.Hero!=HeroId.Timmy)return;
            float elapsed=Mathf.Max(0,(float)(NetworkManager.Singleton.ServerTime.Time-sent));
            if(kind!=4&&kind!=6&&(duration<=0||elapsed>=duration+(kind==2||kind==3?.12f:0)))return;
            HeroSkillVfx.TimmyEffect(actor,kind,new Vector3(x,y,z),new Vector3(tx,ty,tz),radius,duration,false,elapsed);
        }
        public static void BroadcastVoidKnightEffect(GameObject actor,int kind,Vector3 from,Vector3 to,float radius,float duration)
        {
            if(HeroVfxEvents.InsideSemanticEffect)return;
            var service=audioRelay;var manager=NetworkManager.Singleton;
            if(actor==null||service==null||service.messaging==null||!CoopAuthority.Active||
                !CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer||actor.GetComponent<HeroIdentity>()?.Hero!=HeroId.VoidKnight)return;
            var slot=actor.GetComponent<CoopHeroSlot>();if(slot==null||slot.Slot<0||slot.Slot>=MatchSession.MaxPlayers)return;
            using FastBufferWriter writer=new(52,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(slot.Slot);writer.WriteValueSafe(kind);
            writer.WriteValueSafe(from.x);writer.WriteValueSafe(from.y);writer.WriteValueSafe(from.z);
            writer.WriteValueSafe(to.x);writer.WriteValueSafe(to.y);writer.WriteValueSafe(to.z);
            writer.WriteValueSafe(radius);writer.WriteValueSafe(duration);writer.WriteValueSafe(manager.ServerTime.Time);
            foreach(ulong id in manager.ConnectedClientsIds)
                if(id!=NetworkManager.ServerClientId)
                    service.messaging.SendNamedMessage(VoidKnightEffectMessage,id,writer,NetworkDelivery.ReliableSequenced);
        }
        private void OnVoidKnightEffect(ulong sender,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||sender!=NetworkManager.ServerClientId||!reader.TryBeginRead(52))return;
            if(!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out int kind);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float tx);reader.ReadValueSafe(out float ty);reader.ReadValueSafe(out float tz);
            reader.ReadValueSafe(out float radius);reader.ReadValueSafe(out float duration);reader.ReadValueSafe(out double sent);
            if(slot<0||slot>=slots.Length||slots[slot]==null||kind<0||kind>3||
                !TimmyFxFinite(x)||!TimmyFxFinite(y)||!TimmyFxFinite(z)||!TimmyFxFinite(tx)||!TimmyFxFinite(ty)||!TimmyFxFinite(tz)||
                !TimmyFxFinite(radius)||radius<0||radius>6||!TimmyFxFinite(duration)||duration<0||duration>2.1f||
                double.IsNaN(sent)||double.IsInfinity(sent))return;
            GameObject actor=slots[slot].gameObject;if(actor.GetComponent<HeroIdentity>()?.Hero!=HeroId.VoidKnight)return;
            float elapsed=Mathf.Max(0,(float)(NetworkManager.Singleton.ServerTime.Time-sent));
            if(kind==3){HeroSkillVfx.CancelVoidKnightWarnings(actor,false);return;}
            if(duration<=0||elapsed>=duration)return;
            Vector3 origin=new Vector3(x,y,z),direction=new Vector3(tx-x,ty-y,tz-z);
            HeroSkillVfx.VoidKnightField(actor,direction,radius,duration,kind!=1,kind==2,false,elapsed,origin);
        }
        private static bool TimmyFxFinite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);

    }
}
