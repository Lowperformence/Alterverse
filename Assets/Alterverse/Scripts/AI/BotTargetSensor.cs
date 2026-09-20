using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;

namespace Alterverse.AI
{
    [RequireComponent(typeof(TeamMember))]
    public sealed class BotTargetSensor : MonoBehaviour
    {
        [SerializeField] private float fallbackScanRadius = 10f, scanInterval = .22f;
        private TeamMember teamMember;
        private Health selfHealth, currentTarget;
        private HeroCombatProfile profile;
        private float nextScanTime, candidateSince, retainedSince, lastSeenTime=-100f;
        private Health candidateTarget, disengagedTarget, recentAttacker;
        private float disengagedUntil, recentAttackerUntil;
        // Consumers steer toward the last observation, never a hidden live transform.
        public Vector3 ObservedTargetPosition => lastKnownPosition;
        public bool CanObserve(Health target) => IsEnemy(target) && Visible(target);
        public void Disengage(Health target)
        {
            ClearTarget();
            disengagedTarget=target;
            disengagedUntil=Time.time+Mathf.Lerp(1.2f,2.5f,Difficulty.ChaseDiscipline);
        }
        private Vector3 lastKnownPosition;
        public Vector3 ObservedTargetVelocity { get; private set; }
        public bool HasSteadyMotion { get; private set; }
        public bool TryObservedVelocity(Health target,out Vector3 velocity)
        {
            velocity=Vector3.zero;
            if(target==null||target!=currentTarget||Time.time-lastSeenTime>.5f)return false;
            velocity=ObservedTargetVelocity;return true;
        }
        private void ObserveMotion(Vector3 position)
        {
            float elapsed=Time.time-lastSeenTime;
            Vector3 velocity=elapsed>.04f&&elapsed<.6f?(position-lastKnownPosition)/elapsed:Vector3.zero;
            velocity.y=0;velocity=Vector3.ClampMagnitude(velocity,8f);
            HasSteadyMotion=velocity.sqrMagnitude<.25f||ObservedTargetVelocity.sqrMagnitude>.25f&&
                Vector3.Dot(velocity.normalized,ObservedTargetVelocity.normalized)>.8f;
            ObservedTargetVelocity=velocity;
        }
        private MobaBotBrain brain;
        private readonly RaycastHit[] sightHits=new RaycastHit[32];
        private readonly BotTeamCoordinator.Observation[] observations=new BotTeamCoordinator.Observation[48];
        public int ObservationCount { get; private set; }
        public BotTeamCoordinator.Observation ObservationAt(int index)=>observations[index];
        public int VisibleHeroesNear(Vector3 point,float radius)
        {
            int count=0;
            for(int i=0;i<ObservationCount;i++)
                if(observations[i].Hero&&Time.time-observations[i].SeenAt<.5f&&
                    (observations[i].Position-point).sqrMagnitude<=radius*radius)count++;
            return count;
        }
        public int NearbyEnemies { get; private set; }
        public int NearbyAllies { get; private set; }
        public Vector3 RallyPosition { get; private set; }
        public bool HasRecentEnemyMemory=>Time.time-lastSeenTime<=Mathf.Min(1.8f,Difficulty.MemorySeconds);
        public Vector3 LastKnownEnemyPosition=>lastKnownPosition;
        private BotDifficultyProfile Difficulty=>brain!=null?brain.Difficulty:BotDifficultyProfile.For(BotDifficulty.Normal);
        private float Radius=>profile!=null?Mathf.Max(fallbackScanRadius,profile.AttackRange+4f):fallbackScanRadius;
        public Health CurrentTarget => IsEnemy(currentTarget) && InRadius(currentTarget) ? currentTarget : null;

