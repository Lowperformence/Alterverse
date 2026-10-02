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
        private Health health,target,supportLeader;
        private HeroLocomotion locomotion;
        private float nextAttack,nextScan,nextSupportScan,blockedTime,detourUntil,shotAt;
        private Vector3 patrolTarget,home,detourDirection,supportAnchor;
        private int patrolIndex;
        private bool shotPending;
        private Health shotTarget;
        private Transform turretPivot,barrelPivot,leftTrack,rightTrack;
        private Transform moonBodyRoot,moonLeftArm,moonRightArm,moonLeftLeg,moonRightLeg;
        private Quaternion moonLeftArmBase,moonRightArmBase,moonLeftLegBase,moonRightLegBase;
        private Vector3 moonBodyRest;
        private float moonMoveBlend;
        private PlanetSentinelVisual planetVisual;
        public bool AttackWindup=>shotPending;
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
        private float moonGaitPhase,skeletonGaitPhase,xenoGaitPhase,bearGaitPhase;
        private float skeletonAttackKick,spiderAttackKick,bearAttackKick;
        private Vector3 lastMovementSample;
        private bool movementSampleReady;
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

        private static int coopSpawnSerial;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry(){active.Clear();coopSpawnSerial=0;}
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
            go.name=ArenaVariantRuntime.IsGJ504b?(team==0?"DF_ZeroGJ504bSandBeast":"DF_LumenGJ504bSandBeast"):bear?(team==0?"DF_ZeroKryonFrostBear":"DF_LumenKryonFrostBear"):
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
            if(Alterverse.Multiplayer.CoopAuthority.Active)
                go.AddComponent<Alterverse.Multiplayer.CoopWorldEntityKey>().Configure($"HOTZONE:{coopSpawnSerial++}");
            go.AddComponent<HotZoneTank>().Initialize(team,spawn,skeleton,alien,spider,bear);
            return true;
        }

        private ReferenceMonsterRig referenceRig;

        private void Initialize(int assignedTeam,Vector3 spawn,bool skeleton,bool alien,bool spider,bool bear)
        {
            gameObject.layer=GameplayCollision.Units;
            team=assignedTeam;marsSkeleton=skeleton;xenoAlien=alien;noxaraSpider=spider;kryonBear=bear;home=spawn;health=GetComponent<Health>();
            locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();
            if(ArenaVariantRuntime.IsGJ504b)
            {
                referenceRig=ReferenceMonsterRig.Build(transform,"GJMagentaGolem");
                if(referenceRig!=null)barrelPivot=referenceRig.Muzzle;
                else { planetVisual=PlanetSentinelVisual.Build(transform,team,true);barrelPivot=planetVisual.Muzzle; }
            }
            else if(ArenaVariantRuntime.IsDiamondCore) { /* Separate world-space crystal guardian supplies its own body. */ }
            else if(kryonBear)BuildKryonBearVisuals();
            else if(noxaraSpider)
            {
                BuildNoxaraSpiderVisuals();
                if(GetComponent<NoxaraSpiderMeteorShower>()==null)gameObject.AddComponent<NoxaraSpiderMeteorShower>();
            }
            else if(xenoAlien){BuildAlienVisuals();health.Damaged+=OnXenoDamaged;}
            else if(marsSkeleton)BuildSkeletonVisuals();
            else BuildVisuals();
            if(!FrontierPlanetLayout.Active)BuildModernMonsterDetails();
            AuthoredHotZoneArt.Apply(transform,ArenaVariantRuntime.IsGJ504b?"Sandstone":
                kryonBear?"Ice":noxaraSpider?"Spider":xenoAlien?"Xeno":marsSkeleton?"Skeleton":"Machine");
            lastMovementSample=transform.position;
            movementSampleReady=true;
            SetNextPatrol();
        }

        private void Update()
        {
            if(Time.timeScale<=0f||health==null||health.IsDead)return;
            TrackRootMovement(Time.deltaTime);
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
            {
                if(xenoAlien)AnimateXeno();
                if(marsSkeleton)AnimateSkeleton();
                if(noxaraSpider)AnimateNoxaraSpider();
                if(kryonBear)AnimateKryonBear();
                if(!FrontierPlanetLayout.Active&&!xenoAlien&&!marsSkeleton&&!noxaraSpider&&!kryonBear)AnimateMoonGuard();
                return;
            }
            if(xenoAlien)AnimateXeno();
            if(marsSkeleton)AnimateSkeleton();
            if(noxaraSpider)AnimateNoxaraSpider();
            if(kryonBear)AnimateKryonBear();
            if(!FrontierPlanetLayout.Active&&!xenoAlien&&!marsSkeleton&&!noxaraSpider&&!kryonBear)AnimateMoonGuard();
            if(shotPending&&Time.time>=shotAt)ResolveShot();
            if(Time.time>=nextSupportScan)
            {
                nextSupportScan=Time.time+.42f;
                if(!TeamSupportGroup.TryGetMonsterAnchor(team,transform.position,out supportLeader,out supportAnchor))
                {
                    supportLeader=null;
                    supportAnchor=Vector3.zero;
                }
            }

            if(Time.time>=nextScan)
            {
                nextScan=Time.time+ScanInterval;
                ValidateOrAcquireTarget();
            }

            if(target!=null)
            {
                AimTurretAt(target.transform.position);
                Vector3 delta=target.transform.position-transform.position;delta.y=0;
                bool hasGroup=supportLeader!=null&&!supportLeader.IsDead;
                if(delta.sqrMagnitude>ChaseLeash*ChaseLeash||
                   hasGroup&&(target.transform.position-supportAnchor).sqrMagnitude>11.5f*11.5f)
                {
                    target=null;
                }
                else
                {
                    if(delta.sqrMagnitude<=AttackRange*AttackRange)
                    {
                        locomotion.FaceAction(delta,AttackCooldown);
                        if(Time.time>=nextAttack&&!shotPending)BeginFire();
                        return;
                    }
                    MoveToward(target.transform.position,1.25f);
                    return;
                }
            }

            if(supportLeader!=null&&!supportLeader.IsDead)
            {
                // Large rewards take an outer formation slot so they feel like part of the army
                // without standing on top of the small infantry or heroes.
                Vector3 forward=supportLeader.transform.forward;forward.y=0;
                if(forward.sqrMagnitude<.01f)forward=team==0?Vector3.right:Vector3.left;
                forward.Normalize();
                Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
                float side=((transform.GetSiblingIndex() & 1) == 0 ? -1f : 1f);
                Vector3 groupDestination=supportAnchor-forward*2.8f+right*side*2.4f;
                groupDestination.y=transform.position.y;
                AimTurretAt(transform.position+forward*4f);
                MoveToward(groupDestination,1.25f);
                return;
            }

            if((patrolTarget-transform.position).sqrMagnitude<1.2f*1.2f)SetNextPatrol();
            AimTurretAt(transform.position+transform.forward*4f);
            MoveToward(patrolTarget,.65f);
        }

        private void ValidateOrAcquireTarget()
        {
            if(target!=null&&!TeamSupportGroup.IsEnemyCombatUnit(target,team))target=null;

            // Respect the same team focus as soldiers/bots whenever that target is close enough
            // to the group. This makes the monster visibly participate in the same fight.
            Health focus=TeamSupportGroup.FocusFor(team);
            if(focus!=null)
            {
                Vector3 fromMonster=focus.transform.position-transform.position;fromMonster.y=0;
                Vector3 fromGroup=focus.transform.position-(supportLeader!=null?supportAnchor:transform.position);fromGroup.y=0;
                if(fromMonster.sqrMagnitude<=AcquireRadius*AcquireRadius&&fromGroup.sqrMagnitude<=11.5f*11.5f)
                {
                    target=focus;
                    return;
                }
            }

            if(target!=null)
            {
                Vector3 d=target.transform.position-transform.position;d.y=0;
                Vector3 dg=target.transform.position-(supportLeader!=null?supportAnchor:transform.position);dg.y=0;
                if(d.sqrMagnitude<=AcquireRadius*AcquireRadius*1.35f&&
                   (supportLeader==null||dg.sqrMagnitude<=11.5f*11.5f))return;
                target=null;
            }

            float bestScore=AcquireRadius*AcquireRadius;
            Health best=null;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health h=Health.Active[i];
                if(h==null||h.IsDead||h==health||!TeamSupportGroup.IsEnemyCombatUnit(h,team))continue;
                Vector3 d=h.transform.position-transform.position;d.y=0;
                float sqr=d.sqrMagnitude;if(sqr>AcquireRadius*AcquireRadius)continue;

                if(supportLeader!=null)
                {
                    Vector3 groupDelta=h.transform.position-supportAnchor;groupDelta.y=0;
                    if(groupDelta.sqrMagnitude>11.5f*11.5f)continue;
                }

                float score=sqr;
                if(h.GetComponent<HeroIdentity>()!=null)score*=.68f;
                if(h.GetComponent<TrenchSoldier>()!=null)score*=.82f;
                if(score<bestScore){bestScore=score;best=h;}
            }
            target=best;
        }

        private void BeginFire()
        {
            if(target==null||target.IsDead)return;
            planetVisual?.PulseAttack();
            referenceRig?.PulseAttack();
            shotPending=true;shotTarget=target;shotAt=Time.time+ShotWindup;
            nextAttack=Time.time+AttackCooldown;
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color energy=referenceRig!=null?referenceRig.EnergyColor:planetVisual!=null?planetVisual.EnergyColor:ArenaVariantRuntime.IsDiamondCore?new Color(.30f,.86f,1f):kryonBear?bearEnergyBase:noxaraSpider?spiderEnergyBase:xenoAlien?new Color(.62f,.90f,.18f):teamEnergy;
            bool creature=FrontierPlanetLayout.Active||marsSkeleton||xenoAlien||noxaraSpider||kryonBear;
            Vector3 muzzle=ArenaVariantRuntime.IsDiamondCore?transform.position+Vector3.up*2.95f:creature&&barrelPivot!=null?barrelPivot.position:(barrelPivot!=null?barrelPivot.TransformPoint(new Vector3(0,0,1.70f)):transform.position+transform.forward*1.7f+Vector3.up*.95f);
            PrototypeVfx.Ring(muzzle,energy,kryonBear?.40f:noxaraSpider?.36f:xenoAlien?.32f:.34f,ShotWindup);
            if(kryonBear)PrototypeVfx.Burst(muzzle,Color.Lerp(energy,Color.white,.35f),.15f,4);
            else if(noxaraSpider)PrototypeVfx.Burst(muzzle,energy,.12f,3);
            else if(xenoAlien)PrototypeVfx.Burst(muzzle,teamEnergy,.10f,2);
        }

        private void ResolveShot()
        {
            shotPending=false;
            if(marsSkeleton)skeletonAttackKick=1f;
            if(noxaraSpider)spiderAttackKick=1f;
            if(kryonBear)bearAttackKick=1f;
            Health victim=shotTarget;shotTarget=null;
            if(victim==null||victim.IsDead)return;
            TeamMember victimTeam=victim.GetComponent<TeamMember>();
            if(victimTeam==null||victimTeam.Team==team)return;
            Vector3 delta=victim.transform.position-transform.position;delta.y=0;
            if(delta.sqrMagnitude>(AttackRange+.75f)*(AttackRange+.75f))return;

            bool creature=FrontierPlanetLayout.Active||marsSkeleton||xenoAlien||noxaraSpider||kryonBear;
            Vector3 muzzle=ArenaVariantRuntime.IsDiamondCore?transform.position+Vector3.up*2.95f:creature&&barrelPivot!=null?barrelPivot.position:(barrelPivot!=null?barrelPivot.TransformPoint(new Vector3(0,0,1.70f)):transform.position+transform.forward*1.7f+Vector3.up*.95f);
            Vector3 hit=victim.transform.position+Vector3.up*.55f;
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color energy=referenceRig!=null?referenceRig.EnergyColor:planetVisual!=null?planetVisual.EnergyColor:ArenaVariantRuntime.IsDiamondCore?new Color(.30f,.86f,1f):kryonBear?bearEnergyBase:noxaraSpider?spiderEnergyBase:xenoAlien?new Color(.62f,.90f,.18f):teamEnergy;
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
            victim.TakeDamage(kryonBear?Damage+5f:Damage,gameObject);
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

            // Moon Guard: compact military security mech, based on the supplied reference.
            // Layered olive armor, exposed dark mechanics, cyan optics and readable hard-surface
            // segmentation give it a much higher-detail silhouette without expensive textures.
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color cyan=new(.08f,.82f,1f,1f);
            Color glow=Color.Lerp(teamEnergy,cyan,.70f);
            Color olive=new(.185f,.235f,.195f,1f);
            Color oliveLight=new(.285f,.325f,.270f,1f);
            Color dark=new(.028f,.036f,.038f,1f);
            Color joint=new(.070f,.082f,.080f,1f);

            armorMat=MakeMaterial(shader,olive,.58f,.30f,Color.black);
            darkMat=MakeMaterial(shader,dark,.76f,.22f,Color.black);
            jointMat=MakeMaterial(shader,joint,.84f,.26f,Color.black);
            energyMat=MakeMaterial(shader,glow,.08f,.80f,glow*3.25f);
            teamMat=MakeMaterial(shader,teamEnergy,.10f,.50f,teamEnergy*1.10f);

            Mesh chestPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_MoonGuardChestPlate",new[]{
                new Vector2(-.64f,-.42f),new Vector2(.64f,-.42f),new Vector2(.78f,.02f),
                new Vector2(.50f,.52f),new Vector2(.12f,.66f),new Vector2(-.48f,.57f),new Vector2(-.76f,.08f)},.18f));
            Mesh shoulderPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_MoonGuardShoulderPlate",new[]{
                new Vector2(-.50f,-.36f),new Vector2(.46f,-.38f),new Vector2(.58f,.20f),
                new Vector2(.25f,.47f),new Vector2(-.44f,.43f),new Vector2(-.60f,.04f)},.18f));
            Mesh forearmPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_MoonGuardForearmPlate",new[]{
                new Vector2(-.36f,-.50f),new Vector2(.36f,-.50f),new Vector2(.43f,.34f),
                new Vector2(.16f,.55f),new Vector2(-.34f,.46f)},.16f));
            Mesh shinPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_MoonGuardShinPlate",new[]{
                new Vector2(-.34f,-.55f),new Vector2(.34f,-.55f),new Vector2(.42f,.22f),
                new Vector2(.20f,.58f),new Vector2(-.30f,.48f)},.16f));
            Mesh headPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_MoonGuardHeadPlate",new[]{
                new Vector2(-.46f,-.24f),new Vector2(.46f,-.24f),new Vector2(.40f,.28f),
                new Vector2(.16f,.46f),new Vector2(-.30f,.41f)},.15f));
            Mesh footPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_MoonGuardFootPlate",new[]{
                new Vector2(-.46f,-.22f),new Vector2(.46f,-.22f),new Vector2(.52f,.18f),
                new Vector2(.20f,.36f),new Vector2(-.42f,.30f)},.18f));

            moonBodyRoot=Pivot("DF_MoonGuardBody",transform,new Vector3(0,.32f,0),Quaternion.identity);
            moonBodyRest=moonBodyRoot.localPosition;

            // Pelvis and flexible waist expose the machine under the armor.
            Part("MoonGuardPelvisCore",PrimitiveType.Cylinder,moonBodyRoot,new Vector3(0,.04f,-.06f),new Vector3(.62f,.18f,.62f),jointMat,Quaternion.identity);
            Part("MoonGuardWaist",PrimitiveType.Cylinder,moonBodyRoot,new Vector3(0,.32f,-.02f),new Vector3(.48f,.20f,.48f),darkMat,Quaternion.identity);
            Part("MoonGuardHipArmorL",PrimitiveType.Cube,moonBodyRoot,new Vector3(-.49f,.05f,.05f),new Vector3(.40f,.42f,.62f),armorMat,Quaternion.Euler(0,-4,-5));
            Part("MoonGuardHipArmorR",PrimitiveType.Cube,moonBodyRoot,new Vector3(.49f,.05f,.05f),new Vector3(.40f,.42f,.62f),armorMat,Quaternion.Euler(0,4,5));

            // The upper body rotates toward targets, like the reference's compact armored torso.
            turretPivot=Pivot("DF_MoonGuardUpperBody",moonBodyRoot,new Vector3(0,.73f,0),Quaternion.identity);
            Part("MoonGuardTorsoCore",PrimitiveType.Cube,turretPivot,new Vector3(0,0,-.04f),new Vector3(1.45f,.76f,.86f),darkMat,Quaternion.identity);
            MeshPart("MoonGuardChestArmor",chestPlate,turretPivot,new Vector3(0,.03f,.53f),new Vector3(1.20f,1.02f,1f),armorMat,Quaternion.Euler(-4,0,0));
            Part("MoonGuardUpperChest",PrimitiveType.Cube,turretPivot,new Vector3(0,.30f,.22f),new Vector3(1.26f,.30f,.72f),armorMat,Quaternion.Euler(-4,0,0));
            Part("MoonGuardChestInset",PrimitiveType.Cube,turretPivot,new Vector3(0,-.12f,.57f),new Vector3(.54f,.18f,.055f),jointMat,Quaternion.identity);
            Part("MoonGuardChestLightL",PrimitiveType.Cube,turretPivot,new Vector3(-.23f,-.11f,.61f),new Vector3(.16f,.065f,.035f),energyMat,Quaternion.identity);
            Part("MoonGuardChestLightR",PrimitiveType.Cube,turretPivot,new Vector3(.23f,-.11f,.61f),new Vector3(.16f,.065f,.035f),energyMat,Quaternion.identity);
            Part("MoonGuardPowerCore",PrimitiveType.Cube,turretPivot,new Vector3(0,-.10f,.615f),new Vector3(.12f,.11f,.035f),energyMat,Quaternion.Euler(0,0,45));

            // Compact sensor head recessed into the torso.
            Transform head=Pivot("DF_MoonGuardHead",turretPivot,new Vector3(0,.64f,.12f),Quaternion.Euler(-3,0,0));
            Part("MoonGuardHeadCore",PrimitiveType.Cube,head,new Vector3(0,0,0),new Vector3(.70f,.42f,.56f),darkMat,Quaternion.identity);
            MeshPart("MoonGuardHeadArmor",headPlate,head,new Vector3(0,.04f,.34f),new Vector3(.82f,.78f,.92f),armorMat,Quaternion.Euler(-4,0,0));
            Part("MoonGuardVisor",PrimitiveType.Cube,head,new Vector3(0,-.05f,.49f),new Vector3(.43f,.075f,.045f),energyMat,Quaternion.identity);
            Part("MoonGuardSensorL",PrimitiveType.Cube,head,new Vector3(-.28f,.12f,.35f),new Vector3(.10f,.10f,.08f),energyMat,Quaternion.identity);
            Part("MoonGuardSensorR",PrimitiveType.Cube,head,new Vector3(.28f,.12f,.35f),new Vector3(.10f,.10f,.08f),energyMat,Quaternion.identity);

            // Shoulder blocks and backpack sensor pods create the recognizable military silhouette.
            BuildMoonGuardArm(-1f,shoulderPlate,forearmPlate,oliveLight);
            BuildMoonGuardArm(1f,shoulderPlate,forearmPlate,oliveLight);
            Part("MoonGuardBackpack",PrimitiveType.Cube,turretPivot,new Vector3(0,.24f,-.56f),new Vector3(.90f,.66f,.38f),darkMat,Quaternion.identity);
            Part("MoonGuardPodL",PrimitiveType.Cube,turretPivot,new Vector3(-.43f,.67f,-.48f),new Vector3(.40f,.42f,.42f),armorMat,Quaternion.Euler(-2,-5,-3));
            Part("MoonGuardPodR",PrimitiveType.Cube,turretPivot,new Vector3(.43f,.67f,-.48f),new Vector3(.40f,.42f,.42f),armorMat,Quaternion.Euler(-2,5,3));
            Part("MoonGuardPodLensL",PrimitiveType.Cube,turretPivot,new Vector3(-.43f,.66f,-.25f),new Vector3(.18f,.09f,.04f),energyMat,Quaternion.identity);
            Part("MoonGuardPodLensR",PrimitiveType.Cube,turretPivot,new Vector3(.43f,.66f,-.25f),new Vector3(.18f,.09f,.04f),energyMat,Quaternion.identity);
            Part("MoonGuardAntenna",PrimitiveType.Cylinder,turretPivot,new Vector3(.50f,1.13f,-.48f),new Vector3(.025f,.50f,.025f),jointMat,Quaternion.Euler(0,0,-5));
            Part("MoonGuardAntennaTip",PrimitiveType.Sphere,turretPivot,new Vector3(.54f,1.65f,-.48f),new Vector3(.055f,.055f,.055f),energyMat,Quaternion.identity);

            // Weapon emitter is integrated into the chest instead of a tank cannon.
            barrelPivot=Pivot("DF_MoonGuardAttackSocket",turretPivot,new Vector3(0,-.12f,.72f),Quaternion.identity);
            Part("MoonGuardEmitterHousing",PrimitiveType.Cylinder,barrelPivot,Vector3.zero,new Vector3(.15f,.055f,.15f),jointMat,Quaternion.Euler(90,0,0));
            Part("MoonGuardEmitter",PrimitiveType.Sphere,barrelPivot,new Vector3(0,0,.07f),new Vector3(.13f,.10f,.08f),energyMat,Quaternion.identity);

            BuildMoonGuardLeg(-1f,shinPlate,footPlate);
            BuildMoonGuardLeg(1f,shinPlate,footPlate);

            // Small panel details sell scale at gameplay-camera distance.
            Part("MoonGuardIDPlate",PrimitiveType.Cube,turretPivot,new Vector3(-.43f,.25f,.57f),new Vector3(.22f,.09f,.035f),teamMat,Quaternion.identity);
            for(int i=-1;i<=1;i++)
            {
                float x=i*.32f;
                Part("MoonGuardChestBolt",PrimitiveType.Cylinder,turretPivot,new Vector3(x,-.27f,.54f),
                    new Vector3(.045f,.018f,.045f),jointMat,Quaternion.Euler(90,0,0));
            }
        }

        private void BuildMoonGuardArm(float side,Mesh shoulderPlate,Mesh forearmPlate,Color accent)
        {
            bool left=side<0f;
            Transform shoulder=Pivot(left?"DF_MoonGuardArmL":"DF_MoonGuardArmR",turretPivot,new Vector3(side*.91f,.24f,-.02f),Quaternion.Euler(0,0,side*4f));
            Part(left?"MoonGuardShoulderCoreL":"MoonGuardShoulderCoreR",PrimitiveType.Sphere,shoulder,Vector3.zero,new Vector3(.44f,.38f,.46f),jointMat,Quaternion.identity);
            MeshPart(left?"MoonGuardShoulderArmorL":"MoonGuardShoulderArmorR",shoulderPlate,shoulder,new Vector3(side*.08f,.09f,.29f),new Vector3(.90f,.88f,.86f),armorMat,Quaternion.Euler(-4,side*6f,side*-5f));
            Part(left?"MoonGuardUpperArmL":"MoonGuardUpperArmR",PrimitiveType.Capsule,shoulder,new Vector3(0,-.48f,.02f),new Vector3(.25f,.54f,.27f),darkMat,Quaternion.identity);
            Part(left?"MoonGuardElbowL":"MoonGuardElbowR",PrimitiveType.Sphere,shoulder,new Vector3(0,-.82f,.06f),new Vector3(.24f,.22f,.24f),jointMat,Quaternion.identity);
            MeshPart(left?"MoonGuardForearmArmorL":"MoonGuardForearmArmorR",forearmPlate,shoulder,new Vector3(0,-1.08f,.22f),new Vector3(.84f,.88f,.82f),armorMat,Quaternion.Euler(-5,0,side*3f));
            Part(left?"MoonGuardForearmCoreL":"MoonGuardForearmCoreR",PrimitiveType.Cube,shoulder,new Vector3(0,-1.05f,.02f),new Vector3(.36f,.58f,.36f),darkMat,Quaternion.identity);
            Part(left?"MoonGuardWristL":"MoonGuardWristR",PrimitiveType.Cylinder,shoulder,new Vector3(0,-1.39f,.08f),new Vector3(.16f,.12f,.16f),jointMat,Quaternion.identity);
            Part(left?"MoonGuardHandL":"MoonGuardHandR",PrimitiveType.Cube,shoulder,new Vector3(0,-1.54f,.14f),new Vector3(.31f,.25f,.36f),darkMat,Quaternion.Euler(-4,0,0));

            if(left){moonLeftArm=shoulder;moonLeftArmBase=shoulder.localRotation;}
            else{moonRightArm=shoulder;moonRightArmBase=shoulder.localRotation;}
        }

        private void BuildMoonGuardLeg(float side,Mesh shinPlate,Mesh footPlate)
        {
            bool left=side<0f;
            Transform leg=Pivot(left?"DF_MoonGuardLegL":"DF_MoonGuardLegR",moonBodyRoot,new Vector3(side*.42f,.02f,-.04f),Quaternion.Euler(3,0,side*2f));
            Part(left?"MoonGuardThighCoreL":"MoonGuardThighCoreR",PrimitiveType.Capsule,leg,new Vector3(0,-.38f,.01f),new Vector3(.34f,.58f,.36f),darkMat,Quaternion.identity);
            Part(left?"MoonGuardThighArmorL":"MoonGuardThighArmorR",PrimitiveType.Cube,leg,new Vector3(0,-.34f,.23f),new Vector3(.54f,.58f,.38f),armorMat,Quaternion.Euler(-3,0,side*2f));
            Part(left?"MoonGuardKneeL":"MoonGuardKneeR",PrimitiveType.Sphere,leg,new Vector3(0,-.73f,.12f),new Vector3(.29f,.25f,.30f),jointMat,Quaternion.identity);
            Part(left?"MoonGuardKneeLightL":"MoonGuardKneeLightR",PrimitiveType.Cube,leg,new Vector3(0,-.72f,.32f),new Vector3(.15f,.07f,.045f),energyMat,Quaternion.identity);
            Part(left?"MoonGuardShinCoreL":"MoonGuardShinCoreR",PrimitiveType.Capsule,leg,new Vector3(0,-1.02f,.09f),new Vector3(.32f,.52f,.35f),darkMat,Quaternion.identity);
            MeshPart(left?"MoonGuardShinArmorL":"MoonGuardShinArmorR",shinPlate,leg,new Vector3(0,-1.01f,.32f),new Vector3(.88f,.82f,.90f),armorMat,Quaternion.Euler(-6,0,side*2f));

            Transform foot=Pivot(left?"DF_MoonGuardFootL":"DF_MoonGuardFootR",leg,new Vector3(0,-1.34f,.28f),Quaternion.identity);
            Part(left?"MoonGuardFootCoreL":"MoonGuardFootCoreR",PrimitiveType.Cube,foot,new Vector3(0,0,.08f),new Vector3(.58f,.22f,.78f),darkMat,Quaternion.identity);
            MeshPart(left?"MoonGuardFootArmorL":"MoonGuardFootArmorR",footPlate,foot,new Vector3(0,.02f,.42f),new Vector3(.78f,.58f,.88f),armorMat,Quaternion.Euler(-12,0,0));
            Part(left?"MoonGuardHeelL":"MoonGuardHeelR",PrimitiveType.Cube,foot,new Vector3(0,.01f,-.26f),new Vector3(.42f,.18f,.34f),jointMat,Quaternion.identity);

            if(left){moonLeftLeg=leg;moonLeftLegBase=leg.localRotation;}
            else{moonRightLeg=leg;moonRightLegBase=leg.localRotation;}
        }

        private void AnimateMoonGuard()
        {
            if(moonBodyRoot==null)return;
            float t=Time.time;
            float stride=Mathf.Sin(moonGaitPhase)*moonMoveBlend;
            float bob=(1f-Mathf.Cos(moonGaitPhase*2f))*.032f*moonMoveBlend;
            moonBodyRoot.localPosition=moonBodyRest+Vector3.up*bob;
            moonBodyRoot.localRotation=Quaternion.Euler(-3f+Mathf.Cos(moonGaitPhase*2f)*1.4f*moonMoveBlend,0,stride*2f);

            if(moonLeftLeg!=null)moonLeftLeg.localRotation=moonLeftLegBase*Quaternion.Euler(stride*34f,0,0);
            if(moonRightLeg!=null)moonRightLeg.localRotation=moonRightLegBase*Quaternion.Euler(-stride*34f,0,0);
            if(moonLeftArm!=null)moonLeftArm.localRotation=moonLeftArmBase*Quaternion.Euler(-stride*27f,0,0);
            if(moonRightArm!=null)moonRightArm.localRotation=moonRightArmBase*Quaternion.Euler(stride*27f,0,0);
        }

        private void BuildSkeletonVisuals()
        {
            referenceRig=ReferenceMonsterRig.Build(transform,"MarsSkeleton");
            if(referenceRig!=null){barrelPivot=referenceRig.Muzzle;return;}
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return;
            Color energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color bone=new Color(.36f,.41f,.47f,1),deep=new Color(.045f,.055f,.075f,1);
            armorMat=MakeMaterial(shader,bone,.76f,.56f,Color.black);darkMat=MakeMaterial(shader,deep,.64f,.34f,Color.black);
            jointMat=MakeMaterial(shader,new Color(.16f,.19f,.23f,1),.84f,.48f,Color.black);
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
            Part("TitanRibRunesL",PrimitiveType.Cube,transform,new Vector3(-.45f,1.15f,.18f),new Vector3(.045f,.42f,.025f),energyMat,Quaternion.Euler(0,0,-32));
            Part("TitanRibRunesR",PrimitiveType.Cube,transform,new Vector3(.45f,1.15f,.18f),new Vector3(.045f,.42f,.025f),energyMat,Quaternion.Euler(0,0,32));
        
            // Bone-built wrist cuffs and brow plates sharpen the old Martian silhouette.
            Part("TitanWristCuffL",PrimitiveType.Cube,transform,new Vector3(-1.34f,.45f,.10f),new Vector3(.34f,.16f,.32f),jointMat,Quaternion.Euler(0,0,-8));
            Part("TitanWristCuffR",PrimitiveType.Cube,transform,new Vector3(1.34f,.45f,.10f),new Vector3(.34f,.16f,.32f),jointMat,Quaternion.Euler(0,0,8));
            Part("TitanBrowRidge",PrimitiveType.Cube,transform,new Vector3(0,2.49f,.39f),new Vector3(.78f,.14f,.20f),darkMat,Quaternion.Euler(0,0,-3));
            Part("TitanJawRune",PrimitiveType.Cube,transform,new Vector3(0,1.93f,.43f),new Vector3(.34f,.045f,.04f),energyMat,Quaternion.Euler(0,0,2));
        }

        private void AnimateSkeleton()
        {
            skeletonAttackKick=Mathf.MoveTowards(skeletonAttackKick,0f,Time.deltaTime*7f);
            float idle=Mathf.Sin(Time.time*1.65f),stride=Mathf.Sin(skeletonGaitPhase)*skeletonMoveBlend;
            if(skeletonLeftArm!=null)skeletonLeftArm.localRotation=skeletonLeftArmBase*Quaternion.Euler(stride*30f,0,idle*1.5f);
            if(skeletonRightArm!=null)skeletonRightArm.localRotation=skeletonRightArmBase*Quaternion.Euler(-stride*30f-(shotPending?18f:0f)+skeletonAttackKick*24f,0,-idle*1.5f);
            if(skeletonLeftLeg!=null)skeletonLeftLeg.localRotation=skeletonLeftLegBase*Quaternion.Euler(-stride*32f,0,0);
            if(skeletonRightLeg!=null)skeletonRightLeg.localRotation=skeletonRightLegBase*Quaternion.Euler(stride*32f,0,0);
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
            Part("SpiderAbdomenRidge",PrimitiveType.Cube,spiderBodyRoot,new Vector3(0,.65f,-.52f),new Vector3(.12f,.055f,.92f),energyMat,Quaternion.Euler(4,0,0));
            // Segmented carapace and glowing cuts stay legible from the overhead camera.
            for(int segment=0;segment<3;segment++)
            {
                float z=-.93f+segment*.40f;
                for(int sideIndex=0;sideIndex<2;sideIndex++)
                {
                    float side=sideIndex==0?-1f:1f;
                    Part($"SpiderCarapacePlate_{segment}_{sideIndex}",PrimitiveType.Cube,spiderBodyRoot,
                        new Vector3(side*.32f,.665f,z),new Vector3(.43f,.075f,.29f),
                        armorMat,Quaternion.Euler(5f,side*10f,side*-9f));
                    Part($"SpiderCarapaceSeam_{segment}_{sideIndex}",PrimitiveType.Cube,spiderBodyRoot,
                        new Vector3(side*.29f,.708f,z+.14f),new Vector3(.30f,.018f,.035f),
                        jointMat,Quaternion.Euler(5f,side*10f,side*-9f));
                }
            }
            Part("SpiderThoraxSpine",PrimitiveType.Cube,spiderBodyRoot,
                new Vector3(0,.68f,.33f),new Vector3(.13f,.055f,.56f),energyMat,Quaternion.Euler(-5,0,0));
            for(int sideIndex=0;sideIndex<2;sideIndex++)
            {
                float side=sideIndex==0?-1f:1f;
                Part($"SpiderThoraxShoulder_{sideIndex}",PrimitiveType.Cube,spiderBodyRoot,
                    new Vector3(side*.48f,.55f,.37f),new Vector3(.28f,.12f,.58f),
                    armorMat,Quaternion.Euler(-5f,side*12f,side*-13f));
                Part($"SpiderRearSpinneret_{sideIndex}",PrimitiveType.Capsule,spiderBodyRoot,
                    new Vector3(side*.29f,.22f,-1.21f),new Vector3(.17f,.18f,.38f),
                    jointMat,Quaternion.Euler(70f,side*12f,0));
            }


            turretPivot=Pivot("DF_NoxaraSpiderHeadPivot",spiderBodyRoot,new Vector3(0,.52f,.86f),Quaternion.identity);
            Part("SpiderHead",PrimitiveType.Cube,turretPivot,new Vector3(0,0,.16f),new Vector3(.88f,.46f,.70f),armorMat,Quaternion.Euler(-4,0,0));
            Part("SpiderFace",PrimitiveType.Cube,turretPivot,new Vector3(0,-.03f,.49f),new Vector3(.72f,.30f,.10f),darkMat,Quaternion.identity);
            float[] eyeX={-.27f,-.09f,.09f,.27f};
            for(int i=0;i<eyeX.Length;i++)
                Part("SpiderEye"+i,PrimitiveType.Sphere,turretPivot,new Vector3(eyeX[i],.06f,.56f),new Vector3(.105f,.085f,.075f),energyMat,Quaternion.identity);
            for(int sideIndex=0;sideIndex<2;sideIndex++)
            {
                float side=sideIndex==0?-1f:1f;
                Part($"SpiderBrow_{sideIndex}",PrimitiveType.Cube,turretPivot,
                    new Vector3(side*.28f,.17f,.48f),new Vector3(.34f,.075f,.27f),
                    jointMat,Quaternion.Euler(-10f,side*14f,side*-12f));
                Part($"SpiderOuterEye_{sideIndex}",PrimitiveType.Sphere,turretPivot,
                    new Vector3(side*.42f,.03f,.49f),new Vector3(.11f,.10f,.095f),
                    energyMat,Quaternion.identity);
                Part($"SpiderFang_{sideIndex}",PrimitiveType.Capsule,turretPivot,
                    new Vector3(side*.24f,-.34f,.76f),new Vector3(.085f,.22f,.085f),
                    armorMat,Quaternion.Euler(0,0,side*18f));
            }


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
                     Part("SpiderUpperLegPlate"+index,PrimitiveType.Cube,root,
                         new Vector3(SpiderUpperLength*.52f,.092f,0),
                         new Vector3(.67f,.065f,.22f),jointMat,Quaternion.identity);
                     Part("SpiderUpperLegVein"+index,PrimitiveType.Cube,root,
                         new Vector3(SpiderUpperLength*.53f,.130f,0),
                         new Vector3(.52f,.022f,.055f),energyMat,Quaternion.identity);


                    Transform knee=Pivot($"DF_NoxaraSpiderKnee_{index}",root,
                        new Vector3(SpiderUpperLength,0,0),Quaternion.identity);
                    spiderKneePivots[index]=knee;
                    Part("SpiderKnee"+index,PrimitiveType.Sphere,knee,Vector3.zero,
                        new Vector3(.20f,.20f,.20f),jointMat,Quaternion.identity);

                    Part("SpiderLowerLeg"+index,PrimitiveType.Cube,knee,
                        new Vector3(SpiderLowerLength*.5f,0,0),
                        new Vector3(SpiderLowerLength,.11f,.145f),darkMat,Quaternion.identity);
                     Part("SpiderShinArmor"+index,PrimitiveType.Cube,knee,
                         new Vector3(SpiderLowerLength*.35f,.074f,0),
                         new Vector3(.48f,.055f,.19f),armorMat,Quaternion.identity);


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
        
            // Small paired armor vents and venom sacs make the abdomen read in motion.
            Part("SpiderAbdomenVentL",PrimitiveType.Cube,spiderBodyRoot,new Vector3(-.47f,.68f,-.55f),new Vector3(.20f,.035f,.46f),jointMat,Quaternion.Euler(7,0,-8));
            Part("SpiderAbdomenVentR",PrimitiveType.Cube,spiderBodyRoot,new Vector3(.47f,.68f,-.55f),new Vector3(.20f,.035f,.46f),jointMat,Quaternion.Euler(7,0,8));
            Part("SpiderVenomSackL",PrimitiveType.Sphere,spiderBodyRoot,new Vector3(-.51f,.28f,-.82f),new Vector3(.13f,.14f,.16f),energyMat,Quaternion.identity);
            Part("SpiderVenomSackR",PrimitiveType.Sphere,spiderBodyRoot,new Vector3(.51f,.28f,-.82f),new Vector3(.13f,.14f,.16f),energyMat,Quaternion.identity);
            Part("SpiderMandibleTipL",PrimitiveType.Capsule,turretPivot,new Vector3(-.28f,-.20f,.58f),new Vector3(.12f,.18f,.14f),teamMat,Quaternion.Euler(18,0,12));
            Part("SpiderMandibleTipR",PrimitiveType.Capsule,turretPivot,new Vector3(.28f,-.20f,.58f),new Vector3(.12f,.18f,.14f),teamMat,Quaternion.Euler(18,0,-12));
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

            // Keep phase bounded and advance only while the gameplay root actually travels.
            // This prevents foot skating during braking and loss of phase precision in long matches.
            if(displacement.sqrMagnitude>0f)
                spiderGaitClock=Mathf.Repeat(spiderGaitClock+displacement.magnitude/2.45f,1f);

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
            spiderAttackKick=Mathf.MoveTowards(spiderAttackKick,0f,Time.deltaTime*7f);
            float windup=shotPending?Mathf.Clamp01(1f-(shotAt-Time.time)/ShotWindup):0f;
            spiderBodyRoot.localPosition=spiderBodyRest+Vector3.up*(gaitBob+idle*.006f+windup*.025f);
            spiderBodyRoot.localRotation=Quaternion.Euler(pitch+windup*2.2f-spiderAttackKick*7.5f,spiderVisualYaw,roll);

            if(!spiderFeetInitialized)
            {
                for(int i=0;i<8;i++)
                {
                    Vector3 rest=SpiderDesiredFootWorld(i,Vector3.zero);
                    spiderFootPlant[i]=spiderSwingStart[i]=spiderSwingEnd[i]=rest;
                    spiderWasSwing[i]=false;
                }
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
                energyMat.SetColor("_EmissionColor",spiderEnergyBase*(2.7f+idle*.18f+windup*1.9f));
            }
            if(barrelPivot!=null)
            {
                float pulse=1f+(shotPending?.16f:0f)+Mathf.Max(0,idle)*.03f;
                barrelPivot.localScale=Vector3.one*pulse;
                barrelPivot.localPosition=spiderBarrelRest+Vector3.back*(windup*.045f+spiderAttackKick*.12f);
                barrelPivot.localRotation=Quaternion.Euler(-windup*8f+spiderAttackKick*28f,0,0);
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
            Color armor=new(.72f,.80f,.84f,1f),ice=new(.88f,.95f,.98f,1f),deep=new(.022f,.038f,.052f,1f),joint=new(.07f,.12f,.15f,1f);
            armorMat=MakeMaterial(shader,armor,.68f,.42f,Color.black);
            darkMat=MakeMaterial(shader,deep,.78f,.24f,Color.black);
            jointMat=MakeMaterial(shader,joint,.86f,.30f,Color.black);
            energyMat=MakeMaterial(shader,bearEnergyBase,.10f,.80f,bearEnergyBase*3.6f);
            teamMat=MakeMaterial(shader,teamEnergy,.12f,.52f,teamEnergy*1.15f);
            bearIceMat=MakeMaterial(shader,ice,.24f,.68f,new Color(.18f,.62f,.72f)*.34f);

            // The original bear relied heavily on spheres/cubes and read like a prototype.
            // These authored low-poly plates and blades create the segmented robotic silhouette
            // from the concept reference while keeping the runtime-only/mobile-friendly workflow.
            Mesh shoulderPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_KryonBearShoulderPlate",new[]{
                new Vector2(-.55f,-.38f),new Vector2(.46f,-.42f),new Vector2(.68f,.04f),
                new Vector2(.40f,.50f),new Vector2(-.24f,.60f),new Vector2(-.62f,.18f)},.20f));
            Mesh chestPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_KryonBearChestPlate",new[]{
                new Vector2(-.54f,-.38f),new Vector2(.54f,-.38f),new Vector2(.64f,.12f),
                new Vector2(.28f,.58f),new Vector2(0,.66f),new Vector2(-.28f,.58f),new Vector2(-.64f,.12f)},.18f));
            Mesh facePlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_KryonBearFacePlate",new[]{
                new Vector2(-.50f,-.20f),new Vector2(.50f,-.20f),new Vector2(.42f,.28f),
                new Vector2(.20f,.48f),new Vector2(-.20f,.48f),new Vector2(-.42f,.28f)},.16f));
            Mesh jawPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_KryonBearJawPlate",new[]{
                new Vector2(-.38f,-.18f),new Vector2(.38f,-.18f),new Vector2(.30f,.18f),
                new Vector2(0,.29f),new Vector2(-.30f,.18f)},.15f));
            Mesh limbPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_KryonBearLimbPlate",new[]{
                new Vector2(-.30f,-.46f),new Vector2(.30f,-.46f),new Vector2(.40f,.08f),
                new Vector2(.15f,.50f),new Vector2(-.28f,.40f)},.15f));
            Mesh clawBlade=TrackXenoMesh(XenoTitanMeshFactory.ClawBlade("DF_KryonBearClaw",.48f,.13f,.10f,.12f));
            Mesh iceFin=TrackXenoMesh(XenoTitanMeshFactory.Spine("DF_KryonBearIceFin",.56f,.25f,.28f));

            bearBodyRoot=Pivot("DF_KryonBearBody",transform,new Vector3(0,.18f,0),Quaternion.Euler(-2f,0,0));
            bearBodyRest=bearBodyRoot.localPosition;

            // Broad mechanical polar-bear mass with visible dark inner chassis.
            Part("KryonBearInnerTorso",PrimitiveType.Sphere,bearBodyRoot,new Vector3(0,.40f,-.18f),new Vector3(1.58f,.90f,2.05f),darkMat,Quaternion.identity);
            Part("KryonBearUpperShell",PrimitiveType.Sphere,bearBodyRoot,new Vector3(0,.54f,-.12f),new Vector3(1.46f,.74f,1.78f),armorMat,Quaternion.identity);
            MeshPart("KryonBearChestArmor",chestPlate,bearBodyRoot,new Vector3(0,.48f,.92f),new Vector3(1.10f,.94f,1f),armorMat,Quaternion.Euler(-8,0,0));
            MeshPart("KryonBearShoulderArmorL",shoulderPlate,bearBodyRoot,new Vector3(-.78f,.66f,.50f),new Vector3(1.05f,1.10f,1f),bearIceMat,Quaternion.Euler(-10,-12,-10));
            MeshPart("KryonBearShoulderArmorR",shoulderPlate,bearBodyRoot,new Vector3(.78f,.66f,.50f),new Vector3(1.05f,1.10f,1f),bearIceMat,Quaternion.Euler(-10,12,10));
            MeshPart("KryonBearFlankArmorL",shoulderPlate,bearBodyRoot,new Vector3(-.72f,.54f,-.66f),new Vector3(.90f,.82f,.90f),armorMat,Quaternion.Euler(2,-18,-8));
            MeshPart("KryonBearFlankArmorR",shoulderPlate,bearBodyRoot,new Vector3(.72f,.54f,-.66f),new Vector3(.90f,.82f,.90f),armorMat,Quaternion.Euler(2,18,8));

            // Layered dorsal armor and ice fins break up the toy-like smooth back.
            MeshPart("KryonBearBackPlateA",chestPlate,bearBodyRoot,new Vector3(0,.94f,.25f),new Vector3(.92f,.45f,.86f),bearIceMat,Quaternion.Euler(78,0,0));
            MeshPart("KryonBearBackPlateB",chestPlate,bearBodyRoot,new Vector3(0,.91f,-.44f),new Vector3(.84f,.40f,.78f),armorMat,Quaternion.Euler(82,0,0));
            MeshPart("KryonBearIceFinC",iceFin,bearBodyRoot,new Vector3(0,.92f,-.92f),new Vector3(.70f,.92f,.72f),bearIceMat,Quaternion.Euler(7,0,0));
            MeshPart("KryonBearIceFinL",iceFin,bearBodyRoot,new Vector3(-.48f,.82f,-.58f),new Vector3(.50f,.68f,.58f),bearIceMat,Quaternion.Euler(9,-12,-18));
            MeshPart("KryonBearIceFinR",iceFin,bearBodyRoot,new Vector3(.48f,.82f,-.58f),new Vector3(.50f,.68f,.58f),bearIceMat,Quaternion.Euler(9,12,18));

            Part("KryonBearChestCore",PrimitiveType.Sphere,bearBodyRoot,new Vector3(0,.43f,1.05f),new Vector3(.24f,.21f,.16f),energyMat,Quaternion.identity);
            Part("KryonBearTeamMark",PrimitiveType.Cube,bearBodyRoot,new Vector3(0,.98f,-.12f),new Vector3(.30f,.035f,.22f),teamMat,Quaternion.identity);
            Part("KryonBearSpineLight",PrimitiveType.Cube,bearBodyRoot,new Vector3(0,1.01f,-.56f),new Vector3(.075f,.035f,.56f),energyMat,Quaternion.Euler(5,0,0));

            // Lower, angular head with cheek armor, mechanical jaw and visible fangs.
            turretPivot=Pivot("DF_KryonBearHeadPivot",bearBodyRoot,new Vector3(0,.66f,.92f),Quaternion.Euler(-4,0,0));
            bearHeadPivot=turretPivot;
            Part("KryonBearHeadCore",PrimitiveType.Sphere,turretPivot,new Vector3(0,.00f,.12f),new Vector3(.86f,.61f,.74f),darkMat,Quaternion.identity);
            MeshPart("KryonBearForehead",facePlate,turretPivot,new Vector3(0,.14f,.54f),new Vector3(.92f,.90f,.88f),armorMat,Quaternion.Euler(-7,0,0));
            MeshPart("KryonBearCheekL",shoulderPlate,turretPivot,new Vector3(-.36f,-.06f,.48f),new Vector3(.55f,.56f,.70f),bearIceMat,Quaternion.Euler(-10,-18,-10));
            MeshPart("KryonBearCheekR",shoulderPlate,turretPivot,new Vector3(.36f,-.06f,.48f),new Vector3(.55f,.56f,.70f),bearIceMat,Quaternion.Euler(-10,18,10));
            Part("KryonBearSnout",PrimitiveType.Cube,turretPivot,new Vector3(0,-.15f,.66f),new Vector3(.54f,.28f,.50f),jointMat,Quaternion.Euler(3,0,0));
            MeshPart("KryonBearJaw",jawPlate,turretPivot,new Vector3(0,-.33f,.70f),new Vector3(.90f,.82f,.90f),darkMat,Quaternion.Euler(-6,0,0));
            Part("KryonBearNose",PrimitiveType.Sphere,turretPivot,new Vector3(0,-.10f,.94f),new Vector3(.25f,.15f,.16f),jointMat,Quaternion.identity);
            Part("KryonBearEarL",PrimitiveType.Sphere,turretPivot,new Vector3(-.37f,.36f,-.02f),new Vector3(.20f,.24f,.16f),jointMat,Quaternion.identity);
            Part("KryonBearEarR",PrimitiveType.Sphere,turretPivot,new Vector3(.37f,.36f,-.02f),new Vector3(.20f,.24f,.16f),jointMat,Quaternion.identity);
            Part("KryonBearEyeL",PrimitiveType.Sphere,turretPivot,new Vector3(-.22f,.07f,.64f),new Vector3(.105f,.072f,.060f),energyMat,Quaternion.identity);
            Part("KryonBearEyeR",PrimitiveType.Sphere,turretPivot,new Vector3(.22f,.07f,.64f),new Vector3(.105f,.072f,.060f),energyMat,Quaternion.identity);
            MeshPart("KryonBearFangL",clawBlade,turretPivot,new Vector3(-.18f,-.28f,.84f),new Vector3(.62f,.72f,.72f),bearIceMat,Quaternion.Euler(92,-5,0));
            MeshPart("KryonBearFangR",clawBlade,turretPivot,new Vector3(.18f,-.28f,.84f),new Vector3(.62f,.72f,.72f),bearIceMat,Quaternion.Euler(92,5,0));

            barrelPivot=Pivot("DF_KryonBearMuzzle",turretPivot,new Vector3(0,-.18f,1.02f),Quaternion.identity);
            bearBarrelRest=barrelPivot.localPosition;
            Part("KryonBearMuzzleCore",PrimitiveType.Cylinder,barrelPivot,Vector3.zero,new Vector3(.18f,.06f,.18f),energyMat,Quaternion.Euler(90,0,0));

            // Articulated legs: oversized front shoulders, visible joint cores and long metal claws.
            Vector3[] hips={new(-.66f,.30f,.58f),new(.66f,.30f,.58f),new(-.64f,.27f,-.78f),new(.64f,.27f,-.78f)};
            for(int i=0;i<4;i++)
            {
                bool front=i<2;
                float side=i%2==0?-1f:1f;
                Transform leg=Pivot($"DF_KryonBearLeg_{i}",bearBodyRoot,hips[i],Quaternion.Euler(front?7f:4f,0,side*(front?6f:3f)));
                bearLegRoots[i]=leg;bearLegBase[i]=leg.localRotation;

                Part("KryonBearUpperLegCore"+i,PrimitiveType.Capsule,leg,new Vector3(0,-.43f,.02f),
                    new Vector3(front ? .42f : .34f,front ? .69f : .61f,front ? .44f : .37f),darkMat,Quaternion.identity);
                MeshPart("KryonBearUpperLegArmor"+i,limbPlate,leg,new Vector3(0,-.38f,.30f),
                    new Vector3(front ? .95f : .78f,front ? .90f : .78f,.90f),armorMat,Quaternion.Euler(-5,0,side*3f));
                Part("KryonBearKnee"+i,PrimitiveType.Sphere,leg,new Vector3(0,-.84f,.09f),new Vector3(.31f,.27f,.31f),jointMat,Quaternion.identity);
                Part("KryonBearKneeGlow"+i,PrimitiveType.Cylinder,leg,new Vector3(side*.19f,-.84f,.09f),
                    new Vector3(.15f,.055f,.15f),energyMat,Quaternion.Euler(0,0,90));
                Part("KryonBearLowerLeg"+i,PrimitiveType.Capsule,leg,new Vector3(0,-1.10f,.15f),new Vector3(.30f,.48f,.32f),jointMat,Quaternion.Euler(-7f,0,side*2f));
                MeshPart("KryonBearShinArmor"+i,limbPlate,leg,new Vector3(0,-1.08f,.37f),
                    new Vector3(.70f,.64f,.74f),bearIceMat,Quaternion.Euler(-8,0,side*2f));
                Part("KryonBearPaw"+i,PrimitiveType.Sphere,leg,new Vector3(0,-1.36f,.35f),new Vector3(.62f,.22f,.78f),darkMat,Quaternion.identity);
                MeshPart("KryonBearPawCap"+i,shoulderPlate,leg,new Vector3(0,-1.34f,.58f),new Vector3(.68f,.44f,.72f),armorMat,Quaternion.Euler(-18,0,0));
                for(int claw=-1;claw<=1;claw++)
                    MeshPart("KryonBearClaw"+i+"_"+claw,clawBlade,leg,new Vector3(claw*.18f,-1.43f,.68f),
                        new Vector3(.78f,claw==0 ? .92f : .80f,claw==0 ? 1.08f : .94f),bearIceMat,Quaternion.Euler(-8,claw*5f,0));
            }
        
            // Broken ice ridges continue the back silhouette and catch the map lighting.
            MeshPart("KryonBearDorsalShardL",iceFin,bearBodyRoot,new Vector3(-.30f,.98f,-.96f),new Vector3(.44f,.70f,.48f),bearIceMat,Quaternion.Euler(2,-12,-14));
            MeshPart("KryonBearDorsalShardR",iceFin,bearBodyRoot,new Vector3(.30f,1.00f,-.94f),new Vector3(.40f,.62f,.44f),bearIceMat,Quaternion.Euler(-4,13,13));
            Part("KryonBearEyeGuardL",PrimitiveType.Cube,bearHeadPivot,new Vector3(-.23f,.17f,.64f),new Vector3(.25f,.045f,.065f),jointMat,Quaternion.Euler(0,0,-10));
            Part("KryonBearEyeGuardR",PrimitiveType.Cube,bearHeadPivot,new Vector3(.23f,.17f,.64f),new Vector3(.25f,.045f,.065f),jointMat,Quaternion.Euler(0,0,10));
        }

        private void AnimateKryonBear()
        {
            if(bearBodyRoot==null)return;
            bearAttackKick=Mathf.MoveTowards(bearAttackKick,0f,Time.deltaTime*6.5f);
            float idle=Mathf.Sin(Time.time*1.55f);
            float windup=shotPending?Mathf.Clamp01(1f-(shotAt-Time.time)/ShotWindup):0f;
            float stride=Mathf.Sin(bearGaitPhase)*bearMoveBlend;
            float lift=(1f-Mathf.Cos(bearGaitPhase*2f))*.035f*bearMoveBlend;
            bearBodyRoot.localPosition=bearBodyRest+Vector3.up*(lift+idle*.006f);
            bearBodyRoot.localRotation=Quaternion.Euler(-1.5f+Mathf.Cos(bearGaitPhase*2f)*.65f*bearMoveBlend+windup*4f-bearAttackKick*10f,0,stride*.85f);

            for(int i=0;i<bearLegRoots.Length;i++)
            {
                Transform leg=bearLegRoots[i];if(leg==null)continue;
                float phase=(i==0||i==3?1f:-1f)*stride;
                float attackLift=i<2?windup*8f-bearAttackKick*13f:windup*2f-bearAttackKick*4f;
                float side=i%2==0?-1f:1f;
                leg.localRotation=bearLegBase[i]*Quaternion.Euler(phase*34f+attackLift,0,side*Mathf.Abs(phase)*3.5f);
            }

            if(bearHeadPivot!=null)
                bearHeadPivot.localRotation=Quaternion.Euler(idle*1.2f+windup*5f-bearAttackKick*12f,-stride*2.2f,0);
            if(energyMat!=null&&energyMat.HasProperty("_EmissionColor"))
            {
                energyMat.SetColor("_EmissionColor",bearEnergyBase*(2.8f+idle*.12f+windup*2.0f));
            }
        }

        private void BuildAlienVisuals()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return;
            Color teamEnergy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;

            // Reference direction: glossy obsidian bio-mechanical predator with long limbs,
            // amber eyes and a crown of threatening back tendrils. Green remains only as a
            // subtle map-biome undertone instead of making the whole creature look toy-like.
            xenoEnergyBase=new Color(1f,.54f,.07f,1f);
            xenoArmorBase=new Color(.050f,.072f,.060f,1f);
            Color hide=new(.017f,.027f,.024f,1f),joint=new(.025f,.040f,.035f,1f);
            armorMat=MakeMaterial(shader,xenoArmorBase,.28f,.58f,Color.black);
            darkMat=MakeMaterial(shader,hide,.12f,.36f,Color.black);
            jointMat=MakeMaterial(shader,joint,.22f,.42f,Color.black);
            energyMat=MakeMaterial(shader,xenoEnergyBase,.04f,.72f,xenoEnergyBase*3.2f);
            teamMat=MakeMaterial(shader,Color.Lerp(teamEnergy,new Color(.18f,.75f,.20f),.45f),.10f,.48f,teamEnergy*1.20f);

            Mesh chestCenter=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoChestCenter",new[]{
                new Vector2(-.42f,-.68f),new Vector2(.42f,-.68f),new Vector2(.60f,.02f),
                new Vector2(.36f,.72f),new Vector2(0,.92f),new Vector2(-.36f,.72f),new Vector2(-.60f,.02f)},.15f));
            Mesh chestSide=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoChestSide",new[]{
                new Vector2(-.34f,-.58f),new Vector2(.28f,-.52f),new Vector2(.46f,.12f),
                new Vector2(.18f,.72f),new Vector2(-.32f,.58f),new Vector2(-.48f,.00f)},.13f));
            Mesh shoulderPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoShoulderPlate",new[]{
                new Vector2(-.52f,-.22f),new Vector2(.38f,-.30f),new Vector2(.66f,.14f),
                new Vector2(.18f,.62f),new Vector2(-.44f,.46f)},.16f));
            Mesh forearmPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoForearmPlate",new[]{
                new Vector2(-.27f,-.60f),new Vector2(.27f,-.54f),new Vector2(.36f,.34f),
                new Vector2(0,.66f),new Vector2(-.32f,.30f)},.11f));
            Mesh thighPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoThighPlate",new[]{
                new Vector2(-.34f,-.56f),new Vector2(.34f,-.56f),new Vector2(.42f,.20f),
                new Vector2(.12f,.66f),new Vector2(-.30f,.52f)},.12f));
            Mesh jawPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoJawPlate",new[]{
                new Vector2(-.42f,-.17f),new Vector2(.42f,-.17f),new Vector2(.30f,.22f),
                new Vector2(0,.36f),new Vector2(-.30f,.22f)},.17f));
            Mesh markPlate=TrackXenoMesh(XenoTitanMeshFactory.Plate("DF_XenoTeamMark",new[]{
                new Vector2(-.14f,-.11f),new Vector2(.14f,-.11f),new Vector2(.10f,.13f),
                new Vector2(0,.21f),new Vector2(-.10f,.13f)},.040f));
            Mesh headShell=TrackXenoMesh(XenoTitanMeshFactory.HeadShell("DF_XenoHeadShell"));
            Mesh clawBlade=TrackXenoMesh(XenoTitanMeshFactory.ClawBlade("DF_XenoClawBlade",1.04f,.14f,.10f,.24f));
            Mesh toeBlade=TrackXenoMesh(XenoTitanMeshFactory.ClawBlade("DF_XenoToeBlade",.62f,.12f,.09f,.12f));
            Mesh spineLong=TrackXenoMesh(XenoTitanMeshFactory.Spine("DF_XenoSpineLong",1.20f,.18f,.22f));
            Mesh spineShort=TrackXenoMesh(XenoTitanMeshFactory.Spine("DF_XenoSpineShort",.82f,.15f,.19f));

            xenoVisualRoot=Pivot("DF_XenoVisualRoot",transform,new Vector3(0,.02f,0),Quaternion.identity);
            xenoVisualRoot.localScale=new Vector3(.94f,1.08f,.94f);
            xenoVisualRootBasePosition=xenoVisualRoot.localPosition;

            // Narrow pelvis + tall ribcage give a much more predatory humanoid silhouette.
            xenoPelvisPivot=Pivot("DF_XenoPelvisPivot",xenoVisualRoot,new Vector3(0,.16f,-.10f),Quaternion.Euler(5,0,0));
            Part("XenoPelvisMass",PrimitiveType.Sphere,xenoPelvisPivot,Vector3.zero,new Vector3(1.04f,.48f,.70f),darkMat,Quaternion.identity);
            MeshPart("XenoHipCarapace",chestSide,xenoPelvisPivot,new Vector3(0,.08f,.46f),new Vector3(1.02f,.60f,.76f),armorMat,Quaternion.Euler(-6,0,0));

            xenoTorsoPivot=Pivot("DF_XenoTorsoPivot",xenoVisualRoot,new Vector3(0,1.34f,.02f),Quaternion.Euler(11,0,0));
            Part("XenoTorsoMass",PrimitiveType.Sphere,xenoTorsoPivot,new Vector3(0,-.05f,-.08f),new Vector3(1.36f,1.12f,.78f),darkMat,Quaternion.identity);
            MeshPart("XenoChestCenter",chestCenter,xenoTorsoPivot,new Vector3(0,.04f,.57f),new Vector3(.94f,1.06f,.92f),armorMat,Quaternion.Euler(-4,0,0));
            MeshPart("XenoChestLeft",chestSide,xenoTorsoPivot,new Vector3(-.61f,.02f,.43f),new Vector3(.88f,1.02f,.92f),armorMat,Quaternion.Euler(-3,-15,-7));
            MeshPart("XenoChestRight",chestSide,xenoTorsoPivot,new Vector3(.61f,-.01f,.43f),new Vector3(.94f,.98f,.92f),armorMat,Quaternion.Euler(-2,15,7));
            MeshPart("XenoRibL",forearmPlate,xenoTorsoPivot,new Vector3(-.50f,-.38f,.40f),new Vector3(.62f,.74f,.70f),jointMat,Quaternion.Euler(-8,-14,-18));
            MeshPart("XenoRibR",forearmPlate,xenoTorsoPivot,new Vector3(.50f,-.38f,.40f),new Vector3(.62f,.74f,.70f),jointMat,Quaternion.Euler(-8,14,18));

            Transform core=Part("XenoChestCore",PrimitiveType.Sphere,xenoTorsoPivot,new Vector3(0,-.10f,.68f),new Vector3(.17f,.16f,.12f),energyMat,Quaternion.identity);
            xenoCoreRenderer=core.GetComponent<Renderer>();xenoCoreBaseScale=core.localScale;

            Mesh vein1=TrackXenoMesh(ProductionMeshFactory.Tube(new Vector3(-.38f,.26f,.61f),new Vector3(-.10f,.03f,.69f),.026f,6));
            Mesh vein2=TrackXenoMesh(ProductionMeshFactory.Tube(new Vector3(.38f,.23f,.60f),new Vector3(.10f,.02f,.69f),.026f,6));
            Mesh vein3=TrackXenoMesh(ProductionMeshFactory.Tube(new Vector3(-.10f,-.28f,.65f),new Vector3(.10f,-.39f,.58f),.020f,6));
            MeshPart("XenoVeinL",vein1,xenoTorsoPivot,Vector3.zero,Vector3.one,energyMat,Quaternion.identity);
            MeshPart("XenoVeinR",vein2,xenoTorsoPivot,Vector3.zero,Vector3.one,energyMat,Quaternion.identity);
            MeshPart("XenoVeinLower",vein3,xenoTorsoPivot,Vector3.zero,Vector3.one,energyMat,Quaternion.identity);

            BuildXenoArm(-1f,shoulderPlate,forearmPlate,clawBlade,markPlate);
            BuildXenoArm(1f,shoulderPlate,forearmPlate,clawBlade,markPlate);
            BuildXenoLeg(-1f,thighPlate,toeBlade);
            BuildXenoLeg(1f,thighPlate,toeBlade);

            // Elongated skull, heavy brow and hot amber eyes.
            xenoHeadPivot=Pivot("DF_XenoHeadPivot",xenoTorsoPivot,new Vector3(0,1.40f,.03f),Quaternion.Euler(-12,0,0));
            MeshPart("XenoHeadShell",headShell,xenoHeadPivot,new Vector3(0,0,.10f),new Vector3(.88f,.92f,1.18f),armorMat,Quaternion.identity);
            MeshPart("XenoCranialPlate",chestSide,xenoHeadPivot,new Vector3(0,.22f,-.50f),new Vector3(1.00f,.56f,1.48f),darkMat,Quaternion.Euler(9,0,180));
            MeshPart("XenoCranialShardL",shoulderPlate,xenoHeadPivot,new Vector3(-.23f,.28f,-.70f),new Vector3(.56f,.40f,.74f),armorMat,Quaternion.Euler(8,-9,-12));
            MeshPart("XenoCranialShardR",shoulderPlate,xenoHeadPivot,new Vector3(.22f,.23f,-.66f),new Vector3(.46f,.36f,.66f),armorMat,Quaternion.Euler(11,11,10));
            MeshPart("XenoJaw",jawPlate,xenoHeadPivot,new Vector3(0,-.37f,.76f),new Vector3(.92f,.92f,1f),jointMat,Quaternion.Euler(-10,0,0));
            Part("XenoEyeL",PrimitiveType.Sphere,xenoHeadPivot,new Vector3(-.24f,.015f,.88f),new Vector3(.13f,.078f,.055f),energyMat,Quaternion.identity);
            Part("XenoEyeR",PrimitiveType.Sphere,xenoHeadPivot,new Vector3(.24f,.015f,.88f),new Vector3(.13f,.078f,.055f),energyMat,Quaternion.identity);

            // Long, irregular appendages reproduce the reference's threatening crown silhouette.
            xenoBackRoot=Pivot("DF_XenoBackRoot",xenoTorsoPivot,new Vector3(0,.30f,-.43f),Quaternion.Euler(20,0,0));
            BuildXenoTendril("C",new Vector3(.02f,.18f,-.02f),new Vector3(8,4,-2),1.12f,spineLong,spineShort);
            BuildXenoTendril("L1",new Vector3(-.30f,.10f,0),new Vector3(18,-10,-23),1.02f,spineLong,spineShort);
            BuildXenoTendril("R1",new Vector3(.31f,.08f,.01f),new Vector3(15,10,21),.98f,spineLong,spineShort);
            BuildXenoTendril("L2",new Vector3(-.52f,-.05f,.06f),new Vector3(27,-14,-38),.86f,spineLong,spineShort);
            BuildXenoTendril("R2",new Vector3(.50f,-.08f,.07f),new Vector3(25,13,35),.82f,spineLong,spineShort);
            BuildXenoTendril("L3",new Vector3(-.65f,-.22f,.12f),new Vector3(34,-18,-52),.68f,spineShort,spineShort);
            BuildXenoTendril("R3",new Vector3(.63f,-.24f,.12f),new Vector3(33,18,50),.66f,spineShort,spineShort);
        
            // Raised rib ridges and paired amber nodes break up the broad front chest plate.
            MeshPart("XenoRibL",chestSide,xenoTorsoPivot,new Vector3(-.48f,.02f,.53f),new Vector3(.36f,.50f,.34f),jointMat,Quaternion.Euler(0,0,-18));
            MeshPart("XenoRibR",chestSide,xenoTorsoPivot,new Vector3(.48f,.02f,.53f),new Vector3(.36f,.50f,.34f),jointMat,Quaternion.Euler(0,0,18));
            Part("XenoChestNodeL",PrimitiveType.Sphere,xenoTorsoPivot,new Vector3(-.39f,.06f,.72f),new Vector3(.075f,.075f,.055f),energyMat,Quaternion.identity);
            Part("XenoChestNodeR",PrimitiveType.Sphere,xenoTorsoPivot,new Vector3(.39f,.06f,.72f),new Vector3(.075f,.075f,.055f),energyMat,Quaternion.identity);
        }

        private void BuildXenoTendril(string suffix,Vector3 rootPosition,Vector3 rotation,float scale,Mesh firstMesh,Mesh secondMesh)
        {
            Transform root=Pivot("DF_XenoTendrilRoot_"+suffix,xenoBackRoot,rootPosition,Quaternion.Euler(rotation));
            MeshPart("XenoTendril_"+suffix+"_A",firstMesh,root,Vector3.zero,new Vector3(scale*.78f,scale*1.35f,scale*.78f),armorMat,Quaternion.identity);
            Transform bend=Pivot("DF_XenoTendrilBend_"+suffix,root,new Vector3(0,1.28f*scale,0),Quaternion.Euler(18f,0,rotation.z*.22f));
            MeshPart("XenoTendril_"+suffix+"_B",secondMesh,bend,Vector3.zero,new Vector3(scale*.66f,scale*1.05f,scale*.66f),jointMat,Quaternion.identity);
        }

        private void BuildXenoArm(float side,Mesh shoulderPlate,Mesh forearmPlate,Mesh clawBlade,Mesh markPlate)
        {
            bool left=side<0f;
            Transform shoulder=Pivot(left?"DF_XenoShoulderPivotL":"DF_XenoShoulderPivotR",xenoTorsoPivot,new Vector3(side*1.08f,.30f,-.03f),Quaternion.Euler(-10,0,side*10f));
            Part(left?"XenoShoulderMassL":"XenoShoulderMassR",PrimitiveType.Sphere,shoulder,new Vector3(0,-.04f,0),new Vector3(.64f,.46f,.64f),darkMat,Quaternion.identity);
            MeshPart(left?"XenoShoulderArmorL":"XenoShoulderArmorR",shoulderPlate,shoulder,new Vector3(side*.10f,.12f,.36f),new Vector3(left?1.08f:1f,1.04f,.96f),armorMat,Quaternion.Euler(-3,side*10f,side*-10f));
            MeshPart(left?"XenoTeamMarkL":"XenoTeamMarkR",markPlate,shoulder,new Vector3(side*.05f,.13f,.49f),new Vector3(.62f,.62f,.62f),teamMat,Quaternion.Euler(-3,side*8f,0));

            Part(left?"XenoUpperArmL":"XenoUpperArmR",PrimitiveType.Capsule,shoulder,new Vector3(0,-.72f,.03f),new Vector3(.29f,.88f,.31f),darkMat,Quaternion.Euler(0,0,side*3f));
            Transform forearm=Pivot(left?"DF_XenoForearmPivotL":"DF_XenoForearmPivotR",shoulder,new Vector3(0,-1.36f,.10f),Quaternion.Euler(10,0,side*5f));
            Part(left?"XenoForearmMassL":"XenoForearmMassR",PrimitiveType.Capsule,forearm,new Vector3(0,-.44f,.02f),new Vector3(.36f,.76f,.38f),jointMat,Quaternion.identity);
            MeshPart(left?"XenoForearmArmorL":"XenoForearmArmorR",forearmPlate,forearm,new Vector3(0,-.40f,.31f),new Vector3(left ? .94f : 1.08f,1.10f,.94f),armorMat,Quaternion.Euler(-5,0,side*4f));

            Transform claw=Pivot(left?"DF_XenoClawPivotL":"DF_XenoClawPivotR",forearm,new Vector3(0,-.98f,.20f),Quaternion.Euler(-9,0,side*5f));
            Part(left?"XenoPalmL":"XenoPalmR",PrimitiveType.Sphere,claw,new Vector3(0,0,.08f),new Vector3(.50f,.29f,.62f),armorMat,Quaternion.identity);
            for(int i=-1;i<=1;i++)
            {
                float spread=i*.20f;
                Transform finger=MeshPart(left?"XenoClawBladeL":"XenoClawBladeR",clawBlade,claw,new Vector3(spread,-.04f,.38f),
                    new Vector3(.92f,i==0?1.18f:.96f,i==0?1.16f:.98f),jointMat,Quaternion.Euler(-10-i*3f,side*i*8f,side*i*4f));
                finger.localPosition+=Vector3.forward*(i==0?.10f:0f);
            }

            if(left){xenoLeftShoulderPivot=shoulder;xenoLeftForearmPivot=forearm;xenoLeftClaw=claw;}
            else
            {
                xenoRightShoulderPivot=shoulder;xenoRightForearmPivot=forearm;xenoRightClaw=claw;
                barrelPivot=Pivot("DF_XenoAttackSocket",claw,new Vector3(0,.02f,1.48f),Quaternion.identity);
            }
        }

        private void BuildXenoLeg(float side,Mesh thighPlate,Mesh toeBlade)
        {
            bool left=side<0f;
            Transform hip=Pivot(left?"DF_XenoHipPivotL":"DF_XenoHipPivotR",xenoPelvisPivot,new Vector3(side*.47f,-.22f,.01f),Quaternion.Euler(6,0,side*6f));
            Part(left?"XenoThighMassL":"XenoThighMassR",PrimitiveType.Capsule,hip,new Vector3(0,-.51f,-.02f),new Vector3(.43f,.84f,.46f),darkMat,Quaternion.identity);
            MeshPart(left?"XenoThighArmorL":"XenoThighArmorR",thighPlate,hip,new Vector3(0,-.45f,.36f),new Vector3(.88f,1.05f,.90f),armorMat,Quaternion.Euler(-4,0,side*3f));

            Transform shin=Pivot(left?"DF_XenoShinPivotL":"DF_XenoShinPivotR",hip,new Vector3(0,-1.16f,.10f),Quaternion.Euler(11,0,side*4f));
            Part(left?"XenoShinMassL":"XenoShinMassR",PrimitiveType.Capsule,shin,new Vector3(0,-.44f,.08f),new Vector3(.33f,.74f,.39f),jointMat,Quaternion.identity);
            MeshPart(left?"XenoShinArmorL":"XenoShinArmorR",thighPlate,shin,new Vector3(0,-.41f,.34f),new Vector3(.72f,.88f,.82f),armorMat,Quaternion.Euler(-9,0,side*3f));

            Transform foot=Pivot(left?"DF_XenoFootPivotL":"DF_XenoFootPivotR",shin,new Vector3(0,-.98f,.35f),Quaternion.Euler(0,side*-4f,0));
            Part(left?"XenoFootMassL":"XenoFootMassR",PrimitiveType.Sphere,foot,new Vector3(0,0,.18f),new Vector3(.60f,.24f,.82f),darkMat,Quaternion.identity);
            for(int i=-1;i<=1;i++)
                MeshPart(left?"XenoToeL":"XenoToeR",toeBlade,foot,new Vector3(i*.18f,-.05f,.41f),
                    new Vector3(.86f,.82f,i==0?1.14f:.96f),jointMat,Quaternion.Euler(-5,side*i*9f,0));

            if(left){xenoLeftHipPivot=hip;xenoLeftShinPivot=shin;xenoLeftFoot=foot;}
            else{xenoRightHipPivot=hip;xenoRightShinPivot=shin;xenoRightFoot=foot;}
        }

        private void AnimateXeno()
        {
            float t=Time.time;
            float idle=Mathf.Sin(t*1.55f);
            float stride=Mathf.Sin(xenoGaitPhase)*xenoMoveBlend;
            float strideAbs=Mathf.Abs(stride);
            float attackWindup=shotPending?Mathf.Clamp01(1f-(shotAt-Time.time)/ShotWindup):0f;
            xenoAttackKick=Mathf.MoveTowards(xenoAttackKick,0f,Time.deltaTime*5.5f);

            if(xenoVisualRoot!=null)
                xenoVisualRoot.localPosition=xenoVisualRootBasePosition+Vector3.up*(strideAbs*.045f*xenoMoveBlend);
            if(xenoTorsoPivot!=null)
                xenoTorsoPivot.localRotation=Quaternion.Euler(11f+idle*1.5f,-stride*4.0f,stride*1.8f);
            if(xenoPelvisPivot!=null)
                xenoPelvisPivot.localRotation=Quaternion.Euler(5f,-stride*4.1f,-stride*1.1f);
            if(xenoHeadPivot!=null)
                xenoHeadPivot.localRotation=Quaternion.Euler(-12f+idle*1.8f,idle*4.2f+stride*1.8f,0);
            if(xenoBackRoot!=null)
                xenoBackRoot.localRotation=Quaternion.Euler(20f+idle*2.0f,0,stride*1.1f);

            if(xenoLeftShoulderPivot!=null)xenoLeftShoulderPivot.localRotation=Quaternion.Euler(-8f+stride*16f,idle*.8f,-8f+stride*1.5f);
            if(xenoRightShoulderPivot!=null)xenoRightShoulderPivot.localRotation=Quaternion.Euler(-8f-stride*16f-attackWindup*25f+xenoAttackKick*31f,-idle*.8f,8f-stride*1.5f);
            if(xenoLeftForearmPivot!=null)xenoLeftForearmPivot.localRotation=Quaternion.Euler(8f-stride*7f,0,-4f);
            if(xenoRightForearmPivot!=null)xenoRightForearmPivot.localRotation=Quaternion.Euler(8f+stride*7f+attackWindup*11f-xenoAttackKick*13f,0,4f);
            if(xenoLeftClaw!=null)xenoLeftClaw.localRotation=Quaternion.Euler(-7f+idle*1.2f,0,-4f);
            if(xenoRightClaw!=null)xenoRightClaw.localRotation=Quaternion.Euler(-7f-attackWindup*8f+xenoAttackKick*9f,0,4f);

            if(xenoLeftHipPivot!=null)xenoLeftHipPivot.localRotation=Quaternion.Euler(5f-stride*34f,0,-5f);
            if(xenoRightHipPivot!=null)xenoRightHipPivot.localRotation=Quaternion.Euler(5f+stride*34f,0,5f);
            if(xenoLeftShinPivot!=null)xenoLeftShinPivot.localRotation=Quaternion.Euler(9f+Mathf.Max(0f,stride)*27f,0,-3f);
            if(xenoRightShinPivot!=null)xenoRightShinPivot.localRotation=Quaternion.Euler(9f+Mathf.Max(0f,-stride)*27f,0,3f);
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

        private void BuildModernMonsterDetails()
        {
            // Small rigid details inherit their actual armor mount's animation.
            // Existing shared materials and beveled box mesh keep the addition inexpensive.
            string[] names=kryonBear?new[]{"KryonBearChestArmor","KryonBearShoulderArmorL","KryonBearShoulderArmorR"}:
                xenoAlien?new[]{"XenoChestCenter","XenoChestLeft","XenoChestRight"}:
                marsSkeleton?new[]{"TitanPelvis","TitanSkull"}:
                noxaraSpider?new[]{"SpiderFace"}:new[]{"MoonGuardChestArmor","MoonGuardUpperChest"};
            Transform[] mounts=GetComponentsInChildren<Transform>(true);
            foreach(string name in names)
            {
                Transform mount=null;
                foreach(Transform candidate in mounts)
                    if(candidate.name=="DF_HotZoneTank_"+name){mount=candidate;break;}
                MeshFilter filter=mount!=null?mount.GetComponent<MeshFilter>():null;
                if(filter==null||filter.sharedMesh==null)continue;
                Bounds b=filter.sharedMesh.bounds;
                float width=b.size.x,height=b.size.y;
                for(int side=-1;side<=1;side+=2)
                {
                    float x=b.center.x+side*width*.23f,z=b.max.z+.012f;
                    Part("ModernEdge"+name+side,PrimitiveType.Cube,mount,
                        new Vector3(x,b.center.y,z),new Vector3(width*.025f,height*.48f,.018f),
                        jointMat,Quaternion.identity);
                    // Living Xeno uses three segmented luminous growths rather than metal vents.
                    for(int i=0;i<3;i++)
                        Part("ModernInset"+name+side+i,PrimitiveType.Cube,mount,
                            new Vector3(x,b.center.y+height*(.17f-i*.12f),z+.014f),
                            new Vector3(width*.10f,height*.027f,.014f),
                            xenoAlien||i==0?energyMat:darkMat,Quaternion.Euler(0,0,xenoAlien?side*22f:0));
                }
            }
        }

        private Material MakeMaterial(Shader shader,Color color,float metallic,float smoothness,Color emission)
        {
            Material m=new(shader){color=color};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",Mathf.Max(metallic,.08f));
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",Mathf.Clamp(smoothness+(emission.maxColorComponent<=0&&metallic>.5f?.09f:0f),.24f,.86f));
            if(emission.maxColorComponent>0&&m.HasProperty("_EmissionColor")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);}
            return m;
        }

        private Transform Part(string name,PrimitiveType type,Transform parent,Vector3 localPosition,Vector3 localScale,Material material,Quaternion rotation)
        {
            GameObject part=GameObject.CreatePrimitive(type);part.name="DF_HotZoneTank_"+name;part.transform.SetParent(parent,false);
            part.transform.localPosition=localPosition;part.transform.localScale=localScale;part.transform.localRotation=rotation;
            if(type==PrimitiveType.Cube)part.GetComponent<MeshFilter>().sharedMesh=ProductionMeshFactory.BeveledBox();
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

        private void TrackRootMovement(float deltaTime)
        {
            Vector3 position=transform.position;
            if(!movementSampleReady)
            {
                lastMovementSample=position;
                movementSampleReady=true;
                return;
            }

            Vector3 displacement=position-lastMovementSample;
            lastMovementSample=position;
            displacement.y=0f;
            float dt=Mathf.Max(deltaTime,.0001f);
            if(displacement.magnitude>Mathf.Max(2f,dt*40f))
            {
                // Relocation resets every gait and the spider's planted-foot cache. It must not
                // be interpreted as a sprint step or a reason to swing a leg across the arena.
                moonMoveBlend=skeletonMoveBlend=xenoMoveBlend=bearMoveBlend=spiderMoveBlend=0f;
                moonGaitPhase=skeletonGaitPhase=xenoGaitPhase=bearGaitPhase=spiderGaitClock=0f;
                spiderTurnLean=spiderVisualYaw=0f;
                skeletonAttackKick=spiderAttackKick=bearAttackKick=xenoAttackKick=0f;
                spiderPreviousWorldPosition=position;
                spiderPreviousForward=transform.forward;
                spiderFeetInitialized=false;
                return;
            }

            float speed=displacement.magnitude/dt;
            float target=Mathf.InverseLerp(.10f,MoveSpeed*.68f,speed);
            float rate=target>.01f?8f:5f;
            moonMoveBlend=Mathf.MoveTowards(moonMoveBlend,target,dt*rate);
            skeletonMoveBlend=Mathf.MoveTowards(skeletonMoveBlend,target,dt*rate);
            xenoMoveBlend=Mathf.MoveTowards(xenoMoveBlend,target,dt*rate);
            bearMoveBlend=Mathf.MoveTowards(bearMoveBlend,target,dt*rate);

            // One gait cycle spans two steps. Phase advances by travelled distance, so stops,
            // acceleration and collision blocking cannot make the legs run in place.
            if(displacement.sqrMagnitude>0f)
            {
                float distance=displacement.magnitude;
                moonGaitPhase=Mathf.Repeat(moonGaitPhase+distance/1.85f*Mathf.PI*2f,Mathf.PI*2f);
                skeletonGaitPhase=Mathf.Repeat(skeletonGaitPhase+distance/2.35f*Mathf.PI*2f,Mathf.PI*2f);
                xenoGaitPhase=Mathf.Repeat(xenoGaitPhase+distance/1.72f*Mathf.PI*2f,Mathf.PI*2f);
                bearGaitPhase=Mathf.Repeat(bearGaitPhase+distance/2.45f*Mathf.PI*2f,Mathf.PI*2f);
            }
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


