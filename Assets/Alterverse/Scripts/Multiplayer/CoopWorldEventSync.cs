using Alterverse.Combat;
using Alterverse.Characters;
using Alterverse.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Replicates sparse deterministic battlefield events. Damage/scoring remain server-owned;
    /// clients receive the exact Hot Zone position, planet spawn and bomber route seed for visuals.
    /// </summary>
    public sealed class CoopWorldEventSync : MonoBehaviour
    {
        private const string AttackMotionMessage="DF_COOP_ATTACK_MOTION_V1";
        private const string MonsterImpactMessage="DF_COOP_MONSTER_IMPACT_V1";
        private const string MonsterLaserMessage="DF_COOP_MONSTER_LASER_V1";
        private const string HotOpenMessage="DF_COOP_HOT_OPEN_V2";
        private const string HotResolveMessage="DF_COOP_HOT_RESOLVE_V2";
        private const string HotProgressMessage="DF_COOP_HOT_PROGRESS_V1";
        private const string PlanetMessage="DF_COOP_PLANET_V2";
        private const string BomberMessage="DF_COOP_BOMBER_V2";
        private const string PlanetMeteorMessage="DF_COOP_PLANET_METEOR_V2";
        private const string NoxaraMeteorMessage="DF_COOP_NOXARA_METEOR_V2";
        private const string PlanetPulseStartMessage="DF_COOP_PLANET_PULSE_START_V2";
        private const string PlanetPulseDetonateMessage="DF_COOP_PLANET_PULSE_DETONATE_V2";
        private const string CentralHealMessage="DF_COOP_CENTRAL_HEAL_V2";
        private const string PlanetBuffMessage="DF_COOP_PLANET_BUFF_V2";
        private const string LaneUnitMessage="DF_COOP_LANE_UNIT_V2";
        private const string FractureSpawnMessage="DF_COOP_FRACTURE_SPAWN_V2";
        private const string FractureClaimMessage="DF_COOP_FRACTURE_CLAIM_V2";

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
            HotZoneDirector.NetworkZoneProgress+=OnHostHotProgress;
            DimensionPlanetDirector.NetworkPlanetSpawned+=OnHostPlanetSpawned;
            SpaceBomberDirector.NetworkFlyoverSpawned+=OnHostBomberSpawned;
            PlanetMeteorShower.NetworkMeteorSpawned+=OnHostPlanetMeteor;
            NoxaraSpiderMeteorShower.NetworkMeteorSpawned+=OnHostNoxaraMeteor;
            PlanetPulseAttack.NetworkPulseStarted+=OnHostPlanetPulseStart;
            PlanetPulseAttack.NetworkPulseDetonated+=OnHostPlanetPulseDetonate;
            CentralHealingPickup.NetworkAvailabilityChanged+=OnHostCentralHealChanged;
            DimensionPlanet.NetworkBuffGranted+=OnHostPlanetBuffGranted;
            LaneWaveSpawner.NetworkUnitSpawned+=OnHostLaneUnitSpawned;
            FractureCorePickup.NetworkSpawned+=OnHostFractureSpawned;
            FractureCorePickup.NetworkClaimed+=OnHostFractureClaimed;
            Alterverse.Visuals.CombatMotion.Started+=OnAttackStarted;
            Alterverse.Visuals.MonsterAttackVfx.Resolved+=OnMonsterImpact;
            Alterverse.Visuals.MonsterAttackVfx.LaserChanged+=OnMonsterLaser;
            eventsBound=true;
        }

        private void UnbindEvents()
        {
            if(!eventsBound)return;
            HotZoneDirector.NetworkZoneOpened-=OnHostHotOpened;
            HotZoneDirector.NetworkZoneResolved-=OnHostHotResolved;
            HotZoneDirector.NetworkZoneProgress-=OnHostHotProgress;
            DimensionPlanetDirector.NetworkPlanetSpawned-=OnHostPlanetSpawned;
            SpaceBomberDirector.NetworkFlyoverSpawned-=OnHostBomberSpawned;
            PlanetMeteorShower.NetworkMeteorSpawned-=OnHostPlanetMeteor;
            NoxaraSpiderMeteorShower.NetworkMeteorSpawned-=OnHostNoxaraMeteor;
            PlanetPulseAttack.NetworkPulseStarted-=OnHostPlanetPulseStart;
            PlanetPulseAttack.NetworkPulseDetonated-=OnHostPlanetPulseDetonate;
            CentralHealingPickup.NetworkAvailabilityChanged-=OnHostCentralHealChanged;
            DimensionPlanet.NetworkBuffGranted-=OnHostPlanetBuffGranted;
            LaneWaveSpawner.NetworkUnitSpawned-=OnHostLaneUnitSpawned;
            FractureCorePickup.NetworkSpawned-=OnHostFractureSpawned;
            FractureCorePickup.NetworkClaimed-=OnHostFractureClaimed;
            Alterverse.Visuals.CombatMotion.Started-=OnAttackStarted;
            Alterverse.Visuals.MonsterAttackVfx.Resolved-=OnMonsterImpact;
            Alterverse.Visuals.MonsterAttackVfx.LaserChanged-=OnMonsterLaser;
            eventsBound=false;
        }

        private void OnAttackStarted(GameObject actor,float windup,Vector3 aim)
        {
            if(!CanBroadcast())return;
            var hero=actor.GetComponent<CoopHeroSlot>();var entity=actor.GetComponent<CoopWorldEntityKey>();
            if(hero==null&&entity==null&&(actor.GetComponent<HotZoneTank>()!=null||actor.GetComponent<HealingGuardMech>()!=null))
            {WorldKey(actor);entity=actor.GetComponent<CoopWorldEntityKey>();}
            if(hero==null&&entity==null)return;
            using FastBufferWriter writer=new(256,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(hero!=null?hero.Slot:-1);
            writer.WriteValueSafe(new FixedString128Bytes(entity!=null?entity.Key:""));
            writer.WriteValueSafe(windup);writer.WriteValueSafe(aim.x);writer.WriteValueSafe(aim.y);writer.WriteValueSafe(aim.z);
            writer.WriteValueSafe(NetworkManager.Singleton.ServerTime.Time);
            SendAll(AttackMotionMessage,writer);
        }
        private void OnAttackMotion(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int slot);reader.ReadValueSafe(out FixedString128Bytes key);
            reader.ReadValueSafe(out float windup);reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out double sent);
            if(float.IsNaN(windup)||windup<.001f||windup>2f||!Finite(x)||!Finite(y)||!Finite(z)||double.IsNaN(sent)||double.IsInfinity(sent))return;
            float elapsed=Mathf.Max(0,(float)(NetworkManager.Singleton.ServerTime.Time-sent));
            if(elapsed>windup+.5f)return;
            GameObject actor=null;
            for(int i=0;i<Health.Active.Count;i++)
            {
                var hp=Health.Active[i];if(hp==null||hp.IsDead)continue;
                if(slot>=0)
                {var hero=hp.GetComponent<CoopHeroSlot>();if(hero!=null&&hero.Slot==slot){if(hero.IsLocalHuman&&!(hp.GetComponent<HeroIdentity>()?.Hero==HeroId.Timmy&&windup<=.00101f))return;actor=hp.gameObject;break;}}
                else {var entity=hp.GetComponent<CoopWorldEntityKey>();if(entity!=null&&entity.Key==key.ToString()){actor=hp.gameObject;break;}}
            }
            if(actor==null)return;
            Alterverse.Visuals.CombatMotion.Present(actor,windup,new Vector3(x,y,z));
            actor.GetComponent<Alterverse.Visuals.MeleeBasicAttackAnimation>()?.Seek(elapsed);
            if(actor.GetComponent<HeroIdentity>()?.Hero==HeroId.Timmy&&windup<=.00101f)
            {
                Alterverse.Visuals.HeroSkillVfx.Spell(actor,new Vector3(x,y,z),3);
                Alterverse.Audio.HeroCombatAudio.Impact(actor,3,new Vector3(x,y,z));
            }
            actor.GetComponent<Alterverse.Visuals.CreatureAttackMotion>()?.Seek(elapsed);
            actor.GetComponentInChildren<Alterverse.Visuals.ReferenceMonsterRig>()?.Seek(elapsed);
            actor.GetComponent<Alterverse.Visuals.PlanetSentinelVisual>()?.Seek(elapsed);
            actor.GetComponent<Alterverse.Visuals.LaneUnitVisualMotion>()?.Seek(elapsed);
            actor.GetComponent<Alterverse.Visuals.MonsterAttackCue>()?.Seek(elapsed);
            actor.GetComponentInChildren<Alterverse.Visuals.MechMachineGunVisual>()?.Seek(elapsed);
        }
        private static string WorldKey(GameObject actor)
        {
            var key=actor.GetComponent<CoopWorldEntityKey>()??actor.AddComponent<CoopWorldEntityKey>();
            key.Configure();return key.Key;
        }
        private static GameObject FindWorldActor(string key)
        {
            for(int i=0;i<Health.Active.Count;i++)
            {
                var hp=Health.Active[i];if(hp==null)continue;
                var entity=hp.GetComponent<CoopWorldEntityKey>();
                if(entity!=null&&entity.Key==key)return hp.gameObject;
            }
            return null;
        }
        private void OnMonsterImpact(GameObject actor,Vector3 from,Vector3 to)
        {
            if(!CanBroadcast()||actor==null)return;
            using FastBufferWriter writer=new(256,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(new FixedString128Bytes(WorldKey(actor)));
            writer.WriteValueSafe(from.x);writer.WriteValueSafe(from.y);writer.WriteValueSafe(from.z);
            writer.WriteValueSafe(to.x);writer.WriteValueSafe(to.y);writer.WriteValueSafe(to.z);
            writer.WriteValueSafe(NetworkManager.Singleton.ServerTime.Time);
            SendAll(MonsterImpactMessage,writer);
        }
        private void OnMonsterImpactMessage(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out FixedString128Bytes key);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float tx);reader.ReadValueSafe(out float ty);reader.ReadValueSafe(out float tz);
            reader.ReadValueSafe(out double sent);
            if(!Finite(x)||!Finite(y)||!Finite(z)||!Finite(tx)||!Finite(ty)||!Finite(tz)||double.IsNaN(sent)||double.IsInfinity(sent))return;
            float elapsed=Mathf.Max(0,(float)(NetworkManager.Singleton.ServerTime.Time-sent));
            if(elapsed>.6f)return;
            var actor=FindWorldActor(key.ToString());
            if(actor==null)return;
            var siege=actor.GetComponent<LaneMinion>();
            if(actor.GetComponent<HotZoneTank>()==null&&actor.GetComponent<HealingGuardMech>()==null&&
                !(siege!=null&&siege.IsSiegeUnit&&actor.GetComponentInChildren<Alterverse.Visuals.MechMachineGunVisual>()!=null))return;
            Alterverse.Visuals.MonsterAttackVfx.PresentImpact(actor,new Vector3(x,y,z),new Vector3(tx,ty,tz),elapsed);
        }
        private void OnMonsterLaser(GameObject actor,Health target,float duration)
        {
            if(!CanBroadcast()||actor==null)return;
            var hero=target!=null?target.GetComponent<CoopHeroSlot>():null;
            using FastBufferWriter writer=new(384,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(new FixedString128Bytes(WorldKey(actor)));
            writer.WriteValueSafe(hero!=null?hero.Slot:-1);
            writer.WriteValueSafe(new FixedString128Bytes(target!=null&&hero==null?WorldKey(target.gameObject):""));
            writer.WriteValueSafe(duration);writer.WriteValueSafe(NetworkManager.Singleton.ServerTime.Time);
            SendAll(MonsterLaserMessage,writer);
        }
        private void OnMonsterLaserMessage(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out FixedString128Bytes key);reader.ReadValueSafe(out int slot);
            reader.ReadValueSafe(out FixedString128Bytes targetKey);reader.ReadValueSafe(out float duration);
            reader.ReadValueSafe(out double sent);
            if(!Finite(duration)||duration<0||duration>2||double.IsNaN(sent)||double.IsInfinity(sent))return;
            var actor=FindWorldActor(key.ToString());
            if(actor==null||!ArenaVariantRuntime.IsDark00||actor.GetComponent<HotZoneTank>()==null)return;
            Transform victim=null;
            if(slot>=0)
            {
                for(int i=0;i<Health.Active.Count;i++)
                {
                    var hp=Health.Active[i];if(hp==null||hp.IsDead)continue;
                    var hero=hp.GetComponent<CoopHeroSlot>();if(hero!=null&&hero.Slot==slot){victim=hp.transform;break;}
                }
            }
            else {var target=FindWorldActor(targetKey.ToString());if(target!=null)victim=target.transform;}
            float elapsed=Mathf.Max(0,(float)(NetworkManager.Singleton.ServerTime.Time-sent));
            if(duration>0&&elapsed>=duration)return;
            Alterverse.Visuals.MonsterAttackVfx.PresentLaser(actor,victim,duration,elapsed);
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);

        private void OnHostHotOpened(Vector3 position,int occurrence)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(36,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(position.x);writer.WriteValueSafe(position.y);writer.WriteValueSafe(position.z);
            writer.WriteValueSafe(occurrence);
            SendAll(HotOpenMessage,writer);
        }

        private void OnHostHotResolved(int winner,float zero,float lumen,int occurrence)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(36,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(winner);writer.WriteValueSafe(zero);writer.WriteValueSafe(lumen);writer.WriteValueSafe(occurrence);
            SendAll(HotResolveMessage,writer);
        }

        private void OnHostHotProgress(int occurrence,float active,float zero,float lumen,int zeroInside,int lumenInside)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(40,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(occurrence);writer.WriteValueSafe(active);writer.WriteValueSafe(zero);writer.WriteValueSafe(lumen);
            writer.WriteValueSafe(zeroInside);writer.WriteValueSafe(lumenInside);
            SendAll(HotProgressMessage,writer);
        }

        private void OnHostPlanetSpawned(int variant)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(12,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(variant);
            SendAll(PlanetMessage,writer);
        }

        private void OnHostBomberSpawned(int pass,int seed)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(16,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(pass);writer.WriteValueSafe(seed);
            SendAll(BomberMessage,writer);
        }

        private void OnHostPlanetMeteor(string ownerName,Vector3 point,float delay,float damage,float stagger)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(260,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(new FixedString128Bytes(ownerName??""));
            writer.WriteValueSafe(point.x);writer.WriteValueSafe(point.y);writer.WriteValueSafe(point.z);
            writer.WriteValueSafe(delay);writer.WriteValueSafe(damage);writer.WriteValueSafe(stagger);
            SendAll(PlanetMeteorMessage,writer);
        }

        private void OnHostNoxaraMeteor(int team,Vector3 point,float delay,float damage,float stagger)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(132,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(team);
            writer.WriteValueSafe(point.x);writer.WriteValueSafe(point.y);writer.WriteValueSafe(point.z);
            writer.WriteValueSafe(delay);writer.WriteValueSafe(damage);writer.WriteValueSafe(stagger);
            SendAll(NoxaraMeteorMessage,writer);
        }

        private void OnHostPlanetPulseStart(string ownerName,float duration)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(180,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(new FixedString128Bytes(ownerName??""));
            writer.WriteValueSafe(duration);
            SendAll(PlanetPulseStartMessage,writer);
        }

        private void OnHostPlanetPulseDetonate(string ownerName)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(164,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(new FixedString128Bytes(ownerName??""));
            SendAll(PlanetPulseDetonateMessage,writer);
        }

        private void OnHostCentralHealChanged(bool available,float readyIn)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(20,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(available);writer.WriteValueSafe(readyIn);
            SendAll(CentralHealMessage,writer);
        }

        private void OnHostPlanetBuffGranted(int team,PlanetBuffType type,float duration)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(24,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(team);writer.WriteValueSafe((int)type);writer.WriteValueSafe(duration);
            SendAll(PlanetBuffMessage,writer);
        }

        private void OnHostLaneUnitSpawned(string key,int team,Vector3 spawn,Vector3 destination,bool siege)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(260,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(new FixedString128Bytes(key??""));
            writer.WriteValueSafe(team);
            writer.WriteValueSafe(spawn.x);writer.WriteValueSafe(spawn.y);writer.WriteValueSafe(spawn.z);
            writer.WriteValueSafe(destination.x);writer.WriteValueSafe(destination.y);writer.WriteValueSafe(destination.z);
            writer.WriteValueSafe(siege);
            SendAll(LaneUnitMessage,writer);
        }

        private void OnHostFractureSpawned(int id,Vector3 position)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(28,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(position.x);writer.WriteValueSafe(position.y);writer.WriteValueSafe(position.z);
            SendAll(FractureSpawnMessage,writer);
        }

        private void OnHostFractureClaimed(int id)
        {
            if(!CanBroadcast())return;
            using FastBufferWriter writer=new(12,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(id);
            SendAll(FractureClaimMessage,writer);
        }

        private bool CanBroadcast()
        {
            NetworkManager manager=NetworkManager.Singleton;
            return CoopAuthority.Active&&CoopAuthority.LocalHasServerAuthority&&manager!=null&&manager.IsServer&&messaging!=null;
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
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out int occurrence);
            FindAnyObjectByType<HotZoneDirector>()?.ApplyNetworkOpen(new Vector3(x,y,z),occurrence);
        }

        private void OnHotResolve(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int winner);reader.ReadValueSafe(out float zero);reader.ReadValueSafe(out float lumen);
            reader.ReadValueSafe(out int occurrence);
            FindAnyObjectByType<HotZoneDirector>()?.ApplyNetworkResolve(winner,zero,lumen,occurrence);
        }

        private void OnHotProgress(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int occurrence);reader.ReadValueSafe(out float active);
            reader.ReadValueSafe(out float zero);reader.ReadValueSafe(out float lumen);
            reader.ReadValueSafe(out int zeroInside);reader.ReadValueSafe(out int lumenInside);
            FindAnyObjectByType<HotZoneDirector>()?.ApplyNetworkProgress(occurrence,active,zero,lumen,zeroInside,lumenInside);
        }

        private void OnPlanet(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int variant);
            FindAnyObjectByType<DimensionPlanetDirector>()?.ApplyNetworkSpawn(variant);
        }

        private void OnBomber(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int pass);reader.ReadValueSafe(out int seed);
            FindAnyObjectByType<SpaceBomberDirector>()?.ApplyNetworkFlyover(pass,seed);
        }

        private void OnPlanetMeteor(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out FixedString128Bytes owner);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float delay);reader.ReadValueSafe(out float damage);reader.ReadValueSafe(out float stagger);
            PlanetMeteorShower.ApplyNetworkMeteor(owner.ToString(),new Vector3(x,y,z),delay,damage,stagger);
        }

        private void OnNoxaraMeteor(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int team);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float delay);reader.ReadValueSafe(out float damage);reader.ReadValueSafe(out float stagger);
            NoxaraSpiderMeteorShower.ApplyNetworkMeteor(team,new Vector3(x,y,z),delay,damage,stagger);
        }

        private void OnPlanetPulseStart(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out FixedString128Bytes owner);reader.ReadValueSafe(out float duration);
            PlanetPulseAttack.ApplyNetworkPulseStart(owner.ToString(),duration);
        }

        private void OnPlanetPulseDetonate(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out FixedString128Bytes owner);
            PlanetPulseAttack.ApplyNetworkPulseDetonate(owner.ToString());
        }

        private void OnCentralHeal(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out bool available);reader.ReadValueSafe(out float readyIn);
            FindAnyObjectByType<CentralHealingPickup>()?.ApplyNetworkAvailability(available,readyIn);
        }

        private void OnPlanetBuff(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int team);reader.ReadValueSafe(out int rawType);reader.ReadValueSafe(out float duration);
            if(rawType<0||rawType>1)return;
            DimensionPlanet.ApplyNetworkBuff(Mathf.Clamp(team,0,1),(PlanetBuffType)rawType,duration);
        }

        private void OnLaneUnit(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out FixedString128Bytes fixedKey);
            reader.ReadValueSafe(out int team);
            reader.ReadValueSafe(out float sx);reader.ReadValueSafe(out float sy);reader.ReadValueSafe(out float sz);
            reader.ReadValueSafe(out float dx);reader.ReadValueSafe(out float dy);reader.ReadValueSafe(out float dz);
            reader.ReadValueSafe(out bool siege);
            LaneWaveSpawner.ApplyNetworkUnitSpawn(
                fixedKey.ToString(),team,new Vector3(sx,sy,sz),new Vector3(dx,dy,dz),siege);
        }

        private void OnFractureSpawn(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            FractureCorePickup.ApplyNetworkSpawn(id,new Vector3(x,y,z));
        }

        private void OnFractureClaim(ulong sender,FastBufferReader reader)
        {
            if(!ValidServerMessage(sender)||!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int id);
            FractureCorePickup.ApplyNetworkClaim(id);
        }

        private static bool ValidServerMessage(ulong sender)
        {
            NetworkManager manager=NetworkManager.Singleton;
            return CoopAuthority.Active&&!CoopAuthority.LocalHasServerAuthority&&manager!=null&&manager.IsClient&&sender==NetworkManager.ServerClientId;
        }

        private void Attach(CustomMessagingManager value)
        {
            if(value==null||attached&&messaging==value)return;
            Detach();
            messaging=value;
            messaging.RegisterNamedMessageHandler(AttackMotionMessage,OnAttackMotion);
            messaging.RegisterNamedMessageHandler(MonsterImpactMessage,OnMonsterImpactMessage);
            messaging.RegisterNamedMessageHandler(MonsterLaserMessage,OnMonsterLaserMessage);
            messaging.RegisterNamedMessageHandler(HotOpenMessage,OnHotOpen);
            messaging.RegisterNamedMessageHandler(HotResolveMessage,OnHotResolve);
            messaging.RegisterNamedMessageHandler(HotProgressMessage,OnHotProgress);
            messaging.RegisterNamedMessageHandler(PlanetMessage,OnPlanet);
            messaging.RegisterNamedMessageHandler(BomberMessage,OnBomber);
            messaging.RegisterNamedMessageHandler(PlanetMeteorMessage,OnPlanetMeteor);
            messaging.RegisterNamedMessageHandler(NoxaraMeteorMessage,OnNoxaraMeteor);
            messaging.RegisterNamedMessageHandler(PlanetPulseStartMessage,OnPlanetPulseStart);
            messaging.RegisterNamedMessageHandler(PlanetPulseDetonateMessage,OnPlanetPulseDetonate);
            messaging.RegisterNamedMessageHandler(CentralHealMessage,OnCentralHeal);
            messaging.RegisterNamedMessageHandler(PlanetBuffMessage,OnPlanetBuff);
            messaging.RegisterNamedMessageHandler(LaneUnitMessage,OnLaneUnit);
            messaging.RegisterNamedMessageHandler(FractureSpawnMessage,OnFractureSpawn);
            messaging.RegisterNamedMessageHandler(FractureClaimMessage,OnFractureClaim);
            attached=true;
        }

        private void Detach()
        {
            if(!attached||messaging==null){messaging=null;attached=false;return;}
            messaging.UnregisterNamedMessageHandler(AttackMotionMessage);
            messaging.UnregisterNamedMessageHandler(MonsterImpactMessage);
            messaging.UnregisterNamedMessageHandler(MonsterLaserMessage);
            messaging.UnregisterNamedMessageHandler(HotOpenMessage);
            messaging.UnregisterNamedMessageHandler(HotResolveMessage);
            messaging.UnregisterNamedMessageHandler(HotProgressMessage);
            messaging.UnregisterNamedMessageHandler(PlanetMessage);
            messaging.UnregisterNamedMessageHandler(BomberMessage);
            messaging.UnregisterNamedMessageHandler(PlanetMeteorMessage);
            messaging.UnregisterNamedMessageHandler(NoxaraMeteorMessage);
            messaging.UnregisterNamedMessageHandler(PlanetPulseStartMessage);
            messaging.UnregisterNamedMessageHandler(PlanetPulseDetonateMessage);
            messaging.UnregisterNamedMessageHandler(CentralHealMessage);
            messaging.UnregisterNamedMessageHandler(PlanetBuffMessage);
            messaging.UnregisterNamedMessageHandler(LaneUnitMessage);
            messaging.UnregisterNamedMessageHandler(FractureSpawnMessage);
            messaging.UnregisterNamedMessageHandler(FractureClaimMessage);
            messaging=null;attached=false;
        }
    }
}

