using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    /// <summary>One-shot roaming reward from the minute-five Hot Zone.</summary>
    [RequireComponent(typeof(Health),typeof(TeamMember))]
    public sealed class HotZoneTank:MonoBehaviour
    {
        private const float MaxHp=850f;
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
        private bool marsSkeleton;
        private Health health,target;
        private HeroLocomotion locomotion;
        private float nextAttack,nextScan,blockedTime,detourUntil,shotAt;
        private Vector3 patrolTarget,home,detourDirection;
        private int patrolIndex;
        private bool shotPending;
        private Health shotTarget;
        private Transform turretPivot,barrelPivot,leftTrack,rightTrack;
        private static readonly List<HotZoneTank> active=new();
        public static IReadOnlyList<HotZoneTank> Active=>active;
        public int Team=>team;
        public Health TankHealth=>health;
        public bool IsMarsSkeleton=>marsSkeleton;
        private Material armorMat,darkMat,energyMat,jointMat;

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
            for(int i=0;i<active.Count;i++)
            {
                HotZoneTank existing=active[i];
                if(existing!=null&&existing.health!=null&&!existing.health.IsDead)return false;
            }
            Vector3 spawn=new(team==0?-4f:4f,.95f,0f);
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bool skeleton=ArenaVariantRuntime.IsMars;
            go.name=skeleton?(team==0?"DF_ZeroMarsTitan":"DF_LumenMarsTitan"):(team==0?"DF_ZeroHotZoneTank":"DF_LumenHotZoneTank");
            go.transform.position=spawn;
            go.transform.localScale=new Vector3(1.15f,.9f,1.35f);
            GameObject world=GameObject.Find("DF_MatchWorld");if(world!=null)go.transform.SetParent(world.transform,true);

            Renderer baseRenderer=go.GetComponent<Renderer>();if(baseRenderer!=null)baseRenderer.enabled=false;
            Health hp=go.AddComponent<Health>();hp.Configure(MaxHp,true);
            TeamMember member=go.AddComponent<TeamMember>();member.Initialize(team);
            go.AddComponent<HotZoneTank>().Initialize(team,spawn,skeleton);
            return true;
        }

        private void Initialize(int assignedTeam,Vector3 spawn,bool skeleton)
        {
            team=assignedTeam;marsSkeleton=skeleton;home=spawn;health=GetComponent<Health>();
            locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();
            if(marsSkeleton)BuildSkeletonVisuals();else BuildVisuals();
            SetNextPatrol();
        }

        private void Update()
        {
            if(Time.timeScale<=0f||health==null||health.IsDead)return;
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
            Color energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Vector3 muzzle=marsSkeleton&&barrelPivot!=null?barrelPivot.position:(barrelPivot!=null?barrelPivot.TransformPoint(new Vector3(0,0,1.70f)):transform.position+transform.forward*1.7f+Vector3.up*.95f);
            PrototypeVfx.Ring(muzzle,energy,.34f,ShotWindup);
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

            Vector3 muzzle=marsSkeleton&&barrelPivot!=null?barrelPivot.position:(barrelPivot!=null?barrelPivot.TransformPoint(new Vector3(0,0,1.70f)):transform.position+transform.forward*1.7f+Vector3.up*.95f);
            Vector3 hit=victim.transform.position+Vector3.up*.55f;
            Color energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            PrototypeVfx.ProjectileLine(muzzle,hit,energy,.13f);
            PrototypeVfx.Burst(muzzle,energy,.28f,4);
            if(barrelPivot!=null&&!marsSkeleton)barrelPivot.localPosition=new Vector3(0,.08f,.48f);
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

            bool blocked=requested.sqrMagnitude>.00001f&&moved.sqrMagnitude<requested.sqrMagnitude*.08f;
            blockedTime=blocked?blockedTime+Time.deltaTime:0f;
            if(blockedTime>.24f&&Time.time>=detourUntil)
            {
                Vector3 tangent=Vector3.Cross(Vector3.up,delta.normalized);
                if((patrolIndex&1)==0)tangent=-tangent;
                detourDirection=tangent.normalized;
                detourUntil=Time.time+.70f;
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
            Part("TitanArmL",PrimitiveType.Capsule,transform,new Vector3(-1.34f,.90f,.08f),new Vector3(.25f,.92f,.25f),armorMat,Quaternion.Euler(0,0,-10));
            Part("TitanArmR",PrimitiveType.Capsule,transform,new Vector3(1.34f,.90f,.08f),new Vector3(.25f,.92f,.25f),armorMat,Quaternion.Euler(0,0,10));
            Part("TitanClawL",PrimitiveType.Sphere,transform,new Vector3(-1.42f,.02f,.12f),new Vector3(.48f,.34f,.62f),jointMat,Quaternion.identity);
            barrelPivot=Part("TitanClawR",PrimitiveType.Sphere,transform,new Vector3(1.42f,.02f,.12f),new Vector3(.48f,.34f,.62f),energyMat,Quaternion.identity);
            Part("TitanLegL",PrimitiveType.Capsule,transform,new Vector3(-.48f,-.72f,0),new Vector3(.32f,.95f,.32f),armorMat,Quaternion.Euler(0,0,-4));
            Part("TitanLegR",PrimitiveType.Capsule,transform,new Vector3(.48f,-.72f,0),new Vector3(.32f,.95f,.32f),armorMat,Quaternion.Euler(0,0,4));
            Transform skull=Part("TitanSkull",PrimitiveType.Sphere,transform,new Vector3(0,2.22f,.10f),new Vector3(.88f,.70f,.82f),armorMat,Quaternion.identity);
            Part("TitanJaw",PrimitiveType.Cube,skull,new Vector3(0,-.52f,.42f),new Vector3(.62f,.24f,.42f),jointMat,Quaternion.Euler(-8,0,0));
            Part("TitanEyeL",PrimitiveType.Sphere,skull,new Vector3(-.28f,.08f,.72f),new Vector3(.16f,.12f,.10f),energyMat,Quaternion.identity);
            Part("TitanEyeR",PrimitiveType.Sphere,skull,new Vector3(.28f,.08f,.72f),new Vector3(.16f,.12f,.10f),energyMat,Quaternion.identity);
            Part("TitanChestRift",PrimitiveType.Cube,transform,new Vector3(0,1.25f,.27f),new Vector3(.12f,.82f,.08f),energyMat,Quaternion.Euler(0,0,-18));
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
            Collider c=part.GetComponent<Collider>();if(c!=null)c.enabled=false;
            Renderer r=part.GetComponent<Renderer>();if(r!=null){r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
            return part.transform;
        }

        private void AimTurretAt(Vector3 worldTarget)
        {
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
                barrelPivot.localPosition=Vector3.Lerp(barrelPivot.localPosition,new Vector3(0,.08f,.62f),1f-Mathf.Exp(-18f*Time.deltaTime));
            }
        }

        private void AnimateTracks(float movement)
        {
            if(marsSkeleton){float stride=Mathf.Sin(Time.time*6f)*Mathf.Clamp01(movement*7f);transform.localPosition+=Vector3.up*(stride*.002f);return;}
            float bob=Mathf.Sin(Time.time*9f)*Mathf.Clamp01(movement*8f)*.008f;
            if(leftTrack!=null)leftTrack.localPosition=new Vector3(0,bob,0);
            if(rightTrack!=null)rightTrack.localPosition=new Vector3(0,-bob,0);
        }

        private void OnDestroy()
        {
            active.Remove(this);
            if(armorMat!=null)Destroy(armorMat);
            if(darkMat!=null)Destroy(darkMat);
            if(energyMat!=null)Destroy(energyMat);
            if(jointMat!=null)Destroy(jointMat);
        }
    }
}
