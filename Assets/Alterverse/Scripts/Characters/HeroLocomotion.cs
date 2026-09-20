using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Characters
{
    /// <summary>Shared displacement and facing owner. Presentation never sets gameplay speed.</summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Health))]
    public sealed class HeroLocomotion : MonoBehaviour
    {
        private Health health;
        private CharacterController controller;
        private HeroCombatStatus status;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private Vector3 moveFacing, actionFacing, dashDirection;
        private float actionUntil, dashRemaining, dashSpeed;
        private int dashFrame = -1;
        private bool focused = true;
        public Vector3 DashDestination=>transform.position+dashDirection*dashRemaining;
        public bool IsDashing => dashRemaining > .001f;
        public bool OwnsMovementFrame => IsDashing || dashFrame == Time.frameCount;
        public bool CanMove => isActiveAndEnabled && focused && health != null && !health.IsDead && !status.Stunned && Time.timeScale > 0;
        public float SpeedMultiplier => status.MoveMultiplier;
        public Vector3 Velocity { get; private set; }
        public Vector3 LastCollisionNormal { get; private set; }
        public Collider LastCollisionCollider { get; private set; }
        private void Awake()
        {
            health = GetComponent<Health>(); controller = GetComponent<CharacterController>();
            status = GetComponent<HeroCombatStatus>() ?? gameObject.AddComponent<HeroCombatStatus>();
            health.Died += ResetMotion;
            if(controller!=null) { controller.minMoveDistance=0; controller.stepOffset=0; }
        }
        public void ResetMotion()
        {
            dashRemaining=0; dashFrame=-1; Velocity=Vector3.zero;
            moveFacing=actionFacing=Vector3.zero; actionUntil=0; LastCollisionNormal=Vector3.zero; LastCollisionCollider=null;
        }
        private void OnDisable() => ResetMotion();
        private void OnDestroy() { if(health!=null)health.Died-=ResetMotion; }
        private void OnApplicationFocus(bool value) { focused=value; if(!value)ResetMotion(); }
        private void OnApplicationPause(bool value) { focused=!value; if(value)ResetMotion(); }
        public void FaceMovement(Vector3 direction) { direction.y=0; moveFacing=direction; }
        public void FaceAction(Vector3 direction, float seconds=.16f)
        {
            direction.y=0; if(direction.sqrMagnitude<.0001f)return;
            actionFacing=direction.normalized; actionUntil=Time.time+seconds;
        }
        private void LateUpdate()
        {
            if(!CanMove)return;
            // Movement wins while steering; an attack may face its target only at rest.
            // Thus ranged attacks never turn a running hero backwards or lock locomotion.
            Vector3 facing=IsDashing?dashDirection:moveFacing.sqrMagnitude>.0001f?moveFacing:Time.time<actionUntil?actionFacing:Vector3.zero;
            if(facing.sqrMagnitude>.0001f)
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),1080f*Time.deltaTime);
            moveFacing=Vector3.zero;
        }
        public bool BeginDash(Vector3 end)
        {
            if(!CanMove || OwnsMovementFrame)return false;
            Vector3 delta=end-transform.position;delta.y=0;
            if(delta.magnitude<.1f)return false;
            dashDirection=delta.normalized;
            Sweep(transform.position,dashDirection,delta.magnitude,out float clearDistance,out _,out _);
            if(clearDistance<.1f)return false;
            dashRemaining=clearDistance;
            // Fixed readable traversal time; existing ability distance and cooldown remain intact.
            dashSpeed=delta.magnitude/.16f; dashFrame=Time.frameCount;
            return true;
        }
        private void Update()
        {
            if(!CanMove){ResetMotion();return;}
            if(!IsDashing)return;
            dashFrame=Time.frameCount;
            float distance=Mathf.Min(dashRemaining,dashSpeed*Time.deltaTime);
            Vector3 actual=Move(dashDirection*distance,40,33,false);
            dashRemaining-=distance;
            if(Vector3.Dot(actual,dashDirection)<distance-.015f)dashRemaining=0;
        }
        private void Sweep(Vector3 center,Vector3 direction,float distance,out float allowed,out Vector3 normal,out Collider blocker)
        {
            allowed=distance;normal=Vector3.zero;blocker=null;
            int count=Physics.CapsuleCastNonAlloc(center+Vector3.up*.45f,center-Vector3.up*.45f,.48f,direction,hits,distance+.025f,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length){allowed=0;return;}
            for(int i=0;i<count;i++)
            {
                Collider other=hits[i].collider;
                if(other==null||other.transform.IsChildOf(transform)||hits[i].normal.y>.65f)continue;
                // Crowd spacing is owned by bot separation. Static structures remain solid.
                if(controller==null&&(other.GetComponentInParent<HeroIdentity>()!=null||other.GetComponentInParent<LaneMinion>()!=null))continue;
                if(Vector3.Dot(direction,hits[i].normal)>=-.001f)continue;
                float travel=Mathf.Max(0,hits[i].distance-.025f);
                if(travel<allowed){allowed=travel;normal=hits[i].normal;blocker=other;}
            }
        }

        /// <summary>Non-allocating local steering probe used by bots after a real collision.</summary>
        public bool TryFindClearDirection(Vector3 desired,float probeDistance,int sidePreference,out Vector3 clearDirection)
        {
            clearDirection=Vector3.zero;desired.y=0;
            if(desired.sqrMagnitude<.0001f)return false;
            desired.Normalize();probeDistance=Mathf.Max(.35f,probeDistance);
            int preferred=sidePreference>=0?1:-1;
            if(DirectionClear(desired,probeDistance)){clearDirection=desired;return true;}
            Vector3 right=Vector3.Cross(Vector3.up,desired).normalized;
            for(int pass=0;pass<5;pass++)
            {
                float angle=pass==0?42f:pass==1?68f:pass==2?92f:pass==3?135f:180f;
                float radians=angle*Mathf.Deg2Rad,cos=Mathf.Cos(radians),sin=Mathf.Sin(radians);
                Vector3 first=(desired*cos+right*(preferred*sin)).normalized;
                if(DirectionClear(first,probeDistance)){clearDirection=first;return true;}
                Vector3 second=(desired*cos-right*(preferred*sin)).normalized;
                if(DirectionClear(second,probeDistance)){clearDirection=second;return true;}
            }
            return false;
        }

        // Low-frequency recovery only. Bounds cover walls, towers and moving guardians alike.
        // The collider remains solid; candidates must pass the same swept-capsule test as movement.
        public bool TryFindObstacleWaypoint(Collider obstacle,Vector3 goal,int side,out Vector3 waypoint,float clearance=.8f)
        {
            waypoint=transform.position;
            Bounds bounds=obstacle.bounds;bounds.Expand(new Vector3(clearance*2f,0,clearance*2f));
            float best=float.MaxValue;bool found=false;
            Vector3 desired=goal-transform.position;desired.y=0;
            for(int i=0;i<4;i++)
            {
                Vector3 point=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,transform.position.y,
                    (i&2)==0?bounds.min.z:bounds.max.z);
                if(Mathf.Abs(point.x)>39.5f||Mathf.Abs(point.z)>32.5f)continue;
                Vector3 delta=point-transform.position;float distance=delta.magnitude;
                if(distance<.3f||distance>8f||!DirectionClear(delta/distance,distance))continue;
                float bias=Vector3.Dot(Vector3.Cross(Vector3.up,desired.normalized),delta.normalized)*side;
                float score=distance+Vector3.Distance(point,goal)-bias*.6f;
                if(score>=best)continue;
                best=score;waypoint=point;found=true;
            }
            return found;
        }

        private bool DirectionClear(Vector3 direction,float distance)
        {
            Sweep(transform.position,direction,distance,out float allowed,out _,out _);
            return allowed>=distance-.03f;
        }
        public Vector3 Move(Vector3 displacement,float halfWidth,float halfHeight,bool slide=true)
        {
            Vector3 before=transform.position; LastCollisionNormal=Vector3.zero; LastCollisionCollider=null;
            if(!CanMove){Velocity=Vector3.zero;return Vector3.zero;}
            displacement.y=0;
            Vector3 goal=before+displacement;
            goal.x=Mathf.Clamp(goal.x,-halfWidth,halfWidth);goal.z=Mathf.Clamp(goal.z,-halfHeight,halfHeight);
            Vector3 remaining=goal-before;
            // Sweeps also protect transform-driven bots. Two contact planes permit wall sliding.
            for(int pass=0;pass<(slide?2:1)&&remaining.sqrMagnitude>.000001f;pass++)
            {
                float distance=remaining.magnitude;Vector3 direction=remaining/distance;
                Sweep(transform.position,direction,distance,out float allowed,out Vector3 normal,out Collider blocker);
                Vector3 bounded=transform.position+direction*allowed;
                bounded.x=Mathf.Clamp(bounded.x,-halfWidth,halfWidth);bounded.z=Mathf.Clamp(bounded.z,-halfHeight,halfHeight);
                Vector3 step=bounded-transform.position;
                if(controller!=null){if(!controller.enabled)break;controller.Move(step);}
                else transform.position+=step;
                if(normal.sqrMagnitude<.01f)break;
                LastCollisionNormal=normal;LastCollisionCollider=blocker;
                remaining=Vector3.ProjectOnPlane(remaining-step,normal);remaining.y=0;
            }
            if(controller!=null&&controller.enabled)controller.Move(Vector3.down*(2f*Time.deltaTime));
            Vector3 moved=transform.position-before;moved.y=0;
            Velocity=Time.deltaTime>0?moved/Time.deltaTime:Vector3.zero;
            return moved;
        }
    }
}
