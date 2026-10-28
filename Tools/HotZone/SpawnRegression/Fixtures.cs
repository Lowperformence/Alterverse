using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Multiplayer
{
    public static class CoopAuthority
    {public static bool Active,LocalHasServerAuthority=true;public static bool CanSimulateHostGameplay=>LocalHasServerAuthority;}
}
namespace Alterverse.Visuals
{
    public sealed class HeroPresentationBuffer
    {public HeroPresentationBuffer(float delay){}public void Clear(){}public void Push(Vector3 p,Quaternion q,float t,bool reset){}public void PushRemote(Vector3 p,Quaternion q,float sample,float now,bool reset){}public bool Evaluate(float now,out Vector3 p,out Quaternion q){p=default;q=default;return false;}}
    public static class LunarSurfaceField {public static Vector3 Surface(float x,float z)=>new(x,2,z);}
    public static class DiamondCoreGuardianWorldVisual
    {public static void UploadPreparedGeometry(Alterverse.Combat.HotZoneTank t,ISet<Mesh> uploaded=null){}public static void SetReservedVisibility(Alterverse.Combat.HotZoneTank t,bool visible){}public static bool TryGetBounds(Alterverse.Combat.HotZoneTank t,out Bounds b){b=default;return false;}}
    public static class AuthoredHotZoneArt
    {public static int Uploads;public static void UploadPreparedGeometry(Transform root,ISet<Mesh> uploaded=null){Uploads++;}}
}
namespace Alterverse.Combat
{
    public sealed class TeamMember:MonoBehaviour{public int Team;}
    public sealed class NoxaraSpiderMeteorShower:MonoBehaviour{public void ResetForSpawn(){}}
    public sealed class Locomotion {public int Resets;public void ResetMotion(){Resets++;}}
    public sealed partial class HotZoneTank:MonoBehaviour
    {
        const float MaxHp=476f;
        static int coopSpawnSerial;
        static readonly List<HotZoneTank> active=new();
        public static IReadOnlyList<HotZoneTank> Active=>active;
        public Health TankHealth=>health;
        public int Team=>team;
        public static int Constructions;
        int team,patrolIndex,movementRecoveryAttempt;
        Health health,target,supportLeader,shotTarget;
        Locomotion locomotion=new();
        bool shotPending,spiderFeetInitialized,movementSampleReady;
        Vector3 home,lastMovementSample,spiderPreviousWorldPosition;
        float nextAttack,nextScan,nextSupportScan,blockedTime,detourUntil,shotAt,moonMoveBlend,skeletonMoveBlend,xenoMoveBlend,bearMoveBlend,spiderMoveBlend,skeletonAttackKick,spiderAttackKick,bearAttackKick,xenoAttackKick;
        private void OnEnable(){if(!active.Contains(this))active.Add(this);}
        private void OnDisable()=>active.Remove(this);
        void SetNextPatrol(){}
        static HotZoneTank CreateReward(int team,Vector3 position,Transform parent,bool reusable)
        {
            Constructions++;var go=new GameObject("TestMonster");go.transform.SetParent(parent,false);go.transform.position=position;
            var hp=go.AddComponent<Health>();hp.Configure(MaxHp,!reusable);
            go.AddComponent<TeamMember>().Team=team;
            go.AddComponent<MeshRenderer>();
            var tank=go.AddComponent<HotZoneTank>();tank.team=team;tank.health=hp;return tank;
        }
    }
}

