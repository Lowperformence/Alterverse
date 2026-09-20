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
        [SerializeField] private float fallbackMoveSpeed = 3.35f, laneStopDistance = .35f, chaseLimit = 12f;
        private const float ArenaHalfWidth = 40f, ArenaHalfHeight = 33f, TowerMargin = .75f;
        private static readonly List<MobaBotMover> ActiveBots = new();
        private static long nextSeparationOrder;
        private static float nextSharedWorldScan;
        private static DimensionPlanet[] sharedPlanets=System.Array.Empty<DimensionPlanet>();
        private static LaneTower[] sharedTowers=System.Array.Empty<LaneTower>();
        private static Alterverse.Combat.PlanetPulseAttack[] sharedPulses=System.Array.Empty<Alterverse.Combat.PlanetPulseAttack>();
        private static HotZoneDirector sharedHotZone;
        private long separationOrder;
        private HeroLocomotion locomotion;
        private float blockedTime, detourUntil, freeMoveTime, nextStuckLog;
        private float progressCheckAt, hazardSeenAt;
        private Vector3 detour, progressAnchor;
        private bool progressWatchActive;
        private Alterverse.Combat.PlanetPulseAttack observedHazard;
        private int recoveryAttempts;
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
        private LaneTower[] towers = System.Array.Empty<LaneTower>();
        private Alterverse.Combat.PlanetPulseAttack[] pulses = System.Array.Empty<Alterverse.Combat.PlanetPulseAttack>();
        private Vector3 velocity;
        private Health chaseTarget;
        private float chaseStarted;
        private int rotationSlot;
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
            float stopDistance = laneStopDistance;
            Health target = sensor != null ? sensor.CurrentTarget : null;
            if(brain.State!=MobaBotBrain.BotState.Fight){chaseTarget=null;chaseStarted=0;}

            if (target != null && !target.IsDead && brain.State == MobaBotBrain.BotState.Fight)
            {
                if(target!=chaseTarget){chaseTarget=target;chaseStarted=Time.time;}
                Vector3 delta = target.transform.position - transform.position; delta.y = 0;
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
                bool chaseExpired=Time.time-chaseStarted>maxChase&&
                    delta.sqrMagnitude>combatStop*combatStop*1.45f;

                if (!chaseExpired && delta.sqrMagnitude <= chaseLimit * chaseLimit)
                {
                    destination = target.transform.position; stopDistance = combatStop;
                    if(profile!=null&&profile.AttackRange>4f&&delta.sqrMagnitude<CombatStop*CombatStop*.55f)
                    {
                        Vector3 back=transform.position-delta.normalized*1.6f*brain.Difficulty.Positioning;
                        if(CanRepositionTo(back)){destination=back;stopDistance=.15f;}
                    }
                }
            }
            else if(brain.State==MobaBotBrain.BotState.Fight&&sensor!=null&&sensor.HasRecentEnemyMemory)
            {
                // Investigate only the frozen last-seen point. The hidden enemy's live transform
                // is never consulted, so this behaves like human short-term memory rather than wall vision.
                destination=sensor.LastKnownEnemyPosition;stopDistance=.75f;
            }
            else if(brain.State==MobaBotBrain.BotState.Lane&&hotZone!=null&&hotZone.IsActive&&ShouldContestHotZone())
            { destination=hotZone.transform.position;stopDistance=3.4f; }
            else if(brain.State==MobaBotBrain.BotState.Lane&&brain.GroupWithAllies&&sensor!=null&&sensor.NearbyAllies>0&&sensor.NearbyEnemies>0)
            { destination=sensor.RallyPosition;stopDistance=3f; }
            else if (brain.State == MobaBotBrain.BotState.Lane && objective != null && objective.Available && ShouldRotate())
            { destination = objective.transform.position; stopDistance = 2.2f; }

            if(brain.State==MobaBotBrain.BotState.Fight&&brain.GroupWithAllies&&sensor!=null&&sensor.NearbyAllies>0
                &&sensor.NearbyEnemies>sensor.NearbyAllies+1&&profile!=null&&profile.AttackRange>4f)
            {destination=sensor.RallyPosition;stopDistance=3f;}

            if (TryGetPulseEscape(out Vector3 escape))
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
                sharedPulses=FindObjectsByType<Alterverse.Combat.PlanetPulseAttack>();
                sharedTowers=FindObjectsByType<LaneTower>();
                sharedHotZone=FindAnyObjectByType<HotZoneDirector>();
            }
            planets=sharedPlanets;pulses=sharedPulses;towers=sharedTowers;hotZone=sharedHotZone;
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
            if(target==null||target.GetComponent<HeroIdentity>()!=null||target.GetComponent<LaneMinion>()!=null)return fallback;
            Collider solid=target.GetComponent<Collider>();
            if(solid==null||!solid.enabled||solid.isTrigger)return fallback;
            Vector3 extents=solid.bounds.extents;
            // HeroLocomotion sweeps a .48 m capsule. Leave a small visible gap so melee bots
            // can attack solid objectives without repeatedly requesting movement inside them.
            return Mathf.Max(fallback,Mathf.Max(extents.x,extents.z)+.58f);
        }

        private bool ShouldContestHotZone()
        {
            if(hotZone==null||!hotZone.IsActive)return false;
            Vector3 delta=hotZone.transform.position-transform.position;delta.y=0;

            // Support should not peel off across the map to solo an objective. If already close,
            // it may still contest naturally; otherwise it waits for at least one nearby ally.
            if(profile!=null&&profile.Hero==HeroId.Mira&&sensor!=null&&sensor.NearbyAllies==0&&delta.sqrMagnitude>6.5f*6.5f)
                return false;

            if(delta.sqrMagnitude<11f*11f)return true;

            float own=brain.Team==0?hotZone.ZeroControlSeconds:hotZone.LumenControlSeconds;
            float enemy=brain.Team==0?hotZone.LumenControlSeconds:hotZone.ZeroControlSeconds;
            float deficit=enemy-own;
            float focus=brain.ObjectiveFocus*Mathf.Lerp(.55f,1.15f,brain.Difficulty.ObjectiveAwareness);
            int slots=focus>.72f?4:focus>.52f?3:2;
            if(deficit>6f)slots=Mathf.Min(5,slots+1);
            else if(deficit<-10f)slots=Mathf.Max(2,slots-1);
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

        private bool TryGetPulseEscape(out Vector3 escape)
        {
            escape = transform.position;
            float deepest = 0f;
            Alterverse.Combat.PlanetPulseAttack danger = null;
            foreach (Alterverse.Combat.PlanetPulseAttack pulse in pulses)
            {
                if (pulse == null || !pulse.IsTelegraphing) continue;
                Vector3 away = transform.position - pulse.transform.position; away.y = 0;
                float distance = away.magnitude, safeRadius = pulse.BlastRadius + .7f;
                if (distance >= safeRadius || safeRadius - distance <= deepest) continue;
                deepest = safeRadius - distance;
                danger = pulse;
                if (distance < .01f) away = brain.Team == 0 ? Vector3.left : Vector3.right;
                escape = pulse.transform.position + away.normalized * safeRadius;
                escape.y = transform.position.y;
            }

            if(danger==null)
            {
                observedHazard=null;hazardSeenAt=0;
                return false;
            }

            if(danger!=observedHazard)
            {
                observedHazard=danger;
                hazardSeenAt=Time.time;
            }

            // All difficulties react only after the telegraph is visible. Better bots simply
            // process that visible information faster; nobody gets pre-knowledge of the impact.
            float reaction=Mathf.Lerp(.55f,.06f,brain.Difficulty.DodgeAwareness)*brain.DecisionRhythm;
            return Time.time-hazardSeenAt>=reaction;
        }

        public void ResetMotion()
        {
            ResetSteering(); locomotion?.ResetMotion(); objective = null; chaseTarget=null; chaseStarted=0; nextWorldScan = 0; IsAvoidingHazard = false;
        }

        public void ResetSteering()
        {
            velocity=Vector3.zero;arrived=false;blockedTime=detourUntil=freeMoveTime=0;detour=Vector3.zero;
            recoveryAttempts=0;nextStuckLog=0;progressCheckAt=0;progressAnchor=transform.position;progressWatchActive=false;
            observedHazard=null;hazardSeenAt=0;
        }

        public bool CanRepositionTo(Vector3 destination)
        {
            ScanWorld();
            foreach (LaneTower tower in towers)
            {
                if (tower == null || !tower.isActiveAndEnabled || brain == null) continue;
                Health hp = tower.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                Vector3 from = transform.position - tower.transform.position; from.y = 0;
                Vector3 to = destination - tower.transform.position; to.y = 0;
                Vector3 step = to - from;

                // Physical tower bodies are solid for every team. Never let a mobility dash cut
                // through an allied bastion or through a covered enemy tower.
                float bodyRadius = TowerBodyClearance(tower);
                if (from.sqrMagnitude < bodyRadius * bodyRadius)
                {
                    if (to.sqrMagnitude <= from.sqrMagnitude) return false;
                }
                else
                {
                    float bodyT = step.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector3.Dot(from, step) / step.sqrMagnitude) : 0;
                    if ((from + step * bodyT).sqrMagnitude < bodyRadius * bodyRadius) return false;
                }

                if (tower.Team == brain.Team || tower.HasMinionCoverFor(brain.Team)) continue;

                float safeRadius = tower.AttackRange + TowerMargin;
                // Allow an escape from existing danger, never a new dive or a crossing dash.
                if (from.sqrMagnitude < safeRadius * safeRadius)
                {
                    if (to.sqrMagnitude <= from.sqrMagnitude) return false;
                    continue;
                }
                float t = step.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector3.Dot(from, step) / step.sqrMagnitude) : 0;
                if ((from + step * t).sqrMagnitude < safeRadius * safeRadius) return false;
            }
            foreach (Alterverse.Combat.PlanetPulseAttack pulse in pulses)
            {
                if (pulse == null || !pulse.IsTelegraphing) continue;
                Vector3 from = transform.position - pulse.transform.position; from.y = 0;
                Vector3 to = destination - pulse.transform.position; to.y = 0;
                float safeRadius = pulse.BlastRadius + .7f;
                if (to.sqrMagnitude < safeRadius * safeRadius && to.sqrMagnitude <= from.sqrMagnitude) return false;
            }
            return true;
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

        private Vector3 GuardAgainstTowers(Vector3 next)
        {
            if (brain == null) return next;
            foreach (LaneTower tower in towers)
            {
                if (tower == null || !tower.isActiveAndEnabled) continue;
                Health towerHealth = tower.GetComponent<Health>();
                if (towerHealth == null || towerHealth.IsDead) continue;

                // Every tower body gets local path skirting, including allied towers. This is the
                // missing case that caused lane bots to repeatedly collide with their own bastions.
                next = SkirtTowerBody(tower,next);

                if (tower.Team == brain.Team || tower.HasMinionCoverFor(brain.Team)) continue;
                Vector3 away = transform.position - tower.transform.position; away.y = 0;
                float safeRadius = tower.AttackRange + TowerMargin;
                if (away.sqrMagnitude < safeRadius * safeRadius)
                {
                    // If cover dies, leave the danger zone instead of continuing to dive.
                    if (away.sqrMagnitude < .001f) away = brain.Team == 0 ? Vector3.left : Vector3.right;
                    Vector3 escape = tower.transform.position + away.normalized * (safeRadius + .18f);
                    Vector3 tangent=Vector3.Cross(Vector3.up,away.normalized);
                    escape+=tangent*((rotationSlot-2)*.28f);
                    escape.y = transform.position.y;
                    next = Vector3.MoveTowards(transform.position, escape, MoveSpeed * Time.deltaTime);
                    Vector3 escapeDelta=escape-transform.position;escapeDelta.y=0;
                    if(escapeDelta.sqrMagnitude>.001f)
                        velocity=Vector3.Lerp(velocity,escapeDelta.normalized*MoveSpeed,1f-Mathf.Exp(-12f*Time.deltaTime));
                }
                else
                {
                    // Test the whole step, so a long frame cannot cross the tower circle.
                    Vector3 step = next - transform.position; step.y = 0;
                    float along = step.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector3.Dot(away, step) / step.sqrMagnitude) : 0f;
                    Vector3 closest = away + step * along;
                    if (closest.sqrMagnitude < safeRadius * safeRadius)
                    {
                        // Do not freeze on an invisible tower-radius line. If our lane wave is approaching,
                        // follow just behind it until it enters tower range and becomes real cover.
                        // With no wave nearby, fall back to a readable staging position outside danger.
                        Vector3 staging;
                        if(!TryGetWaveStaging(tower,out staging))
                        {
                            Vector3 radial=away.sqrMagnitude>.001f?away.normalized:(brain.Team==0?Vector3.left:Vector3.right);
                            Vector3 tangent=Vector3.Cross(Vector3.up,radial);
                            float spread=(rotationSlot-2)*.34f;
                            staging=tower.transform.position+radial*(safeRadius+.55f)+tangent*spread;
                            staging.y=transform.position.y;
                        }

                        Vector3 stageDelta=staging-transform.position;stageDelta.y=0;
                        float stageSpeed=MoveSpeed*Mathf.Clamp01(stageDelta.magnitude/1.4f+.18f);
                        next=Vector3.MoveTowards(transform.position,staging,stageSpeed*Time.deltaTime);
                        if(stageDelta.sqrMagnitude>.001f)
                            velocity=Vector3.Lerp(velocity,stageDelta.normalized*stageSpeed,1f-Mathf.Exp(-7f*Time.deltaTime));
                    }
                }
            }
            return next;
        }

        private float TowerBodyClearance(LaneTower tower)
        {
            Collider body=tower!=null?tower.GetComponent<Collider>():null;
            float radius=body!=null&&body.enabled&&!body.isTrigger?Mathf.Max(body.bounds.extents.x,body.bounds.extents.z):.70f;
            // HeroLocomotion uses a .48 m sweep radius. Add a small visual margin so steering
            // starts before the physics cast is already touching the structure.
            return radius+.62f;
        }

        private Vector3 SkirtTowerBody(LaneTower tower,Vector3 next)
        {
            Vector3 current=transform.position;
            Vector3 step=next-current;step.y=0;
            float stepLength=step.magnitude;
            if(stepLength<.0001f)return next;

            Vector3 towerPos=tower.transform.position;
            Vector3 from=current-towerPos;from.y=0;
            Vector3 to=next-towerPos;to.y=0;
            float clearance=TowerBodyClearance(tower),clearanceSqr=clearance*clearance;
            Vector3 segment=to-from;
            float t=segment.sqrMagnitude>.000001f?Mathf.Clamp01(-Vector3.Dot(from,segment)/segment.sqrMagnitude):0f;
            bool crossing=(from+segment*t).sqrMagnitude<clearanceSqr;
            bool trapped=from.sqrMagnitude<clearanceSqr;
            if(!crossing&&!trapped)return next;

            Vector3 radial=from.sqrMagnitude>.001f?from.normalized:(brain.Team==0?Vector3.left:Vector3.right);
            Vector3 desired=step/stepLength;
            Vector3 tangent=Vector3.Cross(Vector3.up,radial).normalized;
            Vector3 opposite=-tangent;
            float tangentScore=Vector3.Dot(tangent,desired),oppositeScore=Vector3.Dot(opposite,desired);
            if(oppositeScore>tangentScore+.02f)tangent=opposite;
            else if(Mathf.Abs(oppositeScore-tangentScore)<=.02f&&((rotationSlot&1)==0))tangent=opposite;

            // Tangential motion gets the largest weight, while a small outward component prevents
            // the bot from scraping the collider and the desired component keeps lane progress.
            Vector3 steer=(tangent*.82f+radial*.42f+desired*.24f).normalized;
            Vector3 skirted=current+steer*stepLength;
            skirted.y=next.y;
            return skirted;
        }

        private bool TryGetWaveStaging(LaneTower tower,out Vector3 staging)
        {
            staging=transform.position;
            if(tower==null||brain==null)return false;

            LaneMinion best=null;
            float bestSqr=12f*12f;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health candidate=Health.Active[i];
                if(candidate==null||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                LaneMinion minion=candidate.GetComponent<LaneMinion>();
                if(minion==null||minion.Team!=brain.Team)continue;

                Vector3 toTower=candidate.transform.position-tower.transform.position;toTower.y=0;
                if(Mathf.Abs(candidate.transform.position.z-brain.LaneZ)>5f||toTower.sqrMagnitude>bestSqr)continue;
                best=minion;bestSqr=toTower.sqrMagnitude;
            }
            if(best==null)return false;

            Vector3 outward=best.transform.position-tower.transform.position;outward.y=0;
            if(outward.sqrMagnitude<.01f)outward=brain.Team==0?Vector3.left:Vector3.right;
            Vector3 tangent=Vector3.Cross(Vector3.up,outward.normalized);
            staging=best.transform.position+outward.normalized*1.35f+tangent*((rotationSlot-2)*.22f);
            staging.y=transform.position.y;
            return true;
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
            if(Time.time<detourUntil&&!arrived)advance=detour*MoveSpeed*Time.deltaTime;
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
            Vector3 next = GuardAgainstTowers(transform.position + step);
            next.x = Mathf.Clamp(next.x, -ArenaHalfWidth, ArenaHalfWidth);
            next.z = Mathf.Clamp(next.z, -ArenaHalfHeight, ArenaHalfHeight);
            Vector3 requested=next-transform.position;
            Vector3 movement=locomotion.Move(requested,ArenaHalfWidth,ArenaHalfHeight);
            if(!arrived&&movement.sqrMagnitude>.00001f)locomotion.FaceMovement(movement);

            // Severe partial stalls count as blocked too. The previous 9% threshold missed bots
            // that only scraped a few centimetres along a collider and could remain wedged.
            bool hasIntent=!arrived&&requested.sqrMagnitude>.00001f;
            float requestedSqr=requested.sqrMagnitude;
            float progressRatio=requestedSqr>.000001f?movement.sqrMagnitude/requestedSqr:1f;
            bool blocked=hasIntent&&progressRatio<.30f;
            if(blocked){blockedTime+=Time.deltaTime;freeMoveTime=0;}
            else if(hasIntent&&progressRatio>.70f)
            {
                blockedTime=0;freeMoveTime+=Time.deltaTime;
                if(freeMoveTime>.65f){recoveryAttempts=0;freeMoveTime=.65f;}
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
                    progressCheckAt=Time.time+.65f;
                }
                else if(Time.time>=progressCheckAt)
                {
                    Vector3 travelled=transform.position-progressAnchor;travelled.y=0;
                    if(travelled.sqrMagnitude<.14f*.14f&&!recoveredThisFrame&&Time.time>=detourUntil)
                        BeginStuckRecovery(delta);
                    progressAnchor=transform.position;
                    progressCheckAt=Time.time+.65f;
                }
            }
            else
            {
                progressWatchActive=false;
                progressAnchor=transform.position;
                progressCheckAt=0;
            }
        }

        private void BeginStuckRecovery(Vector3 towardDestination)
        {
            blockedTime=0;freeMoveTime=0;recoveryAttempts++;
            towardDestination.y=0;
            Vector3 desired=towardDestination.sqrMagnitude>.01f?towardDestination.normalized:transform.forward;
            Vector3 normal=locomotion.LastCollisionNormal;normal.y=0;
            int side=((rotationSlot+recoveryAttempts)&1)==0?1:-1;

            // Prefer a tangent around the actual blocker, but verify it is open before committing.
            Vector3 preferred=desired;
            if(normal.sqrMagnitude>.01f)
            {
                preferred=Vector3.Cross(Vector3.up,normal.normalized)*side;
                if(Vector3.Dot(preferred,desired)<-.25f)preferred=-preferred;
            }

            if(!locomotion.TryFindClearDirection(preferred,1.35f,side,out detour)
                &&!locomotion.TryFindClearDirection(desired,1.35f,side,out detour))
                detour=Vector3.Cross(Vector3.up,desired)*side;

            if(recoveryAttempts>=5&&locomotion.TryFindClearDirection(desired,2.2f,-side,out Vector3 longDetour))
                detour=longDetour;

            detour.y=0;
            if(detour.sqrMagnitude<.01f)detour=brain!=null&&brain.Team==0?Vector3.right:Vector3.left;
            detour.Normalize();
            detourUntil=Time.time+Mathf.Min(1.25f,.52f+recoveryAttempts*.12f);
            velocity=Vector3.zero;

            // Repeated collision means the tactical target/route may be stale. Reset only steering
            // context; no combat stats or positions are changed and there is no teleport.
            if(recoveryAttempts>=3)
            {
                sensor?.ClearTarget();
                lanePlan?.ResetRoute();
                detourUntil=Time.time+.92f;
            }

#if UNITY_EDITOR || DEBUG
            if(Time.time>=nextStuckLog)
            {
                nextStuckLog=Time.time+2f;
                string blocker=locomotion.LastCollisionCollider!=null?locomotion.LastCollisionCollider.name:"unknown";
                string targetName=sensor!=null&&sensor.CurrentTarget!=null?sensor.CurrentTarget.name:"none";
                Debug.LogWarning($"BOT STUCK RECOVERY: {name} pos={transform.position} state={(brain!=null?brain.State.ToString():"?")} lane={(brain!=null?brain.LaneZ:0)} target={targetName} blocker={blocker} attempt={recoveryAttempts}");
            }
#endif
        }
    }
}

