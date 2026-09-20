using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health))]
    public sealed class MobaBotBrain : MonoBehaviour
    {
        public enum BotState { Lane, Fight, Retreat, Recover }
        [SerializeField] private int team;
        [SerializeField] private float laneZ;
        [SerializeField, Range(.1f, 1f)] private float aggression = .55f, caution = .55f;
        [SerializeField] private float decisionInterval = .35f, lowHealthThreshold = .30f;
        private Health health;
        private MobaBotRespawn respawn;
        private BotPersonality personality;
        private BotTargetSensor targetSensor;
        private BotState state = BotState.Lane;
        private float nextDecisionTime, retreatUntil;
        private bool wasDead;
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
        public float LaneZ => laneZ;
        public BotState State => state;
        public float Aggression => aggression;
        public float Caution => caution;
        public float DecisionRhythm => decisionRhythm;
        public float CommitmentRhythm => commitmentRhythm;

        private void Awake()
        {
            DifficultyLevel=BotDifficultySettings.Selected;Difficulty=BotDifficultyProfile.For(DifficultyLevel);

            // Each bot gets a stable personal rhythm so a whole team never thinks on the same beat.
            uint hash=2166136261u;
            foreach(char ch in gameObject.name){hash^=ch;hash*=16777619u;}
            decisionRhythm=Mathf.Lerp(.88f,1.14f,(hash%997u)/996f);
            commitmentRhythm=Mathf.Lerp(.90f,1.12f,((hash/997u)%991u)/990f);
            decisions=new System.Random(unchecked((int)hash^System.Environment.TickCount));

            health = GetComponent<Health>();lastHealth=health.Normalized; respawn = GetComponent<MobaBotRespawn>();
            personality = GetComponent<BotPersonality>(); targetSensor = GetComponent<BotTargetSensor>();
            ApplyPersonality();
        }
        private void Update()
        {
            if (Time.timeScale <= 0f || health == null) return;
            // The roster adds respawn after the brain, so Awake alone cannot resolve it.
            if (respawn == null) respawn = GetComponent<MobaBotRespawn>();
            if (targetSensor == null) targetSensor = GetComponent<BotTargetSensor>();
            if (health.IsDead) { wasDead = true; return; }
            if (respawn != null && respawn.IsRespawning) return;
            if (wasDead) ResumeLaneAfterRespawn();
            if (Time.time < nextDecisionTime) return;
            nextDecisionTime = Time.time + Mathf.Max(.15f, decisionInterval*Difficulty.DecisionScale*decisionRhythm);
            Decide();
        }
        public void Initialize(int assignedTeam, float assignedLaneZ, float personalityAggression = .55f, float personalityCaution = .55f)
        {
            team = assignedTeam; laneZ = assignedLaneZ;
            aggression = Mathf.Clamp01(personalityAggression); caution = Mathf.Clamp01(personalityCaution);
            lowHealthThreshold = Mathf.Lerp(.22f, .42f, caution);
            state = BotState.Lane; nextDecisionTime = 0; fightCommitUntil=groupDecisionUntil=0;
        }
        public void ResumeLaneAfterRespawn()
        {
            wasDead = false;retreatUntil=0;threatUntil=0;fightCommitUntil=groupDecisionUntil=0;lastHealth=health.Normalized;GroupWithAllies=false; state = BotState.Lane; nextDecisionTime = Time.time + .1f*decisionRhythm;
            GetComponent<BotTargetSensor>()?.ClearTarget();
            GetComponent<MobaBotMover>()?.ResetMotion();
            GetComponent<BotLanePlan>()?.ResetRoute();
        }
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
            bool badlyOutnumbered=pressure>=2&&hp<=outnumberedRetreat;
            if (hp <= retreat || badlyOutnumbered)
            {
                if(state!=BotState.Retreat&&state!=BotState.Recover)
                    retreatUntil=Time.time+1.2f*commitmentRhythm;
                state = BotState.Retreat;
                fightCommitUntil=0;
                return;
            }

            if (state == BotState.Retreat || state == BotState.Recover)
            {
                state = hp < .8f || Time.time<retreatUntil || pressure>0 ? BotState.Recover : BotState.Lane;
                if(state==BotState.Lane)fightCommitUntil=0;
                return;
            }

            // Perception owns what the bot can know. A short frozen last-seen memory is allowed,
            // but it never exposes the hidden live position of an unseen enemy.
            Health enemy = targetSensor != null ? targetSensor.CurrentTarget : null;
            bool perceivedThreat=enemy!=null&&!enemy.IsDead;
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

            if(state==BotState.Fight&&(recentMemory||Time.time<fightCommitUntil))return;
            state=BotState.Lane;
        }
    }
}

