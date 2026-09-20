using System.Collections.Generic;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;

namespace Alterverse.AI
{
    [DefaultExecutionOrder(-25)]
    [RequireComponent(typeof(Health), typeof(MobaBotBrain), typeof(BotLanePlan))]
    [RequireComponent(typeof(BotTargetSensor))]
    public sealed class MobaBotMover : MonoBehaviour
    {
        [SerializeField] private float fallbackMoveSpeed = 3.35f, roamStopDistance = .35f, chaseLimit = 12f;
        private const float ArenaHalfWidth = 40f, ArenaHalfHeight = 33f, DefenseMargin = .65f;
        private static readonly List<MobaBotMover> ActiveBots = new();
        private static long nextSeparationOrder;
        private static float nextSharedWorldScan;
        private static DimensionPlanet[] sharedPlanets=System.Array.Empty<DimensionPlanet>();
        private static HotZoneDirector sharedHotZone;
        private long separationOrder;
        private HeroLocomotion locomotion;
        private float blockedTime, detourUntil, freeMoveTime, nextStuckLog;
        private float progressCheckAt;
        private Vector3 detour, progressAnchor, progressGoal;
        private Vector3 recoveryWaypoint;
        private bool hasRecoveryWaypoint;
        private Collider recoveryBlocker;
        private string recoveryMode = "None";
        private int recoveryCycle;
        private Collider loggedBlocker;
        private string loggedMode;
        private int loggedAttempt;
        private bool progressWatchActive;
        private int recoveryAttempts, recoverySide;
        private Collider skirtStructure;
        private float skirtUntil;
        private int skirtSide;
        private bool keepingDistance;
        private bool arrived;
        private Health health;
        private MobaBotBrain brain;
        private BotLanePlan lanePlan;
        private BotTargetSensor sensor;
        private HeroCombatProfile profile;
        private PlanetBuffReceiver planetBuff;
        private float nextWorldScan;
        private DimensionPlanet objective;
        private HotZoneDirector hotZone;
        private DimensionPlanet[] planets = System.Array.Empty<DimensionPlanet>();
        private Vector3 velocity;
        private Health chaseTarget;
        private float chaseStarted;
        private int rotationSlot;
        private static readonly float[] ExplosionEscapeAngles={0f,32f,-32f,64f,-64f,98f,-98f,132f,-132f};
        private const float ExplosionSafetyMargin=.72f;
        public bool IsAvoidingHazard { get; private set; }
        private float MoveSpeed => (profile != null ? profile.MoveSpeed : fallbackMoveSpeed) * (planetBuff != null ? planetBuff.MoveSpeedMultiplier : 1f) * locomotion.SpeedMultiplier;
        private float CombatStop => profile != null ? Mathf.Max(1.15f, profile.AttackRange * Mathf.Lerp(.65f,.88f,brain!=null?brain.Difficulty.Positioning:.6f)) : 1.4f;

        private void Awake()
        {
            separationOrder = ++nextSeparationOrder;
            locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();
            health = GetComponent<Health>(); brain = GetComponent<MobaBotBrain>();
            lanePlan = GetComponent<BotLanePlan>(); sensor = GetComponent<BotTargetSensor>();
            profile = GetComponent<HeroCombatProfile>(); planetBuff = GetComponent<PlanetBuffReceiver>()??gameObject.AddComponent<PlanetBuffReceiver>();
            // Stable across sessions; string.GetHashCode does not promise this.
            uint hash = 0;
            foreach (char c in gameObject.name) hash = unchecked(hash * 31u + c);
            rotationSlot = (int)(hash % 5u);
        }
        private void OnEnable() { if (!ActiveBots.Contains(this)) ActiveBots.Add(this); }
        private void OnDisable() { ActiveBots.Remove(this); if(ActiveBots.Count==0){nextSharedWorldScan=0;sharedHotZone=null;} ResetMotion(); }