        private void Awake()
        {
            teamMember = GetComponent<TeamMember>();
            selfHealth = GetComponent<Health>();
            profile = GetComponent<HeroCombatProfile>();
            nextScanTime=Time.time+(Mathf.Abs(GetEntityId().GetHashCode()%17)/17f)*scanInterval;
        }
        private void OnEnable(){if(selfHealth!=null)selfHealth.Damaged+=OnDamaged;}
        private void OnDisable(){if(selfHealth!=null)selfHealth.Damaged-=OnDamaged;}
        private void OnDamaged(float applied,float absorbed,GameObject source)
        {
            if(applied<=0||source==null)return;
            Health attacker=source.GetComponentInParent<Health>();
            if(attacker==null||!IsEnemy(attacker)||attacker.GetComponent<HeroIdentity>()==null)return;
            // Hero combat is the primary game loop. Remember only a hero who actually hurt us;
            // selection still requires normal visibility, so this never becomes wall vision.
            recentAttacker=attacker;
            recentAttackerUntil=Time.time+1.8f;
            // A hero that attacks us again has re-entered the fight and may immediately be
            // considered for self-defence even if an earlier chase was intentionally abandoned.
            if(disengagedTarget==attacker){disengagedTarget=null;disengagedUntil=0;}
        }
        public void ClearTarget() { ObservedTargetVelocity=Vector3.zero;HasSteadyMotion=false;ObservationCount=0; currentTarget = candidateTarget = disengagedTarget = recentAttacker = null; disengagedUntil=recentAttackerUntil=0; candidateSince=retainedSince=0; lastSeenTime=-100f; lastKnownPosition=transform.position; nextScanTime = 0; NearbyEnemies=NearbyAllies=0; }
        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if(brain==null)brain=GetComponent<MobaBotBrain>();
            if (selfHealth != null && selfHealth.IsDead) { ClearTarget(); return; }
            if (Time.time < nextScanTime) return;
            float rhythm=brain!=null?brain.DecisionRhythm:1f;
            nextScanTime = Time.time + Mathf.Max(.12f, scanInterval*Difficulty.DecisionScale*rhythm);
            Scan();
        }
        private bool InRadius(Health candidate)
        {
            Vector3 delta=candidate.transform.position-transform.position;delta.y=0;
            return delta.sqrMagnitude<=Radius*Radius;
        }
        private bool Visible(Health candidate)
        {
            if(!InRadius(candidate))return false;
            Vector3 origin=transform.position+Vector3.up*.4f;
            Vector3 end=candidate.transform.position+Vector3.up*.4f;
            Vector3 ray=end-origin;
            if(ray.sqrMagnitude<.001f)return true;
            int count=Physics.RaycastNonAlloc(origin,ray.normalized,sightHits,ray.magnitude,GameplayCollision.SightMask,QueryTriggerInteraction.Ignore);
            if(count==sightHits.Length)return false;
            for(int i=0;i<count;i++)
            {
                // Mobile combat units are not opaque walls. This includes the healing guard,
                // which previously could hide an attacker from bots standing on the healing plate.
                // Static gameplay structures and environment colliders still block sight.
                var owner=sightHits[i].collider.GetComponentInParent<Health>();
                if(owner==selfHealth||owner==candidate)continue;
                if(owner!=null)
                {
                    bool mobileUnit=owner.GetComponent<HeroIdentity>()!=null||
                        owner.GetComponent<LaneMinion>()!=null||
                        owner.GetComponent<TeamMember>()!=null&&
                        owner.GetComponent<LaneTower>()==null&&owner.GetComponent<RealityCore>()==null;
                    if(mobileUnit)continue;
                }
                return false;
            }
            return true;
        }
        private bool IsEnemy(Health candidate)
        {
            if (candidate == null || candidate == selfHealth || candidate.IsDead ||
                !candidate.isActiveAndEnabled || teamMember == null) return false;

            // Bot heroes intentionally ignore lane towers and ordinary lane minions as combat
            // targets. Siege mechs reuse LaneMinion for movement/combat, so preserve them by
            // checking the explicit siege role instead of rejecting every LaneMinion component.
            if (candidate.GetComponent<LaneTower>() != null) return false;

            DimensionPlanet planet = candidate.GetComponent<DimensionPlanet>();
            if (planet != null) return planet.Available;
            RealityCore core = candidate.GetComponent<RealityCore>();
            if (core != null) return core.Team != teamMember.Team && core.IsVulnerable;

            LaneMinion minion = candidate.GetComponent<LaneMinion>();
            if (minion != null) return minion.IsSiegeUnit && minion.Team != teamMember.Team;

            TeamMember other = candidate.GetComponent<TeamMember>();
            return other != null && teamMember.IsEnemy(other);
        }
        private int TargetTier(Health candidate)
        {
            // Lower is better. Towers and ordinary lane minions never reach this function because
            // IsEnemy rejects them. Heroes stay primary, while real mechs/special units remain valid.
            if(candidate.GetComponent<HeroIdentity>()!=null)return 0;
            if(candidate.GetComponent<RealityCore>()!=null||candidate.GetComponent<DimensionPlanet>()!=null)return 1;

            LaneMinion minion=candidate.GetComponent<LaneMinion>();
            if(minion!=null&&minion.IsSiegeUnit)return 2;
            if(candidate.GetComponent<TeamMember>()!=null)return 2;
            return 4;
        }

