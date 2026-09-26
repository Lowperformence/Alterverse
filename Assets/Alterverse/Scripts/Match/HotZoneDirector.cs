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

        private enum State { Waiting, Active, Finished }
        private State state;
        private static HotZoneDirector owner;
        private bool rewardIssued,openingAnnounced;
        private int occurrence;
        private float elapsed,nextOpenAt=SpawnInterval,activeElapsed,zeroTime,lumenTime,nextScan;
        private int zeroInside,lumenInside;
        private GameObject zoneRoot;
        private Transform pulseRing,innerRing;
        private Renderer[] markerRenderers=System.Array.Empty<Renderer>();
        private Material neutralMat,zeroMat,lumenMat;
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

            // Every hero contributes one point per second to their own team.
            // Opposing heroes do not cancel scoring: both teams can score simultaneously.
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

            // Deterministic safe fallback in the central arena if random rejection is exhausted.
            float fallbackZ=(occurrence&1)==0?12f:-12f;
            fallback=LunarSurfaceField.Surface(0f,fallbackZ);
            lastSpawnPosition=fallback;hasLastSpawn=true;
            return fallback;
        }

        private bool IsSpawnPositionSafe(Vector3 candidate)
        {
            // Keep the enlarged circle away from both base healing/spawn pads.
            float baseClearance=Radius+2.25f;
            Vector3 left=new(-SpawnPadX,candidate.y,0);
            Vector3 right=new(SpawnPadX,candidate.y,0);
            Vector3 dl=candidate-left;dl.y=0;
            Vector3 dr=candidate-right;dr.y=0;
            if(dl.sqrMagnitude<baseClearance*baseClearance||dr.sqrMagnitude<baseClearance*baseClearance)return false;

            // Spawn-time-only structure check: never place the zone centre directly on a large
            // gameplay structure. This is intentionally not a per-frame physics cost.
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
            if(zoneRoot!=null){Destroy(zoneRoot,1.2f);zoneRoot=null;}
            pulseRing=innerRing=null;markerRenderers=System.Array.Empty<Renderer>();

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

            if(zoneRoot!=null){Destroy(zoneRoot,1.2f);zoneRoot=null;}
            pulseRing=innerRing=null;markerRenderers=System.Array.Empty<Renderer>();
            zeroInside=lumenInside=0;
            occurrence++;
            state=State.Waiting;
        }

        private void ResolveNoReward()
        {
            state=State.Finished;rewardIssued=true;
            if(zoneRoot!=null){Destroy(zoneRoot);zoneRoot=null;}
            pulseRing=innerRing=null;markerRenderers=System.Array.Empty<Renderer>();
        }

        private void BuildZoneVisual()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return;

            // Reuse the same three materials across recurring Hot Zones to avoid allocations.
            neutralMat??=CreateMaterial(shader,new Color(.42f,.44f,.52f,1),1.1f);
            zeroMat??=CreateMaterial(shader,StylizedArtDirection.ZeroEnergy,2.1f);
            lumenMat??=CreateMaterial(shader,StylizedArtDirection.LumenEnergy,2.1f);

            if(zoneRoot!=null)Destroy(zoneRoot);
            zoneRoot=new GameObject($"DF_HotZoneVisual_{occurrence+1}");
            zoneRoot.transform.position=transform.position;
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;if(world!=null)zoneRoot.transform.SetParent(world,true);

            pulseRing=CreateRing(zoneRoot.transform,"DF_HotZoneOuter",Radius,.14f,neutralMat,64);
            innerRing=CreateRing(zoneRoot.transform,"DF_HotZoneInner",Radius*.72f,.075f,neutralMat,48);
            CreateRing(zoneRoot.transform,"DF_HotZoneCoreRing",Radius*.34f,.055f,neutralMat,32);
            GameObject center=GameObject.CreatePrimitive(PrimitiveType.Cylinder);center.name="DF_HotZoneCenter";
            center.transform.SetParent(zoneRoot.transform,false);center.transform.localPosition=Vector3.up*.045f;center.transform.localScale=new Vector3(2.1f,.018f,2.1f);
            Collider centerCollider=center.GetComponent<Collider>();if(centerCollider!=null)centerCollider.enabled=false;
            Renderer centerRenderer=center.GetComponent<Renderer>();if(centerRenderer!=null){centerRenderer.sharedMaterial=neutralMat;centerRenderer.shadowCastingMode=ShadowCastingMode.Off;centerRenderer.receiveShadows=false;}
            markerRenderers=new Renderer[8];
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*2f/8f;
                Vector3 p=new(Mathf.Cos(a)*(Radius-.45f),.18f,Mathf.Sin(a)*(Radius-.45f));
                GameObject shard=GameObject.CreatePrimitive(PrimitiveType.Cube);shard.name="DF_HotZoneMarker";shard.transform.SetParent(zoneRoot.transform,false);
                shard.transform.localPosition=p;shard.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,45);
                shard.transform.localScale=new Vector3(.10f,.42f,.22f);
                Collider col=shard.GetComponent<Collider>();if(col!=null)col.enabled=false;
                Renderer r=shard.GetComponent<Renderer>();
                if(r!=null){r.sharedMaterial=neutralMat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;markerRenderers[i]=r;}
            }
        }

        private void AnimateZone()
        {
            if(zoneRoot==null)return;
            bool zero=zeroInside>0&&lumenInside==0,lumen=lumenInside>0&&zeroInside==0;
            Material active=zero?zeroMat:lumen?lumenMat:neutralMat;
            if(pulseRing!=null&&pulseRing.TryGetComponent(out LineRenderer a))a.sharedMaterial=active;
            if(innerRing!=null&&innerRing.TryGetComponent(out LineRenderer b))b.sharedMaterial=active;
            for(int i=0;i<markerRenderers.Length;i++)
                if(markerRenderers[i]!=null)markerRenderers[i].sharedMaterial=active;
            zoneRoot.transform.Rotate(0,9f*Time.deltaTime,0,Space.World);
            float pulse=1f+Mathf.Sin(Time.time*4.2f)*.028f;
            if(pulseRing!=null)pulseRing.localScale=new Vector3(pulse,1,pulse);
        }

        private static Material CreateMaterial(Shader shader,Color color,float emission)
        {
            Material m=new(shader){color=color};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_EmissionColor")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.35f);
            return m;
        }

        private static Transform CreateRing(Transform parent,string name,float radius,float width,Material material,int segments)
        {
            GameObject go=new(name);go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.up*.12f;
            LineRenderer lr=go.AddComponent<LineRenderer>();lr.sharedMaterial=material;lr.loop=true;lr.useWorldSpace=false;lr.positionCount=segments;
            lr.widthMultiplier=width;lr.shadowCastingMode=ShadowCastingMode.Off;lr.receiveShadows=false;
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;lr.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            return go.transform;
        }

        private void OnDestroy()
        {
            if(neutralMat!=null)Destroy(neutralMat);if(zeroMat!=null)Destroy(zeroMat);if(lumenMat!=null)Destroy(lumenMat);
        }
    }
}