        private void Update()
        {
            IsAvoidingHazard = false;
            if (Time.timeScale <= 0f || health == null || health.IsDead || brain == null || lanePlan == null)
            { velocity = Vector3.zero; return; }
            if (!locomotion.CanMove || locomotion.OwnsMovementFrame) { velocity=Vector3.zero; return; }
            ScanWorld();
            Vector3 destination = lanePlan.GetDestination(brain.State);
            float stopDistance = roamStopDistance;
            Health target = sensor != null ? sensor.CurrentTarget : null;
            if(brain.State!=MobaBotBrain.BotState.Fight){chaseTarget=null;chaseStarted=0;}

            // Under active pressure, allied bastions/trenches are temporary protection anchors.
            // Once Retreat ends the normal free-roam/search destination immediately takes over.
            if(brain.State==MobaBotBrain.BotState.Retreat&&sensor!=null&&sensor.NearbyEnemies>0&&TryGetOwnDefenseRefuge(out Vector3 defenseRefuge))
            {destination=defenseRefuge;stopDistance=.65f;}

            if (target != null && !target.IsDead && brain.State == MobaBotBrain.BotState.Fight)
            {
                // A defensive re-engage is intentionally local. Once the attacker leaves the
                // base area, drop the chase and return to normal defence/recovery instead of
                // following them back toward mid lane.
                if(brain.BaseDefenseActive&&!brain.InsideOwnBase(sensor.ObservedTargetPosition))
                {
                    sensor.Disengage(target);
                    chaseTarget=null;keepingDistance=false;target=null;
                }
            }

            if (target != null && !target.IsDead && brain.State == MobaBotBrain.BotState.Fight)
            {
                if(target!=chaseTarget){chaseTarget=target;chaseStarted=Time.time;keepingDistance=false;}
                Vector3 delta = sensor.ObservedTargetPosition - transform.position; delta.y = 0;
                float combatStop=SolidTargetStopDistance(target,CombatStop);

                // Staying meaningfully engaged resets the chase clock. The timer only limits a
                // prolonged pursuit of a target that keeps escaping, never normal combat.
                if(delta.sqrMagnitude<=combatStop*combatStop*1.30f)chaseStarted=Time.time;
                float maxChase=Mathf.Lerp(2.7f,4.8f,brain.Aggression)*brain.CommitmentRhythm;
                int chasePressure=sensor!=null?Mathf.Max(0,sensor.NearbyEnemies-(sensor.NearbyAllies+1)):0;
                if(target.Normalized<.35f)
                    maxChase*=Mathf.Lerp(1f,1.45f,brain.Difficulty.ChaseDiscipline);
                if(chasePressure>0)
                    maxChase*=Mathf.Lerp(1f,.55f,brain.Difficulty.ChaseDiscipline);
                if(brain.Difficulty.Coordinated)
                {
                    bool finish=brain.Tactics.CanFinish(target,transform.position);
                    maxChase*=finish?1.2f:.85f;
                    if(brain.Tactics.UnsafeDefense(sensor.ObservedTargetPosition))maxChase=0;
                }
                bool chaseExpired=Time.time-chaseStarted>maxChase&&
                    delta.sqrMagnitude>combatStop*combatStop*1.45f;

                if (!chaseExpired && delta.sqrMagnitude <= chaseLimit * chaseLimit)
                {
                    destination = sensor.ObservedTargetPosition; stopDistance = combatStop;
                    bool ranged=profile!=null&&profile.AttackRange>4f;
                    if(ranged)
                    {
                        float exitRange=CombatStop*.96f,enterRange=CombatStop*.74f;
                        if(delta.sqrMagnitude<enterRange*enterRange)keepingDistance=true;
                        else if(delta.sqrMagnitude>exitRange*exitRange)keepingDistance=false;
                    }
                    if(ranged&&keepingDistance)
                    {
                        Vector3 back=transform.position-delta.normalized*1.6f*brain.Difficulty.Positioning;
                        if(CanRepositionTo(back)){destination=back;stopDistance=.15f;}
                    }
                }
                else
                {
                    sensor.Disengage(target);
                    chaseTarget=null;keepingDistance=false;target=null;
                }
            }
            else if(brain.State==MobaBotBrain.BotState.Fight&&sensor!=null&&sensor.HasRecentEnemyMemory)
            {
                // Investigate only the frozen last-seen point. The hidden enemy's live transform
                // is never consulted, so this behaves like human short-term memory rather than wall vision.
                destination=sensor.LastKnownEnemyPosition;stopDistance=.75f;
            }
            else if(ShouldPursueHotZone(brain.State,target!=null,hotZone!=null&&hotZone.IsActive)&&ShouldContestHotZone())
            { destination=hotZone.transform.position;stopDistance=3.4f; }
            else if(brain.State==MobaBotBrain.BotState.Roam&&brain.GroupWithAllies&&sensor!=null&&sensor.NearbyAllies>0&&sensor.NearbyEnemies>0)
            { destination=sensor.RallyPosition;stopDistance=3f; }
            else if (brain.State == MobaBotBrain.BotState.Roam && objective != null && objective.Available && ShouldRotate())
            { destination = objective.transform.position; stopDistance = 2.2f; }

            if(brain.State==MobaBotBrain.BotState.Fight&&brain.GroupWithAllies&&sensor!=null&&sensor.NearbyAllies>0
                &&sensor.NearbyEnemies>sensor.NearbyAllies+1&&profile!=null&&profile.AttackRange>4f)
            {destination=sensor.RallyPosition;stopDistance=3f;}

            if(brain.Difficulty.Coordinated&&brain.Tactics.HasPlan)
            {
                bool retreating=brain.State==MobaBotBrain.BotState.Retreat||brain.State==MobaBotBrain.BotState.Recover;
                if(retreating&&brain.TacticalWithdrawal)
                {
                    if(!TryGetOwnDefenseRefuge(out destination))destination=brain.Tactics.SafeRetreat(transform.position);
                    stopDistance=.65f;
                }
                else if(!retreating&&brain.FollowTeamCall)
                {
                    HeroId role=profile!=null?profile.Hero:HeroId.Veyr;
                    bool combat=brain.State==MobaBotBrain.BotState.Fight&&target!=null&&target.GetComponent<HeroIdentity>()!=null;
                    if(combat)
                    {
                        Vector3 formed=brain.Tactics.Formation(brain,role,sensor.ObservedTargetPosition,true);
                        if(role==HeroId.Mira)formed=brain.Tactics.SupportPosition(brain,formed);
                        // Formation offsets cannot park an engaged hero outside basic range.
                        Vector3 offset=formed-sensor.ObservedTargetPosition;offset.y=0;
                        formed=sensor.ObservedTargetPosition+Vector3.ClampMagnitude(offset,
                            profile!=null?Mathf.Max(.1f,profile.AttackRange-.5f):CombatStop);
                        formed.y=transform.position.y;
                        // Retreat/kiting still has priority when already inside the danger distance.
                        if(!keepingDistance&&CanRepositionTo(formed)){destination=formed;stopDistance=.3f;}
                    }
                    else if(brain.State==MobaBotBrain.BotState.Roam||target==null)
                    {
                        destination=brain.Tactics.Formation(brain,role,brain.Tactics.Destination,false);stopDistance=.75f;
                    }
                }
                else if(!retreating&&profile!=null&&BotTeamCoordinator.IsBackline(profile.Hero)&&sensor.NearbyAllies==0&&sensor.NearbyEnemies>0)
                {destination=brain.Tactics.SafeRetreat(transform.position);stopDistance=.75f;}
            }

            if(brain.Difficulty.Coordinated&&brain.NeedsCombatReset)
            {destination=brain.Tactics.SafeRetreat(transform.position);stopDistance=.4f;}

            if (TryGetExplosionEscape(out Vector3 escape))
            {
                IsAvoidingHazard = true; detourUntil=0;
                destination = escape; stopDistance = .1f;
            }
            MoveToward(destination, stopDistance);
        }