        private float Score(Health candidate)
        {
            Vector3 delta = candidate.transform.position - transform.position;
            delta.y = 0;
            HeroIdentity candidateHero=candidate.GetComponent<HeroIdentity>();
            float objectiveAwareness=Difficulty.ObjectiveAwareness;
            float priority=1f;

            if(candidateHero!=null)
            {
                // Within the hero tier, wounded/controlled/recently aggressive heroes become better
                // choices. TargetTier already guarantees a tower or minion cannot steal the fight.
                float heroBias=Mathf.Lerp(.46f,.24f,Difficulty.TargetAccuracy);
                priority*=heroBias;
                priority*=Mathf.Lerp(.42f,1f,candidate.Normalized);

                HeroCombatStatus status=candidate.GetComponent<HeroCombatStatus>();
                if(status!=null&&(status.Stunned||status.MoveMultiplier<.8f))priority*=.72f;

                if(brain!=null&&brain.InsideOwnBase(transform.position)&&brain.IsLocalBaseThreat(candidate))
                    priority*=.34f;
                if(candidate==recentAttacker&&Time.time<recentAttackerUntil)
                    priority*=.40f;

                float aggression=brain!=null?brain.Aggression:.55f;
                priority*=Mathf.Lerp(1.08f,.82f,aggression);

                // Role tendencies are soft preferences; they never grant hidden information.
                HeroId selfHero=profile!=null?profile.Hero:HeroId.Veyr;
                switch(selfHero)
                {
                    case HeroId.Veyr: // assassin: prefer vulnerable backline, avoid healthy tank fixation
                        if(BotTeamCoordinator.IsBackline(candidateHero.Hero))priority*=.72f;
                        if(candidateHero.Hero==HeroId.Kaelon)priority*=1.18f;
                        break;
                    case HeroId.Kaelon: // tank: peel threats near allied backline through team bias
                        priority*=.90f;
                        break;
                    case HeroId.Nyra: // marksman: reliable nearest safe hero pressure
                        priority*=.96f;
                        break;
                    case HeroId.Elyra:
                        priority*=.93f;
                        break;
                    case HeroId.Mira: // support remains slightly less eager to start a solo duel
                        priority*=1.05f;
                        break;
                    default: // bruiser
                        priority*=.92f;
                        break;
                }
            }
            else
            {
                LaneMinion minion=candidate.GetComponent<LaneMinion>();
                if(minion!=null&&minion.IsSiegeUnit)priority=.96f;
                if(candidate.GetComponent<RealityCore>()!=null)priority=Mathf.Lerp(1.22f,.72f,objectiveAwareness);
                if(candidate.GetComponent<DimensionPlanet>()!=null)priority=Mathf.Lerp(1.30f,.78f,objectiveAwareness);
            }

            float accuracy=Difficulty.TargetAccuracy;
            // Stable perceptual bias: never reroll target preference every scan.
            uint bias=unchecked((uint)(candidate.GetEntityId().GetHashCode()*397)^((uint)GetEntityId().GetHashCode()));
            float noise=1f+(1f-accuracy)*.32f*(bias%101u)/100f;
            float tacticalBias=brain!=null&&brain.FollowTeamCall
                ?brain.Tactics.TargetBias(candidate,profile!=null?profile.Hero:HeroId.Veyr):1f;
            return delta.sqrMagnitude*priority*noise*tacticalBias;
        }

