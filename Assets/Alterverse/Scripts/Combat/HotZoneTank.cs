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
        private const float AcquireRadius=17f;
        private const float ChaseLeash=14f;
        private const float ScanInterval=.18f;
        private const float ArenaHalfWidth=39f;
        private const float ArenaHalfHeight=32f;

        private int team;
        private Health health,target;
        private HeroLocomotion locomotion;
        private float nextAttack,nextScan,blockedTime,detourUntil;
        private Vector3 patrolTarget,home,detourDirection;
        private int patrolIndex;

        private static readonly Vector3[] ZeroPatrol=
        {
            new(-4,0,0),new(0,0,12),new(8,0,4),new(3,0,-12),new(14,0,-3)
        };
        private static readonly Vector3[] LumenPatrol=
        {
            new(4,0,0),new(0,0,-12),new(-8,0,-4),new(-3,0,12),new(-14,0,3)
        };

        public static void Spawn(int winningTeam)
        {
            int team=Mathf.Clamp(winningTeam,0,1);
            Vector3 spawn=new(team==0?-4f:4f,.95f,0f);
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name=team==0?"DF_ZeroHotZoneTank":"DF_LumenHotZoneTank";
            go.transform.position=spawn;
            go.transform.localScale=new Vector3(1.15f,.9f,1.35f);
            GameObject world=GameObject.Find("DF_MatchWorld");if(world!=null)go.transform.SetParent(world.transform,true);

            Renderer baseRenderer=go.GetComponent<Renderer>();if(baseRenderer!=null)baseRenderer.enabled=false;
            Health hp=go.AddComponent<Health>();hp.Configure(MaxHp,true);
            TeamMember member=go.AddComponent<TeamMember>();member.Initialize(team);
            go.AddComponent<HotZoneTank>().Initialize(team,spawn);
        }

        private void Initialize(int assignedTeam,Vector3 spawn)
        {
            team=assignedTeam;home=spawn;health=GetComponent<Health>();
            locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();
            BuildVisuals();
            SetNextPatrol();
        }

        private void Update()
        {
            if(Time.timeScale<=0f||health==null||health.IsDead)return;
            if(Time.time>=nextScan)
            {
                nextScan=Time.time+ScanInterval;
                ValidateOrAcquireTarget();
            }

            if(target!=null)
            {
                Vector3 delta=target.transform.position-transform.position;delta.y=0;
                if(delta.sqrMagnitude>ChaseLeash*ChaseLeash){target=null;SetNextPatrol();return;}
                if(delta.sqrMagnitude<=AttackRange*AttackRange)
                {
                    locomotion.FaceAction(delta,AttackCooldown);
                    if(Time.time>=nextAttack)Fire();
                    return;
                }
                MoveToward(target.transform.position,.9f);
                return;
            }

            if((patrolTarget-transform.position).sqrMagnitude<1.2f*1.2f)SetNextPatrol();
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

        private void Fire()
        {
            if(target==null||target.IsDead)return;
            nextAttack=Time.time+AttackCooldown;
            Vector3 muzzle=transform.position+transform.forward*1.05f+Vector3.up*.78f;
            Vector3 hit=target.transform.position+Vector3.up*.55f;
            PrototypeVfx.ProjectileLine(muzzle,hit,team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy,.13f);
            target.TakeDamage(Damage,gameObject);
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
            if(moved.sqrMagnitude>.00001f)locomotion.FaceMovement(moved);

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
            Color energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color dark=team==0?StylizedArtDirection.ZeroDark:StylizedArtDirection.LumenDark;
            Color armor=Color.Lerp(dark,new Color(.30f,.33f,.38f),.48f);

            Part("Hull",PrimitiveType.Cube,new Vector3(0,.10f,0),new Vector3(1.85f,.62f,2.35f),armor,Quaternion.identity);
            Part("LowerHull",PrimitiveType.Cube,new Vector3(0,-.28f,.04f),new Vector3(1.65f,.32f,2.10f),dark,Quaternion.identity);
            Part("TurretBase",PrimitiveType.Cylinder,new Vector3(0,.60f,-.05f),new Vector3(.72f,.18f,.72f),dark,Quaternion.identity);
            Part("Turret",PrimitiveType.Cube,new Vector3(0,.88f,.10f),new Vector3(1.18f,.42f,1.02f),armor,Quaternion.identity);
            Part("Cannon",PrimitiveType.Cube,new Vector3(0,.87f,1.00f),new Vector3(.24f,.20f,1.38f),dark,Quaternion.identity);
            Part("Muzzle",PrimitiveType.Cube,new Vector3(0,.87f,1.72f),new Vector3(.34f,.30f,.20f),energy,Quaternion.identity);

            for(int side=-1;side<=1;side+=2)
            {
                Part(side<0?"TrackL":"TrackR",PrimitiveType.Cube,new Vector3(side*1.03f,-.30f,0),new Vector3(.42f,.46f,2.48f),dark,Quaternion.identity);
                for(int z=-1;z<=1;z++)
                    Part("Wheel",PrimitiveType.Cylinder,new Vector3(side*1.24f,-.30f,z*.72f),new Vector3(.28f,.16f,.28f),armor,Quaternion.Euler(0,0,90));
            }

            Part("Core",PrimitiveType.Sphere,new Vector3(0,.86f,-.45f),new Vector3(.34f,.34f,.34f),energy,Quaternion.identity);
            Part("Sensor",PrimitiveType.Cube,new Vector3(0,1.18f,.18f),new Vector3(.62f,.18f,.22f),energy,Quaternion.identity);
            if(team==0)Part("FractureFin",PrimitiveType.Cube,new Vector3(-.62f,.78f,-.62f),new Vector3(.18f,.62f,.42f),energy,Quaternion.Euler(0,0,-16));
            else
            {
                Part("LumenFinL",PrimitiveType.Cube,new Vector3(-.54f,.76f,-.60f),new Vector3(.12f,.48f,.36f),energy,Quaternion.identity);
                Part("LumenFinR",PrimitiveType.Cube,new Vector3(.54f,.76f,-.60f),new Vector3(.12f,.48f,.36f),energy,Quaternion.identity);
            }
        }

        private void Part(string name,PrimitiveType type,Vector3 localPosition,Vector3 localScale,Color color,Quaternion rotation)
        {
            GameObject part=GameObject.CreatePrimitive(type);part.name="DF_HotZoneTank_"+name;part.transform.SetParent(transform,false);
            part.transform.localPosition=localPosition;part.transform.localScale=localScale;part.transform.localRotation=rotation;
            Collider c=part.GetComponent<Collider>();if(c!=null)c.enabled=false;
            Renderer r=part.GetComponent<Renderer>();if(r!=null){r.material.color=color;r.shadowCastingMode=ShadowCastingMode.On;}
        }
    }
}
