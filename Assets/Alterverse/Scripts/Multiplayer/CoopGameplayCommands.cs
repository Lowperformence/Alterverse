using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Routes non-server player combat intentions to the authoritative NGO server. The server
    /// validates cast direction and ground position. Support, homing and automatic basics
    /// carry a target reference; automatic basics retain the host-validated target through contact.
    /// Clients never directly mutate authoritative combat state.
    /// </summary>
    public sealed class CoopGameplayCommands : MonoBehaviour
    {
        private const string BasicMessage="DF_COOP_BASIC_V7";
        private const string AbilityMessage="DF_COOP_ABILITY_V7";

        private const int TargetNone=0;
        private const int TargetHero=1;
        private const int TargetMinion=2;
        private const int TargetTower=3;
        private const int TargetCore=4;
        private const int TargetPlanet=5;
        private const int TargetOther=6;

        public static CoopGameplayCommands Instance { get; private set; }

        private CustomMessagingManager messaging;
        private bool attached;
        private CoopMatchPlan commandPlan;
        private uint nextCommandSequence;
        private readonly uint[] pendingActions=new uint[5];
        private readonly uint[] processedCommands=new uint[MatchSession.MaxPlayers];
        private GameObject pendingActor;
        private void EnsureCommandRound()
        {
            if(ReferenceEquals(commandPlan,CoopAuthority.ActivePlan))return;
            commandPlan=CoopAuthority.ActivePlan;nextCommandSequence=0;pendingActor=null;
            System.Array.Clear(pendingActions,0,pendingActions.Length);
            System.Array.Clear(processedCommands,0,processedCommands.Length);
        }
        public static uint ProcessedCommand(int rosterSlot)
        {
            if(Instance==null||rosterSlot<0||rosterSlot>=MatchSession.MaxPlayers)return 0;
            Instance.EnsureCommandRound();return Instance.processedCommands[rosterSlot];
        }
        public static bool CanApplyCooldown(GameObject actor,int abilitySlot,uint acknowledged)
        {
            if(Instance==null)return true;
            Instance.EnsureCommandRound();
            if(Instance.pendingActor!=actor)return true;
            uint pending=Instance.pendingActions[Mathf.Clamp(abilitySlot+1,0,4)];
            return pending==0||(acknowledged!=0&&unchecked((int)(acknowledged-pending))>=0);
        }
        private bool ProcessCommand(GameObject actor,uint sequence)
        {
            EnsureCommandRound();
            int index=actor.GetComponent<CoopHeroSlot>().Slot;
            uint previous=processedCommands[index];
            if(sequence==0||(previous!=0&&unchecked((int)(sequence-previous))<=0))return false;
            // Authorized sequences are acknowledged even if their target or gameplay rejects them.
            processedCommands[index]=sequence;return true;
        }
        private int receivedCombat,authorizedCombat,acceptedCombat,damagingHits,humanDamagingHits;
        private float removedHealthAndShield,humanHealthAndShieldRemoved,nextCombatReport;
        private readonly int[] frameMilliseconds=new int[201];
        private int sampledFrames;
        private float frameSeconds,maxFrameMilliseconds;
        private void SampleServerFrame()
        {
            float seconds=Time.unscaledDeltaTime;
            if(seconds<=0||!MatchNetworkProtocol.Finite(seconds))return;
            sampledFrames++;frameSeconds+=seconds;
            float milliseconds=seconds*1000f;maxFrameMilliseconds=Mathf.Max(maxFrameMilliseconds,milliseconds);
            frameMilliseconds[Mathf.Clamp(Mathf.CeilToInt(milliseconds),0,200)]++;
        }
        private string ConsumeFrameReport()
        {
            int threshold=Mathf.CeilToInt(sampledFrames*.95f),total=0,p95=0;
            for(int i=0;i<frameMilliseconds.Length;i++){total+=frameMilliseconds[i];if(total>=threshold){p95=i;break;}}
            string report=$"serverFps={(frameSeconds>0?sampledFrames/frameSeconds:0):F1}, frameP95Ms={(p95==200?">=200":p95.ToString())}, frameMaxMs={maxFrameMilliseconds:F1}";
            System.Array.Clear(frameMilliseconds,0,frameMilliseconds.Length);sampledFrames=0;frameSeconds=maxFrameMilliseconds=0;
            return report;
        }

        public static void RecordAuthoritativeDamage(GameObject actor,float amount)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(amount<=0||!MatchNetworkProtocol.Finite(amount)||Instance==null||!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer)return;
            Instance.damagingHits++;Instance.removedHealthAndShield+=amount;
            if(actor!=null&&actor.GetComponent<CoopHeroSlot>()?.IsHuman==true)
            {Instance.humanDamagingHits++;Instance.humanHealthAndShieldRemoved+=amount;}
        }
        private void ReportServerCombat(NetworkManager manager)
        {
            if(!manager.IsServer||!CoopAuthority.LocalHasServerAuthority)return;
            SampleServerFrame();
            if(Time.unscaledTime<nextCombatReport)return;
            nextCombatReport=Time.unscaledTime+10f;
            string frameReport=ConsumeFrameReport();
            if(receivedCombat==0&&damagingHits==0)return;
            Debug.Log($"DIMENSIONS FRONT COMBAT: received={receivedCombat}, authorized={authorizedCombat}, accepted={acceptedCombat}, damagingHits={damagingHits}, healthAndShieldRemoved={removedHealthAndShield:F1}, humanDamagingHits={humanDamagingHits}, humanHealthAndShieldRemoved={humanHealthAndShieldRemoved:F1}, {frameReport}.");
            receivedCombat=authorizedCombat=acceptedCombat=damagingHits=humanDamagingHits=0;removedHealthAndShield=humanHealthAndShieldRemoved=0;
        }
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
            ReportServerCombat(manager);
        }

        public static bool ShouldRouteClientCommand(GameObject actor)
        {
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||actor==null)return false;
            CoopHeroSlot slot=actor.GetComponent<CoopHeroSlot>();
            return slot!=null&&slot.IsHuman&&slot.IsLocalHuman;
        }

        public static bool TrySendBasic(GameObject actor, Health target)
            => target != null && TrySendBasic(actor, new AimPose(target.transform.position - actor.transform.position, target.transform.position, target, true));
        public static bool TrySendBasic(GameObject actor, AimPose pose)
            => Instance != null && Instance.SendCommand(actor, -1, pose);
        public static bool TrySendAbility(GameObject actor, int abilitySlot, Health target)
            => TrySendAbility(actor, abilitySlot, new AimPose(actor.transform.forward,
                target != null ? target.transform.position : actor.transform.position, target));
        public static bool TrySendAbility(GameObject actor, int abilitySlot, AimPose pose)
            => abilitySlot >= 0 && abilitySlot <= 3 && Instance != null && Instance.SendCommand(actor, abilitySlot, pose);

        private bool SendCommand(GameObject actor, int abilitySlot, AimPose pose)
        {
            if (!AimGeometry.Finite(pose.Direction) || !AimGeometry.Finite(pose.Point)) return false;
            Health target = pose.Target;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||manager.IsServer||!manager.IsConnectedClient||messaging==null)return false;
            CoopHeroSlot heroSlot=actor!=null?actor.GetComponent<CoopHeroSlot>():null;
            Health health=actor!=null?actor.GetComponent<Health>():null;
            if(heroSlot==null||!heroSlot.IsLocalHuman||health==null||health.IsDead)return false;

            FixedString128Bytes id=new(heroSlot.PlayerId??"");
            EnsureCommandRound();
            uint sequence=unchecked(nextCommandSequence+1);if(sequence==0)sequence=1;
            using FastBufferWriter writer=new(512,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(heroSlot.Slot);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(sequence);

            bool hasTarget=target!=null&&!target.IsDead&&target.isActiveAndEnabled;
            writer.WriteValueSafe(hasTarget);
            if(hasTarget)
            {
                int kind=TargetKind(target);
                int targetHeroSlot=-1;
                if(kind==TargetHero)
                {
                    CoopHeroSlot targetSlot=target.GetComponent<CoopHeroSlot>();
                    if(targetSlot!=null)targetHeroSlot=targetSlot.Slot;
                }
                int targetTeam=TargetTeam(target);
                Vector3 position=target.transform.position;
                writer.WriteValueSafe(kind);
                writer.WriteValueSafe(targetHeroSlot);
                writer.WriteValueSafe(targetTeam);
                writer.WriteValueSafe(position.x);writer.WriteValueSafe(position.y);writer.WriteValueSafe(position.z);
                if(kind!=TargetHero)
                {
                    CoopWorldEntityKey key=target.GetComponent<CoopWorldEntityKey>()??target.gameObject.AddComponent<CoopWorldEntityKey>();
                    key.Configure();
                    writer.WriteValueSafe(new FixedString128Bytes(key.Key));
                }
            }

            string message=abilitySlot<0?BasicMessage:AbilityMessage;
            if (abilitySlot >= 0) writer.WriteValueSafe(abilitySlot);
            writer.WriteValueSafe(pose.Direction.x); writer.WriteValueSafe(pose.Direction.y); writer.WriteValueSafe(pose.Direction.z);
            writer.WriteValueSafe(pose.Point.x); writer.WriteValueSafe(pose.Point.y); writer.WriteValueSafe(pose.Point.z);
            writer.WriteValueSafe(pose.HasAimPoint);
            // Optional trailing V5 field; old commands retain manual capture.
            if (!pose.AllowDashTargetCapture) writer.WriteValueSafe(false);
            messaging.SendNamedMessage(message,NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableSequenced);
            nextCommandSequence=sequence;pendingActor=actor;pendingActions[abilitySlot+1]=sequence;
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
            receivedCombat++;
            if(!TryReadAuthorizedActor(senderClientId,ref reader,out GameObject actor))return;
            authorizedCombat++;
            if(!reader.TryBeginRead(sizeof(uint)))return;
            reader.ReadValueSafe(out uint sequence);
            if(!ProcessCommand(actor,sequence))return;
            if(!TryReadTarget(actor,ref reader,out Health requestedTarget))return;
            if (!TryReadAim(ref reader, requestedTarget, out AimPose pose)) return;
            if(actor.GetComponent<BasicAttack>()?.TryAttackNetworkAuthoritative(pose)==true)acceptedCombat++;
        }

        private void OnAbility(ulong senderClientId,FastBufferReader reader)
        {
            receivedCombat++;
            if(!TryReadAuthorizedActor(senderClientId,ref reader,out GameObject actor))return;
            authorizedCombat++;
            if(!reader.TryBeginRead(sizeof(uint)))return;
            reader.ReadValueSafe(out uint sequence);
            if(!ProcessCommand(actor,sequence))return;
            if(!TryReadTarget(actor,ref reader,out Health requestedTarget))return;
            if (!reader.TryBeginRead(sizeof(int))) return;
            reader.ReadValueSafe(out int slot);
            if (slot < 0 || slot > 3 || !TryReadAim(ref reader, requestedTarget, out AimPose pose)) return;
            if(actor.GetComponent<PlayerHeroAbilities>()?.CastNetworkAuthoritative(slot, pose)==true)acceptedCombat++;
        }

        private static bool TryReadAim(ref FastBufferReader reader, Health target, out AimPose pose)
        {
            pose = default;
            if (!reader.TryBeginRead(6 * sizeof(float) + sizeof(bool))) return false;
            reader.ReadValueSafe(out float dx); reader.ReadValueSafe(out float dy); reader.ReadValueSafe(out float dz);
            reader.ReadValueSafe(out float px); reader.ReadValueSafe(out float py); reader.ReadValueSafe(out float pz);
            reader.ReadValueSafe(out bool hasAimPoint);
            bool allowDashTargetCapture = true;
            if (reader.TryBeginRead(sizeof(bool))) reader.ReadValueSafe(out allowDashTargetCapture);
            Vector3 direction = new Vector3(dx, dy, dz), point = new Vector3(px, py, pz);
            if (!AimGeometry.Finite(direction) || !AimGeometry.Finite(point) || AimGeometry.Flat(direction).sqrMagnitude < .0001f) return false;
            pose = new AimPose(direction, point, target, hasAimPoint, allowDashTargetCapture);
            return true;
        }

        private static bool TryReadTarget(GameObject actor,ref FastBufferReader reader,out Health target)
        {
            target=null;
            if(!reader.TryBeginRead(sizeof(bool)))return false;
            reader.ReadValueSafe(out bool hasTarget);
            if(!hasTarget)return true;

            if(!reader.TryBeginRead(3*sizeof(int)+3*sizeof(float)))return false;
            reader.ReadValueSafe(out int kind);
            reader.ReadValueSafe(out int heroSlot);
            reader.ReadValueSafe(out int targetTeam);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            if(kind<TargetHero||kind>TargetOther||
               !MatchNetworkProtocol.Finite(x)||!MatchNetworkProtocol.Finite(y)||!MatchNetworkProtocol.Finite(z))return false;

            if(kind==TargetHero)
            {
                if(heroSlot<0||heroSlot>=MatchSession.MaxPlayers)return false;
                // Health already maintains the live registry; do not allocate/search the scene per shot.
                var heroes=Health.Active;
                for(int i=0;i<heroes.Count;i++)
                {
                    Health health=heroes[i];
                    CoopHeroSlot slot=health!=null?health.GetComponent<CoopHeroSlot>():null;
                    if(slot==null||slot.Slot!=heroSlot)continue;
                    if(health!=null&&!health.IsDead&&health.isActiveAndEnabled)target=health;
                    return target!=null;
                }
                return false;
            }

            if(!reader.TryBeginRead(sizeof(ushort)))return false;
            reader.ReadValueSafe(out FixedString128Bytes fixedKey);
            string requestedKey=fixedKey.ToString();
            if(string.IsNullOrEmpty(requestedKey))return false;
            var candidates=Health.Active;
            for(int i=0;i<candidates.Count;i++)
            {
                Health candidate=candidates[i];
                if(candidate==null||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                CoopWorldEntityKey key=candidate.GetComponent<CoopWorldEntityKey>();
                if(key==null||key.Key!=requestedKey)continue;
                // Only classify/check the team of the exact requested entity.
                if(!MatchesTargetKind(candidate,kind,targetTeam))continue;
                target=candidate;return true;
            }
            // A missing/dead target rejects this intention; never substitute a nearby unit.
            return false;
        }

        private static int TargetKind(Health target)
        {
            if(target==null)return TargetNone;
            if(target.GetComponent<HeroIdentity>()!=null)return TargetHero;
            if(target.GetComponent<LaneMinion>()!=null)return TargetMinion;
            if(target.GetComponent<LaneTower>()!=null)return TargetTower;
            if(target.GetComponent<RealityCore>()!=null)return TargetCore;
            if(target.GetComponent<DimensionPlanet>()!=null)return TargetPlanet;
            return TargetOther;
        }

        private static int TargetTeam(Health target)
        {
            if(target==null)return int.MinValue;
            RealityCore core=target.GetComponent<RealityCore>();if(core!=null)return core.Team;
            LaneMinion minion=target.GetComponent<LaneMinion>();if(minion!=null)return minion.Team;
            LaneTower tower=target.GetComponent<LaneTower>();if(tower!=null)return tower.Team;
            TeamMember member=target.GetComponent<TeamMember>();return member!=null?member.Team:int.MinValue;
        }

        private static bool MatchesTargetKind(Health candidate,int kind,int targetTeam)
        {
            if(TargetKind(candidate)!=kind)return false;
            return targetTeam==int.MinValue||TargetTeam(candidate)==targetTeam;
        }

        private bool TryReadAuthorizedActor(ulong senderClientId,ref FastBufferReader reader,out GameObject actor)
        {
            actor=null;
            NetworkManager manager=NetworkManager.Singleton;
            if(!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer||Time.timeScale<=0f)return false;

            if (!MatchNetworkProtocol.ReadRound(ref reader)) return false;
            if (!reader.TryBeginRead(sizeof(int) + sizeof(ushort))) return false;
            reader.ReadValueSafe(out int rosterSlot);
            reader.ReadValueSafe(out FixedString128Bytes fixedId);
            string playerId=fixedId.ToString();

            CoopMatchPlan plan=CoopAuthority.ActivePlan;
            if(plan?.Session?.Slots==null||rosterSlot<0||rosterSlot>=MatchSession.MaxPlayers)return false;
            MatchPlayerSlot planned=plan.Session.Slots[rosterSlot];
            if(planned==null||planned.IsBot||planned.IsHost||planned.PlayerId!=playerId)return false;

            bool authorized=MatchNetworkProtocol.Authorized(senderClientId,playerId);
            if(!authorized)return false;
            senderOwners[senderClientId]=playerId;

            var heroes=Health.Active;
            for(int i=0;i<heroes.Count;i++)
            {
                Health health=heroes[i];
                CoopHeroSlot h=health!=null?health.GetComponent<CoopHeroSlot>():null;
                if(h!=null&&h.Slot==rosterSlot&&h.PlayerId==playerId)
                {
                    if (health == null || health.IsDead || !health.isActiveAndEnabled) return false;
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



