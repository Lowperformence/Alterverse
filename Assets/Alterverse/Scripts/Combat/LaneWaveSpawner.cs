using System;
using System.Collections;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Combat
{
    public sealed class LaneWaveSpawner:MonoBehaviour
    {
        [SerializeField]private float firstWaveDelay=4,waveInterval=25,unitSpacing=1.1f;
        public const float SoldierSpawnFrontX=18.5f;
        [SerializeField]private int unitsPerWave=3;
        [SerializeField]private float siegeMechTime=360f;

        private static bool siegeMechsSpawned,siegeRoutineClaimed;
        private static int waveSerial;
        private static LaneWaveSpawner siegeOwner;
        private static readonly float[] LaneZ={16,-16};
        private DimensionsFront cachedFront;

        public static event Action<string,int,Vector3,Vector3,bool> NetworkUnitSpawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSiegeState()
        {
            siegeOwner=null;
            siegeMechsSpawned=false;
            siegeRoutineClaimed=false;
            waveSerial=0;
        }

        private void OnDestroy()
        {
            if(siegeOwner==this)
            {
                siegeOwner=null;
                siegeMechsSpawned=false;
                siegeRoutineClaimed=false;
            }
        }

        private IEnumerator Start()
        {
            // Clients never create their own combat waves. They receive the host's exact
            // spawn events and then follow host world-state snapshots.
            if(!CoopAuthority.CanSimulateHostGameplay)yield break;

            if(siegeOwner==null)
            {
                siegeOwner=this;
                siegeMechsSpawned=false;
                siegeRoutineClaimed=false;
            }

            cachedFront=FindAnyObjectByType<DimensionsFront>();
            if(!siegeMechsSpawned&&!siegeRoutineClaimed)
            {
                siegeRoutineClaimed=true;
                StartCoroutine(SiegeMechRoutine());
            }

            yield return WaitGameSeconds(firstWaveDelay);
            while(true)
            {
                SpawnAllLanes();
                yield return WaitGameSeconds(waveInterval);
            }
        }

        private IEnumerator SiegeMechRoutine()
        {
            yield return WaitGameSeconds(Mathf.Max(0f,siegeMechTime));
            if(siegeMechsSpawned||MatchEnded()){siegeRoutineClaimed=false;yield break;}
            siegeMechsSpawned=true;

            for(int lane=0;lane<LaneZ.Length;lane++)
            {
                float laneZ=LaneZ[lane];
                CreateSiegeMech(0,laneZ,$"SIEGE:T0:L{lane}",true);
                CreateSiegeMech(1,laneZ,$"SIEGE:T1:L{lane}",true);
            }
        }

        private bool MatchEnded()
        {
            cachedFront??=FindAnyObjectByType<DimensionsFront>();
            return cachedFront!=null&&cachedFront.HasEnded;
        }

        private static IEnumerator WaitGameSeconds(float seconds)
        {
            float elapsed=0f;
            while(elapsed<seconds)
            {
                if(Time.timeScale>0f)elapsed+=Time.deltaTime;
                yield return null;
            }
        }

        private void SpawnAllLanes()
        {
            if(Time.timeScale<=0f||MatchEnded())return;
            int serial=++waveSerial;
            for(int lane=0;lane<LaneZ.Length;lane++)
            {
                float laneZ=LaneZ[lane];
                SpawnWave(0,laneZ,lane,serial);
                SpawnWave(1,laneZ,lane,serial);
            }
        }

        private void SpawnWave(int team,float laneZ,int laneIndex,int serial)
        {
            // Regular soldiers now spawn on the arena-centre side of the defensive trenches.
            // This keeps the whole wave clear of the rear fortification area where units could bunch up.
            float destinationX=team==0?31:-31;
            for(int i=0;i<unitsPerWave;i++)
            {
                Vector3 spawn=SoldierSpawnPoint(team,laneZ,i,unitSpacing);
                Vector3 destination=new(destinationX,.55f,laneZ);
                string key=$"MINION:W{serial}:T{team}:L{laneIndex}:I{i}";
                CreateMinion(team,spawn,destination,i,key,true);
            }
        }

        public static Vector3 SoldierSpawnPoint(int team,float laneZ,int index,float spacing=1.1f)
        {
            float startX=team==0?-SoldierSpawnFrontX:SoldierSpawnFrontX;
            float lateral=(Mathf.Max(0,index)-1)*Mathf.Max(.45f,spacing*.68f);
            return new Vector3(startX,.55f,laneZ+lateral);
        }

        public static void ApplyNetworkUnitSpawn(string key,int team,Vector3 spawn,Vector3 destination,bool siege)
        {
            if(CoopAuthority.CanSimulateHostGameplay||string.IsNullOrEmpty(key))return;
            if(FindExisting(key)!=null)return;
            if(siege)CreateSiegeMechAt(team,spawn,destination,key,false);
            else CreateMinion(team,spawn,destination,0,key,false);
        }

        private static CoopWorldEntityKey FindExisting(string key)
        {
            CoopWorldEntityKey[] all=FindObjectsByType<CoopWorldEntityKey>(FindObjectsSortMode.None);
            for(int i=0;i<all.Length;i++)if(all[i]!=null&&all[i].Key==key)return all[i];
            return null;
        }

        private static void CreateMinion(int team,Vector3 spawn,Vector3 destination,int index,string key,bool announceNetwork)
        {
            Color energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color dark=team==0?StylizedArtDirection.ZeroDark:StylizedArtDirection.LumenDark;

            GameObject minion=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            minion.name=team==0?$"DF_ZeroMinion_{index}":$"DF_LumenMinion_{index}";
            minion.transform.position=spawn;
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world!=null)minion.transform.SetParent(world.transform,true);
            minion.transform.localScale=new Vector3(.5f,.5f,.5f);

            Renderer renderer=minion.GetComponent<Renderer>();
            if(renderer!=null)
            {
                renderer.material.color=dark;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            }

            Health health=minion.AddComponent<Health>();
            health.Configure(45.5f,true);
            if(CoopAuthority.Active)minion.AddComponent<CoopWorldEntityKey>().Configure(key);
            LaneMinion unit=minion.AddComponent<LaneMinion>();
            unit.Initialize(team,destination);
            unit.ConfigureWaveGroup(key,index);

            CreatePart(minion.transform,"DF_Visor",PrimitiveType.Cube,new Vector3(0,.42f,.42f),new Vector3(.62f,.12f,.10f),energy);
            CreatePart(minion.transform,"DF_Shoulder",PrimitiveType.Cube,new Vector3(0,.12f,0),new Vector3(.92f,.14f,.5f),energy);
            CreatePart(minion.transform,"DF_BackPlate",PrimitiveType.Cube,new Vector3(0,.1f,-.34f),new Vector3(.58f,.3f,.12f),dark);
            CreatePart(minion.transform,"DF_TeamFin",PrimitiveType.Cube,new Vector3(0,.72f,-.05f),new Vector3(.10f,.42f,.28f),energy);
            Destroy(minion,75);

            if(announceNetwork&&CoopAuthority.Active&&CoopAuthority.LocalIsHost)
                NetworkUnitSpawned?.Invoke(key,team,spawn,destination,false);
        }

        private static void CreateSiegeMech(int team,float laneZ,string key,bool announceNetwork)
        {
            float startX=team==0?-30:30,destinationX=team==0?31:-31;
            CreateSiegeMechAt(team,new Vector3(startX,.85f,laneZ),new Vector3(destinationX,.85f,laneZ),key,announceNetwork);
        }

        private static void CreateSiegeMechAt(int team,Vector3 spawn,Vector3 destination,string key,bool announceNetwork)
        {
            GameObject mech=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mech.name=team==0?"DF_ZeroSiegeMech":"DF_LumenSiegeMech";
            mech.transform.position=spawn;
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world!=null)mech.transform.SetParent(world.transform,true);

            mech.transform.localScale=new Vector3(1.05f,1.05f,1.05f);
            Renderer renderer=mech.GetComponent<Renderer>();
            if(renderer!=null)renderer.enabled=false;
            CapsuleCollider collider=mech.GetComponent<CapsuleCollider>();
            if(collider!=null){collider.radius=.72f;collider.height=2.05f;}

            Health hp=mech.AddComponent<Health>();
            hp.Configure(336f,true);
            if(CoopAuthority.Active)mech.AddComponent<CoopWorldEntityKey>().Configure(key);

            LaneMinion unit=mech.AddComponent<LaneMinion>();
            unit.Initialize(team,destination);
            unit.ConfigureFrontInfluence(.30f);
            unit.ConfigureSiegeRole();
            unit.ConfigureCombat(2.15f,8.2f,2.15f,10.5f,1.25f,1.65f);

            if(ArenaVariantRuntime.IsGreenPlanet)GreenAlienMechVisual.Build(mech.transform,team,false);
            else if(ArenaVariantRuntime.IsMars)MarsStoneGolemMechVisual.Build(mech.transform,team,false);
            else HealingGuardMech.BuildRobotVisual(mech.transform,team);

            Destroy(mech,100);

            if(announceNetwork&&CoopAuthority.Active&&CoopAuthority.LocalIsHost)
                NetworkUnitSpawned?.Invoke(key,team,spawn,destination,true);
        }

        private static void CreatePart(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Color color)
        {
            GameObject p=GameObject.CreatePrimitive(type);
            p.name=name;p.transform.SetParent(parent,false);p.transform.localPosition=pos;p.transform.localScale=scale;
            Collider c=p.GetComponent<Collider>();if(c!=null){c.enabled=false;Destroy(c);}
            Renderer r=p.GetComponent<Renderer>();if(r!=null)r.material.color=color;
        }
    }
}