        private void ScanWorld()
        {
            if (Time.time < nextWorldScan) return;
            nextWorldScan = Time.time + brain.Difficulty.ObjectiveInterval;
            if(Time.time>=nextSharedWorldScan)
            {
                nextSharedWorldScan=Time.time+.5f;
                sharedPlanets=FindObjectsByType<DimensionPlanet>();
                sharedHotZone=FindAnyObjectByType<HotZoneDirector>();
            }
            planets=sharedPlanets;hotZone=sharedHotZone;
            objective = null;
            float best = float.MaxValue;
            foreach (DimensionPlanet planet in planets)
            {
                if (planet == null || !planet.Available) continue;
                float distance = (planet.transform.position - transform.position).sqrMagnitude;
                if (distance < best) { best = distance; objective = planet; }
            }
        }
        private float SolidTargetStopDistance(Health target,float fallback)
        {
            if(target==null||target.GetComponent<HeroIdentity>()!=null)return fallback;
            Collider solid=target.GetComponent<Collider>();
            if(solid==null||!solid.enabled||solid.isTrigger)return fallback;
            Vector3 extents=solid.bounds.extents;
            // HeroLocomotion sweeps a .48 m capsule. Leave a small visible gap so melee bots
            // can attack solid objectives without repeatedly requesting movement inside them.
            return Mathf.Max(fallback,Mathf.Max(extents.x,extents.z)+.78f);
        }

        public static bool ShouldPursueHotZone(MobaBotBrain.BotState state,bool hasVisibleCombatTarget,bool hotZoneActive)
        {
            // Combat beats objectives. The explicit target guard closes the small decision-tick
            // window where perception may already see an enemy while the brain still reports Roam.
            return hotZoneActive&&state==MobaBotBrain.BotState.Roam&&!hasVisibleCombatTarget;
        }

        private bool ShouldContestHotZone()
        {
            if(hotZone==null||!hotZone.IsActive)return false;
            Vector3 delta=hotZone.transform.position-transform.position;delta.y=0;

            // Once near the enlarged zone, every available bot may join the skirmish.
            float nearRadius=HotZoneDirector.Radius+3f;
            if(delta.sqrMagnitude<nearRadius*nearRadius)return true;

            float own=brain.Team==0?hotZone.ZeroControlSeconds:hotZone.LumenControlSeconds;
            float enemy=brain.Team==0?hotZone.LumenControlSeconds:hotZone.ZeroControlSeconds;
            float deficit=enemy-own;
            float focus=brain.ObjectiveFocus*Mathf.Lerp(.70f,1.25f,brain.Difficulty.ObjectiveAwareness);

            // The Hot Zone is now the main PvP magnet: even low-objective-focus teams send
            // several heroes, while high-focus teams can commit all five.
            int slots=focus>.82f?5:focus>.58f?4:3;
            if(deficit>5f)slots=Mathf.Min(5,slots+1);
            else if(deficit<-14f)slots=Mathf.Max(3,slots-1);
            return rotationSlot<slots;
        }

        private bool ShouldRotate()
        {
            if (objective == null) return false;
            float sqr = (objective.transform.position - transform.position).sqrMagnitude;
            if(profile!=null&&profile.Hero==HeroId.Mira&&sensor!=null&&sensor.NearbyAllies==0&&sqr>8f*8f)
                return false;
            float focus=brain.ObjectiveFocus*Mathf.Lerp(.55f,1.15f,brain.Difficulty.ObjectiveAwareness);
            int slots=focus>.75f?4:focus>.55f?3:2;
            return sqr <= 26f * 26f && (rotationSlot < slots || sqr < 10f * 10f);
        }

