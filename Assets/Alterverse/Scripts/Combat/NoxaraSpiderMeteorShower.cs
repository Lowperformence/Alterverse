using Alterverse.Match;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    /// <summary>
    /// Noxara-only secondary attack for the Hot Zone reward spider.
    /// Reuses the planet meteor shower's damage value but has poison-green presentation
    /// and only damages the opposing team.
    /// </summary>
    [RequireComponent(typeof(HotZoneTank),typeof(Health))]
    public sealed class NoxaraSpiderMeteorShower : MonoBehaviour
    {
        private const float Radius=11f;
        private const float Cooldown=8.5f;
        private const float Telegraph=1.55f;
        private const int Meteors=7;
        private const int ScanBufferSize=32;
        public static event System.Action<int,Vector3,float,float,float> NetworkMeteorSpawned;

        private readonly Collider[] scanBuffer=new Collider[ScanBufferSize];
        private HotZoneTank owner;
        private Health self;
        private float nextAttack,nextScan;
        private bool enemyNearby;

        private void Awake()
        {
            owner=GetComponent<HotZoneTank>();
            self=GetComponent<Health>();
            nextAttack=Time.time+3f;
        }

        private void Update()
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            if(Time.timeScale<=0f||owner==null||self==null||self.IsDead||!owner.IsNoxaraSpider)return;

            if(Time.time>=nextScan)
            {
                nextScan=Time.time+.25f;
                enemyNearby=HasEnemyInRange();
            }

            if(!enemyNearby||Time.time<nextAttack)return;
            nextAttack=Time.time+Cooldown;

            for(int i=0;i<Meteors;i++)
            {
                Vector2 offset=Random.insideUnitCircle*Radius;
                Vector3 point=transform.position+new Vector3(offset.x,0f,offset.y);
                point.y=LunarSurfaceField.WorldSurface(point.x,point.z).y;
                float stagger=i*.16f;
                NoxaraMeteorStrike.Create(point,Telegraph,PlanetMeteorShower.MeteorDamage,stagger,owner);
                if(Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
                    NetworkMeteorSpawned?.Invoke(owner.Team,point,Telegraph,PlanetMeteorShower.MeteorDamage,stagger);
            }
        }

        public static void ApplyNetworkMeteor(int team,Vector3 point,float delay,float damage,float stagger)
        {
            if(Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            System.Collections.Generic.IReadOnlyList<HotZoneTank> tanks=HotZoneTank.Active;
            for(int i=0;i<tanks.Count;i++)
            {
                HotZoneTank tank=tanks[i];
                if(tank!=null&&tank.IsNoxaraSpider&&tank.Team==team&&tank.TankHealth!=null&&!tank.TankHealth.IsDead)
                {NoxaraMeteorStrike.Create(point,delay,damage,stagger,tank);return;}
            }
        }

        private bool HasEnemyInRange()
        {
            int hits=Physics.OverlapSphereNonAlloc(transform.position,Radius,scanBuffer);
            for(int i=0;i<hits;i++)
            {
                Collider collider=scanBuffer[i];
                if(collider==null)continue;
                TeamMember member=collider.GetComponentInParent<TeamMember>();
                if(member==null||member.Team==owner.Team)continue;
                Health health=member.GetComponent<Health>();
                if(health!=null&&!health.IsDead)return true;
            }
            return false;
        }
    }

    public sealed class NoxaraMeteorStrike : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<NoxaraMeteorStrike> active=new();
        public static System.Collections.Generic.IReadOnlyList<NoxaraMeteorStrike> Active=>active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()=>active.Clear();

        private const float ImpactRadius=2.1f;
        private const int HitBufferSize=32;
        private static readonly Color PoisonGreen=new(.38f,1f,.04f,1f);
        private static readonly Color PoisonGreenHot=new(.62f,1f,.08f,1f);

        private readonly Collider[] hitBuffer=new Collider[HitBufferSize];
        private Material warningInk,warningRingInk,rockMaterial;
        private Mesh meteorMesh;
        private LineRenderer warningLine,trail;
        private float spawnAt,hitAt,damage;
        private GameObject warning,meteor;
        private Vector3 impactPosition;
        private HotZoneTank owner;
        private DimensionsFront front;
        private bool resolved;

        public Vector3 ImpactPosition=>impactPosition;
        public float BlastRadius=>ImpactRadius;
        public float TelegraphAge=>Mathf.Max(0f,Time.time-spawnAt);
        public float TimeRemaining=>Mathf.Max(0f,hitAt-Time.time);
        public bool IsTelegraphing=>!resolved&&Time.time<hitAt&&OwnerAlive();

        private void OnEnable(){if(!active.Contains(this))active.Add(this);}
        private void OnDisable(){active.Remove(this);}

        public static void Create(Vector3 point,float delay,float dmg,float stagger,HotZoneTank source)
        {
            GameObject root=new("DF_NoxaraMeteorStrike");
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;
            if(world!=null)root.transform.SetParent(world,true);

            NoxaraMeteorStrike strike=root.AddComponent<NoxaraMeteorStrike>();
            strike.owner=source;
            strike.damage=Mathf.Max(1f,dmg);
            strike.spawnAt=Time.time;
            strike.hitAt=Time.time+Mathf.Max(.35f,delay)+Mathf.Max(0f,stagger);
            strike.impactPosition=point;
            strike.front=FindAnyObjectByType<DimensionsFront>();
            strike.BuildVisuals(world);
        }

        private bool OwnerAlive()=>owner!=null&&owner.isActiveAndEnabled&&owner.TankHealth!=null&&!owner.TankHealth.IsDead&&owner.IsNoxaraSpider;

        private void BuildVisuals(Transform world)
        {
            Shader ink=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Sprites/Default");
            Shader rock=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(ink==null||rock==null)return;

            warningInk=new Material(ink){name="DF_NoxaraMeteorWarningInk",color=PoisonGreen};
            warningRingInk=new Material(Resources.Load<Shader>("DF_TargetRing")??ink){name="DF_NoxaraMeteorTargetRing",color=PoisonGreen};
            rockMaterial=new Material(rock){name="DF_NoxaraMeteorRock",color=new Color(.055f,.075f,.045f,1f)};
            if(rockMaterial.HasProperty("_BaseColor"))rockMaterial.SetColor("_BaseColor",new Color(.055f,.075f,.045f,1f));
            if(rockMaterial.HasProperty("_Smoothness"))rockMaterial.SetFloat("_Smoothness",.10f);
            if(rockMaterial.HasProperty("_EmissionColor"))
            {
                rockMaterial.EnableKeyword("_EMISSION");
                rockMaterial.SetColor("_EmissionColor",PoisonGreen*2.4f);
            }

            warning=new GameObject("DF_NoxaraMeteorWarning");
            if(world!=null)warning.transform.SetParent(world,false);
            warningLine=warning.AddComponent<LineRenderer>();
            warningLine.sharedMaterial=warningRingInk;
            warningLine.loop=true;
            warningLine.useWorldSpace=true;
            warningLine.positionCount=48;
            warningLine.widthMultiplier=.075f;
            Color warningColor=PoisonGreenHot;
            warningColor.a=.5f;
            warningLine.startColor=warningLine.endColor=warningColor;
            warningLine.shadowCastingMode=ShadowCastingMode.Off;
            warningLine.receiveShadows=false;

            for(int i=0;i<48;i++)
            {
                float angle=i*Mathf.PI*2f/48f;
                warningLine.SetPosition(i,new Vector3(
                    impactPosition.x+Mathf.Cos(angle)*ImpactRadius,
                    impactPosition.y+.15f,
                    impactPosition.z+Mathf.Sin(angle)*ImpactRadius));
            }

            meteorMesh=LunarFragmentGeometry.Create(17);
            meteor=ProductionMeshFactory.MeshObject("DF_NoxaraFallingMeteor",world,meteorMesh,rockMaterial);
            meteor.transform.localScale=Vector3.one*.58f;
            meteor.transform.position=impactPosition+Vector3.up*9f;

            trail=meteor.AddComponent<LineRenderer>();
            trail.sharedMaterial=warningInk;
            trail.useWorldSpace=true;
            trail.positionCount=2;
            trail.startWidth=.17f;
            trail.endWidth=.018f;
            trail.startColor=PoisonGreenHot;
            trail.endColor=new Color(.08f,.42f,.015f,.25f);
            trail.shadowCastingMode=ShadowCastingMode.Off;
            trail.receiveShadows=false;
        }

        private void Update()
        {
            if(!OwnerAlive()||(front!=null&&front.HasEnded)){Cleanup();return;}
            if(Time.timeScale<=0f)return;

            if(Time.time<hitAt)
            {
                if(meteor!=null)
                {
                    float remaining=Mathf.Max(0f,hitAt-Time.time);
                    float height=Mathf.Lerp(.35f,9f,Mathf.Clamp01(remaining/1.7f));
                    meteor.transform.position=new Vector3(impactPosition.x,impactPosition.y+height,impactPosition.z);
                    meteor.transform.Rotate(82f*Time.deltaTime,42f*Time.deltaTime,58f*Time.deltaTime);

                    if(trail!=null)
                    {
                        trail.SetPosition(0,meteor.transform.position);
                        trail.SetPosition(1,meteor.transform.position+new Vector3(.12f,1.55f,.08f));
                    }
                    if(warningLine!=null)
                        warningLine.widthMultiplier=.06f+.03f*(1f-Mathf.Clamp01(remaining/1.7f));
                }
                return;
            }

            ResolveImpact();
        }

        private void ResolveImpact()
        {
            if(resolved)return;
            resolved=true;

            Vector3 impact=new(impactPosition.x,impactPosition.y+.3f,impactPosition.z);
            Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.Explosion,impact,.52f);
            BomberExplosionVfx.Play(impact,PoisonGreenHot,ImpactRadius);
            PrototypeVfx.Ring(impact,PoisonGreen,ImpactRadius*1.05f,.3f);

            int hits=Physics.OverlapSphereNonAlloc(impactPosition,ImpactRadius,hitBuffer);
            for(int i=0;i<hits;i++)
            {
                Collider collider=hitBuffer[i];
                if(collider==null)continue;
                Health health=collider.GetComponentInParent<Health>();
                if(health==null||health.IsDead||health==owner.TankHealth)continue;

                bool alreadyHit=false;
                for(int j=0;j<i;j++)
                {
                    Collider prior=hitBuffer[j];
                    if(prior!=null&&prior.GetComponentInParent<Health>()==health){alreadyHit=true;break;}
                }
                if(alreadyHit)continue;

                TeamMember member=health.GetComponent<TeamMember>();
                if(member==null||member.Team==owner.Team)continue;
                if(health.GetComponent<RealityCore>()!=null||health.GetComponent<LaneTower>()!=null||health.GetComponent<DimensionPlanet>()!=null)continue;

                health.TakeDamage(damage,owner.gameObject);
            }

            Cleanup();
        }

        private void Cleanup()
        {
            if(warning!=null)Destroy(warning);
            if(meteor!=null)Destroy(meteor);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            active.Remove(this);
            if(warning!=null)Destroy(warning);
            if(meteor!=null)Destroy(meteor);
            if(warningInk!=null)Destroy(warningInk);
            if(warningRingInk!=null)Destroy(warningRingInk);
            if(rockMaterial!=null)Destroy(rockMaterial);
            if(meteorMesh!=null)Destroy(meteorMesh);
        }
    }
}