        private void Scan()
        {
            if(brain==null)brain=GetComponent<MobaBotBrain>();
            // Only recently observed, still locally visible deaths grant objective tempo.
            if(brain!=null&&brain.Difficulty.Coordinated)
                for(int i=0;i<ObservationCount;i++)
                {
                    var old=observations[i];
                    if(old.Hero&&old.Target!=null&&old.Target.IsDead&&Time.time-old.SeenAt<.65f&&Visible(old.Target))
                        brain.Tactics.ReportVisibleDefeat(old.Target);
                }
            ObservationCount=0;
            float radius = Radius;
            NearbyEnemies=NearbyAllies=0;RallyPosition=transform.position;
            Vector3 allies=Vector3.zero;
            bool retainedVisible=false;
            if(!IsEnemy(currentTarget))currentTarget=null;
            Health best = null;
            float bestScore = float.MaxValue;
            int bestTier=int.MaxValue;
            for(int index=0;index<Health.Active.Count;index++)
            {
                Health candidate=Health.Active[index];
                if(candidate==null||candidate==selfHealth||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                if(!Visible(candidate))continue;
                if(candidate==currentTarget)
                {
                    retainedVisible=true;
                    ObserveMotion(candidate.transform.position);
                    lastKnownPosition=candidate.transform.position;
                    lastSeenTime=Time.time;
                }
                HeroIdentity identity=candidate.GetComponent<HeroIdentity>();
                if(identity!=null)
                {
                    var member=candidate.GetComponent<TeamMember>();
                    if(member!=null&&member.Team==teamMember.Team){NearbyAllies++;allies+=candidate.transform.position;}
                    else if(member!=null)NearbyEnemies++;
                }
                if (!IsEnemy(candidate)) continue;
                if(ObservationCount<observations.Length)
                {
                    var status=candidate.GetComponent<HeroCombatStatus>();
                    observations[ObservationCount++]=new BotTeamCoordinator.Observation{
                        Target=candidate,Position=candidate.transform.position,Hp=candidate.Normalized,SeenAt=Time.time,
                        Hero=identity!=null,Role=identity!=null?identity.Hero:HeroId.Veyr,
                        Controlled=status!=null&&(status.Stunned||status.MoveMultiplier<.8f)};
                }
                // A failed chase cannot immediately reacquire the same fleeing opponent. A hero
                // that attacks us again clears this lock in OnDamaged, preserving local self-defence.
                float defendRange=profile!=null?profile.AttackRange:fallbackScanRadius*.25f;
                if(candidate==disengagedTarget && Time.time<disengagedUntil &&
                    (candidate.transform.position-transform.position).sqrMagnitude>defendRange*defendRange)continue;
                Vector3 delta = candidate.transform.position - transform.position;
                delta.y = 0;
                if (delta.sqrMagnitude > radius * radius) continue;

                int tier=TargetTier(candidate);
                float score = Score(candidate);
                // Preserve a valid current target against tiny within-tier improvements. Hero targets
                // therefore feel committed instead of flickering between nearby opponents.
                if (candidate == currentTarget) score *= Time.time-retainedSince<Difficulty.MemorySeconds?.62f:.84f;
                if (tier < bestTier || tier == bestTier && score < bestScore)
                {bestTier=tier;bestScore=score;best=candidate;}
            }
            if(!retainedVisible){currentTarget=null;ObservedTargetVelocity=Vector3.zero;HasSteadyMotion=false;}
            if(NearbyAllies>0)RallyPosition=allies/NearbyAllies;

            // Hold a visible hero for a short human-sized commitment window unless a clearly urgent
            // hero target appears (recent attacker, team focus, or strong execute opportunity).
            if(currentTarget!=null&&retainedVisible&&currentTarget.GetComponent<HeroIdentity>()!=null&&
                best!=null&&best!=currentTarget&&best.GetComponent<HeroIdentity>()!=null)
            {
                float commitment=Mathf.Lerp(1.30f,.80f,Difficulty.TeamDiscipline);
                bool urgent=best==recentAttacker&&Time.time<recentAttackerUntil||
                    brain!=null&&brain.Difficulty.Coordinated&&brain.Tactics.Focus==best||
                    best.Normalized<.30f&&currentTarget.Normalized>best.Normalized+.18f;
                if(Time.time-retainedSince<commitment&&!urgent)best=currentTarget;
            }

            if(best==currentTarget){candidateTarget=null;return;}
            // Keep a pending candidate through its reaction window only if it is still a legal
            // bot target and at least as important as the newly found class.
            if(currentTarget==null&&IsEnemy(candidateTarget)&&Visible(candidateTarget)&&
                !(candidateTarget==disengagedTarget&&Time.time<disengagedUntil)&&
                TargetTier(candidateTarget)<=bestTier)best=candidateTarget;
            if(best!=candidateTarget){candidateTarget=best;candidateSince=Time.time;}
            float reaction=Difficulty.ReactionDelay*(brain!=null?brain.DecisionRhythm:1f);
            if(best!=null&&Time.time-candidateSince>=reaction)
            {
                currentTarget=best;retainedSince=Time.time;candidateTarget=null;ObservedTargetVelocity=Vector3.zero;HasSteadyMotion=false;
                lastKnownPosition=best.transform.position;lastSeenTime=Time.time;
            }
        }
    }
}