        private bool TryGetExplosionEscape(out Vector3 escape)
        {
            escape=transform.position;
            float reaction=Mathf.Lerp(.55f,.06f,brain.Difficulty.DodgeAwareness)*brain.DecisionRhythm;
            bool found=false;
            Vector3 primaryCenter=Vector3.zero;
            float primaryRadius=0f,bestUrgency=float.MaxValue;

            foreach(Alterverse.Combat.PlanetPulseAttack pulse in Alterverse.Combat.PlanetPulseAttack.Active)
                if(pulse!=null&&pulse.IsTelegraphing)
                    ConsiderExplosionHazard(pulse.ImpactPosition,pulse.BlastRadius,pulse.TelegraphAge,pulse.TimeRemaining,reaction,
                        ref found,ref primaryCenter,ref primaryRadius,ref bestUrgency);

            foreach(MeteorStrike meteor in MeteorStrike.Active)
                if(meteor!=null&&meteor.IsTelegraphing)
                    ConsiderExplosionHazard(meteor.ImpactPosition,meteor.BlastRadius,meteor.TelegraphAge,meteor.TimeRemaining,reaction,
                        ref found,ref primaryCenter,ref primaryRadius,ref bestUrgency);

            foreach(SpaceBombStrike bomb in SpaceBombStrike.Active)
                if(bomb!=null&&bomb.IsTelegraphing)
                    ConsiderExplosionHazard(bomb.ImpactPosition,bomb.BlastRadius,bomb.TelegraphAge,bomb.TimeRemaining,reaction,
                        ref found,ref primaryCenter,ref primaryRadius,ref bestUrgency);

            if(!found)return false;
            escape=FindBestExplosionEscape(primaryCenter,primaryRadius+ExplosionSafetyMargin);
            return true;
        }

        private void ConsiderExplosionHazard(Vector3 center,float radius,float telegraphAge,float remaining,float reaction,
            ref bool found,ref Vector3 primaryCenter,ref float primaryRadius,ref float bestUrgency)
        {
            if(telegraphAge<reaction)return;
            Vector3 away=transform.position-center;away.y=0;
            float safeRadius=radius+ExplosionSafetyMargin;
            float distance=away.magnitude;
            if(distance>=safeRadius)return;

            float depth=safeRadius-distance;
            // Prefer the hazard that will land first, while giving a slight bias to deeper overlaps.
            float urgency=remaining-depth*.12f;
            if(found&&urgency>=bestUrgency)return;
            found=true;bestUrgency=urgency;primaryCenter=center;primaryRadius=radius;
        }

        private Vector3 FindBestExplosionEscape(Vector3 center,float safeRadius)
        {
            Vector3 current=transform.position;
            Vector3 offset=current-center;offset.y=0;
            Vector3 radial=offset.sqrMagnitude>.001f?offset.normalized:(brain.Team==0?Vector3.left:Vector3.right);
            float bestScore=float.NegativeInfinity;
            Vector3 best=current+radial*(safeRadius+0.45f);

            for(int i=0;i<ExplosionEscapeAngles.Length;i++)
            {
                Vector3 direction=Quaternion.AngleAxis(ExplosionEscapeAngles[i],Vector3.up)*radial;
                float dot=Vector3.Dot(offset,direction);
                float c=offset.sqrMagnitude-safeRadius*safeRadius;
                float discriminant=Mathf.Max(0f,dot*dot-c);
                float exitDistance=Mathf.Max(.25f,-dot+Mathf.Sqrt(discriminant));
                Vector3 candidate=current+direction*(exitDistance+.42f);
                candidate.x=Mathf.Clamp(candidate.x,-ArenaHalfWidth+.35f,ArenaHalfWidth-.35f);
                candidate.z=Mathf.Clamp(candidate.z,-ArenaHalfHeight+.35f,ArenaHalfHeight-.35f);
                candidate.y=current.y;

                float clearance=ExplosionClearance(candidate);
                float travel=(candidate-current).magnitude;
                float outward=Vector3.Dot(direction,radial);
                float score=clearance*4.2f-travel*.16f+outward*.18f;
                if(score<=bestScore)continue;
                bestScore=score;best=candidate;
            }
            return best;
        }

        private float ExplosionClearance(Vector3 point)
        {
            float minimum=float.PositiveInfinity;
            bool any=false;

            foreach(Alterverse.Combat.PlanetPulseAttack pulse in Alterverse.Combat.PlanetPulseAttack.Active)
                if(pulse!=null&&pulse.IsTelegraphing)
                    AccumulateExplosionClearance(point,pulse.ImpactPosition,pulse.BlastRadius,ref minimum,ref any);

            foreach(MeteorStrike meteor in MeteorStrike.Active)
                if(meteor!=null&&meteor.IsTelegraphing)
                    AccumulateExplosionClearance(point,meteor.ImpactPosition,meteor.BlastRadius,ref minimum,ref any);

            foreach(SpaceBombStrike bomb in SpaceBombStrike.Active)
                if(bomb!=null&&bomb.IsTelegraphing)
                    AccumulateExplosionClearance(point,bomb.ImpactPosition,bomb.BlastRadius,ref minimum,ref any);

            return any?minimum:10f;
        }

