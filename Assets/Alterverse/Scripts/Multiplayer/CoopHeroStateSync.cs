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
    /// Lightweight custom-message synchronization for the ten deterministic hero slots.
    /// Human owners send only their movement pose to the authoritative server. The server validates it,
    /// runs authoritative gameplay, and broadcasts hero pose/health snapshots to clients.
    /// </summary>
    public sealed class CoopHeroStateSync : MonoBehaviour
    {
        private static CoopHeroStateSync audioRelay;
        private const string SharkEffectMessage="DF_COOP_SHARK_FX_V2";
        private const string OrunOverdriveMessage="DF_COOP_ORUN_OVERDRIVE_FX_V1";
        private const string KillSoundMessage="DF_COOP_KILL_AUDIO_V2";
        private const string CombatSoundMessage="DF_COOP_COMBAT_AUDIO_V2";
        private const string OwnerPoseMessage="DF_COOP_OWNER_POSE_V3";
        private const string HeroSnapshotMessage="DF_COOP_HERO_SNAPSHOT_V4";
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

        private void OnEnable(){audioRelay=this;RefreshSlots(true);}
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
            messaging.RegisterNamedMessageHandler(OwnerPoseMessage,OnOwnerPose);
            messaging.RegisterNamedMessageHandler(HeroSnapshotMessage,OnHeroSnapshot);
            messaging.RegisterNamedMessageHandler(CombatSoundMessage,OnCombatSound);
            messaging.RegisterNamedMessageHandler(KillSoundMessage,OnKillSound);
            messaging.RegisterNamedMessageHandler(SharkEffectMessage,OnSharkEffect);
            messaging.RegisterNamedMessageHandler(OrunOverdriveMessage,OnOrunOverdrive);
            attached=true;
        }

        private void Detach()
        {
            if(!attached||messaging==null){messaging=null;attached=false;return;}
            messaging.UnregisterNamedMessageHandler(OwnerPoseMessage);
            messaging.UnregisterNamedMessageHandler(HeroSnapshotMessage);
            messaging.UnregisterNamedMessageHandler(CombatSoundMessage);
            messaging.UnregisterNamedMessageHandler(KillSoundMessage);
            messaging.UnregisterNamedMessageHandler(SharkEffectMessage);
            messaging.UnregisterNamedMessageHandler(OrunOverdriveMessage);
            messaging=null;attached=false;
            senderOwners.Clear();
            senderLastPoseAt.Clear();
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

        private void SendOwnerPose(NetworkManager manager)
        {
            if(manager==null||manager.IsServer||messaging==null||localSlot<0)return;
            CoopHeroSlot slot=slots[localSlot];
            Health health=slot!=null?slot.GetComponent<Health>():null;
            if(slot==null||health==null||health.IsDead)return;

            Vector3 p=slot.transform.position;
            float yaw=slot.transform.eulerAngles.y;
            FixedString128Bytes id=new(localPlayerId??"");

            using FastBufferWriter writer=new(256,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(localSlot);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(p.x);writer.WriteValueSafe(p.y);writer.WriteValueSafe(p.z);
            writer.WriteValueSafe(yaw);
            Vector3 run=slot.GetComponent<HeroMotor>()?.SmoothedVelocity??Vector3.zero;
            writer.WriteValueSafe(run.x);writer.WriteValueSafe(run.z);
            messaging.SendNamedMessage(OwnerPoseMessage,NetworkManager.ServerClientId,writer,NetworkDelivery.UnreliableSequenced);
        }

        private void OnOwnerPose(ulong senderClientId,FastBufferReader reader)
        {
            if(!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||Time.timeScale<=0f)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsServer)return;

            if (!MatchNetworkProtocol.ReadRound(ref reader)) return;
            reader.ReadValueSafe(out int rosterSlot);
            reader.ReadValueSafe(out FixedString128Bytes fixedPlayerId);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float yaw);
            reader.ReadValueSafe(out float runX);reader.ReadValueSafe(out float runZ);
            if(!MatchNetworkProtocol.Finite(runX)||!MatchNetworkProtocol.Finite(runZ))return;

            if (!MatchNetworkProtocol.Finite(x) || !MatchNetworkProtocol.Finite(y) || !MatchNetworkProtocol.Finite(z) || !MatchNetworkProtocol.Finite(yaw)) return;
            string playerId=fixedPlayerId.ToString();
            if(!ValidateOwner(senderClientId,rosterSlot,playerId))return;

            CoopHeroSlot actor=rosterSlot>=0&&rosterSlot<slots.Length?slots[rosterSlot]:null;
            Health health=actor!=null?actor.GetComponent<Health>():null;
            if(actor==null||health==null||health.IsDead)return;

            Vector3 desired=GameplayCollision.ClampToRoundedArena(new Vector3(x,y,z),40f,33f);
            desired.y=actor.transform.position.y;
            Vector3 delta=desired-actor.transform.position;delta.y=0;

            HeroLocomotion locomotion=actor.GetComponent<HeroLocomotion>();
            if(locomotion!=null&&(locomotion.IsDashing||locomotion.IsForcedMoving))return;
            locomotion?.SetRemoteRunVelocity(new Vector3(runX,0f,runZ));
            if(locomotion!=null&&locomotion.IsVortexPulled)
            {
                actor.transform.rotation=Quaternion.Euler(0,Mathf.Repeat(yaw,360f),0);
                return;
            }

            float now=Time.unscaledTime;
            float dt=senderLastPoseAt.TryGetValue(senderClientId,out float previous)
                ?Mathf.Clamp(now-previous,.015f,.25f):OwnerSendInterval*2f;
            senderLastPoseAt[senderClientId]=now;

            HeroCombatProfile profile=actor.GetComponent<HeroCombatProfile>();
            PlanetBuffReceiver buff=actor.GetComponent<PlanetBuffReceiver>();
            float baseSpeed=profile!=null?profile.MoveSpeed:6.5f;
            float buffSpeed=buff!=null?buff.MoveSpeedMultiplier:1f;
            float statusSpeed=locomotion!=null?locomotion.SpeedMultiplier:HeroCombatStatus.Speed(actor.gameObject);
            float allowed=Mathf.Max(MinimumMovementSlack,baseSpeed*buffSpeed*statusSpeed*dt*1.40f+MinimumMovementSlack);
            if(delta.magnitude>allowed)delta=delta.normalized*allowed;

            if(locomotion!=null)locomotion.Move(delta,40f,33f);
            else actor.transform.position=GameplayCollision.ClampToRoundedArena(actor.transform.position+delta,40f,33f);
            actor.transform.rotation=Quaternion.Euler(0,Mathf.Repeat(yaw,360f),0);
        }

        private bool ValidateOwner(ulong senderClientId,int rosterSlot,string playerId)
        {
            CoopMatchPlan plan=CoopAuthority.ActivePlan;
            if(plan?.Session?.Slots==null||rosterSlot<0||rosterSlot>=MatchSession.MaxPlayers)return false;
            MatchPlayerSlot slot=plan.Session.Slots[rosterSlot];
            if(slot==null||slot.IsBot||slot.IsHost||slot.PlayerId!=playerId)return false;

            bool authorized=MatchNetworkProtocol.Authorized(senderClientId,playerId);
            if(!authorized)return false;
            senderOwners[senderClientId]=playerId;
            return true;
        }

        private void BroadcastHostSnapshot(NetworkManager manager)
        {
            if(manager==null||!manager.IsServer||messaging==null||manager.ConnectedClientsIds==null)return;

            int count=0;
            for(int i=0;i<slots.Length;i++)if(slots[i]!=null)count++;
            if(count==0)return;

            using FastBufferWriter writer=new(2048,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
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
                HeroCombatStatus status=slot.GetComponent<HeroCombatStatus>();
                writer.WriteValueSafe(status!=null&&status.Stunned);
                writer.WriteValueSafe(slot.GetComponent<HeroLocomotion>()?.IsForcedMoving==true);
                writer.WriteValueSafe(status!=null?status.MoveMultiplier:1f);
                HeroLocomotion body=slot.GetComponent<HeroLocomotion>();
                writer.WriteValueSafe(body!=null&&body.IsVortexPulled);
                Vector3 pull=body!=null?body.VortexCenter:Vector3.zero;
                writer.WriteValueSafe(pull.x);writer.WriteValueSafe(pull.y);writer.WriteValueSafe(pull.z);
                writer.WriteValueSafe(body!=null?body.VortexRadius:0f);
                HeroRespawn respawn=slot.GetComponent<HeroRespawn>();
                writer.WriteValueSafe(respawn!=null&&respawn.IsRespawning);
                writer.WriteValueSafe(respawn!=null?respawn.RemainingSeconds:0f);
                BasicAttack basic=slot.GetComponent<BasicAttack>();
                PlayerHeroAbilities abilities=slot.GetComponent<PlayerHeroAbilities>();
                writer.WriteValueSafe(basic!=null?basic.RemainingCooldown:0f);
                for(int ability=0;ability<4;ability++)
                    writer.WriteValueSafe(abilities!=null?abilities.RemainingFor(ability):0f);
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
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||senderClientId!=NetworkManager.ServerClientId)return;

            if (!MatchNetworkProtocol.ReadRound(ref reader)) return;
            reader.ReadValueSafe(out int count);
            if (count < 0 || count > MatchSession.MaxPlayers) return;
            for(int n=0;n<count;n++)
            {
                reader.ReadValueSafe(out int rosterSlot);
                reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
                reader.ReadValueSafe(out float yaw);
                reader.ReadValueSafe(out float current);
                reader.ReadValueSafe(out float shield);
                reader.ReadValueSafe(out bool dead);
                reader.ReadValueSafe(out bool stunned);
                reader.ReadValueSafe(out bool forcedMoving);
                reader.ReadValueSafe(out float moveMultiplier);
                reader.ReadValueSafe(out bool vortexPulled);
                reader.ReadValueSafe(out float pullX);reader.ReadValueSafe(out float pullY);reader.ReadValueSafe(out float pullZ);
                reader.ReadValueSafe(out float pullRadius);
                reader.ReadValueSafe(out bool respawning);
                reader.ReadValueSafe(out float respawnRemaining);
                reader.ReadValueSafe(out float basicRemaining);
                reader.ReadValueSafe(out float qRemaining);
                reader.ReadValueSafe(out float wRemaining);
                reader.ReadValueSafe(out float eRemaining);
                reader.ReadValueSafe(out float ultRemaining);

                if(rosterSlot<0||rosterSlot>=slots.Length)continue;
                CoopHeroSlot actor=slots[rosterSlot];if(actor==null)continue;
                Health health=actor.GetComponent<Health>();if(health==null)continue;

                Vector3 hostPosition=new(x,y,z);
                bool localOwner=actor.IsLocalHuman;
                float errorSqr=(actor.transform.position-hostPosition).sqrMagnitude;
                if(!localOwner||dead||forcedMoving||errorSqr>36f)
                    actor.transform.SetPositionAndRotation(hostPosition,Quaternion.Euler(0,yaw,0));
                else if(localOwner&&errorSqr>.49f)
                    actor.transform.position=Vector3.Lerp(actor.transform.position,hostPosition,.42f);
                if(!localOwner)actor.transform.rotation=Quaternion.Euler(0,yaw,0);
                if(!localOwner)actor.GetComponent<HeroVisualMotion>()?.ReceivePresentationSnapshot(hostPosition,
                    Quaternion.Euler(0,yaw,0),dead||respawning||errorSqr>9f,forcedMoving);

                health.ApplyNetworkSnapshot(current,shield,dead);
                HeroCombatStatus status=actor.GetComponent<HeroCombatStatus>()??actor.gameObject.AddComponent<HeroCombatStatus>();
                status.ApplyNetworkSnapshot(stunned,forcedMoving?0f:moveMultiplier);
                HeroLocomotion body=actor.GetComponent<HeroLocomotion>()??actor.gameObject.AddComponent<HeroLocomotion>();
                body.SetVortexPull(new Vector3(pullX,pullY,pullZ),pullRadius,vortexPulled?.25f:0f);
                actor.GetComponent<CoopRespawnReplica>()?.Apply(respawning,respawnRemaining);
                if(actor.IsLocalHuman)
                {
                    actor.GetComponent<BasicAttack>()?.ApplyNetworkCooldown(basicRemaining);
                    PlayerHeroAbilities localAbilities=actor.GetComponent<PlayerHeroAbilities>();
                    if(localAbilities!=null)
                    {
                        localAbilities.ApplyNetworkCooldown(0,qRemaining);
                        localAbilities.ApplyNetworkCooldown(1,wRemaining);
                        localAbilities.ApplyNetworkCooldown(2,eRemaining);
                        localAbilities.ApplyNetworkCooldown(3,ultRemaining);
                    }
                }
                actor.GetComponent<CoopHeroReplicaPresentation>()?.Apply(dead);
            }
        }
    }
}



