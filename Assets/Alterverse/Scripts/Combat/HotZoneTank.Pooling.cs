using System.Collections.Generic;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Combat
{
    public sealed partial class HotZoneTank
    {
        // Either team can win both objectives. Reserve two complete, independently
        // animated actors per team, and reuse dead actors for later objectives.
        private static readonly List<HotZoneTank> preparedRewards=new(4);
        private static Transform preparedWorld;
        private static float preparationReadyAt;
        private static bool preparationFinished;
        private bool pooledReward,reservedReward,spawnAuraPending;
        private float spawnAuraRadius=1.5f;
        private Behaviour[] preparationBehaviours;
        private bool[] preparationEnabled;
        public bool IsReservedReward=>reservedReward;

        private static void ResetPreparedRewards()
        {preparedRewards.Clear();preparedWorld=null;preparationFinished=false;}

        public static void PrepareForMatch(Transform world)
        {
            if(world==null||preparedWorld==world)return;
            ResetPreparedRewards();preparedWorld=world;
            for(int team=0;team<2;team++)for(int slot=0;slot<2;slot++)
            {
                var tank=CreateReward(team,new Vector3(team==0?-4f:4f,.95f,slot*2.6f),world,true);
                tank.pooledReward=tank.reservedReward=true;
                // Leave visual scripts awake under the loading screen so the
                // existing planet-detail managers finish their normal preparation.
                // Gameplay and replication registries must never see these reserves.
                tank.preparationBehaviours=tank.GetComponentsInChildren<Behaviour>(true);
                tank.preparationEnabled=new bool[tank.preparationBehaviours.Length];
                for(int i=0;i<tank.preparationBehaviours.Length;i++)
                {
                    var behaviour=tank.preparationBehaviours[i];
                    bool gameplay=behaviour.GetType().Namespace!="Alterverse.Visuals";
                    tank.preparationEnabled[i]=behaviour.enabled;
                    if(gameplay)behaviour.enabled=false;
                }
                preparedRewards.Add(tank);
            }
            HotZoneSpawnAura.Prepare(world);
            preparationReadyAt=Time.unscaledTime+.5f;
        }

        public static bool FinishMatchPreparation(bool immediately=false)
        {
            if(preparationFinished||preparedWorld==null)return true;
            if(!immediately&&Time.unscaledTime<preparationReadyAt)return false;
            foreach(var tank in preparedRewards)
            {
                if(tank==null)continue;
                tank.spawnAuraRadius=tank.MeasureSpawnAuraRadius();
                // Cache the complete post-detail hierarchy before first spawn.
                if(CoopAuthority.Active)
                    tank.gameObject.AddComponent<CoopWorldEntityKey>().Configure("RESERVED_HOTZONE");
                tank.gameObject.SetActive(false);
                DiamondCoreGuardianWorldVisual.SetReservedVisibility(tank,false);
            }
            preparationFinished=true;return true;
        }

        private static HotZoneTank TakePreparedReward(int team)
        {
            foreach(var tank in preparedRewards)
            {
                if(tank==null||tank.team!=team)continue;
                if(!tank.reservedReward&&tank.health!=null&&tank.health.IsDead)ReturnToRewardPool(tank.gameObject);
                if(tank.reservedReward&&!tank.gameObject.activeInHierarchy)return tank;
            }
            return null;
        }

        private void ActivatePreparedReward(Vector3 spawn)
        {
            transform.SetPositionAndRotation(spawn,Quaternion.identity);
            health.Configure(MaxHp,false);locomotion.ResetMotion();
            home=spawn;target=supportLeader=shotTarget=null;shotPending=false;
            nextAttack=nextScan=nextSupportScan=blockedTime=detourUntil=shotAt=0f;
            patrolIndex=movementRecoveryAttempt=0;spiderFeetInitialized=false;
            moonMoveBlend=skeletonMoveBlend=xenoMoveBlend=bearMoveBlend=spiderMoveBlend=0f;
            skeletonAttackKick=spiderAttackKick=bearAttackKick=xenoAttackKick=0f;
            lastMovementSample=spiderPreviousWorldPosition=spawn;movementSampleReady=true;
            reservedReward=false;
            for(int i=0;i<preparationBehaviours.Length;i++)
                if(preparationBehaviours[i]!=null&&preparationBehaviours[i].GetType().Namespace!="Alterverse.Visuals")
                    preparationBehaviours[i].enabled=preparationEnabled[i];
            GetComponent<CoopWorldEntityKey>()?.ApplyAlivePresentation(true);
            GetComponent<NoxaraSpiderMeteorShower>()?.ResetForSpawn();
            gameObject.SetActive(true);SetNextPatrol();
            DiamondCoreGuardianWorldVisual.SetReservedVisibility(this,true);
        }

        public static bool ReturnToRewardPool(GameObject actor)
        {
            var tank=actor!=null?actor.GetComponent<HotZoneTank>():null;
            if(tank==null||!tank.pooledReward)return false;
            tank.reservedReward=true;tank.spawnAuraPending=false;
            actor.SetActive(false);
            DiamondCoreGuardianWorldVisual.SetReservedVisibility(tank,false);
            return true;
        }

        public void ConfirmNetworkSpawn()
        {
            if(!spawnAuraPending)return;
            spawnAuraPending=false;PlaySpawnAura();
        }

        private void PlaySpawnAura()
        {
            var point=LunarSurfaceField.Surface(transform.position.x,transform.position.z)+Vector3.up*.04f;
            HotZoneSpawnAura.TryPlay(point,pooledReward?spawnAuraRadius:MeasureSpawnAuraRadius());
        }

        private float MeasureSpawnAuraRadius()
        {
            bool found=false;Bounds bounds=default;
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
            }
            if(DiamondCoreGuardianWorldVisual.TryGetBounds(this,out var crystal))
            {if(!found){bounds=crystal;found=true;}else bounds.Encapsulate(crystal);}
            return found?Mathf.Clamp(Mathf.Max(bounds.extents.x,bounds.extents.z),.6f,8f):1.5f;
        }
    }
}