        private static void AccumulateExplosionClearance(Vector3 point,Vector3 center,float radius,ref float minimum,ref bool any)
        {
            Vector3 delta=point-center;delta.y=0;
            float clearance=delta.magnitude-(radius+ExplosionSafetyMargin);
            if(clearance<minimum)minimum=clearance;
            any=true;
        }

        private bool ExplosionBlocksReposition(Vector3 destination,Vector3 center,float radius)
        {
            Vector3 from=transform.position-center;from.y=0;
            Vector3 to=destination-center;to.y=0;
            float safeRadius=radius+ExplosionSafetyMargin,safeSqr=safeRadius*safeRadius;
            if(to.sqrMagnitude>=safeSqr)return false;
            // A move that starts inside a warning circle may continue if it increases safety.
            if(from.sqrMagnitude<safeSqr&&to.sqrMagnitude>from.sqrMagnitude)return false;
            return true;
        }

        public void ResetMotion()
        {
            ResetSteering(); locomotion?.ResetMotion(); objective = null; chaseTarget=null; chaseStarted=0; nextWorldScan = 0; IsAvoidingHazard = false;
        }

        public void ResetSteering()
        {
            velocity=Vector3.zero;arrived=false;blockedTime=detourUntil=freeMoveTime=0;detour=Vector3.zero;
            hasRecoveryWaypoint=false;recoveryBlocker=null;recoveryMode="None";recoveryCycle=0;
            loggedBlocker=null;loggedMode=null;loggedAttempt=0;
            recoveryAttempts=recoverySide=0;skirtStructure=null;skirtUntil=0;keepingDistance=false;nextStuckLog=0;progressCheckAt=0;progressAnchor=transform.position;progressWatchActive=false;
        }

        public bool CanRepositionTo(Vector3 destination)
        {
            ScanWorld();
            if(brain!=null)
            {
                foreach(DefenseBunker bunker in DefenseBunker.Active)
                    if(bunker!=null&&DefenseBlocksReposition(destination,bunker.transform,bunker.GetComponent<Collider>(),bunker.Team,0f))return false;
                foreach(DefenseTrench trench in DefenseTrench.Active)
                    if(trench!=null&&DefenseBlocksReposition(destination,trench.transform,trench.GetComponent<Collider>(),trench.Team,trench.DamageRadius))return false;
            }

            foreach(Alterverse.Combat.PlanetPulseAttack pulse in Alterverse.Combat.PlanetPulseAttack.Active)
                if(pulse!=null&&pulse.IsTelegraphing&&ExplosionBlocksReposition(destination,pulse.ImpactPosition,pulse.BlastRadius))return false;
            foreach(MeteorStrike meteor in MeteorStrike.Active)
                if(meteor!=null&&meteor.IsTelegraphing&&ExplosionBlocksReposition(destination,meteor.ImpactPosition,meteor.BlastRadius))return false;
            foreach(SpaceBombStrike bomb in SpaceBombStrike.Active)
                if(bomb!=null&&bomb.IsTelegraphing&&ExplosionBlocksReposition(destination,bomb.ImpactPosition,bomb.BlastRadius))return false;
            return true;
        }

        private bool DefenseBlocksReposition(Vector3 destination,Transform structure,Collider body,int structureTeam,float damageRadius)
        {
            Vector3 from=transform.position-structure.position;from.y=0;
            Vector3 to=destination-structure.position;to.y=0;
            Vector3 step=to-from;
            float bodyRadius=StructureBodyClearance(body);

            if(from.sqrMagnitude<bodyRadius*bodyRadius)
            {
                if(to.sqrMagnitude<=from.sqrMagnitude)return true;
            }
            else
            {
                float bodyT=step.sqrMagnitude>.000001f?Mathf.Clamp01(-Vector3.Dot(from,step)/step.sqrMagnitude):0f;
                if((from+step*bodyT).sqrMagnitude<bodyRadius*bodyRadius)return true;
            }

            if(damageRadius<=0f||structureTeam==brain.Team)return false;

            float safeRadius=damageRadius+DefenseMargin;
            if(from.sqrMagnitude<safeRadius*safeRadius)
                return to.sqrMagnitude<=from.sqrMagnitude;

            float t=step.sqrMagnitude>.000001f?Mathf.Clamp01(-Vector3.Dot(from,step)/step.sqrMagnitude):0f;
            return (from+step*t).sqrMagnitude<safeRadius*safeRadius;
        }

        private Vector3 SeparationVelocity()
        {
            Vector3 push = Vector3.zero;
            const float spacing = 1.15f;
            foreach (MobaBotMover other in ActiveBots)
            {
                if (other == null || other == this || other.health == null || other.health.IsDead) continue;
                Vector3 away = transform.position - other.transform.position; away.y = 0;
                float distance = away.magnitude;
                if (distance >= spacing) continue;
                if (distance < .001f) away = separationOrder < other.separationOrder ? Vector3.left : Vector3.right;
                push += away.normalized * (1f - distance / spacing);
            }
            return Vector3.ClampMagnitude(push, 1f) * 1.5f;
        }

