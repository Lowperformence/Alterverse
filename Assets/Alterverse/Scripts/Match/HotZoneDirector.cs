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
    /// stays active for 30 seconds, and relocates inside the central 60% of the arena.
    /// Every hero inside scores independently for their team, even while both teams occupy the zone.
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
        private const float CentralWidthFraction=.60f;
        private const float SpawnPadX=27.5f;
        private static readonly Color HotZoneGreen=new(.38f,.96f,.28f,1f);

        private enum State { Waiting, Active, Finished }
        private State state;
        private static HotZoneDirector owner;
        private bool rewardIssued,openingAnnounced;
        private int occurrence;
        private float elapsed,nextOpenAt=SpawnInterval,activeElapsed,zeroTime,lumenTime,nextScan;
        private int zeroInside,lumenInside;
        private GameObject zoneRoot;
        private Transform zoneDisc;
        private Mesh zoneMesh;
        private Renderer zoneRenderer;
        private Material zoneMaterial;
        private DimensionsFront front;
        private System.Random rng;
        private Vector3 lastSpawnPosition;
        private bool hasLastSpawn;
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
            PrototypeVfx.RadialBurst(transform.position,new Color(.82f,.86f,1f),Radius*.92f);
            PrototypeVfx.Burst(transform.position,new Color(.82f,.86f,1f),1.15f,10);
            HotZoneOpened?.Invoke();
            if(Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
                NetworkZoneOpened?.Invoke(transform.position,occurrence);
        }

        private Vector3 ChooseSpawnPosition()
        {
            float xLimit=ArenaHalfWidth*CentralWidthFraction;
            float zLimit=Mathf.Max(1f,ArenaHalfHeight-Radius-.75f);
            Vector3 fallback=LunarSurfaceField.Surface(0f,0f);

            for(int attempt=0;attempt<24;attempt++)
            {
                float x=Next(-xLimit,xLimit);
                float z=Next(-zLimit,zLimit);
                Vector3 candidate=LunarSurfaceField.Surface(x,z);
                if(!IsSpawnPositionSafe(candidate))continue;
                if(hasLastSpawn)
                {
                    Vector3 delta=candidate-lastSpawnPosition;delta.y=0;
                    if(delta.sqrMagnitude<6.5f*6.5f)continue;
                }
                lastSpawnPosition=candidate;hasLastSpawn=true;
                return candidate;
            }

            float fallbackZ=(occurrence&1)==0?12f:-12f;
            fallback=LunarSurfaceField.Surface(0f,fallbackZ);
            lastSpawnPosition=fallback;hasLastSpawn=true;
            return fallback;
        }

        private bool IsSpawnPositionSafe(Vector3 candidate)
        {
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

            Color resolutionColor=winner==0?StylizedArtDirection.ZeroEnergy:
                winner==1?StylizedArtDirection.LumenEnergy:new Color(.72f,.74f,.80f);
            PrototypeVfx.RadialBurst(transform.position,resolutionColor,Radius);
            PrototypeVfx.Burst(transform.position,resolutionColor,1.35f,12);
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
            PrototypeVfx.RadialBurst(transform.position,new Color(.82f,.86f,1f),Radius*.92f);
            PrototypeVfx.Burst(transform.position,new Color(.82f,.86f,1f),1.15f,10);
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

            Color resolutionColor=winner==0?StylizedArtDirection.ZeroEnergy:
                winner==1?StylizedArtDirection.LumenEnergy:new Color(.72f,.74f,.80f);
            PrototypeVfx.RadialBurst(transform.position,resolutionColor,Radius);
            PrototypeVfx.Burst(transform.position,resolutionColor,1.35f,12);
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
                zoneMaterial=RuntimeShaderLibrary.CreateGroundTelegraph(HotZoneGreen,.115f,.72f,.045f);
            if(zoneMaterial==null)return;

            DestroyZoneVisual(0f);
            zoneRoot=new GameObject($"DF_HotZoneVisual_{occurrence+1}");
            zoneRoot.transform.position=transform.position;
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;
            if(world!=null)zoneRoot.transform.SetParent(world,true);

            GameObject disc=new("DF_HotZoneGroundDisc");
            disc.transform.SetParent(zoneRoot.transform,false);
            zoneMesh=BuildSurfaceDisc(zoneRoot.transform);
            disc.AddComponent<MeshFilter>().sharedMesh=zoneMesh;
            zoneRenderer=disc.AddComponent<MeshRenderer>();
            if(zoneRenderer!=null)
            {
                zoneRenderer.sharedMaterial=zoneMaterial;
                zoneRenderer.shadowCastingMode=ShadowCastingMode.Off;
                zoneRenderer.receiveShadows=false;
            }
            zoneDisc=disc.transform;
        }

        private static Mesh BuildSurfaceDisc(Transform zone)
        {
            const int slices=96, bands=16;
            Vector3[] vertices=new Vector3[(bands+1)*(slices+1)];
            Vector2[] uv=new Vector2[vertices.Length];
            int[] triangles=new int[bands*slices*6];
            for(int band=0;band<=bands;band++)
            {
                float distance=Radius*band/bands;
                for(int slice=0;slice<=slices;slice++)
                {
                    float angle=slice*Mathf.PI*2f/slices;
                    float x=Mathf.Cos(angle)*distance,z=Mathf.Sin(angle)*distance;
                    Vector3 world=zone.TransformPoint(new Vector3(x,0,z));
                    float y=LunarSurfaceField.Surface(world.x,world.z).y+.24f;
                    int index=band*(slices+1)+slice;
                    vertices[index]=zone.InverseTransformPoint(new Vector3(world.x,y,world.z));
                    uv[index]=new Vector2(.5f+x/(Radius*2f),.5f+z/(Radius*2f));
                }
            }
            for(int band=0;band<bands;band++)
                for(int slice=0;slice<slices;slice++)
                {
                    int a=band*(slices+1)+slice,b=a+slices+1,t=(band*slices+slice)*6;
                    triangles[t]=a;triangles[t+1]=a+1;triangles[t+2]=b;
                    triangles[t+3]=a+1;triangles[t+4]=b+1;triangles[t+5]=b;
                }
            Mesh mesh=new(){name="DF_HotZoneSurfaceDisc",vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateNormals();
            return mesh;
        }

        private void AnimateZone()
        {
            if(zoneRoot==null||zoneMaterial==null)return;
            bool occupied=zeroInside>0||lumenInside>0;
            bool contested=zeroInside>0&&lumenInside>0;
            float wave=.5f+.5f*Mathf.Sin(Time.time*(occupied?3.0f:2.0f));
            float baseFill=contested?.145f:occupied?.135f:.105f;
            float baseRing=contested?.78f:occupied?.82f:.67f;
            zoneMaterial.SetFloat("_FillAlpha",baseFill+wave*.018f);
            zoneMaterial.SetFloat("_RingAlpha",Mathf.Min(1f,baseRing+wave*.08f));
            zoneMaterial.SetFloat("_RingWidth",occupied?.050f:.043f);
        }

        private void DestroyZoneVisual(float delay)
        {
            if(zoneRoot!=null)
            {
                if(delay>0f)Destroy(zoneRoot,delay);else Destroy(zoneRoot);
                zoneRoot=null;
            }
            zoneDisc=null;
            zoneRenderer=null;
            if(zoneMesh!=null){Destroy(zoneMesh,delay);zoneMesh=null;}
        }

        private void OnDestroy()
        {
            if(owner==this)owner=null;
            DestroyZoneVisual(0f);
            if(zoneMaterial!=null)Destroy(zoneMaterial);
        }
    }
}
