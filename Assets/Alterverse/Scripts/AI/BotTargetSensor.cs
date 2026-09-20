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
        private Health candidateTarget, disengagedTarget;
        private float disengagedUntil;
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
        private MobaBotBrain brain;
        private readonly RaycastHit[] sightHits=new RaycastHit[32];
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
            nextScanTime=Time.time+(Mathf.Abs(GetInstanceID()%17)/17f)*scanInterval;
        }
        public void ClearTarget() { currentTarget = candidateTarget = disengagedTarget = null; disengagedUntil=0; candidateSince=retainedSince=0; lastSeenTime=-100f; lastKnownPosition=transform.position; nextScanTime = 0; NearbyEnemies=NearbyAllies=0; }
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
            int count=Physics.RaycastNonAlloc(origin,ray.normalized,sightHits,ray.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==sightHits.Length)return false;
            for(int i=0;i<count;i++)
            {
                // Heroes/minions do not block sight, but health-bearing buildings still do.
                var owner=sightHits[i].collider.GetComponentInParent<Health>();
                if(owner==selfHealth||owner==candidate)continue;
                if(owner==null||(owner.GetComponent<HeroIdentity>()==null&&owner.GetComponent<LaneMinion>()==null))return false;
            }
            return true;
        }
        private bool IsEnemy(Health candidate)
        {
            if (candidate == null || candidate == selfHealth || candidate.IsDead ||
                !candidate.isActiveAndEnabled || teamMember == null) return false;
            DimensionPlanet planet = candidate.GetComponent<DimensionPlanet>();
            if (planet != null) return planet.Available;
            RealityCore core = candidate.GetComponent<RealityCore>();
            if (core != null) return core.Team != teamMember.Team && core.IsVulnerable;
            LaneMinion minion = candidate.GetComponent<LaneMinion>();
            if (minion != null) return minion.Team != teamMember.Team;
            LaneTower tower = candidate.GetComponent<LaneTower>();
            if (tower != null) return tower.Team != teamMember.Team;
            TeamMember other = candidate.GetComponent<TeamMember>();
            return other != null && teamMember.IsEnemy(other);
        }
        private float Score(Health candidate)
        {
            Vector3 delta = candidate.transform.position - transform.position;
            delta.y = 0;
            HeroIdentity candidateHero=candidate.GetComponent<HeroIdentity>();
            float objectiveAwareness=Difficulty.ObjectiveAwareness;
            float priority = candidateHero != null ? .72f : 1f;
            if (candidate.GetComponent<LaneMinion>() != null) priority = .94f;
            if (candidate.GetComponent<LaneTower>() != null) priority = Mathf.Lerp(1.34f,.82f,objectiveAwareness);
            if (candidate.GetComponent<RealityCore>() != null) priority = Mathf.Lerp(1.42f,.68f,objectiveAwareness);
            if (candidate.GetComponent<DimensionPlanet>() != null) priority = Mathf.Lerp(1.48f,.78f,objectiveAwareness);

            float accuracy=Difficulty.TargetAccuracy;
            float aggression=brain!=null?brain.Aggression:.55f;
            if(candidateHero!=null)
            {
                priority*=Mathf.Lerp(1.15f,.8f,aggression);
                // Role tendencies are soft preferences, not hidden information or hard rules.
                HeroId selfHero=profile!=null?profile.Hero:HeroId.Veyr;
                switch(selfHero)
                {
                    case HeroId.Veyr: // assassin: wounded backline targets become more attractive
                        priority*=Mathf.Lerp(.72f,1.05f,candidate.Normalized);
                        if(candidateHero.Hero==HeroId.Kaelon)priority*=1.16f;
                        break;
                    case HeroId.Kaelon: // tank: willing to occupy enemy heroes rather than farm
                        priority*=.88f;
                        break;
                    case HeroId.Nyra: // marksman: steady visible targets, no extreme preference
                        priority*=.96f;
                        break;
                    case HeroId.Elyra:
                        priority*=.93f;
                        break;
                    case HeroId.Mira: // support is slightly less eager to start a solo duel
                        priority*=1.06f;
                        break;
                    default: // bruiser
                        priority*=.92f;
                        break;
                }
            }

            // Easy is more distance-led; stronger difficulties value visible wounded targets more,
            // but no difficulty gets information that a player could not perceive.
            // Stable perceptual bias: random rerolls each scan caused A/B/A target flicker.
            uint bias=unchecked((uint)(candidate.GetInstanceID()*397)^((uint)GetInstanceID()));
            float noise=1f+(1f-accuracy)*.45f*(bias%101u)/100f;
            float woundedFocus=Mathf.Lerp(.30f,1f,candidate.Normalized);
            return delta.sqrMagnitude*Mathf.Lerp(1f,woundedFocus*priority,accuracy)*noise;
        }
        private void Scan()
        {
            float radius = Radius;
            NearbyEnemies=NearbyAllies=0;RallyPosition=transform.position;
            Vector3 allies=Vector3.zero;
            bool retainedVisible=false;
            if(!IsEnemy(currentTarget))currentTarget=null;
            Health best = null;
            float bestScore = float.MaxValue;
            for(int index=0;index<Health.Active.Count;index++)
            {
                Health candidate=Health.Active[index];
                if(candidate==null||candidate==selfHealth||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                if(!Visible(candidate))continue;
                if(candidate==currentTarget)
                {
                    retainedVisible=true;
                    lastKnownPosition=candidate.transform.position;
                    lastSeenTime=Time.time;
                }
                if(candidate.GetComponent<HeroIdentity>()!=null)
                {
                    var member=candidate.GetComponent<TeamMember>();
                    if(member!=null&&member.Team==teamMember.Team){NearbyAllies++;allies+=candidate.transform.position;}
                    else if(member!=null)NearbyEnemies++;
                }
                if (!IsEnemy(candidate)) continue;
                // A failed chase cannot immediately reacquire the same fleeing opponent.
                // Nearby opponents remain valid so disengaging never disables self-defence.
                float defendRange=profile!=null?profile.AttackRange:fallbackScanRadius*.25f;
                if(candidate==disengagedTarget && Time.time<disengagedUntil &&
                    (candidate.transform.position-transform.position).sqrMagnitude>defendRange*defendRange)continue;
                Vector3 delta = candidate.transform.position - transform.position;
                delta.y = 0;
                if (delta.sqrMagnitude > radius * radius) continue;
                float score = Score(candidate);
                // A challenger must be substantially better; small movements cannot flip targets.
                if (candidate == currentTarget) score *= Time.time-retainedSince<Difficulty.MemorySeconds?.65f:.85f;
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            if(!retainedVisible)currentTarget=null;
            if(NearbyAllies>0)RallyPosition=allies/NearbyAllies;
            if(best==currentTarget){candidateTarget=null;return;}
            if(best!=candidateTarget){candidateTarget=best;candidateSince=Time.time;}
            float reaction=Difficulty.ReactionDelay*(brain!=null?brain.DecisionRhythm:1f);
            if(best!=null&&Time.time-candidateSince>=reaction)
            {
                currentTarget=best;retainedSince=Time.time;candidateTarget=null;
                lastKnownPosition=best.transform.position;lastSeenTime=Time.time;
            }
        }
    }
}

