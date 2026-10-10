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
    /// validates cast direction and ground position; only support/homing carry a target reference.
    /// Clients never directly mutate authoritative combat state.
    /// </summary>
    public sealed class CoopGameplayCommands : MonoBehaviour
    {
        private const string BasicMessage="DF_COOP_BASIC_V4";
        private const string AbilityMessage="DF_COOP_ABILITY_V4";

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

        public static bool TrySendBasic(GameObject actor, Health target)
            => target != null && TrySendBasic(actor, new AimPose(target.transform.position - actor.transform.position, target.transform.position));
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
            using FastBufferWriter writer=new(232,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(heroSlot.Slot);
            writer.WriteValueSafe(id);

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
            }

            string message=abilitySlot<0?BasicMessage:AbilityMessage;
            if (abilitySlot >= 0) writer.WriteValueSafe(abilitySlot);
            writer.WriteValueSafe(pose.Direction.x); writer.WriteValueSafe(pose.Direction.y); writer.WriteValueSafe(pose.Direction.z);
            writer.WriteValueSafe(pose.Point.x); writer.WriteValueSafe(pose.Point.y); writer.WriteValueSafe(pose.Point.z);
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
            if(!TryReadAuthorizedActor(senderClientId,ref reader,out GameObject actor))return;
            if(!TryReadTarget(actor,ref reader,out Health requestedTarget))return;
            if (!TryReadAim(ref reader, requestedTarget, out AimPose pose)) return;
            actor.GetComponent<BasicAttack>()?.TryAttackNetworkAuthoritative(pose);
        }

        private void OnAbility(ulong senderClientId,FastBufferReader reader)
        {
            if(!TryReadAuthorizedActor(senderClientId,ref reader,out GameObject actor))return;
            if(!TryReadTarget(actor,ref reader,out Health requestedTarget))return;
            if (!reader.TryBeginRead(sizeof(int))) return;
            reader.ReadValueSafe(out int slot);
            if (slot < 0 || slot > 3 || !TryReadAim(ref reader, requestedTarget, out AimPose pose)) return;
            actor.GetComponent<PlayerHeroAbilities>()?.CastNetworkAuthoritative(slot, pose);
        }

        private static bool TryReadAim(ref FastBufferReader reader, Health target, out AimPose pose)
        {
            pose = default;
            if (!reader.TryBeginRead(6 * sizeof(float))) return false;
            reader.ReadValueSafe(out float dx); reader.ReadValueSafe(out float dy); reader.ReadValueSafe(out float dz);
            reader.ReadValueSafe(out float px); reader.ReadValueSafe(out float py); reader.ReadValueSafe(out float pz);
            Vector3 direction = new Vector3(dx, dy, dz), point = new Vector3(px, py, pz);
            if (!AimGeometry.Finite(direction) || !AimGeometry.Finite(point) || AimGeometry.Flat(direction).sqrMagnitude < .0001f) return false;
            pose = new AimPose(direction, point, target);
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

            if(kind==TargetHero&&heroSlot>=0&&heroSlot<MatchSession.MaxPlayers)
            {
                CoopHeroSlot[] heroes=FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
                for(int i=0;i<heroes.Length;i++)
                {
                    CoopHeroSlot slot=heroes[i];
                    if(slot==null||slot.Slot!=heroSlot)continue;
                    Health health=slot.GetComponent<Health>();
                    if(health!=null&&!health.IsDead&&health.isActiveAndEnabled)target=health;
                    return true;
                }
                return true;
            }

            Vector3 requestedPosition=new(x,y,z);
            float bestSqr=float.MaxValue;
            var candidates=Health.Active;
            for(int i=0;i<candidates.Count;i++)
            {
                Health candidate=candidates[i];
                if(candidate==null||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                if(!MatchesTargetKind(candidate,kind,targetTeam))continue;
                Vector3 delta=candidate.transform.position-requestedPosition;delta.y=0f;
                float sqr=delta.sqrMagnitude;
                if(sqr<bestSqr){bestSqr=sqr;target=candidate;}
            }
            return true;
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

            CoopHeroSlot[] heroes=FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
            for(int i=0;i<heroes.Length;i++)
            {
                CoopHeroSlot h=heroes[i];
                if(h!=null&&h.Slot==rosterSlot&&h.PlayerId==playerId)
                {
                    Health health=h.GetComponent<Health>();
                    if (health == null || health.IsDead) return false;
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