        private Vector3 GuardAgainstDefenses(Vector3 next)
        {
            if(brain==null)return next;
            foreach(DefenseBunker bunker in DefenseBunker.Active)
                if(bunker!=null)next=ApplyDefenseSteering(next,bunker.transform,bunker.GetComponent<Collider>(),bunker.Team,0f);
            foreach(DefenseTrench trench in DefenseTrench.Active)
                if(trench!=null)next=ApplyDefenseSteering(next,trench.transform,trench.GetComponent<Collider>(),trench.Team,trench.DamageRadius);
            return next;
        }

        private Vector3 ApplyDefenseSteering(Vector3 next,Transform structure,Collider body,int structureTeam,float damageRadius)
        {
            next=SkirtStructureBody(structure,body,next);
            if(damageRadius<=0f||structureTeam==brain.Team)return next;

            Vector3 away=transform.position-structure.position;away.y=0;
            float safeRadius=damageRadius+DefenseMargin;
            if(away.sqrMagnitude<safeRadius*safeRadius)
            {
                if(away.sqrMagnitude<.001f)away=brain.Team==0?Vector3.left:Vector3.right;
                Vector3 escape=structure.position+away.normalized*(safeRadius+.18f);
                Vector3 tangent=Vector3.Cross(Vector3.up,away.normalized);
                escape+=tangent*((rotationSlot-2)*.24f);
                escape.y=transform.position.y;
                next=Vector3.MoveTowards(transform.position,escape,MoveSpeed*Time.deltaTime);
                Vector3 escapeDelta=escape-transform.position;escapeDelta.y=0;
                if(escapeDelta.sqrMagnitude>.001f)
                    velocity=Vector3.Lerp(velocity,escapeDelta.normalized*MoveSpeed,1f-Mathf.Exp(-12f*Time.deltaTime));
                return next;
            }

            Vector3 step=next-transform.position;step.y=0;
            float along=step.sqrMagnitude>.000001f?Mathf.Clamp01(-Vector3.Dot(away,step)/step.sqrMagnitude):0f;
            Vector3 closest=away+step*along;
            if(closest.sqrMagnitude>=safeRadius*safeRadius)return next;

            Vector3 radial=away.sqrMagnitude>.001f?away.normalized:(brain.Team==0?Vector3.left:Vector3.right);
            Vector3 tangent2=Vector3.Cross(Vector3.up,radial);
            float spread=(rotationSlot-2)*.30f;
            Vector3 staging=structure.position+radial*(safeRadius+.48f)+tangent2*spread;
            staging.y=transform.position.y;
            Vector3 stageDelta=staging-transform.position;stageDelta.y=0;
            float stageSpeed=MoveSpeed*Mathf.Clamp01(stageDelta.magnitude/1.4f+.18f);
            next=Vector3.MoveTowards(transform.position,staging,stageSpeed*Time.deltaTime);
            if(stageDelta.sqrMagnitude>.001f)
                velocity=Vector3.Lerp(velocity,stageDelta.normalized*stageSpeed,1f-Mathf.Exp(-7f*Time.deltaTime));
            return next;
        }

        private bool TryGetOwnDefenseRefuge(out Vector3 refuge)
        {
            refuge=transform.position;
            if(brain==null)return false;
            float best=20f*20f;
            bool found=false;
            Vector3 homeDirection=brain.Team==0?Vector3.left:Vector3.right;

            foreach(DefenseBunker bunker in DefenseBunker.Active)
                if(bunker!=null&&bunker.Team==brain.Team)
                    ConsiderRefuge(bunker.transform,bunker.GetComponent<Collider>(),homeDirection,ref refuge,ref best,ref found);
            foreach(DefenseTrench trench in DefenseTrench.Active)
                if(trench!=null&&trench.Team==brain.Team)
                    ConsiderRefuge(trench.transform,trench.GetComponent<Collider>(),homeDirection,ref refuge,ref best,ref found);
            return found;
        }

        private void ConsiderRefuge(Transform structure,Collider body,Vector3 homeDirection,ref Vector3 refuge,ref float best,ref bool found)
        {
            float bodyExtent=body!=null&&body.enabled&&!body.isTrigger?Mathf.Max(body.bounds.extents.x,body.bounds.extents.z):2.5f;
            Vector3 point=structure.position+homeDirection*(bodyExtent+.8f);
            point.y=transform.position.y;
            Vector3 d=point-transform.position;d.y=0;
            if(d.sqrMagnitude>=best)return;
            best=d.sqrMagnitude;refuge=point;found=true;
        }

        private float StructureBodyClearance(Collider body)
        {
            float radius=body!=null&&body.enabled&&!body.isTrigger?Mathf.Max(body.bounds.extents.x,body.bounds.extents.z):.70f;
            return radius+.62f;
        }

