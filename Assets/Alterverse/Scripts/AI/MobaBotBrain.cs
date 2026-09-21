using UnityEngine;
using Alterverse.Combat;
using Alterverse.Characters;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health))]
    public sealed class MobaBotBrain : MonoBehaviour
    {
        // Roam is the active free-map search state. Lane remains only as a numeric compatibility alias
        // for older serialized/editor content and is not used by current runtime decisions.
        public enum BotState { Roam=0, Lane=Roam, Fight=1, Retreat=2, Recover=3 }
        [SerializeField] private int team;
        [SerializeField, Range(.1f, 1f)] private float aggression = .55f, caution = .55f;
        [SerializeField] private float decisionInterval = .35f, lowHealthThreshold = .30f;
        private Health health;
        private MobaBotRespawn respawn;
        private BotPersonality personality;
        private BotTargetSensor targetSensor;
        private BotState state = BotState.Roam;
        private float nextDecisionTime, retreatUntil, baseDefenseUntil;
        private bool wasDead;
        private int observedCall=-1;
        private float callReadyAt;
        private HeroIdentity identity;
        public Health Vitals=>health;
        public BotTargetSensor Sensor=>targetSensor!=null?targetSensor:(targetSensor=GetComponent<BotTargetSensor>());
        public BotTeamCoordinator Tactics=>BotTeamCoordinator.ForTeam(team);
        private bool assignedCall;
        public bool FollowTeamCall=>Difficulty.Coordinated&&assignedCall&&Time.time>=callReadyAt&&observedCall==Tactics.Revision;
        public bool TacticalWithdrawal { get; private set; }
        private float combatResetAt,combatResetUntil;
        public bool NeedsCombatReset=>Time.time>=combatResetAt&&Time.time<combatResetUntil;
        public void RequestCombatReset(){combatResetAt=Time.time+.4f;combatResetUntil=combatResetAt+1.1f;}
        private void OnEnable()=>BotTeamCoordinator.Register(this);
        private void OnDisable()=>BotTeamCoordinator.Unregister(this);
        private System.Random decisions;
        private float lastHealth, threatUntil, fightCommitUntil, groupDecisionUntil;
        private float decisionRhythm=1f, commitmentRhythm=1f;
        public BotDifficulty DifficultyLevel { get; private set; }
        public BotDifficultyProfile Difficulty { get; private set; }
        public bool GroupWithAllies { get; private set; }
        public float TeamFocus=>personality!=null?personality.TeamFocus:.6f;
        public float ObjectiveFocus=>personality!=null?personality.ObjectiveFocus:.6f;
        public bool Chance(float probability)=>decisions.NextDouble()<Mathf.Clamp01(probability);
        public int Team => team;
        public BotState State => state;
        public float Aggression => aggression;
        public float Caution => caution;
        public float DecisionRhythm => decisionRhythm;
        public float CommitmentRhythm => commitmentRhythm;
        public bool BaseDefenseActive => Time.time < baseDefenseUntil;

        public bool InsideOwnBase(Vector3 point)
        {
            Vector3 home=new(team==0?-27.5f:27.5f,point.y,0f);
            Vector3 delta=point-home;delta.y=0;
            return delta.sqrMagnitude<=13f*13f;
        }

        public bool IsLocalBaseThreat(Health target)
        {
            return target!=null&&!target.IsDead&&InsideOwnBase(target.transform.position);
        }

        private void Awake()
        {
            DifficultyLevel=BotDifficultySettings.Current;Difficulty=BotDifficultyProfile.For(DifficultyLevel);

            // Each bot gets a stable personal rhythm so a whole team never thinks on the same beat.
            uint hash=2166136261u;
            foreach(char ch in gameObject.name){hash^=ch;hash*=16777619u;}
            decisionRhythm=Mathf.Lerp(.88f,1.14f,(hash%997u)/996f);
            commitmentRhythm=Mathf.Lerp(.90f,1.12f,((hash/997u)%991u)/990f);
            decisions=new System.Random(unchecked((int)hash^System.Environment.TickCount));

            identity=GetComponent<HeroIdentity>();
            health = GetComponent<Health>();lastHealth=health.Normalized; respawn = GetComponent<MobaBotRespawn>();
            personality = GetComponent<BotPersonality>(); targetSensor = GetComponent<BotTargetSensor>();
            ApplyPersonality();
        }
        private void Update()
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            if (Time.timeScale <= 0f || health == null) return;
            // The roster adds respawn after the brain, so Awake alone cannot resolve it.
            if (respawn == null) respawn = GetComponent<MobaBotRespawn>();
            if (targetSensor == null) targetSensor = GetComponent<BotTargetSensor>();
            if (health.IsDead) { wasDead = true; return; }
            if (respawn != null && respawn.IsRespawning) return;
            if (wasDead) ResumeRoamAfterRespawn();
            if (Time.time < nextDecisionTime) return;
            nextDecisionTime = Time.time + Mathf.Max(.15f, decisionInterval*Difficulty.DecisionScale*decisionRhythm);
            Decide();
        }
        public void Initialize(int assignedTeam, float assignedLaneZ, float personalityAggression = .55f, float personalityCaution = .55f)
        {
            team = Mathf.Clamp(assignedTeam,0,1); observedCall=-1;TacticalWithdrawal=false;baseDefenseUntil=0;
            aggression = Mathf.Clamp01(personalityAggression); caution = Mathf.Clamp01(personalityCaution);
            lowHealthThreshold = Mathf.Lerp(.22f, .42f, caution);
            state = BotState.Roam; nextDecisionTime = 0; fightCommitUntil=groupDecisionUntil=0;
        }
        public void ResumeRoamAfterRespawn()
        {
            wasDead = false;combatResetAt=combatResetUntil=0;observedCall=-1;TacticalWithdrawal=false;retreatUntil=baseDefenseUntil=0;threatUntil=0;fightCommitUntil=groupDecisionUntil=0;lastHealth=health.Normalized;GroupWithAllies=false; state = BotState.Roam; nextDecisionTime = Time.time + .1f*decisionRhythm;
            GetComponent<BotTargetSensor>()?.ClearTarget();
            GetComponent<MobaBotMover>()?.ResetMotion();
            GetComponent<BotLanePlan>()?.ResetRoute();
        }
        // Compatibility entry point for older editor checks/content.
        public void ResumeLaneAfterRespawn()=>ResumeRoamAfterRespawn();
        public void RefreshPersonality()
        {
            personality = GetComponent<BotPersonality>(); targetSensor = GetComponent<BotTargetSensor>();
            ApplyPersonality();
        }
        private void ApplyPersonality()
        {
            if (personality == null) return;
            aggression = personality.Aggression; caution = personality.Caution;
            decisionInterval = personality.ReactionDelay;
            lowHealthThreshold = Mathf.Lerp(.22f, .42f, caution);
        }
        private void Decide()
        {
            if(Difficulty.Coordinated)
            {
                Tactics.Tick(DifficultyLevel);
                if(observedCall!=Tactics.Revision)
                {observedCall=Tactics.Revision;callReadyAt=Time.time+Difficulty.TeamCallDelay*decisionRhythm;}
                assignedCall=Tactics.Follows(this);
            }
            float hp = health.Normalized;
            if(lastHealth-hp>.08f)threatUntil=Time.time+Difficulty.MemorySeconds;
            lastHealth=hp;

            int nearbyEnemies=targetSensor!=null?targetSensor.NearbyEnemies:0;
            int nearbyAllies=targetSensor!=null?targetSensor.NearbyAllies:0;
            int pressure=Mathf.Max(0,nearbyEnemies-(nearbyAllies+1));
            float danger=pressure*Mathf.Lerp(.018f,.085f,Difficulty.ThreatAwareness);
            if(Time.time<threatUntil)danger+=Mathf.Lerp(.018f,.060f,Difficulty.ThreatAwareness);

            // Team intention persists for a short human-sized window instead of being rerolled
            // every decision tick. Different personal rhythms prevent hive-mind synchronization.
            if(Time.time>=groupDecisionUntil)
            {
                float groupChance=Mathf.Clamp01(TeamFocus*Difficulty.Grouping*Mathf.Lerp(.85f,1.08f,Difficulty.TeamDiscipline));
                GroupWithAllies=Chance(groupChance);
                float groupHold=Mathf.Lerp(2.25f,1.00f,Difficulty.TeamDiscipline)*commitmentRhythm;
                groupDecisionUntil=Time.time+groupHold;
            }

            float retreat=Mathf.Clamp(lowHealthThreshold+Difficulty.RetreatAdjustment+danger,.18f,.58f);
            float outnumberedRetreat=Mathf.Lerp(.34f,.70f,Difficulty.TeamDiscipline);
            Health visibleEnemy=targetSensor!=null?targetSensor.CurrentTarget:null;
            bool coordinated=Difficulty.Coordinated&&Tactics.HasPlan;
            bool winningFinish=coordinated&&hp>.27f&&Tactics.CanFinish(visibleEnemy,transform.position);
            bool localBaseThreat=IsLocalBaseThreat(visibleEnemy)||(coordinated&&Tactics.CoreEmergency);
            if(localBaseThreat)baseDefenseUntil=Time.time+1.5f;
            bool badlyOutnumbered=pressure>=2&&(coordinated||hp<=outnumberedRetreat);
            bool teamWithdrawal=FollowTeamCall&&Tactics.Call==BotTeamCoordinator.Intent.Disengage;
            bool losingFight=coordinated&&nearbyEnemies>0&&Tactics.FightRatio(transform.position)<.70f;

            // Being outnumbered normally justifies a retreat. Inside our own base that same rule
            // must never turn into passive surrender: once reasonably healthy, a visible invader
            // forces a defensive fight instead of another trip back onto the healing plate.
            bool healthyForBaseDefense=hp>=Mathf.Max(.48f,retreat+.10f);
            bool shouldRetreat=(!winningFinish&&hp<=retreat)||badlyOutnumbered||losingFight||teamWithdrawal;
            if(localBaseThreat&&visibleEnemy!=null&&healthyForBaseDefense)shouldRetreat=false;
            if (shouldRetreat)
            {
                if(state!=BotState.Retreat&&state!=BotState.Recover)
                    retreatUntil=Time.time+1.2f*commitmentRhythm;
                TacticalWithdrawal=coordinated&&hp>retreat&&(badlyOutnumbered||losingFight||teamWithdrawal);
                state = BotState.Retreat;
                fightCommitUntil=0;
                return;
            }

            if (state == BotState.Retreat || state == BotState.Recover)
            {
                float recoverHealth=TacticalWithdrawal?.55f:.8f;

                // Do not require a losing team to reach 100% HP before defending its own base.
                // A reasonably recovered bot with a visible local threat re-enters Fight immediately.
                if(localBaseThreat&&visibleEnemy!=null&&hp>=Mathf.Min(recoverHealth,.58f))
                {
                    state=BotState.Fight;
                    TacticalWithdrawal=false;
                    fightCommitUntil=Time.time+.65f*commitmentRhythm;
                    return;
                }

                // If the retreat is no longer tactically necessary and the pursuing hero keeps
                // attacking us, turn and fight instead of running all the way to 80% HP.
                // Critical HP, real outnumbering and team disengage orders still keep retreat priority.
                bool pressuredByAttacker=visibleEnemy!=null&&visibleEnemy.GetComponent<HeroIdentity>()!=null&&
                    targetSensor!=null&&targetSensor.IsRecentAttacker(visibleEnemy);
                float counterFightHealth=Mathf.Max(.42f,retreat+.10f);
                bool canCounterFight=pressuredByAttacker&&hp>=counterFightHealth&&pressure==0&&
                    !teamWithdrawal&&!losingFight&&!badlyOutnumbered&&Time.time>=retreatUntil-.35f;
                if(canCounterFight)
                {
                    state=BotState.Fight;
                    TacticalWithdrawal=false;
                    fightCommitUntil=Time.time+.80f*commitmentRhythm;
                    return;
                }

                state = (!winningFinish&&hp<recoverHealth)||Time.time<retreatUntil||pressure>0 ? BotState.Recover : BotState.Roam;
                if(state==BotState.Roam){fightCommitUntil=0;TacticalWithdrawal=false;}
                return;
            }

            // Perception owns what the bot can know. A short frozen last-seen memory is allowed,
            // but it never exposes the hidden live position of an unseen enemy.
            Health enemy = targetSensor != null ? targetSensor.CurrentTarget : null;
            bool perceivedThreat=enemy!=null&&!enemy.IsDead;
            if(identity==null)identity=GetComponent<HeroIdentity>();
            // Backline cannot start a solo duel while its team is reachable but elsewhere.
            if(coordinated&&identity!=null&&BotTeamCoordinator.IsBackline(identity.Hero)&&nearbyAllies==0&&
                nearbyEnemies>0&&Tactics.AliveHeroes>1&&!winningFinish)
            {state=BotState.Roam;return;}
            // A base defence/regroup call is not cancelled by a harmless farming target.
            if(FollowTeamCall&&Tactics.Call==BotTeamCoordinator.Intent.Defend&&
                (transform.position-Tactics.Destination).sqrMagnitude>12f*12f&&nearbyEnemies==0)
                {state=BotState.Roam;return;}
            bool recentMemory=targetSensor!=null&&targetSensor.HasRecentEnemyMemory;

            if(perceivedThreat)
            {
                if(state!=BotState.Fight)
                {
                    state=BotState.Fight;
                    float hold=Mathf.Lerp(1.15f,.46f,Difficulty.TeamDiscipline)*commitmentRhythm;
                    fightCommitUntil=Time.time+hold;
                }
                return;
            }

            if(state==BotState.Fight&&recentMemory)return;
            fightCommitUntil=0;
            state=BotState.Roam;
        }
    }
}


