using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Match
{
    /// <summary>
    /// Recurring free-roam PvP objective. A new Hot Zone opens every 60 match-seconds,
    /// stays active for 30 seconds, and relocates randomly inside the middle 50%
    /// of the arena width. The entire capture circle stays out of both outer quarters. Every hero inside scores independently for their
    /// team, even while both teams occupy the zone.
    /// </summary>
    public sealed class HotZoneDirector : MonoBehaviour
    {
        public const float SpawnInterval=60f;
        public const float FirstOpenAt=SpawnInterval;
        public const float SecondOpenAt=SpawnInterval*2f;
        public const float OpenAt=FirstOpenAt;
        public const float ActiveDuration=30f;
        public const int MaxScoringHeroesPerTeam=5;
        public const float Radius=9.75f;

        private const float ArenaHalfWidth=42f;
        private const float ArenaHalfHeight=36f;
        private const float ZoneEdgeMargin=1.25f;
        private const float SpawnPadX=27.5f;
        public const float ExcludedSideFraction=.25f;
        private Bounds arenaBounds;
        private const float InnerRingRadiusScale=.76f;
        private const float RingAlpha=.42f;
        private static readonly Color HotZoneGreen=new(.38f,.96f,.28f,1f);

        private enum State { Waiting, Active, Finished }
        private State state;
        private static HotZoneDirector owner;
        private bool rewardIssued,openingAnnounced;
        private int occurrence;
        private float elapsed,nextOpenAt=SpawnInterval,activeElapsed,zeroTime,lumenTime,nextScan;
        private int zeroInside,lumenInside;
        private GameObject zoneRoot;
        private Mesh outerRingMesh,innerRingMesh;
        private Material zoneMaterial;
        private DimensionsFront front;
        private System.Random rng;
        private readonly Vector3[] recentSpawns=new Vector3[4];
        private int recentSpawnCount,recentSpawnIndex;
        private readonly Collider[] spawnBlockers=new Collider[12];

        public static event System.Action HotZoneOpening;
        public static event System.Action HotZoneOpened;
        public static event System.Action<int,float,float> HotZoneResolved;
        public static event System.Action<Vector3,int> NetworkZoneOpened;
        public static event System.Action<int,float,float,int> NetworkZoneResolved;

        public int Occurrence=>occurrence+1;
        public float ZeroControlSeconds=>zeroTime;
        public float LumenControlSeconds=>lumenTime;
        public float Remaining=>state==State.Active?Mathf.Max(0,ActiveDuration-activeElapsed):
            state==State.Waiting?Mathf.Max(0,nextOpenAt-elapsed):0;
        public bool IsActive=>state==State.Active;
        public int ZeroInside=>zeroInside;
        public int LumenInside=>lumenInside;
        public bool RewardSpawned=>rewardIssued;

        private void Awake()
        {
            if(owner!=null&&owner!=this){enabled=false;Destroy(gameObject);return;}
            owner=this;
            front=FindAnyObjectByType<DimensionsFront>();
            rng=new System.Random(unchecked(System.Environment.TickCount^GetHashCode()));
            nextOpenAt=SpawnInterval;
        }

        private void OnDisable(){if(owner==this)owner=null;}

        private void Update()
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
            {
                if(state==State.Active&&Time.timeScale>0f)AnimateZone();
                return;
            }
            if(Time.timeScale<=0f||state==State.Finished)return;
            if(front==null)front=FindAnyObjectByType<DimensionsFront>();
            if(front!=null&&front.HasEnded){ResolveNoReward();return;}

            elapsed+=Time.deltaTime;
            if(state==State.Waiting)
            {
                if(!openingAnnounced&&elapsed>=nextOpenAt-10f)
                {
                    openingAnnounced=true;
                    HotZoneOpening?.Invoke();
                }
                if(elapsed<nextOpenAt)return;
                Open();
            }

            if(state!=State.Active)return;
            activeElapsed+=Time.deltaTime;
            if(Time.time>=nextScan){nextScan=Time.time+.10f;ScanHeroes();}

            Vector2 scoreDelta=ScoreDelta(zeroInside,lumenInside,Time.deltaTime);
            zeroTime+=scoreDelta.x;
            lumenTime+=scoreDelta.y;

            AnimateZone();
            if(activeElapsed>=ActiveDuration)Resolve();
        }

        private void Open()
        {
            state=State.Active;
            rewardIssued=false;
            activeElapsed=0;zeroTime=0;lumenTime=0;zeroInside=lumenInside=0;nextScan=0;
            transform.position=ChooseSpawnPosition();
            BuildZoneVisual();
            HotZoneOpened?.Invoke();
            if(Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
                NetworkZoneOpened?.Invoke(transform.position,occurrence);
        }

        private Vector3 ChooseSpawnPosition()
        {
            // Every current map uses DF_ArenaFloor. Read its actual collision bounds so
            // this restriction follows future map sizes instead of a fixed world X range.
            Collider floor=GameObject.Find("DF_ArenaFloor")?.GetComponent<Collider>();
            arenaBounds=floor!=null?floor.bounds:
                new Bounds(Vector3.zero,new Vector3(ArenaHalfWidth*2f,1f,ArenaHalfHeight*2f));
            Vector2 limits=SpawnCenterHalfExtents(arenaBounds);
            float xLimit=limits.x;
            float zLimit=limits.y;

            Vector3 spawn;
            if(TryChooseRandomSpawn(xLimit,zLimit,true,out spawn))
                return RememberSpawn(spawn);

            // If recent positions leave no room, relax only the repeat-avoidance rule.
            if(TryChooseRandomSpawn(xLimit,zLimit,false,out spawn))
                return RememberSpawn(spawn);

            // Rare crowded arenas still get a deterministic safe fallback.
            Vector3[] anchors={
                LunarSurfaceField.Surface(arenaBounds.center.x,arenaBounds.center.z),
                LunarSurfaceField.Surface(arenaBounds.center.x,arenaBounds.center.z+zLimit*.5f),
                LunarSurfaceField.Surface(arenaBounds.center.x,arenaBounds.center.z-zLimit*.5f)};
            foreach(Vector3 anchor in anchors)
                if(IsSpawnPositionSafe(anchor))return RememberSpawn(anchor);
            return RememberSpawn(anchors[0]);
        }

        private bool TryChooseRandomSpawn(float xLimit,float zLimit,bool avoidRecent,out Vector3 spawn)
        {
            for(int attempt=0;attempt<72;attempt++)
            {
                float x=arenaBounds.center.x+Next(-xLimit,xLimit);
                float z=arenaBounds.center.z+Next(-zLimit,zLimit);
                Vector3 candidate=LunarSurfaceField.Surface(x,z);
                if(!IsSpawnPositionSafe(candidate))continue;
                if(avoidRecent&&IsTooCloseToRecentSpawn(candidate))continue;
                spawn=candidate;
                return true;
            }
            spawn=default;
            return false;
        }

        private bool IsTooCloseToRecentSpawn(Vector3 candidate)
        {
            float minDistance=Radius*1.35f;
            float minDistanceSqr=minDistance*minDistance;
            for(int i=0;i<recentSpawnCount;i++)
            {
                Vector3 delta=candidate-recentSpawns[i];
                delta.y=0f;
                if(delta.sqrMagnitude<minDistanceSqr)return true;
            }
            return false;
        }

        private Vector3 RememberSpawn(Vector3 spawn)
        {
            recentSpawns[recentSpawnIndex]=spawn;
            recentSpawnIndex=(recentSpawnIndex+1)%recentSpawns.Length;
            recentSpawnCount=Mathf.Min(recentSpawnCount+1,recentSpawns.Length);
            return spawn;
        }

        /// <summary>Center limits keep the full zone in the middle half of X and on the floor in Z.</summary>
        public static Vector2 SpawnCenterHalfExtents(Bounds bounds)=>new Vector2(
            Mathf.Max(0f,bounds.size.x*(.5f-ExcludedSideFraction)-Radius),
            Mathf.Max(0f,bounds.extents.z-Radius-ZoneEdgeMargin));

        public static bool IsInsideSpawnBand(Vector3 candidate,Bounds bounds)
        {
            Vector2 limits=SpawnCenterHalfExtents(bounds);
            return Mathf.Abs(candidate.x-bounds.center.x)<=limits.x&&
                Mathf.Abs(candidate.z-bounds.center.z)<=limits.y;
        }

        private bool IsSpawnPositionSafe(Vector3 candidate)
        {
            if(!IsInsideSpawnBand(candidate,arenaBounds))return false;
            float baseClearance=Radius+2.25f;
            Vector3 left=new(-SpawnPadX,candidate.y,0);
            Vector3 right=new(SpawnPadX,candidate.y,0);
            Vector3 dl=candidate-left;dl.y=0;
            Vector3 dr=candidate-right;dr.y=0;
            if(dl.sqrMagnitude<baseClearance*baseClearance||dr.sqrMagnitude<baseClearance*baseClearance)return false;

            int count=Physics.OverlapSphereNonAlloc(candidate,2.2f,spawnBlockers,1<<GameplayCollision.Structures,QueryTriggerInteraction.Ignore);
            return count==0;
        }

        private float Next(float min,float max)=>(float)(min+(max-min)*rng.NextDouble());

        public static Vector2 ScoreDelta(int zeroHeroes,int lumenHeroes,float seconds)
        {
            float dt=Mathf.Max(0f,seconds);
            return new Vector2(Mathf.Max(0,zeroHeroes)*dt,Mathf.Max(0,lumenHeroes)*dt);
        }

        private void ScanHeroes()
        {
            zeroInside=0;lumenInside=0;
            float radiusSqr=Radius*Radius;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health h=Health.Active[i];
                if(h==null||h.IsDead||!h.isActiveAndEnabled||h.GetComponent<HeroIdentity>()==null)continue;
                TeamMember member=h.GetComponent<TeamMember>();if(member==null)continue;
                Vector3 d=h.transform.position-transform.position;d.y=0;
                if(d.sqrMagnitude>radiusSqr)continue;
                if(member.Team==0)zeroInside++;else if(member.Team==1)lumenInside++;
            }
        }

        private void Resolve()
        {
            if(state!=State.Active)return;
            front??=FindAnyObjectByType<DimensionsFront>();
            if(front!=null&&front.HasEnded){ResolveNoReward();return;}

            int winner=zeroTime>lumenTime+.05f?0:lumenTime>zeroTime+.05f?1:-1;
            RecordWinningParticipants(winner);

            if(winner>=0&&!rewardIssued)
                rewardIssued=HotZoneTank.Spawn(winner);

            HotZoneResolved?.Invoke(winner,zeroTime,lumenTime);
            if(Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
                NetworkZoneResolved?.Invoke(winner,zeroTime,lumenTime,occurrence);
            DestroyZoneVisual(1.2f);

            occurrence++;
            openingAnnounced=false;
            zeroInside=lumenInside=0;
            state=State.Waiting;
            do nextOpenAt+=SpawnInterval; while(nextOpenAt<=elapsed);
        }

        private void RecordWinningParticipants(int winner)
        {
            if (winner < 0)
                return;

            float radiusSquared = Radius * Radius;
            IReadOnlyList<Health> heroes = Health.Active;
            for (int i = 0; i < heroes.Count; i++)
            {
                Health hero = heroes[i];
                if (hero == null || hero.IsDead || !hero.isActiveAndEnabled ||
                    hero.GetComponent<HeroIdentity>() == null)
                    continue;

                TeamMember member = hero.GetComponent<TeamMember>();
                if (member == null || member.Team != winner)
                    continue;

                Vector3 offset = hero.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radiusSquared)
                    MatchTelemetry.Instance?.RecordHotZoneCapture(hero);
            }
        }

        public void ApplyNetworkOpen(Vector3 position,int occurrenceIndex)
        {
            if(Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            occurrence=Mathf.Max(0,occurrenceIndex);
            state=State.Active;
            rewardIssued=false;
            activeElapsed=0;zeroTime=0;lumenTime=0;zeroInside=lumenInside=0;
            transform.position=position;
            BuildZoneVisual();
            HotZoneOpened?.Invoke();
        }

        public void ApplyNetworkResolve(int winner,float zeroScore,float lumenScore,int occurrenceIndex)
        {
            if(Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            occurrence=Mathf.Max(0,occurrenceIndex);
            zeroTime=Mathf.Max(0,zeroScore);lumenTime=Mathf.Max(0,lumenScore);
            rewardIssued=winner>=0;
            RecordWinningParticipants(winner);
            if(winner>=0)HotZoneTank.Spawn(winner);

            HotZoneResolved?.Invoke(winner,zeroTime,lumenTime);

            DestroyZoneVisual(1.2f);
            zeroInside=lumenInside=0;
            occurrence++;
            state=State.Waiting;
        }

        private void ResolveNoReward()
        {
            state=State.Finished;rewardIssued=true;
            DestroyZoneVisual(0f);
        }

        private void BuildZoneVisual()
        {
            if(zoneMaterial==null)
                zoneMaterial=RuntimeShaderLibrary.CreateGroundTelegraph(HotZoneGreen,0f,RingAlpha,.055f);
            if(zoneMaterial==null)return;

            DestroyZoneVisual(0f);
            zoneRoot=new GameObject($"DF_HotZoneVisual_{occurrence+1}");
            zoneRoot.transform.position=transform.position;
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;
            if(world!=null)zoneRoot.transform.SetParent(world,true);

            outerRingMesh=BuildSurfaceDisc(zoneRoot.transform,Radius,.24f);
            CreateRing("DF_HotZoneOuterRing",outerRingMesh);

            innerRingMesh=BuildSurfaceDisc(zoneRoot.transform,Radius*InnerRingRadiusScale,.26f);
            CreateRing("DF_HotZoneInnerRing",innerRingMesh);
        }

        private void CreateRing(string name,Mesh mesh)
        {
            GameObject ring=new(name);
            ring.transform.SetParent(zoneRoot.transform,false);
            ring.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=zoneMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }

        private static Mesh BuildSurfaceDisc(Transform zone,float radius,float heightOffset)
        {
            const int slices=96,bands=16;
            Vector3[] vertices=new Vector3[(bands+1)*(slices+1)];
            Vector2[] uv=new Vector2[vertices.Length];
            int[] triangles=new int[bands*slices*6];
            for(int band=0;band<=bands;band++)
            {
                float distance=radius*band/bands;
                for(int slice=0;slice<=slices;slice++)
                {
                    float angle=slice*Mathf.PI*2f/slices;
                    float x=Mathf.Cos(angle)*distance,z=Mathf.Sin(angle)*distance;
                    Vector3 world=zone.TransformPoint(new Vector3(x,0,z));
                    float y=LunarSurfaceField.Surface(world.x,world.z).y+heightOffset;
                    int index=band*(slices+1)+slice;
                    vertices[index]=zone.InverseTransformPoint(new Vector3(world.x,y,world.z));
                    uv[index]=new Vector2(.5f+x/(radius*2f),.5f+z/(radius*2f));
                }
            }
            for(int band=0;band<bands;band++)
                for(int slice=0;slice<slices;slice++)
                {
                    int a=band*(slices+1)+slice,b=a+slices+1,t=(band*slices+slice)*6;
                    triangles[t]=a;triangles[t+1]=a+1;triangles[t+2]=b;
                    triangles[t+3]=a+1;triangles[t+4]=b+1;triangles[t+5]=b;
                }
            Mesh mesh=new(){name=$"DF_HotZoneSurfaceRing_{radius:0.00}",vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateNormals();
            return mesh;
        }

        private void AnimateZone()
        {
            if(zoneRoot==null||zoneMaterial==null)return;
            bool occupied=zeroInside>0||lumenInside>0;
            bool contested=zeroInside>0&&lumenInside>0;
            float wave=.5f+.5f*Mathf.Sin(Time.time*(occupied?3.0f:2.0f));
            float alpha=(contested?.50f:occupied?.46f:RingAlpha)+wave*.025f;
            zoneMaterial.SetFloat("_FillAlpha",0f);
            zoneMaterial.SetFloat("_RingAlpha",alpha);
        }

        private void DestroyZoneVisual(float delay)
        {
            if(zoneRoot!=null)
            {
                if(delay>0f)Destroy(zoneRoot,delay);else Destroy(zoneRoot);
                zoneRoot=null;
            }
            if(outerRingMesh!=null){Destroy(outerRingMesh,delay);outerRingMesh=null;}
            if(innerRingMesh!=null){Destroy(innerRingMesh,delay);innerRingMesh=null;}
        }

        private void OnDestroy()
        {
            if(owner==this)owner=null;
            DestroyZoneVisual(0f);
            if(zoneMaterial!=null)Destroy(zoneMaterial);
        }
    }
}