        private Vector3 SkirtStructureBody(Transform structure,Collider body,Vector3 next)
        {
            Vector3 current=transform.position;
            Vector3 step=next-current;step.y=0;
            float stepLength=step.magnitude;
            if(stepLength<.0001f)return next;

            Vector3 from=current-structure.position;from.y=0;
            Vector3 to=next-structure.position;to.y=0;
            float clearance=StructureBodyClearance(body),clearanceSqr=clearance*clearance;
            Vector3 segment=to-from;
            float t=segment.sqrMagnitude>.000001f?Mathf.Clamp01(-Vector3.Dot(from,segment)/segment.sqrMagnitude):0f;
            bool crossing=(from+segment*t).sqrMagnitude<clearanceSqr;
            bool trapped=from.sqrMagnitude<clearanceSqr;
            if(!crossing&&!trapped)return next;

            Vector3 radial=from.sqrMagnitude>.001f?from.normalized:(brain.Team==0?Vector3.left:Vector3.right);
            Vector3 desired=step/stepLength;
            Vector3 tangent=Vector3.Cross(Vector3.up,radial).normalized;
            if(skirtStructure!=body||Time.time>=skirtUntil)
            {
                float alignment=Vector3.Dot(tangent,desired);
                skirtSide=Mathf.Abs(alignment)>.08f?(alignment>0?1:-1):((rotationSlot&1)==0?-1:1);
                skirtStructure=body;skirtUntil=Time.time+1.4f;
            }
            tangent*=skirtSide;
            Vector3 steer=(tangent*.82f+radial*.42f+desired*.24f).normalized;
            Vector3 skirted=current+steer*stepLength;
            skirted.y=next.y;
            return skirted;
        }

        private void MoveToward(Vector3 destination, float stopDistance)
        {
            Vector3 delta = destination - transform.position; delta.y = 0;
            Vector3 advance = Vector3.zero;
            float distance=delta.magnitude;
            if(distance<=stopDistance)arrived=true;
            else if(distance>stopDistance+.22f)arrived=false;
            if (arrived) velocity = Vector3.zero;
            else
            {
                if (Vector3.Dot(velocity, delta) < 0f) velocity = Vector3.zero;
                velocity = Vector3.Lerp(velocity, delta.normalized * MoveSpeed, 1f-Mathf.Exp(-18f*Time.deltaTime));
                advance = Vector3.ClampMagnitude(velocity * Time.deltaTime, Mathf.Max(0f, delta.magnitude - stopDistance));
            }
            if(Time.time<detourUntil&&!arrived)
            {
                Vector3 recoveryDelta=hasRecoveryWaypoint?recoveryWaypoint-transform.position:detour;
                recoveryDelta.y=0;
                if(hasRecoveryWaypoint&&recoveryDelta.sqrMagnitude<.25f*.25f)
                {detourUntil=0;hasRecoveryWaypoint=false;}
                else advance=recoveryDelta.normalized*Mathf.Min(MoveSpeed*Time.deltaTime,
                    hasRecoveryWaypoint?recoveryDelta.magnitude:float.MaxValue);
            }
            Vector3 separation=SeparationVelocity();
            if(arrived)
            {
                // Once the tactical destination is reached, crowd separation must not push the bot
                // back across the stop boundary. That produced a repeatable settle -> separate ->
                // re-approach loop that looked like slow forward micro-stutter.
                if(separation.sqrMagnitude<.04f||delta.sqrMagnitude<.0001f)separation=Vector3.zero;
                else
                {
                    Vector3 radial=delta.normalized;
                    separation-=radial*Vector3.Dot(separation,radial);
                    if(separation.sqrMagnitude<.04f)separation=Vector3.zero;
                }
            }
            Vector3 step = Vector3.ClampMagnitude(advance + separation * Time.deltaTime, MoveSpeed * Time.deltaTime);
            Vector3 next = GuardAgainstDefenses(transform.position + step);
            next.x = Mathf.Clamp(next.x, -ArenaHalfWidth, ArenaHalfWidth);
            next.z = Mathf.Clamp(next.z, -ArenaHalfHeight, ArenaHalfHeight);
            Vector3 requested=next-transform.position;
            Vector3 movement=locomotion.Move(requested,ArenaHalfWidth,ArenaHalfHeight);
            if(!arrived&&movement.sqrMagnitude>.00001f)locomotion.FaceMovement(movement);

            // Severe partial stalls count as blocked too, but do not run stuck recovery while
            // a bot is already settling just outside its tactical stop distance. Close-range combat
            // and recovery goals can legitimately move only a few centimetres and previously produced
            // false "route/no collider" SideProbe warnings.
            const float settleMargin=.35f;
            bool hasIntent=!arrived&&distance>stopDistance+settleMargin&&requested.sqrMagnitude>.00001f;
            float requestedSqr=requested.sqrMagnitude;
            float progressRatio=requestedSqr>.000001f?movement.sqrMagnitude/requestedSqr:1f;
            bool blocked=hasIntent&&progressRatio<.30f;
            if(blocked){blockedTime+=Time.deltaTime;freeMoveTime=0;}
            else if(hasIntent&&progressRatio>.70f)
            {
                blockedTime=0;freeMoveTime+=Time.deltaTime;
                if(freeMoveTime>.65f)freeMoveTime=.65f;
            }
            else {blockedTime=0;freeMoveTime=0;}

            bool recoveredThisFrame=false;
            if(blockedTime>.28f&&Time.time>=detourUntil)
            {
                BeginStuckRecovery(delta);
                recoveredThisFrame=true;
            }

            // Collision-only detection misses oscillation and tiny scrape movement. This low-cost
            // watchdog checks real displacement a few times per second and recovers without teleporting.
            if(hasIntent&&!arrived)
            {
                if(!progressWatchActive)
                {
                    progressWatchActive=true;
                    progressAnchor=transform.position;
                    progressCheckAt=Time.time+1.2f;progressGoal=destination;
                }
                else if(Time.time>=progressCheckAt)
                {
                    Vector3 travelled=transform.position-progressAnchor;travelled.y=0;
                    Vector3 oldDelta=progressGoal-progressAnchor;oldDelta.y=0;
                    Vector3 newDelta=progressGoal-transform.position;newDelta.y=0;
                    bool sameGoal=(destination-progressGoal).sqrMagnitude<1f;
                    float approach=oldDelta.magnitude-newDelta.magnitude;
                    // A completed sidestep is useful; oscillation around a fixed goal is not.
                    if(approach>.45f){recoveryAttempts=0;recoveryMode="None";}
                    bool stalled=travelled.sqrMagnitude<.14f*.14f||
                        sameGoal&&approach<.06f&&travelled.sqrMagnitude<.8f*.8f;
                    if(stalled&&!recoveredThisFrame&&Time.time>=detourUntil)
                        BeginStuckRecovery(delta);
                    progressAnchor=transform.position;
                    progressCheckAt=Time.time+1.2f;progressGoal=destination;
                }
            }
            else
            {
                progressWatchActive=false;
                progressAnchor=transform.position;
                progressCheckAt=0;
                if(distance<=stopDistance+settleMargin)
                {
                    blockedTime=0;
                    recoveryAttempts=0;
                    recoveryMode="None";
                }
            }
        }

