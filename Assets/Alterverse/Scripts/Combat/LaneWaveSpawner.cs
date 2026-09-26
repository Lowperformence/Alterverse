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

        private const float TankHp=90f;
        private const float TankDamage=5f;
        private static readonly float[] TankSpawnTimes={240f,480f};
        private static readonly float[] LaneZ={16,-16};

        private static bool siegeMechsSpawned,siegeRoutineClaimed,tankRoutineClaimed;
        private static readonly bool[] TankSpawned={false,false};
        private static int waveSerial;
        private static LaneWaveSpawner siegeOwner;
        private DimensionsFront cachedFront;

        public static int ScheduledTankCount=>TankSpawnTimes.Length;
        public static float ScheduledTankTime(int occurrence)=>occurrence>=0&&occurrence<TankSpawnTimes.Length?TankSpawnTimes[occurrence]:float.PositiveInfinity;

        public static event Action<string,int,Vector3,Vector3,bool> NetworkUnitSpawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSiegeState()
        {
            siegeOwner=null;
            siegeMechsSpawned=false;
            siegeRoutineClaimed=false;
            tankRoutineClaimed=false;
            ResetScheduledTanksForMatch();
            waveSerial=0;
        }

        public static void ResetScheduledTanksForMatch()
        {
            for(int i=0;i<TankSpawned.Length;i++)TankSpawned[i]=false;
        }

        public static bool TrySpawnScheduledTanks(int occurrence)
        {
            if(!CoopAuthority.CanSimulateHostGameplay||occurrence<0||occurrence>=TankSpawnTimes.Length)return false;
            if(TankSpawned[occurrence])return false;

            DimensionsFront front=FindAnyObjectByType<DimensionsFront>();
            if(front==null||front.HasEnded)return false;

            TankSpawned[occurrence]=true;
            int laneIndex=occurrence%LaneZ.Length;
            float laneZ=LaneZ[laneIndex];
            CreateTank(0,laneZ,occurrence,laneIndex,true);
            CreateTank(1,laneZ,occurrence,laneIndex,true);
            return true;
        }

        private void OnDestroy()
        {
            if(siegeOwner==this)
            {
                siegeOwner=null;
                siegeMechsSpawned=false;
                siegeRoutineClaimed=false;
                tankRoutineClaimed=false;
            }
        }

        private IEnumerator Start()
        {
            if(!CoopAuthority.CanSimulateHostGameplay)yield break;

            if(siegeOwner==null)
            {
                siegeOwner=this;
                siegeMechsSpawned=false;
                siegeRoutineClaimed=false;
                tankRoutineClaimed=false;
            }

            cachedFront=FindAnyObjectByType<DimensionsFront>();
            if(!siegeMechsSpawned&&!siegeRoutineClaimed)
            {
                siegeRoutineClaimed=true;
                StartCoroutine(SiegeMechRoutine());
            }
            if(!tankRoutineClaimed)
            {
                tankRoutineClaimed=true;
                StartCoroutine(ScheduledTankRoutine());
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

        private IEnumerator ScheduledTankRoutine()
        {
            float previousTime=0f;
            for(int occurrence=0;occurrence<TankSpawnTimes.Length;occurrence++)
            {
                float targetTime=TankSpawnTimes[occurrence];
                yield return WaitGameSeconds(Mathf.Max(0f,targetTime-previousTime));
                previousTime=targetTime;
                if(MatchEnded())yield break;
                TrySpawnScheduledTanks(occurrence);
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
            if(key.StartsWith("TANK:",StringComparison.Ordinal))CreateTankAt(team,spawn,destination,key,false);
            else if(siege)CreateSiegeMechAt(team,spawn,destination,key,false);
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

        private static void CreateTank(int team,float laneZ,int occurrence,int laneIndex,bool announceNetwork)
        {
            float startX=team==0?-SoldierSpawnFrontX:SoldierSpawnFrontX;
            float destinationX=team==0?31:-31;
            Vector3 spawn=new(startX,.72f,laneZ);
            Vector3 destination=new(destinationX,.72f,laneZ);
            string key=$"TANK:S{occurrence+1}:T{team}:L{laneIndex}";
            CreateTankAt(team,spawn,destination,key,announceNetwork);
        }

        private static void CreateTankAt(int team,Vector3 spawn,Vector3 destination,string key,bool announceNetwork)
        {
            GameObject tank=new(team==0?"DF_ZeroScheduledTank":"DF_LumenScheduledTank");
            tank.transform.position=spawn;
            tank.transform.rotation=Quaternion.LookRotation(team==0?Vector3.right:Vector3.left,Vector3.up);
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world!=null)tank.transform.SetParent(world.transform,true);

            BoxCollider collider=tank.AddComponent<BoxCollider>();
            collider.center=new Vector3(0,.12f,0);
            collider.size=new Vector3(1.85f,.95f,2.65f);

            Health hp=tank.AddComponent<Health>();
            hp.Configure(TankHp,true);
            if(CoopAuthority.Active)tank.AddComponent<CoopWorldEntityKey>().Configure(key);

            LaneMinion unit=tank.AddComponent<LaneMinion>();
            unit.Initialize(team,destination);
            unit.ConfigureSiegeRole();
            unit.ConfigureTeamSupport();
            unit.ConfigureCombat(2.25f,9f,5f,TankDamage,1.35f,1f);
            if(tank.GetComponent<TankTrackEmitter>()==null)tank.AddComponent<TankTrackEmitter>();
            BuildTankVisual(tank.transform,team);

            if(announceNetwork&&CoopAuthority.Active&&CoopAuthority.LocalIsHost)
                NetworkUnitSpawned?.Invoke(key,team,spawn,destination,true);
        }

        private static void BuildTankVisual(Transform root,int team)
        {
            Color armor=new(.24f,.26f,.29f,1f);
            Color armorLight=new(.67f,.69f,.63f,1f);
            Color dark=new(.075f,.085f,.10f,1f);
            Color accent=new(.88f,.23f,.11f,1f);
            Color energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;

            CreateTankPart(root,"DF_TankLowerHull",new Vector3(0,-.08f,0),new Vector3(1.62f,.30f,2.42f),dark,false);
            CreateTankPart(root,"DF_TankMainHull",new Vector3(0,.16f,.03f),new Vector3(1.78f,.46f,2.16f),armor,false);
            CreateTankPart(root,"DF_TankFrontArmor",new Vector3(0,.19f,.98f),new Vector3(1.90f,.38f,.54f),armorLight,false);
            CreateTankPart(root,"DF_TankRearDeck",new Vector3(0,.28f,-.80f),new Vector3(1.48f,.20f,.52f),armor,false);
            CreateTankPart(root,"DF_TankLeftPod",new Vector3(-.98f,.02f,.02f),new Vector3(.30f,.46f,1.78f),accent,false);
            CreateTankPart(root,"DF_TankRightPod",new Vector3(.98f,.02f,.02f),new Vector3(.30f,.46f,1.78f),accent,false);
            CreateTankPart(root,"DF_TankLeftSkid",new Vector3(-.91f,-.24f,.05f),new Vector3(.36f,.22f,2.08f),dark,false);
            CreateTankPart(root,"DF_TankRightSkid",new Vector3(.91f,-.24f,.05f),new Vector3(.36f,.22f,2.08f),dark,false);
            CreateTankPart(root,"DF_TankTurretBase",new Vector3(0,.48f,-.05f),new Vector3(1.18f,.24f,1.05f),dark,false);
            CreateTankPart(root,"DF_TankTurret",new Vector3(0,.67f,-.02f),new Vector3(1.02f,.42f,.84f),armorLight,false);
            CreateTankPart(root,"DF_TankTurretPlate",new Vector3(0,.90f,-.08f),new Vector3(.72f,.10f,.62f),armor,false);
            CreateTankPart(root,"DF_TankCannonMount",new Vector3(0,.71f,.50f),new Vector3(.48f,.34f,.34f),accent,false);
            CreateTankPart(root,"DF_TankCannon",new Vector3(0,.73f,1.50f),new Vector3(.28f,.26f,2.00f),accent,false);
            CreateTankPart(root,"DF_TankMuzzle",new Vector3(0,.73f,2.52f),new Vector3(.43f,.40f,.28f),armorLight,false);
            CreateTankPart(root,"DF_TankMuzzleDark",new Vector3(0,.73f,2.68f),new Vector3(.28f,.27f,.08f),dark,false);
            CreateTankPart(root,"DF_TankLeftVent",new Vector3(-.47f,.73f,-.34f),new Vector3(.24f,.17f,.42f),dark,false);
            CreateTankPart(root,"DF_TankRightVent",new Vector3(.47f,.73f,-.34f),new Vector3(.24f,.17f,.42f),dark,false);
            CreateTankPart(root,"DF_TankTopEnergy",new Vector3(0,.925f,.10f),new Vector3(.42f,.035f,.34f),energy,true);
            CreateTankPart(root,"DF_TankLeftEnergy",new Vector3(-.995f,.18f,.22f),new Vector3(.035f,.10f,1.02f),energy,true);
            CreateTankPart(root,"DF_TankRightEnergy",new Vector3(.995f,.18f,.22f),new Vector3(.035f,.10f,1.02f),energy,true);
            CreateTankPart(root,"DF_TankFrontEnergy",new Vector3(0,.34f,1.265f),new Vector3(.62f,.08f,.035f),energy,true);
        }

        private static void CreateTankPart(Transform parent,string name,Vector3 pos,Vector3 scale,Color color,bool emissive)
        {
            GameObject part=GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name=name;
            part.transform.SetParent(parent,false);
            part.transform.localPosition=pos;
            part.transform.localScale=scale;
            Collider c=part.GetComponent<Collider>();if(c!=null){c.enabled=false;Destroy(c);}
            Renderer r=part.GetComponent<Renderer>();
            if(r==null)return;
            Material material=r.material;
            material.color=color;
            if(emissive&&material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor",color*2.2f);
            }
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
