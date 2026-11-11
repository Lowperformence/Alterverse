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
        private Vector3 forcedEnd;
        private float forcedUntil;
        private Vector3 vortexCenter, remoteRunVelocity;
        private float vortexUntil, remoteRunUntil;
        private float vortexRadius;
        public bool IsVortexPulled => Time.time < vortexUntil;
        public Vector3 VortexCenter => vortexCenter;
        public float VortexRadius => vortexRadius;
        public Vector3 FrameStartPosition { get; private set; }
        public float RunningSpeed
        {
            get
            {
                var motor=GetComponent<HeroMotor>();
                if(motor!=null)return motor.MoveSpeed;
                var profile=GetComponent<HeroCombatProfile>();
                var minion=GetComponent<LaneMinion>();
                float speed=profile!=null?profile.MoveSpeed:minion!=null?minion.RunningSpeed:6.5f;
                return speed*(GetComponent<Alterverse.Match.PlanetBuffReceiver>()?.MoveSpeedMultiplier??1f)*SpeedMultiplier;
            }
        }
        // A short heartbeat makes interrupted casts, death and lost network snapshots expire safely.
        public void SetVortexPull(Vector3 center,float radius,float seconds)
        {
            vortexCenter=center;vortexRadius=Mathf.Max(0f,radius);
            vortexUntil=Time.time+Mathf.Max(0f,seconds);
        }
        public void SetRemoteRunVelocity(Vector3 velocity)
        {
            velocity.y=0;
            remoteRunVelocity=Vector3.ClampMagnitude(velocity,RunningSpeed);
            remoteRunUntil=Time.time+.2f;
        }
        public bool IsForcedMoving => Time.time < forcedUntil;
        public Vector3 DashDestination=>transform.position+dashDirection*dashRemaining;
        public bool IsDashing => dashRemaining > .001f;
        private uint movementRevision;
        private bool revisionMovementActive;
        public uint MovementRevision
        {
            get
            {
                bool active=IsDashing||IsForcedMoving;
                if(active!=revisionMovementActive)
                {
                    revisionMovementActive=active;
                    unchecked { movementRevision++;if(movementRevision==0)movementRevision=1; }
                }
                return movementRevision;
            }
        }
        private void RecordMovementStart(){_ = MovementRevision;}
        public void MarkNetworkRelocation()
        {
            _ = MovementRevision;
            unchecked { movementRevision++;if(movementRevision==0)movementRevision=1; }
        }
        public bool OwnsMovementFrame => IsForcedMoving || IsDashing || dashFrame == Time.frameCount;
        public bool CanMove => isActiveAndEnabled && focused && health != null && !health.IsDead && !status.Stunned && !status.Rooted && Time.timeScale > 0;
        public float SpeedMultiplier => status.MoveMultiplier;
        public Vector3 Velocity { get; private set; }
        public Vector3 LastCollisionNormal { get; private set; }
        public Collider LastCollisionCollider { get; private set; }
        private void Awake()
        {
            FrameStartPosition=transform.position;
            health = GetComponent<Health>(); controller = GetComponent<CharacterController>();
            status = GetComponent<HeroCombatStatus>() ?? gameObject.AddComponent<HeroCombatStatus>();
            health.Died += ResetMotion;
            if(controller!=null) { controller.minMoveDistance=0; controller.stepOffset=0; }
        }
        public void ResetMotion()
        {
            forcedUntil=vortexUntil=remoteRunUntil=0; remoteRunVelocity=Vector3.zero; dashRemaining=0; dashFrame=-1; Velocity=Vector3.zero;
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
            actionFacing=direction.normalized;
            actionUntil=Mathf.Max(actionUntil,Time.time+Mathf.Max(.02f,seconds));
        }
        private void LateUpdate()
        {
            if(IsVortexPulled && health!=null && !health.IsDead && Time.timeScale>0 && !OwnsMovementFrame)
            {
                var slot=GetComponent<Alterverse.Multiplayer.CoopHeroSlot>();
                bool host=Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay;
                bool localOwner=slot!=null&&slot.IsLocalHuman;
                if(host||localOwner)
                {
                    // Remote owners send steering while trapped; absolute owner positions must not
                    // undo the server's continuous pull. Local owners predict the same displacement.
                    if(host&&slot!=null&&slot.IsHuman&&!localOwner&&Time.time<remoteRunUntil)
                        Move(remoteRunVelocity*Time.deltaTime,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight);
                    Vector3 toward=vortexCenter-transform.position;toward.y=0;
                    Move(Vector3.ClampMagnitude(toward,RunningSpeed*Time.deltaTime),ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight,true,true);
                }
            }
            if(!CanMove)return;
            // Dashes stay authoritative. During a combat action the target/action facing wins
            // over steering, while displacement remains untouched for strafing/backpedaling.
            bool actionActive=Time.time<actionUntil&&actionFacing.sqrMagnitude>.0001f;
            Vector3 facing=IsDashing?dashDirection:actionActive?actionFacing:moveFacing.sqrMagnitude>.0001f?moveFacing:Vector3.zero;
            if(facing.sqrMagnitude>.0001f)
            {
                Quaternion targetRotation=Quaternion.LookRotation(facing);
                float maxTurnSpeed=IsDashing||actionActive?1080f:640f;
                Quaternion stepped=Quaternion.RotateTowards(transform.rotation,targetRotation,maxTurnSpeed*Time.deltaTime);
                transform.rotation=IsDashing?stepped:Quaternion.Slerp(transform.rotation,stepped,1f-Mathf.Exp(-18f*Time.deltaTime));
            }
            moveFacing=Vector3.zero;
        }
        public bool BeginDash(Vector3 end, float duration = .16f)
        {
            _ = MovementRevision;
            if(!CanMove || OwnsMovementFrame)return false;
            Vector3 delta=end-transform.position;delta.y=0;
            if(delta.magnitude<.1f)return false;
            dashDirection=delta.normalized;
            Sweep(transform.position,dashDirection,delta.magnitude,out float clearDistance,out _,out _);
            if(clearDistance<.1f)return false;
            dashRemaining=clearDistance;
            // Fixed readable traversal time; existing ability distance and cooldown remain intact.
            dashSpeed=delta.magnitude/Mathf.Clamp(duration,.08f,.65f); dashFrame=Time.frameCount;
            RecordMovementStart();
            return true;
        }
        // Only an already active dash may adjust its destination. Scratch uses
        // the original cast deadline and field, so steering never starts a new
        // dash or extends the leap through an obstacle.
        public bool RetargetDash(Vector3 end,float remainingSeconds)
        {
            if(!IsDashing||!CanMove||IsForcedMoving)return false;
            Vector3 delta=end-transform.position;delta.y=0;
            if(delta.sqrMagnitude<.000001f){dashRemaining=0;return true;}
            dashDirection=delta.normalized;
            Sweep(transform.position,dashDirection,delta.magnitude,out float clearDistance,out _,out _);
            dashRemaining=clearDistance;
            dashSpeed=delta.magnitude/Mathf.Max(Time.deltaTime,remainingSeconds);
            return true;
        }
        public bool BeginForcedMove(Vector3 destination, float duration)
        {
            _ = MovementRevision;
            if (!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay || health == null || health.IsDead ||
                !isActiveAndEnabled || Time.timeScale <= 0 || IsForcedMoving) return false;
            forcedEnd = GameplayCollision.ClampToRoundedArena(destination,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight);
            forcedEnd.y = transform.position.y;
            forcedUntil = Time.time + Mathf.Clamp(duration,.05f,.5f);
            dashRemaining = 0;
            RecordMovementStart();
            return true;
        }
        private void Update()
        {
            FrameStartPosition=transform.position;
            if (IsForcedMoving && health != null && !health.IsDead && Time.timeScale > 0)
            {
                Vector3 delta = forcedEnd - transform.position; delta.y=0;
                float remaining = Mathf.Max(Time.deltaTime,forcedUntil-Time.time);
                Vector3 step = delta * Mathf.Clamp01(Time.deltaTime/remaining);
                Vector3 forcedActual = Move(step,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight,false,true);
                if (forcedActual.sqrMagnitude < step.sqrMagnitude*.25f || delta.sqrMagnitude < .0025f) forcedUntil=0;
                return;
            }
            if(!CanMove){dashRemaining=0;Velocity=Vector3.zero;return;}
            if(!IsDashing)return;
            dashFrame=Time.frameCount;
            float distance=Mathf.Min(dashRemaining,dashSpeed*Time.deltaTime);
            Vector3 actual=Move(dashDirection*distance,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight,false);
            dashRemaining-=distance;
            if(Vector3.Dot(actual,dashDirection)<distance-.015f)dashRemaining=0;
        }
        private void Sweep(Vector3 center,Vector3 direction,float distance,out float allowed,out Vector3 normal,out Collider blocker)
        {
            allowed=distance;normal=Vector3.zero;blocker=null;

            // Match the sweep to the actual CharacterController instead of assuming hero dimensions.
            // Support soldiers are much shorter than heroes; the previous fixed .48 / +/- .45 capsule
            // extended into the ground for them and could make every horizontal move look blocked.
            Vector3 castTop;
            Vector3 castBottom;
            float castRadius;
            if(controller!=null)
            {
                Vector3 scale=transform.lossyScale;
                float horizontalScale=Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
                float verticalScale=Mathf.Abs(scale.y);
                float controllerRadius=Mathf.Max(.05f,controller.radius*horizontalScale);
                float controllerHeight=Mathf.Max(controllerRadius*2f,controller.height*verticalScale);
                float halfSegment=Mathf.Max(0f,controllerHeight*.5f-controllerRadius);
                Vector3 worldCenter=transform.TransformPoint(controller.center);
                castRadius=Mathf.Max(.04f,controllerRadius-Mathf.Max(.015f,controller.skinWidth));
                castTop=worldCenter+transform.up*halfSegment;
                castBottom=worldCenter-transform.up*halfSegment;
            }
            else
            {
                castRadius=.48f;
                castTop=center+Vector3.up*.45f;
                castBottom=center-Vector3.up*.45f;
            }

            int count=Physics.CapsuleCastNonAlloc(castTop,castBottom,castRadius,direction,hits,distance+.025f,GameplayCollision.MovementMask,QueryTriggerInteraction.Ignore);
            RaycastHit[] sweepHits=hits;
            if(count==hits.Length)
            {
                // A saturated non-allocating query can omit the actual nearest blocker. Obtain
                // the complete result only in this rare case, then apply the same filters;
                // ignored unit/self/ground hits must not turn a crowded area into a movement wall.
                sweepHits=Physics.CapsuleCastAll(castTop,castBottom,castRadius,direction,distance+.025f,GameplayCollision.MovementMask,QueryTriggerInteraction.Ignore);
                count=sweepHits.Length;
            }
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=sweepHits[i];
                Collider other=hit.collider;
                if(other==null||other.transform.IsChildOf(transform)||hit.normal.y>.65f)continue;
                // Crowd spacing is owned by bot separation. Static structures remain solid.
                if(other.GetComponentInParent<HeroIdentity>()!=null||other.GetComponentInParent<LaneMinion>()!=null||other.GetComponentInParent<TrenchSoldier>()!=null)continue;
                if(Vector3.Dot(direction,hit.normal)>=-.001f)continue;
                float travel=Mathf.Max(0,hit.distance-.025f);
                if(travel<allowed){allowed=travel;normal=hit.normal;blocker=other;}
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
                if(!GameplayCollision.ContainsRoundedArena(point,39.5f,32.5f))continue;
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
        public Vector3 Move(Vector3 displacement,float halfWidth,float halfHeight,bool slide=true,bool forced=false)
        {
            Vector3 before=transform.position; LastCollisionNormal=Vector3.zero; LastCollisionCollider=null;
            if(!CanMove && !(forced && health != null && !health.IsDead && Time.timeScale > 0)){Velocity=Vector3.zero;return Vector3.zero;}
            displacement.y=0;
            Vector3 goal=GameplayCollision.ClampToRoundedArena(before+displacement,halfWidth,halfHeight);
            Vector3 remaining=goal-before;
            // Sweeps also protect transform-driven bots. Two contact planes permit wall sliding.
            for(int pass=0;pass<(slide?2:1)&&remaining.sqrMagnitude>.000001f;pass++)
            {
                float distance=remaining.magnitude;Vector3 direction=remaining/distance;
                Sweep(transform.position,direction,distance,out float allowed,out Vector3 normal,out Collider blocker);
                Vector3 bounded=GameplayCollision.ClampToRoundedArena(transform.position+direction*allowed,halfWidth,halfHeight);
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