        private void BeginStuckRecovery(Vector3 towardDestination)
        {
            blockedTime=0;freeMoveTime=0;
            recoveryAttempts=Mathf.Min(6,recoveryAttempts+1);recoveryCycle++;
            towardDestination.y=0;
            Vector3 desired=towardDestination.sqrMagnitude>.01f?towardDestination.normalized:transform.forward;
            Collider blocker=locomotion.LastCollisionCollider;
            // Never carry an unrelated blocker into a later progress-watch recovery. If the
            // current stall has no physical collision, use a clear-direction probe instead.
            recoveryBlocker=blocker;
            recoverySide=(recoveryCycle&1)==0?1:-1;
            hasRecoveryWaypoint=false;
            // Replan navigation, not perception: a collision must not erase a visible opponent.
            if(recoveryAttempts==3)lanePlan?.ResetRoute();
            if((recoveryAttempts<5||recoveryCycle%3!=0)&&recoveryBlocker!=null&&recoveryBlocker.enabled&&
                locomotion.TryFindObstacleWaypoint(recoveryBlocker,transform.position+towardDestination,
                    recoverySide,out recoveryWaypoint,recoveryAttempts>=4?1.4f:.8f))
            {
                hasRecoveryWaypoint=true;recoveryMode=recoveryAttempts>=4?"WideWaypoint":"ObstacleWaypoint";
                detourUntil=Time.time+Mathf.Clamp(Vector3.Distance(transform.position,recoveryWaypoint)/MoveSpeed+.4f,.6f,4f);
            }
            else
            {
                Vector3 probe=recoveryAttempts>=4?-desired:
                    Quaternion.AngleAxis(recoverySide*(recoveryAttempts>=2?75f:35f),Vector3.up)*desired;
                recoveryMode=recoveryAttempts>=4?"LocalEscape":"SideProbe";
                if(!locomotion.TryFindClearDirection(probe,recoveryAttempts>=4?2f:1f,recoverySide,out detour))
                {detour=Vector3.zero;recoveryMode="WaitForBlocker";}
                detourUntil=Time.time+(recoveryAttempts>=4?1f:.65f);
            }
            velocity=Vector3.zero;

#if UNITY_EDITOR || DEBUG
            if(Time.time>=nextStuckLog&&(loggedBlocker!=recoveryBlocker||loggedMode!=recoveryMode||loggedAttempt!=recoveryAttempts))
            {
                nextStuckLog=Time.time+2f;
                loggedBlocker=recoveryBlocker;loggedMode=recoveryMode;loggedAttempt=recoveryAttempts;
                string blockerName=recoveryBlocker!=null?recoveryBlocker.name:"route/no collider";
                string targetName=sensor!=null&&sensor.CurrentTarget!=null?sensor.CurrentTarget.name:"none";
                Debug.LogWarning($"BOT STUCK RECOVERY: {name} pos={transform.position} state={(brain!=null?brain.State.ToString():"?")} target={targetName} destination={transform.position+towardDestination} distance={towardDestination.magnitude:F2} blocker={blockerName} attempt={recoveryAttempts} mode={recoveryMode}");
            }
#endif
        }
    }
}



