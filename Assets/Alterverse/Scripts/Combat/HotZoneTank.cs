using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    /// <summary>Roaming reward spawned by either of the two scheduled Hot Zones.</summary>
    [RequireComponent(typeof(Health),typeof(TeamMember))]
    public sealed class HotZoneTank:MonoBehaviour
    {
        private const float MaxHp=680f;
        private const float Damage=10f;
        private const float AttackRange=5.8f;
        private const float AttackCooldown=1.2f;
        private const float MoveSpeed=2.25f;
        private const float AcquireRadius=14f;
        private const float ChaseLeash=14f;
        private const float ScanInterval=.18f;
        private const float ShotWindup=.22f;
        private const float ArenaHalfWidth=39f;
        private const float ArenaHalfHeight=32f;

        private int team;
        private bool marsSkeleton,xenoAlien,noxaraSpider,kryonBear;
        private Health health,target;
        private HeroLocomotion locomotion;
        private float nextAttack,nextScan,blockedTime,detourUntil,shotAt;
        private Vector3 patrolTarget,home,detourDirection;
        private int patrolIndex;
        private bool shotPending;
        private Health shotTarget;
        private Transform turretPivot,barrelPivot,leftTrack,rightTrack;
        private Transform xenoVisualRoot,xenoTorsoPivot,xenoPelvisPivot,xenoHeadPivot,xenoBackRoot;
        private Transform xenoLeftShoulderPivot,xenoRightShoulderPivot,xenoLeftForearmPivot,xenoRightForearmPivot,xenoLeftClaw,xenoRightClaw;
        private Transform xenoLeftHipPivot,xenoRightHipPivot,xenoLeftShinPivot,xenoRightShinPivot,xenoLeftFoot,xenoRightFoot;
        private Vector3 xenoVisualRootBasePosition,xenoCoreBaseScale;
        private float xenoMoveBlend,xenoAttackKick,xenoHitUntil;
        private Color xenoArmorBase,xenoEnergyBase;
        private Renderer xenoCoreRenderer;
        private readonly List<Mesh> xenoMeshes=new();
        private Transform skeletonLeftArm,skeletonRightArm,skeletonLeftLeg,skeletonRightLeg,skeletonSkull,skeletonPelvis;
        private Quaternion skeletonLeftArmBase,skeletonRightArmBase,skeletonLeftLegBase,skeletonRightLegBase,skeletonSkullBase;
        private Vector3 skeletonPelvisBaseScale;
        private float skeletonMoveBlend;
        private const float SpiderUpperLength=1.08f;
        private const float SpiderLowerLength=1.18f;
        private const float SpiderSwingFraction=.34f;
        private Transform spiderBodyRoot;
        private readonly Transform[] spiderLegRoots=new Transform[8];
        private readonly Transform[] spiderKneePivots=new Transform[8];
        private readonly Transform[] spiderFootPivots=new Transform[8];
        private readonly Quaternion[] spiderLegBase=new Quaternion[8];
        private readonly Vector3[] spiderHipLocal=new Vector3[8];
        private readonly Vector3[] spiderFootPlant=new Vector3[8];
        private readonly Vector3[] spiderSwingStart=new Vector3[8];
        private readonly Vector3[] spiderSwingEnd=new Vector3[8];
        private readonly bool[] spiderWasSwing=new bool[8];
        private Vector3 spiderBodyRest,spiderBarrelRest;
        private float spiderMoveBlend,spiderGaitClock,spiderTurnLean,spiderVisualYaw;
        private Vector3 spiderPreviousForward,spiderPreviousWorldPosition;
        private bool spiderFeetInitialized;
        private Color spiderEnergyBase=new(1f,.72f,.04f,1f);
        private Transform bearBodyRoot,bearHeadPivot;
        private readonly Transform[] bearLegRoots=new Transform[4];
        private readonly Quaternion[] bearLegBase=new Quaternion[4];
        private Vector3 bearBodyRest,bearBarrelRest;
        private float bearMoveBlend;
        private Color bearEnergyBase=new(.28f,.94f,1f,1f);
        private int movementRecoveryAttempt;
        private static readonly List<HotZoneTank> active=new();
        public static IReadOnlyList<HotZoneTank> Active=>active;
        public int Team=>team;
        public Health TankHealth=>health;
        public bool IsMarsSkeleton=>marsSkeleton;
        public bool IsXenoTitan=>xenoAlien;
        public bool IsNoxaraSpider=>noxaraSpider;
        public bool IsKryonBear=>kryonBear;
        private Material armorMat,darkMat,energyMat,jointMat,teamMat,bearIceMat;

        private static readonly Vector3[] ZeroPatrol=
        {
            new(-4,0,0),new(0,0,12),new(8,0,4),new(3,0,-12),new(14,0,-3)
        };
        private static readonly Vector3[] LumenPatrol=
        {
            new(4,0,0),new(0,0,-12),new(-8,0,-4),new(-3,0,12),new(-14,0,3)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()=>active.Clear();
        private void OnEnable(){if(!active.Contains(this))active.Add(this);}
        private void OnDisable(){active.Remove(this);shotPending=false;shotTarget=null;}

        public static bool Spawn(int winningTeam)
        {
            int team=Mathf.Clamp(winningTeam,0,1);

            // Two Hot Zones may resolve in one match. Do not let the early reward suppress
            // the original minute-five reward if its unit is still alive.
            int living=0;
            for(int i=0;i<active.Count;i++)
            {
                HotZoneTank existing=active[i];
                if(existing!=null&&existing.health!=null&&!existing.health.IsDead)living++;
            }
            if(living>=2)return false;

            Vector3 spawn=new(team==0?-4f:4f,.95f,0f);
            for(int i=0;i<active.Count;i++)
            {
                HotZoneTank existing=active[i];
                if(existing==null||existing.health==null||existing.health.IsDead)continue;
                Vector3 flat=existing.transform.position-spawn;flat.y=0;
                if(flat.sqrMagnitude<2.4f*2.4f)
                {
                    spawn.z=team==0?2.6f:-2.6f;
                    break;
                }
            }
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bool skeleton=ArenaVariantRuntime.IsMars,alien=ArenaVariantRuntime.IsGreenPlanet,spider=ArenaVariantRuntime.IsNoxara,bear=ArenaVariantRuntime.IsKryon;
            go.name=bear?(team==0?"DF_ZeroKryonFrostBear":"DF_LumenKryonFrostBear"):
                spider?(team==0?"DF_ZeroNoxaraSpider":"DF_LumenNoxaraSpider"):
                alien?(team==0?"DF_ZeroXenoTitan":"DF_LumenXenoTitan"):
                skeleton?(team==0?"DF_ZeroMarsTitan":"DF_LumenMarsTitan"):
                (team==0?"DF_ZeroHotZoneTank":"DF_LumenHotZoneTank");
            go.transform.position=spawn;
            go.transform.localScale=new Vector3(1.15f,.9f,1.35f);
            GameObject world=GameObject.Find("DF_MatchWorld");if(world!=null)go.transform.SetParent(world.transform,true);

            Renderer baseRenderer=go.GetComponent<Renderer>();if(baseRenderer!=null)baseRenderer.enabled=false;
            Health hp=go.AddComponent<Health>();hp.Configure(MaxHp,true);
            TeamMember member=go.AddComponent<TeamMember>();member.Initialize(team);
            go.AddComponent<HotZoneTank>().Initialize(team,spawn,skeleton,alien,spider,bear);
            return true;
        }

        private void Initialize(int assignedTeam,Vector3 spawn,bool skeleton,bool alien,bool spider,bool bear)
        {
            gameObject.layer=GameplayCollision.Units;
            team=assignedTeam;marsSkeleton=skeleton;xenoAlien=alien;noxaraSpider=spider;kryonBear=bear;home=spawn;health=GetComponent<Health>();
            locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();
            if(kryonBear)BuildKryonBearVisuals();
            else if(noxaraSpider)
            {
                BuildNoxaraSpiderVisuals();
                if(GetComponent<NoxaraSpiderMeteorShower>()==null)gameObject.AddComponent<NoxaraSpiderMeteorShower>();
            }
            else if(xenoAlien){BuildAlienVisuals();health.Damaged+=OnXenoDamaged;}
            else if(marsSkeleton)BuildSkeletonVisuals();
            else BuildVisuals();
            SetNextPatrol();
        }

        private void Update()
        {
            if(Time.timeScale<=0f||health==null||health.IsDead)return;
            if(xenoAlien){xenoMoveBlend=Mathf.MoveTowards(xenoMoveBlend,0f,Time.deltaTime*3.4f);AnimateXeno();}
            if(marsSkeleton){skeletonMoveBlend=Mathf.MoveTowards(skeletonMoveBlend,0f,Time.deltaTime*3.4f);AnimateSkeleton();}
            if(noxaraSpider)AnimateNoxaraSpider();
            if(kryonBear){bearMoveBlend=Mathf.MoveTowards(bearMoveBlend,0f,Time.deltaTime*3.6f);AnimateKryonBear();}
            if(shotPending&&Time.time>=shotAt)ResolveShot();
            if(Time.time>=nextScan)
            {
                nextScan=Time.time+ScanInterval;
                ValidateOrAcquireTarget();
            }

            if(target!=null)
            {
                AimTurretAt(target.transform.position);
                Vector3 delta=target.transform.position-transform.position;delta.y=0;
                if(delta.sqrMagnitude>ChaseLeash*ChaseLeash){target=null;SetNextPatrol();return;}
                if(delta.sqrMagnitude<=AttackRange*AttackRange)
                {
                    locomotion.FaceAction(delta,AttackCooldown);
                    if(Time.time>=nextAttack&&!shotPending)BeginFire();
                    return;
                }
                MoveToward(target.transform.position,.9f);
                return;
            }

            if((patrolTarget-transform.position).sqrMagnitude<1.2f*1.2f)SetNextPatrol();
            AimTurretAt(transform.position+transform.forward*4f);
            MoveToward(patrolTarget,.65f);
        }

        private void ValidateOrAcquireTarget()
        {
            if(target!=null)
            {
                TeamMember tm=target.GetComponent<TeamMember>();
                if(target.IsDead||tm==null||tm.Team==team||target.GetComponent<HeroIdentity>()==null)target=null;
            }
            if(target!=null)return;

            float bestSqr=AcquireRadius*AcquireRadius;
            Health best=null;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health h=Health.Active[i];
                if(h==null||h.IsDead||h==health||h.GetComponent<HeroIdentity>()==null)continue;
                TeamMember member=h.GetComponent<TeamMember>();if(member==null||member.Team==team)continue;
                Vector3 d=h.transform.position-transform.position;d.y=0;
                float sqr=d.sqrMagnitude;if(sqr<bestSqr){bestSqr=sqr;best=h;}
            }
            target=best;
        }

        private void BeginFire()
        {
            if(target==null||target.IsDead)return;
            shotPending=true;shotTarget=target;shotAt=Time.time+ShotWindup;
            nextAttack=Time.time+AttackCooldown;
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color energy=kryonBear?bearEnergyBase:noxaraSpider?spiderEnergyBase:xenoAlien?new Color(.62f,.90f,.18f):teamEnergy;
            bool creature=marsSkeleton||xenoAlien||noxaraSpider||kryonBear;
            Vector3 muzzle=creature&&barrelPivot!=null?barrelPivot.position:(barrelPivot!=null?barrelPivot.TransformPoint(new Vector3(0,0,1.70f)):transform.position+transform.forward*1.7f+Vector3.up*.95f);
            PrototypeVfx.Ring(muzzle,energy,kryonBear?.40f:noxaraSpider?.36f:xenoAlien?.32f:.34f,ShotWindup);
            if(kryonBear)PrototypeVfx.Burst(muzzle,Color.Lerp(energy,Color.white,.35f),.15f,4);
            else if(noxaraSpider)PrototypeVfx.Burst(muzzle,energy,.12f,3);
            else if(xenoAlien)PrototypeVfx.Burst(muzzle,teamEnergy,.10f,2);
        }

        private void ResolveShot()
        {
            shotPending=false;
            Health victim=shotTarget;shotTarget=null;
            if(victim==null||victim.IsDead)return;
            TeamMember victimTeam=victim.GetComponent<TeamMember>();
            if(victimTeam==null||victimTeam.Team==team)return;
            Vector3 delta=victim.transform.position-transform.position;delta.y=0;
            if(delta.sqrMagnitude>(AttackRange+.75f)*(AttackRange+.75f))return;

            bool creature=marsSkeleton||xenoAlien||noxaraSpider||kryonBear;
            Vector3 muzzle=creature&&barrelPivot!=null?barrelPivot.position:(barrelPivot!=null?barrelPivot.TransformPoint(new Vector3(0,0,1.70f)):transform.position+transform.forward*1.7f+Vector3.up*.95f);
            Vector3 hit=victim.transform.position+Vector3.up*.55f;
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color energy=kryonBear?bearEnergyBase:noxaraSpider?spiderEnergyBase:xenoAlien?new Color(.62f,.90f,.18f):teamEnergy;
            if(xenoAlien)
            {
                PrototypeVfx.BioBolt(muzzle,hit,energy,teamEnergy);
                xenoAttackKick=1f;
            }
            else
            {
                PrototypeVfx.ProjectileLine(muzzle,hit,energy,.13f);
                PrototypeVfx.Burst(muzzle,energy,.28f,4);
            }
            if(barrelPivot!=null&&!creature)barrelPivot.localPosition=new Vector3(0,.08f,.48f);
            victim.TakeDamage(Damage,gameObject);
        }

        private void MoveToward(Vector3 destination,float stopDistance)
        {
            Vector3 delta=destination-transform.position;delta.y=0;
            if(delta.sqrMagnitude<=stopDistance*stopDistance){blockedTime=0;return;}

            Vector3 direction=Time.time<detourUntil?detourDirection:delta.normalized;
            Vector3 requested=direction*MoveSpeed*Time.deltaTime;
            Vector3 next=transform.position+requested;
            next.x=Mathf.Clamp(next.x,-ArenaHalfWidth,ArenaHalfWidth);
            next.z=Mathf.Clamp(next.z,-ArenaHalfHeight,ArenaHalfHeight);
            requested=next-transform.position;

            Vector3 moved=locomotion.Move(requested,ArenaHalfWidth,ArenaHalfHeight);
            if(moved.sqrMagnitude>.00001f)
            {
                locomotion.FaceMovement(moved);
                AnimateTracks(moved.magnitude);
            }

            bool blocked=requested.sqrMagnitude>.00001f&&moved.sqrMagnitude<requested.sqrMagnitude*.30f;
            if(blocked)blockedTime+=Time.deltaTime;
            else
            {
                blockedTime=0f;
                if(moved.sqrMagnitude>requested.sqrMagnitude*.65f)movementRecoveryAttempt=0;
            }
            if(blockedTime>.24f&&Time.time>=detourUntil)
            {
                movementRecoveryAttempt++;
                int side=((patrolIndex+movementRecoveryAttempt)&1)==0?1:-1;
                Vector3 desired=delta.sqrMagnitude>.01f?delta.normalized:transform.forward;
                if(!locomotion.TryFindClearDirection(desired,1.35f,side,out detourDirection))
                {
                    detourDirection=Vector3.Cross(Vector3.up,desired)*side;
                    if(detourDirection.sqrMagnitude<.01f)detourDirection=team==0?Vector3.right:Vector3.left;
                    detourDirection.Normalize();
                }
                detourUntil=Time.time+Mathf.Min(1.0f,.62f+movementRecoveryAttempt*.10f);
                blockedTime=0f;
            }
        }

        private void SetNextPatrol()
        {
            Vector3[] points=team==0?ZeroPatrol:LumenPatrol;
            patrolTarget=points[patrolIndex%points.Length];patrolIndex++;
            patrolTarget.y=home.y;
        }

        private void BuildVisuals()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return;

            Color energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color dark=team==0?StylizedArtDirection.ZeroDark:StylizedArtDirection.LumenDark;
            Color armor=Color.Lerp(dark,new Color(.34f,.37f,.42f),.52f);
            Color joint=Color.Lerp(dark,Color.black,.30f);

            armorMat=MakeMaterial(shader,armor,.72f,.28f,Color.black);
            darkMat=MakeMaterial(shader,dark,.46f,.18f,Color.black);
            jointMat=MakeMaterial(shader,joint,.64f,.12f,Color.black);
            energyMat=MakeMaterial(shader,energy,.18f,.62f,energy*2.6f);

            // Lower hull and layered glacis.
            Part("LowerHull",PrimitiveType.Cube,transform,new Vector3(0,-.18f,0),new Vector3(1.78f,.38f,2.36f),darkMat,Quaternion.identity);
            Part("UpperHull",PrimitiveType.Cube,transform,new Vector3(0,.20f,.08f),new Vector3(1.92f,.52f,2.08f),armorMat,Quaternion.Euler(-4,0,0));
            Part("Glacis",PrimitiveType.Cube,transform,new Vector3(0,.28f,.98f),new Vector3(1.72f,.34f,.64f),armorMat,Quaternion.Euler(-17,0,0));
            Part("RearDeck",PrimitiveType.Cube,transform,new Vector3(0,.28f,-1.00f),new Vector3(1.70f,.24f,.48f),darkMat,Quaternion.identity);

            // Side armor and tracks. Wheels are individually visible instead of a single block.
            leftTrack=new GameObject("DF_HotZoneTank_LeftTrack").transform;leftTrack.SetParent(transform,false);
            rightTrack=new GameObject("DF_HotZoneTank_RightTrack").transform;rightTrack.SetParent(transform,false);
            BuildTrack(leftTrack,-1f,armorMat,darkMat,jointMat);
            BuildTrack(rightTrack,1f,armorMat,darkMat,jointMat);

            // Functional turret stack: ring, rotating turret, mantlet and cannon pivot.
            Part("TurretRing",PrimitiveType.Cylinder,transform,new Vector3(0,.56f,-.05f),new Vector3(.78f,.14f,.78f),jointMat,Quaternion.identity);
            turretPivot=new GameObject("DF_HotZoneTank_TurretPivot").transform;turretPivot.SetParent(transform,false);turretPivot.localPosition=new Vector3(0,.76f,-.05f);
            Part("TurretShell",PrimitiveType.Cube,turretPivot,new Vector3(0,.10f,.05f),new Vector3(1.26f,.46f,1.08f),armorMat,Quaternion.Euler(-2,0,0));
            Part("TurretCheekL",PrimitiveType.Cube,turretPivot,new Vector3(-.60f,.08f,.18f),new Vector3(.28f,.40f,.72f),darkMat,Quaternion.Euler(0,8,0));
            Part("TurretCheekR",PrimitiveType.Cube,turretPivot,new Vector3(.60f,.08f,.18f),new Vector3(.28f,.40f,.72f),darkMat,Quaternion.Euler(0,-8,0));
            Part("Mantlet",PrimitiveType.Cylinder,turretPivot,new Vector3(0,.08f,.62f),new Vector3(.30f,.14f,.30f),jointMat,Quaternion.Euler(90,0,0));

            barrelPivot=new GameObject("DF_HotZoneTank_BarrelPivot").transform;barrelPivot.SetParent(turretPivot,false);barrelPivot.localPosition=new Vector3(0,.08f,.62f);
            Part("Cannon",PrimitiveType.Cube,barrelPivot,new Vector3(0,0,.76f),new Vector3(.22f,.18f,1.48f),darkMat,Quaternion.identity);
            Part("BarrelSleeve",PrimitiveType.Cylinder,barrelPivot,new Vector3(0,0,.08f),new Vector3(.30f,.18f,.30f),armorMat,Quaternion.Euler(90,0,0));
            Part("MuzzleBrake",PrimitiveType.Cube,barrelPivot,new Vector3(0,0,1.55f),new Vector3(.36f,.28f,.24f),energyMat,Quaternion.identity);

            // Sensors and command optics.
            Part("CommanderCupola",PrimitiveType.Cylinder,turretPivot,new Vector3(0,.46f,-.06f),new Vector3(.36f,.16f,.36f),darkMat,Quaternion.identity);
            Part("SensorHead",PrimitiveType.Cube,turretPivot,new Vector3(0,.58f,.02f),new Vector3(.62f,.16f,.28f),armorMat,Quaternion.identity);
            Part("Visor",PrimitiveType.Cube,turretPivot,new Vector3(0,.58f,.18f),new Vector3(.46f,.08f,.05f),energyMat,Quaternion.identity);
            Part("RangefinderL",PrimitiveType.Cube,turretPivot,new Vector3(-.38f,.40f,.10f),new Vector3(.18f,.16f,.30f),darkMat,Quaternion.identity);
            Part("RangefinderR",PrimitiveType.Cube,turretPivot,new Vector3(.38f,.40f,.10f),new Vector3(.18f,.16f,.30f),darkMat,Quaternion.identity);

            // Rear reactor and faction silhouette.
            Part("CoreHousing",PrimitiveType.Cube,transform,new Vector3(0,.36f,-1.26f),new Vector3(.72f,.42f,.34f),darkMat,Quaternion.identity);
            Part("Core",PrimitiveType.Sphere,transform,new Vector3(0,.37f,-1.45f),new Vector3(.30f,.30f,.20f),energyMat,Quaternion.identity);
            Part("ExhaustL",PrimitiveType.Cylinder,transform,new Vector3(-.52f,.43f,-1.38f),new Vector3(.12f,.34f,.12f),jointMat,Quaternion.Euler(90,0,0));
            Part("ExhaustR",PrimitiveType.Cylinder,transform,new Vector3(.52f,.43f,-1.38f),new Vector3(.12f,.34f,.12f),jointMat,Quaternion.Euler(90,0,0));

            // Small readable hardware details add scale without adding gameplay colliders.
            Part("HeadlightL",PrimitiveType.Cube,transform,new Vector3(-.58f,.24f,1.34f),new Vector3(.18f,.12f,.08f),energyMat,Quaternion.Euler(-8,0,0));
            Part("HeadlightR",PrimitiveType.Cube,transform,new Vector3(.58f,.24f,1.34f),new Vector3(.18f,.12f,.08f),energyMat,Quaternion.Euler(-8,0,0));
            Part("TowHookL",PrimitiveType.Cylinder,transform,new Vector3(-.46f,-.18f,1.34f),new Vector3(.08f,.12f,.08f),jointMat,Quaternion.Euler(90,0,0));
            Part("TowHookR",PrimitiveType.Cylinder,transform,new Vector3(.46f,-.18f,1.34f),new Vector3(.08f,.12f,.08f),jointMat,Quaternion.Euler(90,0,0));
            Part("AntennaMast",PrimitiveType.Cylinder,turretPivot,new Vector3(.42f,.78f,-.24f),new Vector3(.035f,.38f,.035f),jointMat,Quaternion.Euler(0,0,-4));
            Part("AntennaTip",PrimitiveType.Sphere,turretPivot,new Vector3(.44f,1.17f,-.24f),new Vector3(.075f,.075f,.075f),energyMat,Quaternion.identity);
            Part("TeamBeaconStem",PrimitiveType.Cylinder,turretPivot,new Vector3(-.42f,.76f,-.22f),new Vector3(.045f,.34f,.045f),jointMat,Quaternion.identity);
            Part("TeamBeacon",PrimitiveType.Sphere,turretPivot,new Vector3(-.42f,1.13f,-.22f),new Vector3(.15f,.15f,.15f),energyMat,Quaternion.identity);
            for(int i=-1;i<=1;i++)
            {
                float x=i*.42f;
                Part("DeckBolt",PrimitiveType.Cylinder,transform,new Vector3(x,.50f,.46f),new Vector3(.055f,.025f,.055f),jointMat,Quaternion.identity);
            }

            if(team==0)
            {
                Part("FractureFinL",PrimitiveType.Cube,transform,new Vector3(-.78f,.68f,-.68f),new Vector3(.16f,.62f,.40f),energyMat,Quaternion.Euler(0,0,-17));
                Part("FractureFinR",PrimitiveType.Cube,transform,new Vector3(.72f,.60f,-.76f),new Vector3(.12f,.44f,.34f),energyMat,Quaternion.Euler(0,0,9));
            }
            else
            {
                Part("LumenFinL",PrimitiveType.Cube,transform,new Vector3(-.66f,.66f,-.72f),new Vector3(.11f,.52f,.36f),energyMat,Quaternion.identity);
                Part("LumenFinR",PrimitiveType.Cube,transform,new Vector3(.66f,.66f,-.72f),new Vector3(.11f,.52f,.36f),energyMat,Quaternion.identity);
            }
        }

        private void BuildSkeletonVisuals()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return;
            Color energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color bone=new Color(.29f,.20f,.16f,1),deep=new Color(.10f,.045f,.035f,1);
            armorMat=MakeMaterial(shader,bone,.08f,.20f,Color.black);darkMat=MakeMaterial(shader,deep,.05f,.12f,Color.black);
            jointMat=MakeMaterial(shader,new Color(.18f,.10f,.075f,1),.06f,.16f,Color.black);
            energyMat=MakeMaterial(shader,energy,.10f,.55f,energy*2.3f);

            // Ancient Martian war skeleton: oversized readable bones, rib cage, skull and claws.
            Part("TitanPelvis",PrimitiveType.Cube,transform,new Vector3(0,.20f,0),new Vector3(1.35f,.48f,.72f),armorMat,Quaternion.Euler(0,0,3));
            Part("TitanSpine",PrimitiveType.Cylinder,transform,new Vector3(0,1.22f,0),new Vector3(.22f,1.05f,.22f),jointMat,Quaternion.identity);
            for(int i=0;i<5;i++)
            {
                float y=.72f+i*.30f,w=1.32f-i*.10f;
                Part("TitanRibL",PrimitiveType.Capsule,transform,new Vector3(-w*.43f,y,0),new Vector3(.16f,.62f,.16f),armorMat,Quaternion.Euler(0,0,62));
                Part("TitanRibR",PrimitiveType.Capsule,transform,new Vector3(w*.43f,y,0),new Vector3(.16f,.62f,.16f),armorMat,Quaternion.Euler(0,0,-62));
            }
            Part("TitanShoulderL",PrimitiveType.Sphere,transform,new Vector3(-1.12f,1.72f,0),new Vector3(.52f,.42f,.55f),armorMat,Quaternion.identity);
            Part("TitanShoulderR",PrimitiveType.Sphere,transform,new Vector3(1.12f,1.72f,0),new Vector3(.52f,.42f,.55f),armorMat,Quaternion.identity);
            skeletonLeftArm=Part("TitanArmL",PrimitiveType.Capsule,transform,new Vector3(-1.34f,.90f,.08f),new Vector3(.25f,.92f,.25f),armorMat,Quaternion.Euler(0,0,-10));
            skeletonRightArm=Part("TitanArmR",PrimitiveType.Capsule,transform,new Vector3(1.34f,.90f,.08f),new Vector3(.25f,.92f,.25f),armorMat,Quaternion.Euler(0,0,10));
            skeletonLeftArmBase=skeletonLeftArm.localRotation;skeletonRightArmBase=skeletonRightArm.localRotation;
            Part("TitanClawL",PrimitiveType.Sphere,transform,new Vector3(-1.42f,.02f,.12f),new Vector3(.48f,.34f,.62f),jointMat,Quaternion.identity);
            barrelPivot=Part("TitanClawR",PrimitiveType.Sphere,transform,new Vector3(1.42f,.02f,.12f),new Vector3(.48f,.34f,.62f),energyMat,Quaternion.identity);
            skeletonLeftLeg=Part("TitanLegL",PrimitiveType.Capsule,transform,new Vector3(-.48f,-.72f,0),new Vector3(.32f,.95f,.32f),armorMat,Quaternion.Euler(0,0,-4));
            skeletonRightLeg=Part("TitanLegR",PrimitiveType.Capsule,transform,new Vector3(.48f,-.72f,0),new Vector3(.32f,.95f,.32f),armorMat,Quaternion.Euler(0,0,4));
            skeletonLeftLegBase=skeletonLeftLeg.localRotation;skeletonRightLegBase=skeletonRightLeg.localRotation;
            skeletonPelvis=transform.Find("DF_HotZoneTank_TitanPelvis");if(skeletonPelvis!=null)skeletonPelvisBaseScale=skeletonPelvis.localScale;
            Transform skull=Part("TitanSkull",PrimitiveType.Sphere,transform,new Vector3(0,2.22f,.10f),new Vector3(.88f,.70f,.82f),armorMat,Quaternion.identity);
            skeletonSkull=skull;skeletonSkullBase=skull.localRotation;
            Part("TitanJaw",PrimitiveType.Cube,skull,new Vector3(0,-.52f,.42f),new Vector3(.62f,.24f,.42f),jointMat,Quaternion.Euler(-8,0,0));
            Part("TitanEyeL",PrimitiveType.Sphere,skull,new Vector3(-.28f,.08f,.72f),new Vector3(.16f,.12f,.10f),energyMat,Quaternion.identity);
            Part("TitanEyeR",PrimitiveType.Sphere,skull,new Vector3(.28f,.08f,.72f),new Vector3(.16f,.12f,.10f),energyMat,Quaternion.identity);
            Part("TitanChestRift",PrimitiveType.Cube,transform,new Vector3(0,1.25f,.27f),new Vector3(.12f,.82f,.08f),energyMat,Quaternion.Euler(0,0,-18));
        }

        private void AnimateSkeleton()
        {
            float idle=Mathf.Sin(Time.time*1.65f),stride=Mathf.Sin(Time.time*5.8f)*skeletonMoveBlend;
            if(skeletonLeftArm!=null)skeletonLeftArm.localRotation=skeletonLeftArmBase*Quaternion.Euler(stride*18f,0,idle*1.5f);
            if(skeletonRightArm!=null)skeletonRightArm.localRotation=skeletonRightArmBase*Quaternion.Euler(-stride*18f-(shotPending?18f:0f),0,-idle*1.5f);
            if(skeletonLeftLeg!=null)skeletonLeftLeg.localRotation=skeletonLeftLegBase*Quaternion.Euler(-stride*16f,0,0);
            if(skeletonRightLeg!=null)skeletonRightLeg.localRotation=skeletonRightLegBase*Quaternion.Euler(stride*16f,0,0);
            if(skeletonSkull!=null)skeletonSkull.localRotation=skeletonSkullBase*Quaternion.Euler(idle*1.2f,idle*2.4f,0);
            if(skeletonPelvis!=null)skeletonPelvis.localScale=new Vector3(skeletonPelvisBaseScale.x,skeletonPelvisBaseScale.y*(1f+Mathf.Abs(stride)*.018f),skeletonPelvisBaseScale.z);
        }

        private void BuildNoxaraSpiderVisuals()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return;
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color armor=new Color(.018f,.022f,.030f,1f),deep=new Color(.006f,.008f,.012f,1f),joint=new Color(.055f,.060f,.070f,1f);
            armorMat=MakeMaterial(shader,armor,.78f,.28f,Color.black);
            darkMat=MakeMaterial(shader,deep,.62f,.18f,Color.black);
            jointMat=MakeMaterial(shader,joint,.82f,.24f,Color.black);
            energyMat=MakeMaterial(shader,spiderEnergyBase,.18f,.70f,spiderEnergyBase*3.1f);
            teamMat=MakeMaterial(shader,teamEnergy,.15f,.46f,teamEnergy*1.15f);

            spiderBodyRoot=Pivot("DF_NoxaraSpiderBody",transform,new Vector3(0,.18f,0),Quaternion.identity);
            spiderBodyRest=spiderBodyRoot.localPosition;
            Part("SpiderAbdomen",PrimitiveType.Sphere,spiderBodyRoot,new Vector3(0,.30f,-.52f),new Vector3(1.30f,.66f,1.46f),darkMat,Quaternion.identity);
            Part("SpiderAbdomenArmor",PrimitiveType.Cube,spiderBodyRoot,new Vector3(0,.54f,-.50f),new Vector3(1.02f,.18f,1.10f),armorMat,Quaternion.Euler(4,0,0));
            Part("SpiderThorax",PrimitiveType.Sphere,spiderBodyRoot,new Vector3(0,.34f,.36f),new Vector3(1.18f,.58f,1.02f),armorMat,Quaternion.identity);
            Part("SpiderThoraxPlate",PrimitiveType.Cube,spiderBodyRoot,new Vector3(0,.57f,.37f),new Vector3(.88f,.16f,.74f),jointMat,Quaternion.Euler(-5,0,0));

            turretPivot=Pivot("DF_NoxaraSpiderHeadPivot",spiderBodyRoot,new Vector3(0,.52f,.86f),Quaternion.identity);
            Part("SpiderHead",PrimitiveType.Cube,turretPivot,new Vector3(0,0,.16f),new Vector3(.88f,.46f,.70f),armorMat,Quaternion.Euler(-4,0,0));
            Part("SpiderFace",PrimitiveType.Cube,turretPivot,new Vector3(0,-.03f,.49f),new Vector3(.72f,.30f,.10f),darkMat,Quaternion.identity);
            float[] eyeX={-.27f,-.09f,.09f,.27f};
            for(int i=0;i<eyeX.Length;i++)
                Part("SpiderEye"+i,PrimitiveType.Sphere,turretPivot,new Vector3(eyeX[i],.06f,.56f),new Vector3(.105f,.085f,.075f),energyMat,Quaternion.identity);

            barrelPivot=Pivot("DF_NoxaraSpiderMuzzle",turretPivot,new Vector3(0,-.18f,.56f),Quaternion.identity);
            spiderBarrelRest=barrelPivot.localPosition;
            Part("SpiderMuzzleCore",PrimitiveType.Cylinder,barrelPivot,Vector3.zero,new Vector3(.15f,.08f,.15f),energyMat,Quaternion.Euler(90,0,0));
            Part("SpiderMandibleL",PrimitiveType.Cube,turretPivot,new Vector3(-.31f,-.24f,.54f),new Vector3(.15f,.12f,.38f),jointMat,Quaternion.Euler(12,-10,-10));
            Part("SpiderMandibleR",PrimitiveType.Cube,turretPivot,new Vector3(.31f,-.24f,.54f),new Vector3(.15f,.12f,.38f),jointMat,Quaternion.Euler(12,10,10));
            Part("SpiderTeamMark",PrimitiveType.Cube,spiderBodyRoot,new Vector3(0,.66f,-.43f),new Vector3(.34f,.05f,.22f),teamMat,Quaternion.identity);

            // Eight articulated legs. Each leg has a hip, knee and ankle so the feet can remain
            // planted while the body moves over them instead of all eight legs simply rocking.
            for(int sideIndex=0;sideIndex<2;sideIndex++)
            {
                float side=sideIndex==0?-1f:1f;
                for(int leg=0;leg<4;leg++)
                {
                    int index=sideIndex*4+leg;
                    float z=.78f-leg*.52f;
                    float spreadZ=leg==0?.48f:leg==1?.18f:leg==2?-.18f:-.48f;
                    Vector3 hip=new(side*.68f,.30f,z);
                    Vector3 outward=new Vector3(side,0f,spreadZ).normalized;
                    Quaternion baseRotation=Quaternion.FromToRotation(Vector3.right,outward);

                    Transform root=Pivot($"DF_NoxaraSpiderLeg_{index}",spiderBodyRoot,hip,baseRotation);
                    spiderLegRoots[index]=root;spiderLegBase[index]=baseRotation;spiderHipLocal[index]=hip;

                    Part("SpiderUpperLeg"+index,PrimitiveType.Cube,root,
                        new Vector3(SpiderUpperLength*.5f,0,0),
                        new Vector3(SpiderUpperLength,.13f,.17f),armorMat,Quaternion.identity);

                    Transform knee=Pivot($"DF_NoxaraSpiderKnee_{index}",root,
                        new Vector3(SpiderUpperLength,0,0),Quaternion.identity);
                    spiderKneePivots[index]=knee;
                    Part("SpiderKnee"+index,PrimitiveType.Sphere,knee,Vector3.zero,
                        new Vector3(.20f,.20f,.20f),jointMat,Quaternion.identity);

                    Part("SpiderLowerLeg"+index,PrimitiveType.Cube,knee,
                        new Vector3(SpiderLowerLength*.5f,0,0),
                        new Vector3(SpiderLowerLength,.11f,.145f),darkMat,Quaternion.identity);

                    Transform foot=Pivot($"DF_NoxaraSpiderAnkle_{index}",knee,
                        new Vector3(SpiderLowerLength,0,0),Quaternion.identity);
                    spiderFootPivots[index]=foot;
                    Part("SpiderAnkle"+index,PrimitiveType.Sphere,foot,Vector3.zero,
                        new Vector3(.13f,.13f,.13f),jointMat,Quaternion.identity);
                    Part("SpiderFoot"+index,PrimitiveType.Cube,foot,
                        new Vector3(.17f,-.035f,.03f),new Vector3(.36f,.09f,.23f),
                        jointMat,Quaternion.Euler(0,0,-4f));
                }
            }

            spiderGaitClock=0f;spiderMoveBlend=0f;spiderTurnLean=0f;spiderVisualYaw=0f;
            spiderPreviousForward=transform.forward;
            spiderPreviousWorldPosition=transform.position;
            for(int i=0;i<8;i++)
            {
                Vector3 rest=SpiderDesiredFootWorld(i,Vector3.zero);
                spiderFootPlant[i]=spiderSwingStart[i]=spiderSwingEnd[i]=rest;
                spiderWasSwing[i]=false;
                ApplyNoxaraSpiderLegIk(i,rest);
            }
            spiderFeetInitialized=true;
        }

        private void AnimateNoxaraSpider()
        {
            if(spiderBodyRoot==null)return;

            Vector3 displacement=transform.position-spiderPreviousWorldPosition;
            spiderPreviousWorldPosition=transform.position;
            displacement.y=0f;
            Vector3 velocity=Time.deltaTime>.0001f?displacement/Time.deltaTime:Vector3.zero;
            float speed=velocity.magnitude;
            float movement01=Mathf.Clamp01(speed/(MoveSpeed*.85f));
            float blendRate=movement01>spiderMoveBlend?7.5f:4.2f;
            spiderMoveBlend=Mathf.MoveTowards(spiderMoveBlend,movement01,Time.deltaTime*blendRate);

            if(spiderMoveBlend>.015f)
            {
                float gaitHz=Mathf.Lerp(.92f,1.42f,movement01);
                spiderGaitClock+=Time.deltaTime*gaitHz*Mathf.Lerp(.35f,1f,spiderMoveBlend);
            }

            float idle=Mathf.Sin(Time.time*1.65f);
            float support=Mathf.Sin(spiderGaitClock*Mathf.PI*2f);
            float turnError=speed>.06f?Vector3.SignedAngle(transform.forward,velocity.normalized,Vector3.up):0f;
            float desiredLean=Mathf.Clamp(turnError*.12f,-4.2f,4.2f);
            spiderTurnLean=Mathf.Lerp(spiderTurnLean,desiredLean,1f-Mathf.Exp(-5.5f*Time.deltaTime));

            // The locomotion owner turns the gameplay root quickly for responsiveness. Counter-lag
            // the visual chassis a little so a heavy eight-legged machine appears to carry inertia.
            if(spiderPreviousForward.sqrMagnitude>.01f)
            {
                float parentTurn=Vector3.SignedAngle(spiderPreviousForward,transform.forward,Vector3.up);
                spiderVisualYaw=Mathf.Clamp(spiderVisualYaw-parentTurn,-26f,26f);
            }
            spiderPreviousForward=transform.forward;
            spiderVisualYaw=Mathf.MoveTowards(spiderVisualYaw,0f,150f*Time.deltaTime);

            float gaitBob=Mathf.Abs(Mathf.Sin(spiderGaitClock*Mathf.PI*4f))*.025f*spiderMoveBlend;
            float roll=support*1.15f*spiderMoveBlend-spiderTurnLean*.45f;
            float pitch=Mathf.Cos(spiderGaitClock*Mathf.PI*2f)*.55f*spiderMoveBlend;
            spiderBodyRoot.localPosition=spiderBodyRest+Vector3.up*(gaitBob+idle*.006f);
            spiderBodyRoot.localRotation=Quaternion.Euler(pitch,spiderVisualYaw,roll);

            if(!spiderFeetInitialized)
            {
                for(int i=0;i<8;i++)spiderFootPlant[i]=SpiderDesiredFootWorld(i,Vector3.zero);
                spiderFeetInitialized=true;
            }

            for(int i=0;i<8;i++)
            {
                float cycle=Mathf.Repeat(spiderGaitClock+SpiderLegPhase(i),1f);
                bool swinging=spiderMoveBlend>.08f&&cycle<SpiderSwingFraction;

                if(swinging&&!spiderWasSwing[i])
                {
                    spiderSwingStart[i]=spiderFootPlant[i];
                    // Place the next step slightly ahead of the moving body. This produces a
                    // visible plant / push / lift cycle rather than foot skating.
                    Vector3 lead=velocity*.18f;
                    spiderSwingEnd[i]=SpiderDesiredFootWorld(i,lead);
                }

                if(swinging)
                {
                    float u=Mathf.Clamp01(cycle/SpiderSwingFraction);
                    float smooth=u*u*(3f-2f*u);
                    Vector3 target=Vector3.Lerp(spiderSwingStart[i],spiderSwingEnd[i],smooth);
                    target.y+=Mathf.Sin(u*Mathf.PI)*Mathf.Lerp(.16f,.27f,spiderMoveBlend);
                    spiderFootPlant[i]=target;
                }
                else
                {
                    if(spiderWasSwing[i])
                        spiderFootPlant[i]=SpiderDesiredGroundPoint(spiderSwingEnd[i]);

                    // While idle, quietly settle back into a symmetric stance. During locomotion
                    // the stance foot remains planted unless it would become visibly over-stretched.
                    Vector3 desired=SpiderDesiredFootWorld(i,Vector3.zero);
                    if(spiderMoveBlend<.08f)
                        spiderFootPlant[i]=Vector3.Lerp(spiderFootPlant[i],desired,1f-Mathf.Exp(-4f*Time.deltaTime));
                    else
                    {
                        Vector3 flat=spiderFootPlant[i]-desired;flat.y=0;
                        if(flat.sqrMagnitude>1.45f*1.45f)
                            spiderFootPlant[i]=Vector3.Lerp(spiderFootPlant[i],desired,1f-Mathf.Exp(-2.4f*Time.deltaTime));
                    }
                }

                ApplyNoxaraSpiderLegIk(i,spiderFootPlant[i]);
                spiderWasSwing[i]=swinging;
            }

            if(energyMat!=null&&energyMat.HasProperty("_EmissionColor"))
            {
                float windup=shotPending?Mathf.Clamp01(1f-(shotAt-Time.time)/ShotWindup):0f;
                energyMat.SetColor("_EmissionColor",spiderEnergyBase*(2.7f+idle*.18f+windup*1.9f));
            }
            if(barrelPivot!=null)
            {
                float pulse=1f+(shotPending?.16f:0f)+Mathf.Max(0,idle)*.03f;
                barrelPivot.localScale=Vector3.one*pulse;
            }
        }

        private static float SpiderLegPhase(int index)
        {
            // Alternating tetrapod gait: L1/L3/R2/R4 oppose L2/L4/R1/R3.
            // Tiny offsets prevent the motion from reading as a perfectly synchronized machine.
            return index switch
            {
                0=>0f,1=>.51f,2=>.035f,3=>.545f,
                4=>.50f,5=>.02f,6=>.53f,_=>.06f
            };
        }

        private Vector3 SpiderDesiredFootWorld(int index,Vector3 lead)
        {
            int leg=index&3;
            float side=index<4?-1f:1f;
            float reach=leg==0||leg==3?2.12f:2.22f;
            float z=leg==0?1.30f:leg==1?.45f:leg==2?-.45f:-1.30f;
            Vector3 world=spiderBodyRoot.TransformPoint(new Vector3(side*reach,-.62f,z));
            world+=lead;
            return SpiderDesiredGroundPoint(world);
        }

        private static Vector3 SpiderDesiredGroundPoint(Vector3 world)
        {
            Vector3 surface=LunarSurfaceField.Surface(world.x,world.z);
            world.y=surface.y+.045f;
            return world;
        }

        private void ApplyNoxaraSpiderLegIk(int index,Vector3 targetWorld)
        {
            Transform root=spiderLegRoots[index];
            Transform knee=spiderKneePivots[index];
            Transform foot=spiderFootPivots[index];
            if(root==null||knee==null||foot==null||spiderBodyRoot==null)return;

            Vector3 targetBody=spiderBodyRoot.InverseTransformPoint(targetWorld);
            Vector3 deltaBody=targetBody-spiderHipLocal[index];
            Vector3 local=Quaternion.Inverse(spiderLegBase[index])*deltaBody;

            float planar=Mathf.Sqrt(local.x*local.x+local.z*local.z);
            planar=Mathf.Max(.04f,planar);
            float yaw=Mathf.Atan2(-local.z,local.x)*Mathf.Rad2Deg;
            float distance=Mathf.Sqrt(planar*planar+local.y*local.y);
            distance=Mathf.Clamp(distance,.18f,SpiderUpperLength+SpiderLowerLength-.035f);

            float targetAngle=Mathf.Atan2(local.y,planar)*Mathf.Rad2Deg;
            float hipCos=(SpiderUpperLength*SpiderUpperLength+distance*distance-SpiderLowerLength*SpiderLowerLength)/
                (2f*SpiderUpperLength*distance);
            float hipOffset=Mathf.Acos(Mathf.Clamp(hipCos,-1f,1f))*Mathf.Rad2Deg;
            float hip=targetAngle+hipOffset;

            float kneeCos=(SpiderUpperLength*SpiderUpperLength+SpiderLowerLength*SpiderLowerLength-distance*distance)/
                (2f*SpiderUpperLength*SpiderLowerLength);
            float internalKnee=Mathf.Acos(Mathf.Clamp(kneeCos,-1f,1f))*Mathf.Rad2Deg;
            float kneeBend=-(180f-internalKnee);

            root.localRotation=spiderLegBase[index]*
                Quaternion.AngleAxis(yaw,Vector3.up)*
                Quaternion.AngleAxis(hip,Vector3.forward);
            knee.localRotation=Quaternion.AngleAxis(kneeBend,Vector3.forward);
            foot.localRotation=Quaternion.AngleAxis(-(hip+kneeBend),Vector3.forward);
        }

        private void BuildKryonBearVisuals()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return;
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color armor=new(.68f,.76f,.79f,1f),ice=new(.80f,.90f,.93f,1f),deep=new(.035f,.055f,.070f,1f),joint=new(.10f,.15f,.18f,1f);
            armorMat=MakeMaterial(shader,armor,.62f,.38f,Color.black);
            darkMat=MakeMaterial(shader,deep,.74f,.22f,Color.black);
            jointMat=MakeMaterial(shader,joint,.82f,.26f,Color.black);
            energyMat=MakeMaterial(shader,bearEnergyBase,.12f,.76f,bearEnergyBase*3.2f);
            teamMat=MakeMaterial(shader,teamEnergy,.12f,.48f,teamEnergy*1.05f);
            bearIceMat=MakeMaterial(shader,ice,.18f,.62f,new Color(.16f,.55f,.62f)*.30f);

            bearBodyRoot=Pivot("DF_KryonBearBody",transform,new Vector3(0,.22f,0),Quaternion.identity);
            bearBodyRest=bearBodyRoot.localPosition;
            Part("KryonBearTorso",PrimitiveType.Sphere,bearBodyRoot,new Vector3(0,.42f,-.20f),new Vector3(1.48f,.88f,1.92f),armorMat,Quaternion.identity);
            Part("KryonBearBackArmor",PrimitiveType.Cube,bearBodyRoot,new Vector3(0,.78f,-.28f),new Vector3(1.10f,.18f,1.28f),bearIceMat,Quaternion.Euler(3,0,0));
            Part("KryonBearChest",PrimitiveType.Cube,bearBodyRoot,new Vector3(0,.35f,.62f),new Vector3(1.08f,.68f,.48f),darkMat,Quaternion.Euler(-6,0,0));
            Part("KryonBearReactor",PrimitiveType.Sphere,bearBodyRoot,new Vector3(0,.48f,-1.06f),new Vector3(.34f,.34f,.22f),energyMat,Quaternion.identity);
            Part("KryonBearTeamMark",PrimitiveType.Cube,bearBodyRoot,new Vector3(0,.88f,-.18f),new Vector3(.34f,.045f,.25f),teamMat,Quaternion.identity);

            turretPivot=Pivot("DF_KryonBearHeadPivot",bearBodyRoot,new Vector3(0,.70f,.82f),Quaternion.identity);
            bearHeadPivot=turretPivot;
            Part("KryonBearHead",PrimitiveType.Sphere,turretPivot,new Vector3(0,.02f,.18f),new Vector3(.92f,.66f,.78f),armorMat,Quaternion.identity);
            Part("KryonBearBrow",PrimitiveType.Cube,turretPivot,new Vector3(0,.20f,.48f),new Vector3(.76f,.18f,.28f),bearIceMat,Quaternion.Euler(-7,0,0));
            Part("KryonBearSnout",PrimitiveType.Cube,turretPivot,new Vector3(0,-.12f,.62f),new Vector3(.58f,.34f,.54f),darkMat,Quaternion.Euler(4,0,0));
            Part("KryonBearEarL",PrimitiveType.Sphere,turretPivot,new Vector3(-.38f,.35f,.02f),new Vector3(.26f,.28f,.20f),jointMat,Quaternion.identity);
            Part("KryonBearEarR",PrimitiveType.Sphere,turretPivot,new Vector3(.38f,.35f,.02f),new Vector3(.26f,.28f,.20f),jointMat,Quaternion.identity);
            Part("KryonBearEyeL",PrimitiveType.Sphere,turretPivot,new Vector3(-.22f,.08f,.55f),new Vector3(.11f,.085f,.065f),energyMat,Quaternion.identity);
            Part("KryonBearEyeR",PrimitiveType.Sphere,turretPivot,new Vector3(.22f,.08f,.55f),new Vector3(.11f,.085f,.065f),energyMat,Quaternion.identity);

            barrelPivot=Pivot("DF_KryonBearMuzzle",turretPivot,new Vector3(0,-.13f,.88f),Quaternion.identity);
            bearBarrelRest=barrelPivot.localPosition;
            Part("KryonBearMuzzleCore",PrimitiveType.Cylinder,barrelPivot,Vector3.zero,new Vector3(.19f,.07f,.19f),energyMat,Quaternion.Euler(90,0,0));

            Vector3[] hips={new(-.63f,.28f,.60f),new(.63f,.28f,.60f),new(-.66f,.28f,-.82f),new(.66f,.28f,-.82f)};
            for(int i=0;i<4;i++)
            {
                float side=i%2==0?-1f:1f;
                Transform leg=Pivot($"DF_KryonBearLeg_{i}",bearBodyRoot,hips[i],Quaternion.Euler(6f,0,side*4f));
                bearLegRoots[i]=leg;bearLegBase[i]=leg.localRotation;
                Part("KryonBearUpperLeg"+i,PrimitiveType.Capsule,leg,new Vector3(0,-.43f,.03f),new Vector3(.36f,.64f,.40f),armorMat,Quaternion.identity);
                Part("KryonBearKnee"+i,PrimitiveType.Sphere,leg,new Vector3(0,-.83f,.08f),new Vector3(.30f,.26f,.30f),jointMat,Quaternion.identity);
                Part("KryonBearLowerLeg"+i,PrimitiveType.Cube,leg,new Vector3(0,-1.08f,.13f),new Vector3(.32f,.56f,.36f),darkMat,Quaternion.Euler(-7f,0,side*2f));
                Part("KryonBearPaw"+i,PrimitiveType.Cube,leg,new Vector3(0,-1.35f,.31f),new Vector3(.58f,.22f,.72f),bearIceMat,Quaternion.Euler(0,0,side*1.5f));
                Part("KryonBearPawGlow"+i,PrimitiveType.Cube,leg,new Vector3(0,-1.45f,.35f),new Vector3(.30f,.035f,.38f),energyMat,Quaternion.identity);
            }
        }

        private void AnimateKryonBear()
        {
            if(bearBodyRoot==null)return;
            float idle=Mathf.Sin(Time.time*1.55f);
            float stride=Mathf.Sin(Time.time*4.65f)*bearMoveBlend;
            float lift=Mathf.Abs(stride)*.035f*bearMoveBlend;
            bearBodyRoot.localPosition=bearBodyRest+Vector3.up*(lift+idle*.006f);
            bearBodyRoot.localRotation=Quaternion.Euler(-1.5f+Mathf.Cos(Time.time*4.65f)*.65f*bearMoveBlend,0,stride*.85f);

            for(int i=0;i<bearLegRoots.Length;i++)
            {
                Transform leg=bearLegRoots[i];if(leg==null)continue;
                float phase=(i==0||i==3?1f:-1f)*stride;
                float side=i%2==0?-1f:1f;
                leg.localRotation=bearLegBase[i]*Quaternion.Euler(phase*23f,0,side*Mathf.Abs(phase)*3.5f);
            }

            if(bearHeadPivot!=null)
                bearHeadPivot.localRotation=Quaternion.Euler(idle*1.2f,-stride*2.2f,0);
            if(energyMat!=null&&energyMat.HasProperty("_EmissionColor"))
            {
                float windup=shotPending?Mathf.Clamp01(1f-(shotAt-Time.time)/ShotWindup):0f;
                energyMat.SetColor("_EmissionColor",bearEnergyBase*(2.8f+idle*.12f+windup*2.0f));
            }
        }

        private void BuildAlienVisuals()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return;
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            xenoEnergyBase=new Color(.54f,.86f,.16f,1f);
            xenoArmorBase=new Color(.17f,.245f,.12f,1f);
            Color hide=new Color(.055f,.085f,.052f,1f),joint=new Color(.035f,.055f,.038f,1f);
            armorMat=MakeMaterial(shader,xenoArmorBase,.10f,.31f,Color.black);
            darkMat=MakeMaterial(shader,hide,.02f,.12f,Color.black);
            jointMat=MakeMaterial(shader,joint,.03f,.09f,Color.black);
            energyMat=MakeMaterial(shader,xenoEnergyBase,.04f,.56f,xenoEnergyBase*2.0f);
            teamMat=MakeMaterial(shader,teamEnergy,.10f,.46f,teamEnergy*1.45f);

            // Custom low-poly plates replace the box-heavy silhouette while a few hidden organic
            // masses preserve readable volume from the gameplay camera.
            Mesh chestCenter=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoChestCenter",new[]{
                new Vector2(-.48f,-.60f),new Vector2(.48f,-.60f),new Vector2(.67f,.05f),new Vector2(.42f,.68f),new Vector2(0,.82f),new Vector2(-.42f,.68f),new Vector2(-.67f,.05f)},.16f));
            Mesh chestSide=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoChestSide",new[]{
                new Vector2(-.38f,-.52f),new Vector2(.32f,-.46f),new Vector2(.48f,.16f),new Vector2(.20f,.67f),new Vector2(-.36f,.54f),new Vector2(-.52f,.02f)},.14f));
            Mesh shoulderPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoShoulderPlate",new[]{
                new Vector2(-.52f,-.24f),new Vector2(.40f,-.30f),new Vector2(.62f,.18f),new Vector2(.20f,.56f),new Vector2(-.48f,.42f)},.18f));
            Mesh forearmPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoForearmPlate",new[]{
                new Vector2(-.30f,-.54f),new Vector2(.30f,-.48f),new Vector2(.38f,.32f),new Vector2(0,.58f),new Vector2(-.34f,.28f)},.12f));
            Mesh thighPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoThighPlate",new[]{
                new Vector2(-.38f,-.50f),new Vector2(.38f,-.50f),new Vector2(.46f,.20f),new Vector2(.16f,.60f),new Vector2(-.32f,.50f)},.14f));
            Mesh jawPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoJawPlate",new[]{
                new Vector2(-.46f,-.18f),new Vector2(.46f,-.18f),new Vector2(.34f,.24f),new Vector2(0,.34f),new Vector2(-.34f,.24f)},.20f));
            Mesh markPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoTeamMark",new[]{
                new Vector2(-.16f,-.12f),new Vector2(.16f,-.12f),new Vector2(.12f,.14f),new Vector2(0,.22f),new Vector2(-.12f,.14f)},.055f));
            Mesh headShell=TrackXenoMesh(XenoTitanMeshFactory.HeadShell("DF_XenoHeadShell"));
            Mesh clawBlade=TrackXenoMesh(XenoTitanMeshFactory.ClawBlade("DF_XenoClawBlade",.78f,.16f,.12f,.18f));
            Mesh toeBlade=TrackXenoMesh(XenoTitanMeshFactory.ClawBlade("DF_XenoToeBlade",.52f,.14f,.10f,.10f));
            Mesh spineA=TrackXenoMesh(XenoTitanMeshFactory.Spine("DF_XenoSpineA",.92f,.24f,.30f));
            Mesh spineB=TrackXenoMesh(XenoTitanMeshFactory.Spine("DF_XenoSpineB",.72f,.20f,.25f));

            xenoVisualRoot=Pivot("DF_XenoVisualRoot",transform,new Vector3(0,.05f,0),Quaternion.identity);
            xenoVisualRootBasePosition=xenoVisualRoot.localPosition;
            xenoPelvisPivot=Pivot("DF_XenoPelvisPivot",xenoVisualRoot,new Vector3(0,.18f,-.08f),Quaternion.Euler(6,0,0));
            Part("XenoPelvisMass",PrimitiveType.Sphere,xenoPelvisPivot,Vector3.zero,new Vector3(1.30f,.55f,.86f),darkMat,Quaternion.identity);
            MeshPart("XenoHipCarapace",chestSide,xenoPelvisPivot,new Vector3(0,.10f,.52f),new Vector3(1.18f,.64f,.82f),armorMat,Quaternion.Euler(-5,0,0));

            xenoTorsoPivot=Pivot("DF_XenoTorsoPivot",xenoVisualRoot,new Vector3(0,1.30f,.02f),Quaternion.Euler(10,0,0));
            Part("XenoTorsoMass",PrimitiveType.Sphere,xenoTorsoPivot,new Vector3(0,-.06f,-.06f),new Vector3(1.62f,1.02f,.92f),darkMat,Quaternion.identity);
            MeshPart("XenoChestCenter",chestCenter,xenoTorsoPivot,new Vector3(0,.02f,.64f),Vector3.one,armorMat,Quaternion.Euler(-3,0,0));
            MeshPart("XenoChestLeft",chestSide,xenoTorsoPivot,new Vector3(-.72f,.05f,.48f),new Vector3(.95f,.94f,1f),armorMat,Quaternion.Euler(-2,-12,-4));
            MeshPart("XenoChestRight",chestSide,xenoTorsoPivot,new Vector3(.72f,.02f,.48f),new Vector3(1.03f,.90f,1f),armorMat,Quaternion.Euler(-1,12,5));
            Transform core=Part("XenoChestCore",PrimitiveType.Sphere,xenoTorsoPivot,new Vector3(0,-.05f,.78f),new Vector3(.24f,.20f,.16f),energyMat,Quaternion.identity);
            xenoCoreRenderer=core.GetComponent<Renderer>();xenoCoreBaseScale=core.localScale;

            // Short separated energy channels read as biological veins instead of one neon rectangle.
            Mesh vein1=TrackXenoMesh(ProductionMeshFactory.Tube(new Vector3(-.42f,.25f,.69f),new Vector3(-.14f,.05f,.78f),.030f,6));
            Mesh vein2=TrackXenoMesh(ProductionMeshFactory.Tube(new Vector3(.42f,.22f,.67f),new Vector3(.15f,.03f,.78f),.030f,6));
            Mesh vein3=TrackXenoMesh(ProductionMeshFactory.Tube(new Vector3(-.12f,-.26f,.74f),new Vector3(.11f,-.36f,.68f),.024f,6));
            MeshPart("XenoVeinL",vein1,xenoTorsoPivot,Vector3.zero,Vector3.one,energyMat,Quaternion.identity);
            MeshPart("XenoVeinR",vein2,xenoTorsoPivot,Vector3.zero,Vector3.one,energyMat,Quaternion.identity);
            MeshPart("XenoVeinLower",vein3,xenoTorsoPivot,Vector3.zero,Vector3.one,energyMat,Quaternion.identity);

            BuildXenoArm(-1f,shoulderPlate,forearmPlate,clawBlade,markPlate);
            BuildXenoArm(1f,shoulderPlate,forearmPlate,clawBlade,markPlate);
            BuildXenoLeg(-1f,thighPlate,toeBlade);
            BuildXenoLeg(1f,thighPlate,toeBlade);

            // Elongated low-poly skull with layered cranial armor and a narrow jaw.
            xenoHeadPivot=Pivot("DF_XenoHeadPivot",xenoTorsoPivot,new Vector3(0,1.30f,.05f),Quaternion.Euler(-10,0,0));
            MeshPart("XenoHeadShell",headShell,xenoHeadPivot,new Vector3(0,0,.08f),new Vector3(1.02f,1.02f,1.08f),armorMat,Quaternion.identity);
            MeshPart("XenoCranialPlate",chestSide,xenoHeadPivot,new Vector3(0,.20f,-.48f),new Vector3(1.12f,.62f,1.35f),darkMat,Quaternion.Euler(8,0,180));
            MeshPart("XenoCranialShard",shoulderPlate,xenoHeadPivot,new Vector3(-.24f,.24f,-.72f),new Vector3(.62f,.42f,.72f),armorMat,Quaternion.Euler(8,-7,-8));
            MeshPart("XenoJaw",jawPlate,xenoHeadPivot,new Vector3(0,-.36f,.70f),Vector3.one,jointMat,Quaternion.Euler(-8,0,0));
            Part("XenoEyeL",PrimitiveType.Sphere,xenoHeadPivot,new Vector3(-.27f,.03f,.82f),new Vector3(.15f,.10f,.08f),energyMat,Quaternion.identity);
            Part("XenoEyeR",PrimitiveType.Sphere,xenoHeadPivot,new Vector3(.27f,.03f,.82f),new Vector3(.15f,.10f,.08f),energyMat,Quaternion.identity);

            // Irregular back silhouette: five different-height spines, intentionally not mirrored.
            xenoBackRoot=Pivot("DF_XenoBackRoot",xenoTorsoPivot,new Vector3(0,.28f,-.48f),Quaternion.Euler(22,0,0));
            MeshPart("XenoSpineCenter",spineA,xenoBackRoot,new Vector3(.04f,.05f,-.08f),new Vector3(1.05f,1.15f,1f),armorMat,Quaternion.Euler(8,4,-2));
            MeshPart("XenoSpineL1",spineB,xenoBackRoot,new Vector3(-.42f,-.08f,-.04f),new Vector3(.90f,.92f,.90f),armorMat,Quaternion.Euler(14,-8,-16));
            MeshPart("XenoSpineR1",spineB,xenoBackRoot,new Vector3(.44f,-.12f,-.02f),new Vector3(.78f,.76f,.85f),armorMat,Quaternion.Euler(12,9,13));
            MeshPart("XenoSpineL2",spineB,xenoBackRoot,new Vector3(-.72f,-.30f,.02f),new Vector3(.62f,.58f,.72f),darkMat,Quaternion.Euler(18,-13,-24));
            MeshPart("XenoSpineR2",spineB,xenoBackRoot,new Vector3(.69f,-.34f,.04f),new Vector3(.58f,.50f,.68f),darkMat,Quaternion.Euler(16,11,20));
        }

        private void BuildXenoArm(float side,Mesh shoulderPlate,Mesh forearmPlate,Mesh clawBlade,Mesh markPlate)
        {
            bool left=side<0f;
            Transform shoulder=Pivot(left?"DF_XenoShoulderPivotL":"DF_XenoShoulderPivotR",xenoTorsoPivot,new Vector3(side*1.18f,.28f,-.02f),Quaternion.Euler(-8,0,side*8f));
            Part(left?"XenoShoulderMassL":"XenoShoulderMassR",PrimitiveType.Sphere,shoulder,new Vector3(0,-.03f,0),new Vector3(.72f,.52f,.72f),darkMat,Quaternion.identity);
            MeshPart(left?"XenoShoulderArmorL":"XenoShoulderArmorR",shoulderPlate,shoulder,new Vector3(side*.10f,.12f,.38f),new Vector3(left?1.12f:1f,left?1.08f:.98f,1f),armorMat,Quaternion.Euler(-2,side*8f,side*-8f));
            MeshPart(left?"XenoTeamMarkL":"XenoTeamMarkR",markPlate,shoulder,new Vector3(side*.06f,.13f,.51f),new Vector3(.72f,.72f,.72f),teamMat,Quaternion.Euler(-2,side*7f,0));

            Part(left?"XenoUpperArmL":"XenoUpperArmR",PrimitiveType.Capsule,shoulder,new Vector3(0,-.62f,.04f),new Vector3(.34f,.76f,.36f),darkMat,Quaternion.Euler(0,0,side*2f));
            Transform forearm=Pivot(left?"DF_XenoForearmPivotL":"DF_XenoForearmPivotR",shoulder,new Vector3(0,-1.18f,.10f),Quaternion.Euler(8,0,side*4f));
            Part(left?"XenoForearmMassL":"XenoForearmMassR",PrimitiveType.Capsule,forearm,new Vector3(0,-.36f,.02f),new Vector3(.42f,.62f,.44f),jointMat,Quaternion.identity);
            MeshPart(left?"XenoForearmArmorL":"XenoForearmArmorR",forearmPlate,forearm,new Vector3(0,-.34f,.34f),new Vector3(left?1f:1.13f,1f,1f),armorMat,Quaternion.Euler(-4,0,side*3f));

            Transform claw=Pivot(left?"DF_XenoClawPivotL":"DF_XenoClawPivotR",forearm,new Vector3(0,-.82f,.20f),Quaternion.Euler(-7,0,side*4f));
            Part(left?"XenoPalmL":"XenoPalmR",PrimitiveType.Sphere,claw,new Vector3(0,0,.08f),new Vector3(.58f,.34f,.68f),armorMat,Quaternion.identity);
            for(int i=-1;i<=1;i++)
            {
                float spread=i*.22f;
                Transform finger=MeshPart(left?"XenoClawBladeL":"XenoClawBladeR",clawBlade,claw,new Vector3(spread,-.03f,.38f),new Vector3(1f,i==0?1.12f:.92f,i==0?1.08f:.92f),jointMat,Quaternion.Euler(-8-i*3f,side*i*7f,side*i*3f));
                finger.localPosition+=Vector3.forward*(i==0?.06f:0f);
            }

            if(left){xenoLeftShoulderPivot=shoulder;xenoLeftForearmPivot=forearm;xenoLeftClaw=claw;}
            else
            {
                xenoRightShoulderPivot=shoulder;xenoRightForearmPivot=forearm;xenoRightClaw=claw;
                barrelPivot=Pivot("DF_XenoAttackSocket",claw,new Vector3(0,.02f,1.18f),Quaternion.identity);
            }
        }

        private void BuildXenoLeg(float side,Mesh thighPlate,Mesh toeBlade)
        {
            bool left=side<0f;
            Transform hip=Pivot(left?"DF_XenoHipPivotL":"DF_XenoHipPivotR",xenoPelvisPivot,new Vector3(side*.54f,-.22f,.02f),Quaternion.Euler(5,0,side*5f));
            Part(left?"XenoThighMassL":"XenoThighMassR",PrimitiveType.Capsule,hip,new Vector3(0,-.44f,-.02f),new Vector3(.52f,.74f,.54f),darkMat,Quaternion.identity);
            MeshPart(left?"XenoThighArmorL":"XenoThighArmorR",thighPlate,hip,new Vector3(0,-.38f,.42f),Vector3.one,armorMat,Quaternion.Euler(-3,0,side*2f));

            Transform shin=Pivot(left?"DF_XenoShinPivotL":"DF_XenoShinPivotR",hip,new Vector3(0,-1.02f,.08f),Quaternion.Euler(9,0,side*3f));
            Part(left?"XenoShinMassL":"XenoShinMassR",PrimitiveType.Capsule,shin,new Vector3(0,-.38f,.08f),new Vector3(.40f,.64f,.46f),jointMat,Quaternion.identity);
            MeshPart(left?"XenoShinArmorL":"XenoShinArmorR",thighPlate,shin,new Vector3(0,-.36f,.39f),new Vector3(.82f,.78f,.90f),armorMat,Quaternion.Euler(-8,0,side*2f));

            Transform foot=Pivot(left?"DF_XenoFootPivotL":"DF_XenoFootPivotR",shin,new Vector3(0,-.84f,.30f),Quaternion.Euler(0,side*-3f,0));
            Part(left?"XenoFootMassL":"XenoFootMassR",PrimitiveType.Sphere,foot,new Vector3(0,0,.18f),new Vector3(.70f,.28f,.92f),darkMat,Quaternion.identity);
            for(int i=-1;i<=1;i++)
                MeshPart(left?"XenoToeL":"XenoToeR",toeBlade,foot,new Vector3(i*.20f,-.04f,.42f),new Vector3(.92f,.85f,i==0?1.08f:.92f),jointMat,Quaternion.Euler(-4,side*i*8f,0));

            if(left){xenoLeftHipPivot=hip;xenoLeftShinPivot=shin;xenoLeftFoot=foot;}
            else{xenoRightHipPivot=hip;xenoRightShinPivot=shin;xenoRightFoot=foot;}
        }

        private void AnimateXeno()
        {
            float t=Time.time;
            float idle=Mathf.Sin(t*1.55f);
            float stride=Mathf.Sin(t*4.9f)*xenoMoveBlend;
            float strideAbs=Mathf.Abs(stride);
            float attackWindup=shotPending?Mathf.Clamp01(1f-(shotAt-Time.time)/ShotWindup):0f;
            xenoAttackKick=Mathf.MoveTowards(xenoAttackKick,0f,Time.deltaTime*5.5f);

            if(xenoVisualRoot!=null)
                xenoVisualRoot.localPosition=xenoVisualRootBasePosition+Vector3.up*(strideAbs*.045f*xenoMoveBlend);
            if(xenoTorsoPivot!=null)
                xenoTorsoPivot.localRotation=Quaternion.Euler(10f+idle*1.1f,-stride*3.2f,stride*1.4f);
            if(xenoPelvisPivot!=null)
                xenoPelvisPivot.localRotation=Quaternion.Euler(6f,-stride*3.6f,-stride*.9f);
            if(xenoHeadPivot!=null)
                xenoHeadPivot.localRotation=Quaternion.Euler(-10f+idle*1.4f,idle*3.2f+stride*1.4f,0);
            if(xenoBackRoot!=null)
                xenoBackRoot.localRotation=Quaternion.Euler(22f+idle*1.2f,0,stride*.6f);

            if(xenoLeftShoulderPivot!=null)xenoLeftShoulderPivot.localRotation=Quaternion.Euler(-8f+stride*16f,idle*.8f,-8f+stride*1.5f);
            if(xenoRightShoulderPivot!=null)xenoRightShoulderPivot.localRotation=Quaternion.Euler(-8f-stride*16f-attackWindup*25f+xenoAttackKick*31f,-idle*.8f,8f-stride*1.5f);
            if(xenoLeftForearmPivot!=null)xenoLeftForearmPivot.localRotation=Quaternion.Euler(8f-stride*7f,0,-4f);
            if(xenoRightForearmPivot!=null)xenoRightForearmPivot.localRotation=Quaternion.Euler(8f+stride*7f+attackWindup*11f-xenoAttackKick*13f,0,4f);
            if(xenoLeftClaw!=null)xenoLeftClaw.localRotation=Quaternion.Euler(-7f+idle*1.2f,0,-4f);
            if(xenoRightClaw!=null)xenoRightClaw.localRotation=Quaternion.Euler(-7f-attackWindup*8f+xenoAttackKick*9f,0,4f);

            if(xenoLeftHipPivot!=null)xenoLeftHipPivot.localRotation=Quaternion.Euler(5f-stride*20f,0,-5f);
            if(xenoRightHipPivot!=null)xenoRightHipPivot.localRotation=Quaternion.Euler(5f+stride*20f,0,5f);
            if(xenoLeftShinPivot!=null)xenoLeftShinPivot.localRotation=Quaternion.Euler(9f+stride*11f,0,-3f);
            if(xenoRightShinPivot!=null)xenoRightShinPivot.localRotation=Quaternion.Euler(9f-stride*11f,0,3f);
            if(xenoLeftFoot!=null)xenoLeftFoot.localRotation=Quaternion.Euler(Mathf.Max(0f,stride)*-8f,3f,0);
            if(xenoRightFoot!=null)xenoRightFoot.localRotation=Quaternion.Euler(Mathf.Max(0f,-stride)*-8f,-3f,0);

            float emission=1.75f+idle*.10f+attackWindup*1.65f+xenoAttackKick*.55f;
            if(energyMat!=null&&energyMat.HasProperty("_EmissionColor"))energyMat.SetColor("_EmissionColor",xenoEnergyBase*emission);
            if(xenoCoreRenderer!=null)xenoCoreRenderer.transform.localScale=xenoCoreBaseScale*(1f+attackWindup*.18f+idle*.025f);

            if(armorMat!=null)
            {
                bool hit=Time.unscaledTime<xenoHitUntil;
                Color armor=hit?Color.Lerp(xenoArmorBase,xenoEnergyBase,.42f):xenoArmorBase;
                armorMat.color=armor;if(armorMat.HasProperty("_BaseColor"))armorMat.SetColor("_BaseColor",armor);
            }
        }

        private void OnXenoDamaged(float damage,float absorbed,GameObject source)
        {
            if(!xenoAlien||damage<=0f)return;
            xenoHitUntil=Time.unscaledTime+.12f;
        }

        private void BuildTrack(Transform track,float side,Material armor,Material dark,Material joint)
        {
            Part(side<0?"TrackHousingL":"TrackHousingR",PrimitiveType.Cube,track,new Vector3(side*1.04f,-.30f,0),new Vector3(.46f,.52f,2.54f),dark,Quaternion.identity);
            Part(side<0?"TrackArmorL":"TrackArmorR",PrimitiveType.Cube,track,new Vector3(side*1.06f,-.02f,0),new Vector3(.38f,.20f,2.18f),armor,Quaternion.identity);
            for(int i=0;i<4;i++)
            {
                float z=-.90f+i*.60f;
                Part("RoadWheel",PrimitiveType.Cylinder,track,new Vector3(side*1.25f,-.33f,z),new Vector3(.27f,.15f,.27f),joint,Quaternion.Euler(0,0,90));
            }
            Part("DriveWheel",PrimitiveType.Cylinder,track,new Vector3(side*1.25f,-.29f,-1.20f),new Vector3(.34f,.17f,.34f),armor,Quaternion.Euler(0,0,90));
            Part("IdlerWheel",PrimitiveType.Cylinder,track,new Vector3(side*1.25f,-.29f,1.20f),new Vector3(.34f,.17f,.34f),armor,Quaternion.Euler(0,0,90));
        }

        private Material MakeMaterial(Shader shader,Color color,float metallic,float smoothness,Color emission)
        {
            Material m=new(shader){color=color};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);
            if(emission.maxColorComponent>0&&m.HasProperty("_EmissionColor")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);}
            return m;
        }

        private Transform Part(string name,PrimitiveType type,Transform parent,Vector3 localPosition,Vector3 localScale,Material material,Quaternion rotation)
        {
            GameObject part=GameObject.CreatePrimitive(type);part.name="DF_HotZoneTank_"+name;part.transform.SetParent(parent,false);
            part.transform.localPosition=localPosition;part.transform.localScale=localScale;part.transform.localRotation=rotation;
            Collider c=part.GetComponent<Collider>();if(c!=null){c.enabled=false;Object.Destroy(c);}
            Renderer r=part.GetComponent<Renderer>();if(r!=null){r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
            return part.transform;
        }

        private Transform Pivot(string name,Transform parent,Vector3 localPosition,Quaternion localRotation)
        {
            Transform pivot=new GameObject(name).transform;pivot.SetParent(parent,false);
            pivot.localPosition=localPosition;pivot.localRotation=localRotation;return pivot;
        }

        private Mesh TrackXenoMesh(Mesh mesh){if(mesh!=null)xenoMeshes.Add(mesh);return mesh;}

        private Transform MeshPart(string name,Mesh mesh,Transform parent,Vector3 localPosition,Vector3 localScale,Material material,Quaternion rotation)
        {
            GameObject part=ProductionMeshFactory.MeshObject("DF_HotZoneTank_"+name,parent,mesh,material);
            part.transform.localPosition=localPosition;part.transform.localScale=localScale;part.transform.localRotation=rotation;
            Renderer renderer=part.GetComponent<Renderer>();
            if(renderer!=null){renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;}
            return part.transform;
        }

        private void AimTurretAt(Vector3 worldTarget)
        {
            if(xenoAlien)return;
            if(marsSkeleton){if(barrelPivot!=null){Vector3 d=worldTarget-barrelPivot.position;d.y=0;if(d.sqrMagnitude>.01f)barrelPivot.rotation=Quaternion.Slerp(barrelPivot.rotation,Quaternion.LookRotation(d.normalized),1f-Mathf.Exp(-7f*Time.deltaTime));}return;}
            if(turretPivot==null)return;
            Vector3 localTarget=transform.InverseTransformPoint(worldTarget);localTarget.y=turretPivot.localPosition.y;
            Vector3 flat=new(localTarget.x,0,localTarget.z);
            if(flat.sqrMagnitude>.01f)
            {
                Quaternion desired=Quaternion.LookRotation(flat.normalized,Vector3.up);
                turretPivot.localRotation=Quaternion.Slerp(turretPivot.localRotation,desired,1f-Mathf.Exp(-9f*Time.deltaTime));
            }
            if(barrelPivot!=null)
            {
                Vector3 rest=noxaraSpider?spiderBarrelRest:kryonBear?bearBarrelRest:new Vector3(0,.08f,.62f);
                barrelPivot.localPosition=Vector3.Lerp(barrelPivot.localPosition,rest,1f-Mathf.Exp(-18f*Time.deltaTime));
            }
        }

        private void AnimateTracks(float movement)
        {
            if(noxaraSpider)return;
            if(kryonBear){bearMoveBlend=1f;return;}
            if(xenoAlien){xenoMoveBlend=1f;return;}
            if(marsSkeleton){skeletonMoveBlend=1f;return;}
            float bob=Mathf.Sin(Time.time*9f)*Mathf.Clamp01(movement*8f)*.008f;
            if(leftTrack!=null)leftTrack.localPosition=new Vector3(0,bob,0);
            if(rightTrack!=null)rightTrack.localPosition=new Vector3(0,-bob,0);
        }

        private void OnDestroy()
        {
            active.Remove(this);
            if(health!=null)health.Damaged-=OnXenoDamaged;
            foreach(Mesh mesh in xenoMeshes)if(mesh!=null)Destroy(mesh);
            if(armorMat!=null)Destroy(armorMat);
            if(darkMat!=null)Destroy(darkMat);
            if(energyMat!=null)Destroy(energyMat);
            if(jointMat!=null)Destroy(jointMat);
            if(teamMat!=null)Destroy(teamMat);
            if(bearIceMat!=null)Destroy(bearIceMat);
        }
    }
}
