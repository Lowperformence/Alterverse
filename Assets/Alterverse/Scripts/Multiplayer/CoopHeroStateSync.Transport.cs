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
    public sealed partial class CoopHeroStateSync
    {
        private readonly OwnerPoseHistory ownerPoseHistory=new();
        private readonly uint[] ownerAcknowledgements=new uint[MatchSession.MaxPlayers];
        private readonly Vector3[] acknowledgedPositions=new Vector3[MatchSession.MaxPlayers];
        private readonly Dictionary<ulong,uint> receivedOwnerSequences=new();
        private readonly uint[] receivedMovementRevisions=new uint[MatchSession.MaxPlayers];
        private uint poseRound;
        private bool poseRoundReady;

        private void EnsurePoseRound()
        {
            uint round=CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan);
            if(poseRoundReady&&poseRound==round)return;
            ResetPoseTransport();senderOwners.Clear();senderLastPoseAt.Clear();
            poseRound=round;poseRoundReady=true;
        }

        private void ResetPoseTransport()
        {
            poseRoundReady=false;
            ownerPoseHistory.Clear(true);
            System.Array.Clear(ownerAcknowledgements,0,ownerAcknowledgements.Length);
            System.Array.Clear(acknowledgedPositions,0,acknowledgedPositions.Length);
            receivedOwnerSequences.Clear();
            System.Array.Clear(receivedMovementRevisions,0,receivedMovementRevisions.Length);
        }

        private void SendOwnerPose(NetworkManager manager)
        {
            if(manager==null||manager.IsServer||messaging==null||localSlot<0)return;
            CoopHeroSlot slot=slots[localSlot];
            Health health=slot!=null?slot.GetComponent<Health>():null;
            if(slot==null||health==null||health.IsDead)return;

            EnsurePoseRound();
            Vector3 p=slot.transform.position;
            float yaw=slot.transform.eulerAngles.y;
            FixedString128Bytes id=new(localPlayerId??"");

            using FastBufferWriter writer=new(256,Allocator.Temp);
            writer.WriteValueSafe(poseRound); // EnsurePoseRound already computed this packet's identity.
            writer.WriteValueSafe(localSlot);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(ownerPoseHistory.Record(p));
            writer.WriteValueSafe(receivedMovementRevisions[localSlot]);
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
            EnsurePoseRound();
            reader.ReadValueSafe(out int rosterSlot);
            reader.ReadValueSafe(out FixedString128Bytes fixedPlayerId);
            reader.ReadValueSafe(out uint ownerSequence);
            reader.ReadValueSafe(out uint movementRevision);
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
            // Sequence acknowledgements identify the prediction being corrected, rather than
            // comparing an old server pose with the owner's newer, already advanced position.
            if(ownerSequence==0||(receivedOwnerSequences.TryGetValue(senderClientId,out uint previousSequence)&&
                !OwnerPoseHistory.Newer(ownerSequence,previousSequence)))return;
            receivedOwnerSequences[senderClientId]=ownerSequence;
            HeroLocomotion locomotion=actor.GetComponent<HeroLocomotion>();
            // Old owner poses must never undo a server dash/hook, including its final endpoint.
            if(locomotion!=null&&(movementRevision!=locomotion.MovementRevision||locomotion.IsDashing||locomotion.IsForcedMoving))return;
            HeroCombatStatus combatStatus=actor.GetComponent<HeroCombatStatus>();
            if(combatStatus!=null&&(combatStatus.Rooted||combatStatus.Stunned))
            {
                ownerAcknowledgements[rosterSlot]=ownerSequence;
                acknowledgedPositions[rosterSlot]=actor.transform.position;
                return;
            }

            Vector3 desired=GameplayCollision.ClampToRoundedArena(new Vector3(x,y,z),40f,33f);
            desired.y=actor.transform.position.y;
            Vector3 delta=desired-actor.transform.position;delta.y=0;

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
            ownerAcknowledgements[rosterSlot]=ownerSequence;
            acknowledgedPositions[rosterSlot]=actor.transform.position;
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

            EnsurePoseRound();
            int count=0;
            for(int i=0;i<slots.Length;i++)if(slots[i]!=null&&slots[i].GetComponent<Health>()!=null)count++;
            if(count==0)return;

            using FastBufferWriter writer=new(2048,Allocator.Temp);
            writer.WriteValueSafe(poseRound); // EnsurePoseRound already computed this packet's identity.
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
                writer.WriteValueSafe(status!=null&&(status.Stunned||status.ActionLocked));
                writer.WriteValueSafe(slot.GetComponent<HeroLocomotion>() is HeroLocomotion movement&&(movement.IsDashing||movement.IsForcedMoving));
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
                writer.WriteValueSafe(body!=null?body.MovementRevision:0u);
                writer.WriteValueSafe(CoopGameplayCommands.ProcessedCommand(i));
                writer.WriteValueSafe(ownerAcknowledgements[i]);
                Vector3 acknowledgedPosition=acknowledgedPositions[i];
                writer.WriteValueSafe(acknowledgedPosition.x);writer.WriteValueSafe(acknowledgedPosition.y);writer.WriteValueSafe(acknowledgedPosition.z);
                int kills=0,deaths=0,assists=0;
                MatchScoreboard.Instance?.GetNetworkScore(health,out kills,out deaths,out assists);
                writer.WriteValueSafe(kills);writer.WriteValueSafe(deaths);writer.WriteValueSafe(assists);
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
            EnsurePoseRound();
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
                reader.ReadValueSafe(out uint movementRevision);
                reader.ReadValueSafe(out uint acknowledgedCommand);
                reader.ReadValueSafe(out uint acknowledgedPose);
                reader.ReadValueSafe(out float acknowledgedX);reader.ReadValueSafe(out float acknowledgedY);reader.ReadValueSafe(out float acknowledgedZ);
                reader.ReadValueSafe(out int kills);reader.ReadValueSafe(out int deaths);reader.ReadValueSafe(out int assists);

                if(rosterSlot<0||rosterSlot>=slots.Length)continue;
                CoopHeroSlot actor=slots[rosterSlot];if(actor==null)continue;
                Health health=actor.GetComponent<Health>();if(health==null)continue;

                Vector3 hostPosition=new(x,y,z);
                bool localOwner=actor.IsLocalHuman;
                float errorSqr=(actor.transform.position-hostPosition).sqrMagnitude;
                bool timmyChannel=actor.GetComponent<HeroIdentity>()?.Hero==HeroId.Timmy&&stunned&&moveMultiplier<=.001f;
                bool movementChanged=receivedMovementRevisions[rosterSlot]!=movementRevision;
                receivedMovementRevisions[rosterSlot]=movementRevision;
                bool relocation=dead||respawning||forcedMoving||movementChanged||timmyChannel||(!stunned&&moveMultiplier<=.001f);
                if(!localOwner||relocation)
                {
                    actor.transform.SetPositionAndRotation(hostPosition,Quaternion.Euler(0,yaw,0));
                    if(localOwner)ownerPoseHistory.Clear();
                }
                else if(ownerPoseHistory.TryReconcile(acknowledgedPose,new Vector3(acknowledgedX,acknowledgedY,acknowledgedZ),actor.transform.position,out Vector3 corrected))
                    actor.transform.position=corrected;
                if(!localOwner)actor.transform.rotation=Quaternion.Euler(0,yaw,0);
                if(!localOwner)actor.GetComponent<HeroVisualMotion>()?.ReceivePresentationSnapshot(hostPosition,
                    Quaternion.Euler(0,yaw,0),dead||respawning||errorSqr>9f,forcedMoving);

                health.ApplyNetworkSnapshot(current,shield,dead);
                MatchScoreboard.Instance?.ApplyNetworkScore(health,kills,deaths,assists);
                HeroCombatStatus status=actor.GetComponent<HeroCombatStatus>()??actor.gameObject.AddComponent<HeroCombatStatus>();
                status.ApplyNetworkSnapshot(stunned,forcedMoving?0f:moveMultiplier);
                HeroLocomotion body=actor.GetComponent<HeroLocomotion>()??actor.gameObject.AddComponent<HeroLocomotion>();
                body.SetVortexPull(new Vector3(pullX,pullY,pullZ),pullRadius,vortexPulled?.25f:0f);
                actor.GetComponent<CoopRespawnReplica>()?.Apply(respawning,respawnRemaining);
                if(actor.IsLocalHuman)
                {
                    actor.GetComponent<BasicAttack>()?.ApplyNetworkCooldown(basicRemaining,acknowledgedCommand);
                    PlayerHeroAbilities localAbilities=actor.GetComponent<PlayerHeroAbilities>();
                    if(localAbilities!=null)
                    {
                        localAbilities.ApplyNetworkCooldown(0,qRemaining,acknowledgedCommand);
                        localAbilities.ApplyNetworkCooldown(1,wRemaining,acknowledgedCommand);
                        localAbilities.ApplyNetworkCooldown(2,eRemaining,acknowledgedCommand);
                        localAbilities.ApplyNetworkCooldown(3,ultRemaining,acknowledgedCommand);
                    }
                }
                actor.GetComponent<CoopHeroReplicaPresentation>()?.Apply(dead);
            }
        }
    }
}
